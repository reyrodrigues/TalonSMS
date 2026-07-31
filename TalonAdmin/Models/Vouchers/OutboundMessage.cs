using System;

namespace TalonAdmin.Models.Vouchers
{
    /// <summary>
    /// A message Talon owes someone, and what happened to it.
    /// </summary>
    /// <remarks>
    /// Outbound messages used to be fired off inside a <c>Task.Factory.StartNew</c> that nothing
    /// awaited, with every failure swallowed by an empty catch. A beneficiary who never received
    /// their voucher code was therefore invisible: no record, no retry, and nothing for staff to
    /// reconcile against. Persisting the message before it is sent fixes all three — a restart
    /// resumes the queue, failures are retried with backoff, and the delivery log is a screen.
    /// </remarks>
    public class OutboundMessage : TenantEntity
    {
        public virtual string To { get; set; }

        /// <summary>Display name of the recipient, kept for the delivery log.</summary>
        public virtual string RecipientName { get; set; }

        public virtual string Body { get; set; }

        /// <summary>Which channel implementation should carry this, e.g. <c>sms.clickatell</c>.</summary>
        public virtual string ChannelKey { get; set; }

        public virtual OutboundMessageState State { get; set; }

        public virtual int Attempts { get; set; }

        public virtual DateTime CreatedOn { get; set; }

        /// <summary>When the dispatcher may next try. Backoff moves this forward after a failure.</summary>
        public virtual DateTime NextAttemptOn { get; set; }

        public virtual DateTime? SentOn { get; set; }

        /// <summary>Why the last attempt failed. Shown verbatim in the delivery log.</summary>
        public virtual string LastError { get; set; }

        /// <summary>Whatever the gateway returned to identify the message, when it returns one.</summary>
        public virtual string ProviderMessageId { get; set; }
    }

    public enum OutboundMessageState
    {
        Pending = 0,
        Sent = 1,

        /// <summary>Out of attempts, or rejected in a way retrying cannot fix.</summary>
        Failed = 2
    }
}
