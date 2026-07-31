using System;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;

namespace TalonAdmin.Auth
{
    /// <summary>
    /// The shared secret the two inbound webhooks require — the SMS gateway callback and the
    /// Telegram update endpoint. Both are anonymous by necessity: a gateway cannot hold a user's
    /// credentials, and what arrives on them redeems value.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This used to read <c>if (expected != presented &amp;&amp; expected != "")</c>, which meant an
    /// unconfigured secret was not a failed check but <em>no</em> check: a deployment that had never
    /// set <c>SystemSecret</c> accepted a redemption from anyone who could reach the URL. The
    /// startup warning said so, and a warning in a log is not a control.
    /// </para>
    /// <para>
    /// It fails closed now. No secret configured means no inbound redemption, which is the safe
    /// direction to be wrong in when the alternative is a stranger spending a household's voucher.
    /// </para>
    /// </remarks>
    public static class InboundWebhookSecret
    {
        public enum Result
        {
            /// <summary>The presented secret matches.</summary>
            Ok,

            /// <summary>No secret is configured, so nothing can be accepted.</summary>
            NotConfigured,

            /// <summary>A secret is configured and this is not it.</summary>
            Invalid
        }

        public const string NotConfiguredMessage =
            "This endpoint is not configured to accept inbound messages. Set SystemSecret.";

        public const string InvalidMessage = "Secret is invalid.";

        public static Result Check(IConfiguration configuration, string presented)
        {
            var expected = configuration["SystemSecret"];

            if (string.IsNullOrWhiteSpace(expected))
            {
                return Result.NotConfigured;
            }

            return FixedTimeEquals(expected, presented ?? "") ? Result.Ok : Result.Invalid;
        }

        /// <summary>
        /// Compares without returning early on the first differing byte. The previous <c>!=</c>
        /// leaked, in its timing, how much of a guess was correct — which against an endpoint that
        /// can be called repeatedly is a way to recover the secret one character at a time.
        /// Differing lengths short-circuit, but a length was never the secret.
        /// </summary>
        private static bool FixedTimeEquals(string expected, string presented) =>
            CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(expected),
                Encoding.UTF8.GetBytes(presented));
    }
}
