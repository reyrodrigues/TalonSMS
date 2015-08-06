using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Web;

namespace TalonAdmin.Models.Vouchers
{
    public class DistributionVoucherCategory : TenantEntity
    {
        public DistributionVoucherCategory() {
            IssuedVouchers = 0;
        }


        public virtual int TypeId { get; set; }
        public virtual int DistributionId { get; set; }

        public virtual int NumberOfVouchers { get; set; }
        public virtual decimal? Value { get; set; }

        public virtual int? VendorTypeId { get; set; }

        public virtual Distribution Distribution { get; set; }
        public virtual VoucherType Type { get; set; }
        public virtual VendorType VendorType { get; set; }
        public virtual DateTime? ValidAfter { get; set; }

        [DefaultValue(0)]
        public virtual int IssuedVouchers { get; set; }
    }
}