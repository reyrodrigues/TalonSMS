﻿using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace TalonAdmin.Models
{
    public abstract class DictionaryEntity<ParentIdType, ParentType> : Entity
    {
        public virtual string Key { get; set; }
        public virtual string Value { get; set; }
        public virtual ParentIdType ParentId { get; set; }

        [JsonIgnore, XmlIgnore]
        public virtual ParentType Parent { get; set; }
    }
}
