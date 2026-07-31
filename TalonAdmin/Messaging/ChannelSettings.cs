using System;
using System.Linq;
using TalonAdmin.Models.Admin;

namespace TalonAdmin.Messaging
{
    /// <summary>
    /// One channel's slice of a country's configuration.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A country used to hold exactly one service URL, user and password, which was enough when it
    /// sent through exactly one gateway. A chain cannot share them: "Telegram, then Clickatell"
    /// needs a bot token *and* a gateway URL at the same time.
    /// </para>
    /// <para>
    /// So each channel may carry its own credentials in the country's property collection, named
    /// <c>Channel &lt;key&gt; Url</c>, <c>… User</c> and <c>… Password</c>. A channel with none of
    /// its own falls back to the country's original three fields, which is what keeps every
    /// single-channel country working exactly as it did.
    /// </para>
    /// </remarks>
    public sealed class ChannelSettings
    {
        public string ServiceUrl { get; init; }
        public string ServiceUser { get; init; }
        public string ServicePassword { get; init; }

        /// <summary>
        /// The country these came from, for the handful of settings that are not credentials —
        /// the Telegram bot username, for instance.
        /// </summary>
        public CountrySettings Country { get; init; }

        public string Property(string name) =>
            Country?.PropertyCollection?.FirstOrDefault(p => p.Name == name)?.Value;

        /// <summary>Resolves the settings a named channel should use for a country.</summary>
        public static ChannelSettings For(CountrySettings country, string channelKey)
        {
            if (country == null)
            {
                return new ChannelSettings();
            }

            var prefix = $"Channel {channelKey} ";

            string Scoped(string name) => country.PropertyCollection?
                .FirstOrDefault(p => p.Name == prefix + name)?.Value;

            var url = Scoped("Url");
            var user = Scoped("User");
            var password = Scoped("Password");

            // All-or-nothing: a channel either carries its own credentials or uses the country's.
            // Mixing them silently would make a half-filled override look like it had worked.
            var hasOwn = !string.IsNullOrWhiteSpace(url)
                || !string.IsNullOrWhiteSpace(user)
                || !string.IsNullOrWhiteSpace(password);

            return hasOwn
                ? new ChannelSettings
                {
                    ServiceUrl = url,
                    ServiceUser = user,
                    ServicePassword = password,
                    Country = country
                }
                : new ChannelSettings
                {
                    ServiceUrl = country.ServiceUrl,
                    ServiceUser = country.ServiceUser,
                    ServicePassword = country.ServicePassword,
                    Country = country
                };
        }
    }
}
