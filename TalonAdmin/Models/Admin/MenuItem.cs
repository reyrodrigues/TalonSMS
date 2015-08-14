using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Web;
using System.Xml.Serialization;

namespace TalonAdmin.Models.Admin
{
    public class MenuItem : Entity
    {
        public virtual string State { get; set; }
        public virtual string Title { get; set; }
        public virtual string CssClass { get; set; }
        public virtual int? CategoryId { get; set; }
        public virtual int? ParentId { get; set; }

        public virtual MenuCategory Category { get; set; }

        [IgnoreDataMember, XmlIgnore, JsonIgnore]
        public virtual MenuItem Parent { get; set; }

        public virtual ICollection<MenuItem> Children { get; set; }

    }
}