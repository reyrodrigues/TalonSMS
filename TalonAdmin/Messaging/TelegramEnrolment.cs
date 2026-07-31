using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;

namespace TalonAdmin.Messaging
{
    /// <summary>
    /// Builds and checks the one-time payload carried by a Telegram enrolment link.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A bot cannot start a conversation, so a beneficiary has to tap a link that opens the bot and
    /// sends <c>/start &lt;payload&gt;</c>. The payload has to say which phone number is enrolling,
    /// and it arrives from the public internet, so it has to be unforgeable: anyone who could mint
    /// a payload could point another person's voucher messages at their own Telegram account.
    /// </para>
    /// <para>
    /// The payload is therefore the country and number plus an HMAC of both, keyed on the
    /// application's signing key. Telegram caps the <c>start</c> parameter at 64 characters and
    /// allows only <c>A-Z a-z 0-9 _ -</c>, which is why the encoding is base64url and the tag is
    /// truncated. Sixteen bytes of tag is far more than enough for a value that is only useful
    /// against one country's bot.
    /// </para>
    /// </remarks>
    public class TelegramEnrolment
    {
        private const int TagBytes = 16;

        private readonly byte[] _key;

        public TelegramEnrolment(IConfiguration configuration)
        {
            // The JWT signing key is already required to be set outside development, so reuse it
            // rather than adding another secret nobody remembers to configure.
            var secret = configuration["Jwt:SigningKey"]
                ?? configuration["SystemSecret"]
                ?? "talon-development-enrolment-key";

            _key = Encoding.UTF8.GetBytes(secret);
        }

        /// <summary>
        /// Digits only, with the international access prefix removed, so <c>+962 79…</c>,
        /// <c>00962 79…</c> and <c>96279…</c> all compare equal.
        /// </summary>
        /// <remarks>
        /// A number stored in national format — a leading trunk <c>0</c> and no country code — will
        /// not match one stored internationally. Resolving that needs the country's calling code
        /// and is a wider change than enrolment: the same ambiguity already reaches the SMS
        /// gateways, which pass the stored number through untouched.
        /// </remarks>
        public static string Normalize(string mobileNumber)
        {
            var digits = new string((mobileNumber ?? string.Empty).Where(char.IsDigit).ToArray());

            return digits.StartsWith("00", StringComparison.Ordinal) ? digits[2..] : digits;
        }

        /// <summary>The <c>start</c> parameter for a recipient's enrolment link.</summary>
        public string CreatePayload(int countryId, string mobileNumber)
        {
            var number = Normalize(mobileNumber);
            var body = $"{countryId}.{number}";

            return $"{Encode(Encoding.UTF8.GetBytes(body))}~{Encode(Tag(body))}";
        }

        /// <summary>The link staff send to a beneficiary, which opens the bot with the payload.</summary>
        public string CreateLink(string botUserName, int countryId, string mobileNumber) =>
            $"https://t.me/{botUserName}?start={CreatePayload(countryId, mobileNumber)}";

        /// <summary>
        /// Checks a payload and recovers who it is for. Returns false for anything tampered with,
        /// truncated or simply not one of ours.
        /// </summary>
        public bool TryReadPayload(string payload, out int countryId, out string normalizedNumber)
        {
            countryId = 0;
            normalizedNumber = null;

            if (string.IsNullOrWhiteSpace(payload))
            {
                return false;
            }

            var parts = payload.Split('~');
            if (parts.Length != 2)
            {
                return false;
            }

            string body;
            byte[] presented;
            try
            {
                body = Encoding.UTF8.GetString(Decode(parts[0]));
                presented = Decode(parts[1]);
            }
            catch (FormatException)
            {
                return false;
            }

            // Fixed-time comparison: this is a signature check reachable from the open internet.
            if (!CryptographicOperations.FixedTimeEquals(presented, Tag(body)))
            {
                return false;
            }

            var fields = body.Split('.');
            if (fields.Length != 2 || !int.TryParse(fields[0], out countryId) || fields[1].Length == 0)
            {
                return false;
            }

            normalizedNumber = fields[1];
            return true;
        }

        private byte[] Tag(string body)
        {
            using var hmac = new HMACSHA256(_key);
            return hmac.ComputeHash(Encoding.UTF8.GetBytes(body))[..TagBytes];
        }

        // base64url: Telegram's start parameter allows only A-Z a-z 0-9 _ and -.
        private static string Encode(byte[] value) =>
            Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');

        private static byte[] Decode(string value)
        {
            var padded = value.Replace('-', '+').Replace('_', '/');
            return Convert.FromBase64String(padded.PadRight((padded.Length + 3) / 4 * 4, '='));
        }
    }
}
