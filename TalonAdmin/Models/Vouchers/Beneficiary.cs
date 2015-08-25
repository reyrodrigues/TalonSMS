using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Runtime.Serialization;
using System.Security.Cryptography;
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

        public virtual int? GroupId { get; set; }
        public virtual int? LocationId { get; set; }

        public virtual string PIN { get; set; }

        [JsonIgnore, XmlIgnore]
        public virtual string CardId { get; set; }

        [JsonIgnore, XmlIgnore]
        public virtual string CardKey { get; set; }

        public virtual BeneficiaryGroup Group { get; set; }
        public virtual Location Location { get; set; }

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
            using (Aes rm = new AesCryptoServiceProvider())
            {
                rm.GenerateKey();
                this.CardKey = Convert.ToBase64String(rm.Key);
            }
        }
    }
}