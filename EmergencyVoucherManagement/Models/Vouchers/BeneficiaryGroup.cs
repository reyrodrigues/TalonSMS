using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Web;
using System.Xml.Serialization;

namespace EmergencyVoucherManagement.Models.Vouchers
{
    public class BeneficiaryGroup : TenantEntity
    {
        public virtual string Name { get; set; }

        [JsonIgnore, XmlIgnore, IgnoreDataMember]
        public virtual ICollection<Beneficiary> Beneficiaries { get; set; }
    }
}