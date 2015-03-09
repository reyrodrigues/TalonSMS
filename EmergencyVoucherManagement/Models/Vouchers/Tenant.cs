using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace EmergencyVoucherManagement.Models.Vouchers
{
    public class Tenant : Entity
    {
        public virtual string Name { get; set; }
        public virtual string Slug { get; set; }
    }
}