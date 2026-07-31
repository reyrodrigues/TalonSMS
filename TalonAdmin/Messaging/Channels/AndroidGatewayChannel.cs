using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using TalonAdmin.Models.Vouchers;

namespace TalonAdmin.Messaging.Channels
{
    /// <summary>
    /// An Android handset with a local SIM, acting as the modem.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the cheapest way to reach beneficiaries in most of the places Talon runs: messages
    /// go out at domestic rates on a SIM bought locally, which sidesteps both the cost of
    /// international A2P routes and the countries that block them outright. No aggregator contract
    /// is involved.
    /// </para>
    /// <para>
    /// The wire format is the one <c>android-sms-gateway</c> (sms-gate.app) exposes, because that
    /// is the app most likely to already be installed: <c>POST {base}/message</c> with Basic auth
    /// and <c>{ "message": …, "phoneNumbers": [ … ] }</c>. It has two deployment modes and this
    /// channel does not care which is in use — the difference is only what URL is configured:
    /// </para>
    /// <list type="bullet">
    /// <item><description>
    /// <b>Local</b>: the phone serves its own API on the office LAN and Talon posts straight to it.
    /// </description></item>
    /// <item><description>
    /// <b>Cloud</b>: the phone holds an outbound connection to a relay and Talon posts to the
    /// relay. This is what makes the channel work at all when the handset sits behind NAT with no
    /// inbound reachability, which is the normal case for a field office.
    /// </description></item>
    /// </list>
    /// <para>
    /// A consumer SIM is rate-limited by the carrier — somewhere between 30 and 100 messages a day
    /// before throttling, in most markets. This suits a field office sending hundreds, not a
    /// national programme sending tens of thousands.
    /// </para>
    /// </remarks>
    public class AndroidGatewayChannel : IMessageChannel
    {
        public const string ChannelKey = "sms.android";

        private readonly HttpClient _httpClient;

        public AndroidGatewayChannel(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public string Key => ChannelKey;

        /// <summary>
        /// The gateway always requires a password: its API is a send-anything endpoint on a phone
        /// with a real SIM, so an unauthenticated one would be an open relay.
        /// </summary>
        public bool CanServe(ChannelSettings settings) =>
            !string.IsNullOrWhiteSpace(settings?.ServiceUrl)
            && !string.IsNullOrWhiteSpace(settings?.ServicePassword);

        public async Task<DeliveryResult> SendAsync(
            OutboundMessage message,
            ChannelSettings settings,
            CancellationToken cancellationToken)
        {
            var payload = new JObject
            {
                ["message"] = message.Body,
                ["phoneNumbers"] = new JArray(message.To)
            };

            using var request = new HttpRequestMessage(HttpMethod.Post, BuildUrl(settings.ServiceUrl))
            {
                Content = new StringContent(payload.ToString(), Encoding.UTF8, "application/json")
            };

            var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes(
                $"{settings.ServiceUser ?? "sms"}:{settings.ServicePassword}"));
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);

            try
            {
                using var response = await _httpClient.SendAsync(request, cancellationToken);
                var body = await response.Content.ReadAsStringAsync(cancellationToken);

                if (response.IsSuccessStatusCode)
                {
                    return DeliveryResult.Sent(ExtractMessageId(body));
                }

                var summary = $"{(int)response.StatusCode} {response.ReasonPhrase}: {Truncate(body, 300)}";

                // A phone that is asleep, out of signal or briefly off the network answers with a
                // connection failure or a 5xx, and will very likely be back. A 401 means the
                // gateway password is wrong, and a 400 means it rejected the number.
                var retryable = response.StatusCode >= HttpStatusCode.InternalServerError
                    || response.StatusCode == HttpStatusCode.RequestTimeout
                    || response.StatusCode == HttpStatusCode.TooManyRequests
                    || response.StatusCode == HttpStatusCode.ServiceUnavailable;

                return retryable ? DeliveryResult.TryAgain(summary) : DeliveryResult.Rejected(summary);
            }
            catch (Exception exception)
            {
                // An unreachable handset is the normal transient case, not a bad message.
                return DeliveryResult.FromException(exception);
            }
        }

        /// <summary>
        /// Accepts either the API root or the full send endpoint, because both are plausible things
        /// to paste into a settings field.
        /// </summary>
        public static string BuildUrl(string serviceUrl)
        {
            var trimmed = serviceUrl.TrimEnd('/');

            return trimmed.EndsWith("/message", StringComparison.OrdinalIgnoreCase)
                ? trimmed
                : trimmed + "/message";
        }

        /// <summary>
        /// The gateway answers with the id it assigned, which is what a support enquiry needs;
        /// nothing downstream depends on it, so a miss is not an error.
        /// </summary>
        public static string ExtractMessageId(string body)
        {
            if (string.IsNullOrWhiteSpace(body))
            {
                return null;
            }

            try
            {
                return JToken.Parse(body)?["id"]?.ToString();
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
