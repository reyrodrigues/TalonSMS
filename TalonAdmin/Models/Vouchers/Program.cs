using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Web;
using System.Xml.Serialization;

namespace TalonAdmin.Models.Vouchers
{
    public class Program : TenantEntity
    {
        public string Name { get; set; }
        public string FundCodes { get; set; }

        [JsonIgnore, XmlIgnore, IgnoreDataMember]
        public virtual ICollection<Distribution> Distributions { get; set; }
        
    }
}