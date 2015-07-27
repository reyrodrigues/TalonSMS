using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Web;
using System.Xml.Serialization;

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

        public virtual string BirthYear { get; set; }

        public virtual int Sex { get; set; }

        public virtual bool? Disabled { get; set; }
        public virtual bool? WasWelcomeMessageSent { get; set; }
        public virtual int? GroupId { get; set; }
        public virtual int? LocationId { get; set; }

        public virtual string PIN { get; set; }

        [JsonIgnore, XmlIgnore]
        public virtual string CardId { get; set; }

        [JsonIgnore, XmlIgnore]
        public virtual string CardKey { get; set; }

        public virtual BeneficiaryGroup Group { get; set; }
        public virtual Location Location { get; set; }
    }
}