using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Runtime.Serialization;
using System.Security.Cryptography;
using System.Xml.Serialization;

namespace TalonAdmin.Models.Vouchers
{
    public class Beneficiary : TenantEntity
    {
        [NotMapped]
        public string Name
        {
            get
            {
                return String.Format("{0} {1}", FirstName, LastName);
            }
        }

        public virtual string FirstName { get; set; }
        public virtual string LastName { get; set; }

        /// <summary>
        /// The identifier a vendor is asked for when redeeming a voucher, to confirm they are
        /// serving the right person.
        /// </summary>
        /// <remarks>
        /// It was called NationalId, which described one particular way of filling it in and a poor
        /// one: many of the people a cash programme reaches have no national identity document, and
        /// recording a real one where it is not needed creates a protection risk. It can be a
        /// programme-issued number instead — Talon can generate one — so the name no longer implies
        /// a government document.
        ///
        /// The column keeps its old name because this application has no migration framework, so a
        /// rename would break every existing database on the next query.
        /// </remarks>
        [Column("NationalId")]
        public virtual string PersonalIdentifier { get; set; }

        public virtual string IRCId { get; set; }

        public virtual string MobileNumber { get; set; }

        public virtual string BirthYear { get; set; }
        public virtual int Sex { get; set; }

        public virtual bool? Disabled { get; set; }

        public virtual int? GroupId { get; set; }
        public virtual int? LocationId { get; set; }

        public virtual string PIN { get; set; }

        [JsonIgnore, XmlIgnore]
        public virtual string CardId { get; set; }

        [JsonIgnore, XmlIgnore]
        public virtual string CardKey { get; set; }

        public virtual BeneficiaryGroup Group { get; set; }
        public virtual Location Location { get; set; }

        [NotMapped]
        public JToken AdditionalDataObject
        {
            get
            {
                if (AdditionalData == null)
                {
                    return new JObject();
                }
                return JToken.FromObject(AdditionalData.ToDictionary(k => k.Key, v => v.Value));
            }
        }

        public virtual ICollection<BeneficiaryAdditionalData> AdditionalData { get; set; }

        internal void GenerateKey()
        {
            using (Aes rm = Aes.Create())
            {
                rm.GenerateKey();
                this.CardKey = Convert.ToBase64String(rm.Key);
            }
        }
    }
}