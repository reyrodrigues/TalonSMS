using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace EmergencyVoucherManagement.Models.Admin
{
    public class Country : Entity
    {
        public virtual string Name { get; set; }
        public virtual string IsoAlpha2 { get; set; }
        public virtual string IsoAlpha3 { get; set; }
    }
}