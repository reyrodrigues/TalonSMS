using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json.Linq;
using TalonAdmin.Models.Admin;
using TalonAdmin.Models.Vouchers;

namespace TalonAdmin.Messaging.Channels
{
    /// <summary>
    /// Delivers through a Telegram bot instead of the mobile network.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Free, instant, and it works over wifi — which matters where beneficiaries have a smartphone
    /// but little credit. It is an addition to SMS rather than a replacement for it: it reaches
    /// only people with a smartphone, data, and a Telegram account, which in most of the contexts
    /// Talon runs in is a minority of a caseload.
    /// </para>
    /// <para>
    /// The structural constraint is that <b>a bot cannot message someone first</b>. Every recipient
    /// has to have tapped an enrolment link, which is what fills <see cref="TelegramContact"/>. A
    /// message to someone not enrolled is rejected rather than retried — waiting will not enrol
    /// them, and the delivery log is where staff find out they need to.
    /// </para>
    /// </remarks>
    public class TelegramChannel : IMessageChannel
    {
        public const string ChannelKey = "telegram";

        /// <summary>Overridable per country, mostly so tests can point it somewhere local.</summary>
        public const string DefaultApiBase = "https://api.telegram.org";

        private readonly HttpClient _httpClient;
        private readonly Context _context;

        public TelegramChannel(HttpClient httpClient, Context context)
        {
            _httpClient = httpClient;
            _context = context;
        }

        public string Key => ChannelKey;

        /// <summary>The bot token lives in ServicePassword, alongside every other channel's secret.</summary>
        public bool CanServe(CountrySettings settings) =>
            !string.IsNullOrWhiteSpace(settings?.ServicePassword);

        public async Task<DeliveryResult> SendAsync(
            OutboundMessage message,
            CountrySettings settings,
            CancellationToken cancellationToken)
        {
            var number = TelegramEnrolment.Normalize(message.To);

            var contact = await _context.TelegramContacts
                .AsNoTracking()
                .Where(c => c.CountryId == message.CountryId && c.NormalizedNumber == number)
                .FirstOrDefaultAsync(cancellationToken);

            if (contact == null)
            {
                return DeliveryResult.Rejected(
                    $"{message.To} has not enrolled with the Telegram bot. Send them their " +
                    "enrolment link, or switch this country to an SMS channel.");
            }

            var payload = new JObject
            {
                ["chat_id"] = contact.ChatId,
                ["text"] = message.Body,
                // Templates are plain text written by field staff; parsing them as Markdown makes an
                // unescaped underscore in a name fail the whole send.
                ["disable_notification"] = false
            };

            var apiBase = string.IsNullOrWhiteSpace(settings.ServiceUrl)
                ? DefaultApiBase
                : settings.ServiceUrl.TrimEnd('/');

            var url = $"{apiBase}/bot{settings.ServicePassword}/sendMessage";

            try
            {
                using var content = new StringContent(payload.ToString(), Encoding.UTF8, "application/json");
                using var response = await _httpClient.PostAsync(url, content, cancellationToken);
                var body = await response.Content.ReadAsStringAsync(cancellationToken);

                if (response.IsSuccessStatusCode)
                {
                    return DeliveryResult.Sent(ReadMessageId(body));
                }

                // Telegram explains itself in the body; the status alone does not distinguish a bad
                // token from a user who has blocked the bot.
                var description = ReadDescription(body) ?? Truncate(body, 300);
                var summary = $"{(int)response.StatusCode}: {description}";

                // 429 carries retry_after and is the one worth waiting on. 400 and 403 mean the
                // chat is gone, the bot is blocked, or the token is wrong — all permanent.
                var retryable = response.StatusCode == HttpStatusCode.TooManyRequests
                    || response.StatusCode >= HttpStatusCode.InternalServerError;

                return retryable ? DeliveryResult.TryAgain(summary) : DeliveryResult.Rejected(summary);
            }
            catch (Exception exception)
            {
                return DeliveryResult.FromException(exception);
            }
        }

        private static string ReadMessageId(string body)
        {
            try
            {
                return JToken.Parse(body)?["result"]?["message_id"]?.ToString();
            }
            catch (Newtonsoft.Json.JsonReaderException)
            {
                return null;
            }
        }

        private static string ReadDescription(string body)
        {
            try
            {
                return JToken.Parse(body)?["description"]?.ToString();
            }
            catch (Newtonsoft.Json.JsonReaderException)
            {
                return null;
            }
        }

        private static string Truncate(string value, int length) =>
            string.IsNullOrEmpty(value) || value.Length <= length ? value : value[..length];
    }
}
