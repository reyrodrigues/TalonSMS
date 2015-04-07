using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;

namespace TalonAdmin.Models.Vouchers
{
    public class Beneficiary : TenantEntity
    {
        public string Name
        {
            get
            {
                return String.Format("{0} {1}", FirstName, LastName);
            }
        }

        public virtual string FirstName { get; set; }
        public virtual string LastName { get; set; }

        public virtual string NationalId { get; set; }

        public virtual string IRCId { get; set; }

        public virtual string MobileNumber { get; set; }

        public virtual bool? Disabled { get; set; }
        public virtual bool? WasWelcomeMessageSent { get; set; }

        public virtual string PIN { get; set; }

        public virtual int? GroupId { get; set; }
        public virtual BeneficiaryGroup Group { get; set; }

        public virtual int? LocationId { get; set; }
        public virtual Location Location { get; set; }

        public virtual ICollection<BeneficiaryDistribution> Distributions { get; set; }
    }
}