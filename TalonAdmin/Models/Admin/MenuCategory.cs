using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Web;
using System.Xml.Serialization;

namespace TalonAdmin.Models.Admin
{
    public class MenuCategory : Entity
    {
        public virtual string Name { get; set; }
        public virtual int SortOrder { get; set; }
        public virtual bool? NeedsCountry { get; set; }

        public virtual ICollection<MenuItem> Items { get; set; }

        [JsonIgnore, XmlIgnore, IgnoreDataMember]
        public virtual ICollection<MenuCategoryRole> Roles { get; set; }
    }
}