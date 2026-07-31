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

        private async Task AttemptAsync(
            OutboundMessage message,
            IReadOnlyDictionary<int, Models.Admin.CountrySettings> settingsByCountry,
            MessageChannelRegistry registry,
            CancellationToken cancellationToken)
        {
            settingsByCountry.TryGetValue(message.CountryId, out var settings);

            if (!registry.TryResolve(settings, out var channel, out var problem))
            {
                Fail(message, problem);
                return;
            }

            // The channel a message was queued for can be reconfigured underneath it. Honour the
            // country's current choice, and record it, so the log says how it actually went out.
            message.ChannelKey = channel.Key;
            message.Attempts++;

            DeliveryResult result;
            try
            {
                result = await channel.SendAsync(message, settings, cancellationToken);
            }
            catch (Exception exception)
            {
                result = DeliveryResult.FromException(exception);
            }

            if (result.Succeeded)
            {
                message.State = OutboundMessageState.Sent;
                message.SentOn = DateTime.UtcNow;
                message.LastError = null;
                message.ProviderMessageId = result.ProviderMessageId;
                return;
            }

            if (!result.Retryable || message.Attempts >= _maxAttempts)
            {
                var suffix = result.Retryable ? $" (gave up after {message.Attempts} attempts)" : "";
                Fail(message, result.Error + suffix);
                return;
            }

            // Exponential backoff, so a gateway outage is not hammered: 1, 2, 4, 8 minutes.
            var delay = TimeSpan.FromMinutes(Math.Pow(2, message.Attempts - 1));
            message.NextAttemptOn = DateTime.UtcNow.Add(delay);
            message.LastError = result.Error;

            _logger.LogWarning(
                "Message {MessageId} to {Recipient} failed (attempt {Attempt}/{Max}); retrying at {NextAttempt}. {Error}",
                message.Id, message.To, message.Attempts, _maxAttempts, message.NextAttemptOn, result.Error);
        }

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
