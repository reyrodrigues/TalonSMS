using System;
using System.Collections.Generic;
using System.Linq;
using TalonAdmin.Models.Admin;

namespace TalonAdmin.Messaging
{
    /// <summary>
    /// Works out which channels a country sends through, and in what order.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A country used to have exactly one. That is fine for SMS, where every recipient is reachable
    /// by definition, but not for Telegram: a bot can only message people who have enrolled, so a
    /// country on Telegram alone silently fails for everyone who has not. A chain lets Telegram be
    /// tried first — free and instant — with SMS behind it for everyone else.
    /// </para>
    /// <para>
    /// The chain is stored as an ordered list of channel keys in the country setting property
    /// <c>Message Channels</c>. When it is absent the chain is just whatever
    /// <see cref="CountrySettings.SmsBackendType"/> selects, so a country configured before this
    /// existed keeps working untouched.
    /// </para>
    /// </remarks>
    public class MessageChannelRegistry
    {
        /// <summary>The country setting property holding the ordered, comma-separated chain.</summary>
        public const string ChainPropertyName = "Message Channels";

        private readonly Dictionary<string, IMessageChannel> _byKey;

        public MessageChannelRegistry(IEnumerable<IMessageChannel> channels)
        {
            _byKey = channels.ToDictionary(c => c.Key, StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>Every channel this build knows about.</summary>
        public IReadOnlyCollection<IMessageChannel> All => _byKey.Values;

        /// <summary>
        /// The single channel a country's <c>SmsBackendType</c> selects, or null if the value is
        /// not one this build implements.
        /// </summary>
        /// <remarks>
        /// The numbering here is the contract with <see cref="SmsBackendType"/> and with the
        /// <c>SmsBackendType</c> column. It used to disagree with itself: the enum declared
        /// <c>RescueSMS = 1</c> while the dispatch treated 1 as TurboSMS, so a country configured
        /// through the UI was routed to a different gateway than the one it named. An unrecognised
        /// value resolves to nothing rather than falling through to whichever branch was last.
        /// </remarks>
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

        /// <summary>
        /// The ordered channel keys a country wants to try, before checking whether this build
        /// implements them or its settings are complete enough to use them.
        /// </summary>
        public static IReadOnlyList<string> KeysFor(CountrySettings settings)
        {
            var configured = settings?.PropertyCollection?
                .FirstOrDefault(p => p.Name == ChainPropertyName)?.Value;

            if (!string.IsNullOrWhiteSpace(configured))
            {
                var keys = configured
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray();

                if (keys.Length > 0)
                {
                    return keys;
                }
            }

            var single = KeyFor(settings);
            return single == null ? Array.Empty<string>() : new[] { single };
        }

        public IMessageChannel Find(string key) =>
            key != null && _byKey.TryGetValue(key, out var channel) ? channel : null;

        /// <summary>
        /// The channels that can actually serve this country right now, in order. Keys this build
        /// does not implement, or whose settings are incomplete, are dropped — with the reason
        /// collected in <paramref name="problems"/> so it can be shown rather than guessed at.
        /// </summary>
        public IReadOnlyList<IMessageChannel> ResolveChain(
            CountrySettings settings,
            out IReadOnlyList<string> problems)
        {
            var found = new List<IMessageChannel>();
            var reasons = new List<string>();

            var keys = KeysFor(settings);
            if (keys.Count == 0)
            {
                reasons.Add(
                    "No messaging channel is configured for this country " +
                    $"(SmsBackendType {settings?.SmsBackendType.ToString() ?? "null"}). " +
                    "Set one under Organization Administration → Country Settings.");
            }

            foreach (var key in keys)
            {
                var channel = Find(key);

                if (channel == null)
                {
                    reasons.Add($"The '{key}' channel is selected but not registered in this build.");
                }
                else if (!channel.CanServe(ChannelSettings.For(settings, key)))
                {
                    reasons.Add($"The '{key}' channel is selected but its country settings are incomplete.");
                }
                else
                {
                    found.Add(channel);
                }
            }

            problems = reasons;
            return found;
        }

        /// <summary>
        /// Whether anything can send at all, and why not when nothing can. Used at enqueue time, so
        /// a misconfigured country produces a failed row immediately rather than at some later
        /// point in a log nobody reads.
        /// </summary>
        public bool TryResolve(CountrySettings settings, out IMessageChannel channel, out string problem)
        {
            var chain = ResolveChain(settings, out var problems);

            channel = chain.FirstOrDefault();
            problem = channel == null ? string.Join(" ", problems) : null;

            return channel != null;
        }
    }
}
