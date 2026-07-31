using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TalonAdmin.Models.Vouchers;

namespace TalonAdmin.Messaging
{
    /// <summary>
    /// Drains the outbound queue: claims due messages, hands each to its channel, and records what
    /// happened. Because the queue is a table rather than in-process state, a restart mid-send
    /// resumes instead of losing the message.
    /// </summary>
    public class OutboundMessageDispatcher : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<OutboundMessageDispatcher> _logger;
        private readonly TimeSpan _pollInterval;
        private readonly int _batchSize;
        private readonly int _maxAttempts;

        public OutboundMessageDispatcher(
            IServiceScopeFactory scopeFactory,
            IConfiguration configuration,
            ILogger<OutboundMessageDispatcher> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
            _pollInterval = TimeSpan.FromSeconds(
                Math.Max(1, configuration.GetValue("Messaging:PollIntervalSeconds", 5)));
            _batchSize = Math.Max(1, configuration.GetValue("Messaging:BatchSize", 25));
            _maxAttempts = Math.Max(1, configuration.GetValue("Messaging:MaxAttempts", 5));
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var sent = await DispatchBatchAsync(stoppingToken);

                    // A full batch probably means more is waiting, so poll again straight away.
                    if (sent >= _batchSize)
                    {
                        continue;
                    }
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception exception)
                {
                    // The loop must survive anything a single batch throws, or one bad row stops
                    // every message in the deployment.
                    _logger.LogError(exception, "Outbound message dispatch failed; retrying.");
                }

                try
                {
                    await Task.Delay(_pollInterval, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        /// <summary>Processes one batch. Exposed so tests can drive it without the host loop.</summary>
        public async Task<int> DispatchBatchAsync(CancellationToken cancellationToken)
        {
            using var scope = _scopeFactory.CreateScope();
            var services = scope.ServiceProvider;

            var context = services.GetRequiredService<Context>();
            var adminContext = services.GetRequiredService<Models.Admin.AdminContext>();
            var registry = services.GetRequiredService<MessageChannelRegistry>();

            var now = DateTime.UtcNow;

            var due = await context.OutboundMessages
                .Where(m => m.State == OutboundMessageState.Pending && m.NextAttemptOn <= now)
                .OrderBy(m => m.NextAttemptOn)
                .Take(_batchSize)
                .ToListAsync(cancellationToken);

            if (due.Count == 0)
            {
                return 0;
            }

            // Settings are fetched once per country rather than once per message; a distribution
            // enqueues hundreds of messages that all share one configuration.
            var countryIds = due.Select(m => m.CountryId).Distinct().ToArray();
            var settingsByCountry = await adminContext.Countries
                .AsNoTracking()
                .Include(c => c.Settings)
                .ThenInclude(s => s.PropertyCollection)
                .Where(c => countryIds.Contains(c.Id))
                .ToDictionaryAsync(c => c.Id, c => c.Settings, cancellationToken);

            foreach (var message in due)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await AttemptAsync(message, settingsByCountry, registry, cancellationToken);
            }

            await context.SaveChangesAsync(cancellationToken);

            return due.Count;
        }

        /// <summary>
        /// One pass down a country's channel chain. A channel that cannot reach this particular
        /// recipient hands straight over to the next one — that is what makes "Telegram, then SMS"
        /// work for a caseload where only some people have enrolled. A transient failure stops the
        /// pass and retries the same channel later, because falling through on a timeout would send
        /// the message twice as soon as the first channel recovered.
        /// </summary>
        private async Task AttemptAsync(
            OutboundMessage message,
            IReadOnlyDictionary<int, Models.Admin.CountrySettings> settingsByCountry,
            MessageChannelRegistry registry,
            CancellationToken cancellationToken)
        {
            settingsByCountry.TryGetValue(message.CountryId, out var settings);

            // The chain is re-read every attempt rather than pinned when the message was queued:
            // the country's configuration can change underneath a queued message, and a recipient
            // who enrols with the bot after their first message failed should be reachable on the
            // retry.
            var chain = registry.ResolveChain(settings, out var problems);

            if (chain.Count == 0)
            {
                Fail(message, string.Join(" ", problems));
                return;
            }

            message.Attempts++;

            var skipped = new List<string>();

            foreach (var channel in chain)
            {
                message.ChannelKey = channel.Key;

                DeliveryResult result;
                try
                {
                    result = await channel.SendAsync(
                        message, ChannelSettings.For(settings, channel.Key), cancellationToken);
                }
                catch (Exception exception)
                {
                    result = DeliveryResult.FromException(exception);
                }

                switch (result.Outcome)
                {
                    case DeliveryOutcome.Sent:
                        message.State = OutboundMessageState.Sent;
                        message.SentOn = DateTime.UtcNow;
                        message.LastError = null;
                        message.ProviderMessageId = result.ProviderMessageId;
                        return;

                    case DeliveryOutcome.Unroutable:
                        skipped.Add($"{channel.Key}: {result.Error}");
                        continue;

                    case DeliveryOutcome.Rejected:
                        Fail(message, Describe(skipped, $"{channel.Key}: {result.Error}"));
                        return;

                    default:
                        if (message.Attempts >= _maxAttempts)
                        {
                            Fail(message, Describe(skipped,
                                $"{channel.Key}: {result.Error} (gave up after {message.Attempts} attempts)"));
                            return;
                        }

                        Retry(message, Describe(skipped, $"{channel.Key}: {result.Error}"));
                        return;
                }
            }

            // Every channel declined this recipient. Retrying will not change that until somebody
            // acts — enrols them, or corrects the number — so this is a failure, not a wait.
            Fail(message, Describe(skipped, "No channel could reach this recipient."));
        }

        private void Retry(OutboundMessage message, string error)
        {
            // Exponential backoff, so a gateway outage is not hammered: 1, 2, 4, 8 minutes.
            var delay = TimeSpan.FromMinutes(Math.Pow(2, message.Attempts - 1));

            message.NextAttemptOn = DateTime.UtcNow.Add(delay);
            message.LastError = error;

            _logger.LogWarning(
                "Message {MessageId} to {Recipient} failed (attempt {Attempt}/{Max}); retrying at {NextAttempt}. {Error}",
                message.Id, message.To, message.Attempts, _maxAttempts, message.NextAttemptOn, error);
        }

        /// <summary>
        /// Keeps the channels that were tried and passed over in the recorded reason, so the
        /// delivery log explains the whole route rather than only its last hop.
        /// </summary>
        private static string Describe(IReadOnlyCollection<string> skipped, string outcome) =>
            skipped.Count == 0 ? outcome : string.Join(" ", skipped.Append(outcome));

        private void Fail(OutboundMessage message, string error)
        {
            message.State = OutboundMessageState.Failed;
            message.LastError = error;

            _logger.LogError(
                "Message {MessageId} to {Recipient} failed permanently. {Error}",
                message.Id, message.To, error);
        }
    }
}
