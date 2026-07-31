namespace TalonAdmin.Auth
{
    /// <summary>
    /// Names of the rate-limiting policies configured in Program.cs, kept here so the endpoints that
    /// opt into them cannot drift from the registrations by way of a mistyped string.
    /// </summary>
    public static class RateLimitPolicies
    {
        /// <summary>
        /// Sign-in endpoints, where the value being guessed is a password.
        /// </summary>
        public const string Credentials = "credentials";

        /// <summary>
        /// The inbound SMS webhook, where the value being guessed is a voucher code.
        /// </summary>
        public const string SmsRedemption = "sms-redemption";
    }
}
