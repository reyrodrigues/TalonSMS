using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Web;
using System.Xml.Serialization;

namespace TalonAdmin.Models.Admin
{
    public class ApplicationUserCountry : Entity
    {
        public virtual string ApplicationUserId { get; set; }
        public virtual int CountryId { get; set; }

        [JsonIgnore, XmlIgnore, IgnoreDataMember]
        public virtual ApplicationUser ApplicationUser { get; set; }

        public virtual Country Country { get; set; }
    }
}