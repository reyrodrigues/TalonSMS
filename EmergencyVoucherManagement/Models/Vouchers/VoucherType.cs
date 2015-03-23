using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace EmergencyVoucherManagement.Models.Vouchers
{
    public class VoucherType : TenantEntity
    {
        public virtual string Name { get; set; }
    }
}