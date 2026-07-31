using System;
using System.Collections.Generic;
using System.Linq;
using TalonAdmin.Models.Admin;

namespace TalonAdmin.Messaging
{
    /// <summary>
    /// Maps a country's configured backend onto the channel that serves it.
    /// </summary>
    /// <remarks>
    /// The numbering here is the contract with <see cref="SmsBackendType"/> and with the
    /// <c>SmsBackendType</c> column. It used to disagree with itself: the enum declared
    /// <c>RescueSMS = 1</c> while the dispatch treated 1 as TurboSMS, so a country configured
    /// through the UI was routed to a different gateway than the one it named. Resolution is
    /// driven off the enum now, and an unrecognised value fails loudly rather than falling through
    /// to whichever branch happened to be last.
    /// </remarks>
    public class MessageChannelRegistry
    {
        private readonly Dictionary<string, IMessageChannel> _byKey;

        public MessageChannelRegistry(IEnumerable<IMessageChannel> channels)
        {
            _byKey = channels.ToDictionary(c => c.Key, StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>The channel key a country's settings select, or null if the value is unknown.</summary>
        public static string KeyFor(CountrySettings settings) =>
            settings == null
                ? null
                : (SmsBackendType)settings.SmsBackendType switch
                {
                    SmsBackendType.Clickatell => Channels.ClickatellChannel.ChannelKey,
                    SmsBackendType.AndroidGateway => Channels.AndroidGatewayChannel.ChannelKey,
                    SmsBackendType.Telegram => Channels.TelegramChannel.ChannelKey,
                    _ => null
                };

        public IMessageChannel Find(string key) =>
            key != null && _byKey.TryGetValue(key, out var channel) ? channel : null;

        /// <summary>
        /// Resolves the channel for a country, explaining the failure when there is not one. The
        /// reason is stored on the message so it shows up in the delivery log instead of being
        /// swallowed.
        /// </summary>
        public bool TryResolve(CountrySettings settings, out IMessageChannel channel, out string problem)
        {
            channel = null;
            problem = null;

            var key = KeyFor(settings);
            if (key == null)
            {
                problem = $"No messaging channel is configured for this country " +
                          $"(SmsBackendType {settings?.SmsBackendType.ToString() ?? "null"}). " +
                          "Set one under Organization Administration → Country Settings.";
                return false;
            }

            channel = Find(key);
            if (channel == null)
            {
                problem = $"The '{key}' channel is selected but not registered in this build.";
                return false;
            }

            if (!channel.CanServe(settings))
            {
                channel = null;
                problem = $"The '{key}' channel is selected but its country settings are incomplete.";
                return false;
            }

            return true;
        }
    }
}
