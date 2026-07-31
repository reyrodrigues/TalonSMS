using System;
using System.Threading;
using System.Threading.Tasks;
using TalonAdmin.Models.Vouchers;

namespace TalonAdmin.Messaging
{
    /// <summary>
    /// One way of getting a message to a person: an SMS gateway, a phone acting as a modem, a chat
    /// bot. Adding a channel means adding an implementation and registering it — not another branch
    /// in the workflow controller, which is where all of this used to live.
    /// </summary>
    public interface IMessageChannel
    {
        /// <summary>
        /// Stable identifier persisted on the message, so the delivery log still says how a message
        /// was sent after the country's configuration has moved on.
        /// </summary>
        string Key { get; }

        /// <summary>
        /// Whether this channel can serve a country configured this way. Implementations check that
        /// the settings they need are actually filled in, so a half-configured country fails with a
        /// clear message instead of a null reference at send time.
        /// </summary>
        bool CanServe(ChannelSettings settings);

        Task<DeliveryResult> SendAsync(OutboundMessage message, ChannelSettings settings, CancellationToken cancellationToken);
    }

    /// <summary>
    /// The outcome of one attempt, and what the dispatcher should do next.
    /// </summary>
    /// <remarks>
    /// The distinction between the three failures is the whole point. A timeout deserves another
    /// go; a rejected credential does not, and retrying it only burns attempts and delays the error
    /// staff need to see; and "I cannot reach this person at all" is different again — another
    /// channel might.
    /// </remarks>
    public sealed class DeliveryResult
    {
        private DeliveryResult(DeliveryOutcome outcome, string error, string providerMessageId)
        {
            Outcome = outcome;
            Error = error;
            ProviderMessageId = providerMessageId;
        }

        public DeliveryOutcome Outcome { get; }
        public string Error { get; }
        public string ProviderMessageId { get; }

        public bool Succeeded => Outcome == DeliveryOutcome.Sent;
        public bool Retryable => Outcome == DeliveryOutcome.Transient;

        public static DeliveryResult Sent(string providerMessageId = null) =>
            new DeliveryResult(DeliveryOutcome.Sent, null, providerMessageId);

        /// <summary>A transient failure: the dispatcher will try again after a backoff.</summary>
        public static DeliveryResult TryAgain(string error) =>
            new DeliveryResult(DeliveryOutcome.Transient, error, null);

        /// <summary>A failure retrying cannot fix. The message is marked failed immediately.</summary>
        public static DeliveryResult Rejected(string error) =>
            new DeliveryResult(DeliveryOutcome.Rejected, error, null);

        /// <summary>
        /// This channel cannot reach this particular recipient — a Telegram number that never
        /// enrolled, say. Nothing is wrong with the message or the channel, so the dispatcher moves
        /// straight on to the next channel in the country's chain rather than failing or waiting.
        /// </summary>
        public static DeliveryResult Unroutable(string error) =>
            new DeliveryResult(DeliveryOutcome.Unroutable, error, null);

        public static DeliveryResult FromException(Exception exception) =>
            // A cancellation is the host shutting down mid-send, not a bad message.
            exception is OperationCanceledException
                ? TryAgain("The send was cancelled before it completed.")
                : TryAgain(exception.Message);
    }

    public enum DeliveryOutcome
    {
        Sent,

        /// <summary>Worth another attempt on the same channel.</summary>
        Transient,

        /// <summary>Permanently refused; no other channel is likely to do better.</summary>
        Rejected,

        /// <summary>This channel cannot serve this recipient; try the next one.</summary>
        Unroutable
    }
}
