using System;

namespace TalonAdmin.Models.Vouchers
{
    /// <summary>
    /// Links a phone number Talon knows to the Telegram chat it can be reached on.
    /// </summary>
    /// <remarks>
    /// The Bot API does not let a bot message someone who has not written to it first, so a chat id
    /// cannot be derived from a mobile number — it only exists once the person has tapped Start.
    /// This table is what that step produces, and the reason Telegram needs an enrolment flow when
    /// SMS does not.
    /// </remarks>
    public class TelegramContact : TenantEntity
    {
        /// <summary>Digits only, so a number stored as +962 79… still matches 00962 79….</summary>
        public virtual string NormalizedNumber { get; set; }

        /// <summary>As the recipient has it on file, kept for the delivery log and support.</summary>
        public virtual string MobileNumber { get; set; }

        public virtual long ChatId { get; set; }

        /// <summary>Telegram handle, when the account has one. Display only.</summary>
        public virtual string UserName { get; set; }

        public virtual DateTime EnrolledOn { get; set; }
    }
}
