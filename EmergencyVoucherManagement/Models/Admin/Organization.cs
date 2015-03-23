using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace EmergencyVoucherManagement.Models.Admin
{
    public class Organization : Entity
    {
        public virtual string Name { get; set; }
        public virtual string Abbreviation { get; set; }

        public virtual string LogoSVG { get; set; }
    }
}