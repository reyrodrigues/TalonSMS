using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TalonAdmin.Models.Admin;
using TalonAdmin.Models.Vouchers;

namespace TalonAdmin.Messaging.Channels
{
    /// <summary>
    /// Clickatell's HTTP API, driven by a URL template held in the country's settings.
    /// </summary>
    /// <remarks>
    /// The template is a composite format string whose <c>{0}</c> is the recipient and <c>{1}</c>
    /// the message, e.g.
    /// <c>https://platform.clickatell.com/messages/http/send?apiKey=…&amp;to={0}&amp;content={1}</c>.
    /// That shape is inherited: it is what deployed countries already have in
    /// <c>CountrySettings.ServiceUrl</c>, so it is kept rather than migrated.
    /// </remarks>
    public class ClickatellChannel : IMessageChannel
    {
        public const string ChannelKey = "sms.clickatell";

        private readonly HttpClient _httpClient;

        public ClickatellChannel(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public string Key => ChannelKey;

        public bool CanServe(CountrySettings settings) =>
            !string.IsNullOrWhiteSpace(settings?.ServiceUrl);

        public async Task<DeliveryResult> SendAsync(
            OutboundMessage message,
            CountrySettings settings,
            CancellationToken cancellationToken)
        {
            string url;
            try
            {
                url = BuildUrl(settings.ServiceUrl, message.To, message.Body);
            }
            catch (FormatException exception)
            {
                // A malformed template is a configuration error; retrying will not repair it.
                return DeliveryResult.Rejected(
                    $"The Clickatell service URL is not a valid template: {exception.Message}");
            }

            try
            {
                using var response = await _httpClient.GetAsync(url, cancellationToken);
                var body = await response.Content.ReadAsStringAsync(cancellationToken);

                if (response.IsSuccessStatusCode)
                {
                    return DeliveryResult.Sent(ExtractMessageId(body));
                }

                var summary = $"{(int)response.StatusCode} {response.ReasonPhrase}: {Truncate(body, 300)}";

                // 4xx means Clickatell understood us and said no — bad credentials, a barred
                // number, no balance. Only 408 and 429 are worth another attempt.
                var retryable = response.StatusCode >= HttpStatusCode.InternalServerError
                    || response.StatusCode == HttpStatusCode.RequestTimeout
                    || response.StatusCode == HttpStatusCode.TooManyRequests;

                return retryable ? DeliveryResult.TryAgain(summary) : DeliveryResult.Rejected(summary);
            }
            catch (Exception exception)
            {
                return DeliveryResult.FromException(exception);
            }
        }

        /// <summary>
        /// Fills the template. Non-ASCII text is sent as UCS-2 hex with <c>unicode=1</c> appended,
        /// which is how the gateway is told the content is not GSM-7 — Arabic voucher messages
        /// arrive as mojibake otherwise.
        /// </summary>
        public static string BuildUrl(string template, string to, string body)
        {
            body ??= string.Empty;

            var isUnicode = body.Any(c => c > 255);

            if (isUnicode)
            {
                var bytes = Encoding.BigEndianUnicode.GetBytes(body);
                body = string.Concat(bytes.Select(b => b.ToString("X2")));
            }

            var url = string.Format(template, Uri.EscapeDataString(to), Uri.EscapeDataString(body));

            if (isUnicode)
            {
                url += url.Contains('?') ? "&unicode=1" : "?unicode=1";
            }

            return url;
        }

        /// <summary>
        /// Clickatell answers with either <c>ID: xxxxx</c> or a JSON body carrying an apiMessageId.
        /// The id is only used for support enquiries, so a miss is not an error.
        /// </summary>
        private static string ExtractMessageId(string body)
        {
            if (string.IsNullOrWhiteSpace(body))
            {
                return null;
            }

            var match = System.Text.RegularExpressions.Regex.Match(
                body, "\"?(?:apiMessageId|messageId|ID)\"?\\s*[:=]\\s*\"?([A-Za-z0-9_-]+)");

            return match.Success ? match.Groups[1].Value : null;
        }

        private static string Truncate(string value, int length) =>
            string.IsNullOrEmpty(value) || value.Length <= length ? value : value[..length];
    }
}
