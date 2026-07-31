using System;
using System.Threading;
using System.Threading.Tasks;
using TalonAdmin.Models.Admin;
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
        /// Whether this channel can serve the given country. Implementations check that the
        /// settings they need are actually filled in, so a half-configured country fails with a
        /// clear message instead of a null reference at send time.
        /// </summary>
        bool CanServe(CountrySettings settings);

        Task<DeliveryResult> SendAsync(OutboundMessage message, CountrySettings settings, CancellationToken cancellationToken);
    }

    /// <summary>
    /// The outcome of one attempt. <see cref="Retryable"/> is the important part: a timeout or a
    /// 503 deserves another go, an invalid number or a rejected credential does not, and retrying
    /// the latter just burns attempts and delays the failure staff need to see.
    /// </summary>
    public sealed class DeliveryResult
    {
        private DeliveryResult(bool succeeded, bool retryable, string error, string providerMessageId)
        {
            Succeeded = succeeded;
            Retryable = retryable;
            Error = error;
            ProviderMessageId = providerMessageId;
        }

        public bool Succeeded { get; }
        public bool Retryable { get; }
        public string Error { get; }
        public string ProviderMessageId { get; }

        public static DeliveryResult Sent(string providerMessageId = null) =>
            new DeliveryResult(true, false, null, providerMessageId);

        /// <summary>A transient failure: the dispatcher will try again after a backoff.</summary>
        public static DeliveryResult TryAgain(string error) =>
            new DeliveryResult(false, true, error, null);

        /// <summary>A failure retrying cannot fix. The message is marked failed immediately.</summary>
        public static DeliveryResult Rejected(string error) =>
            new DeliveryResult(false, false, error, null);

        public static DeliveryResult FromException(Exception exception) =>
            // A cancellation is the host shutting down mid-send, not a bad message.
            exception is OperationCanceledException
                ? TryAgain("The send was cancelled before it completed.")
                : TryAgain(exception.Message);
    }
}
