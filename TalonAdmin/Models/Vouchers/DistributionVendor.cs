using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace TalonAdmin.Models.Vouchers
{
    public class DistributionVendor : TenantEntity
    {
        public virtual int VendorId { get; set; }
        public virtual int DistributionId { get; set; }

        public virtual Distribution Distribution { get; set; }
        public virtual Vendor Vendor { get; set; }
    }
}