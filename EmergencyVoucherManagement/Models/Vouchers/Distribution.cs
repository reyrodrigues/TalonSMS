using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Web;

namespace EmergencyVoucherManagement.Models.Vouchers
{
    public class Distribution : Entity
    {
        public Distribution() {
            VoucherCodeLength = 6;
        }

        public virtual string Title { get; set; }
        public virtual DateTime? Date { get; set; }

        [DefaultValue(6)]
        public virtual int VoucherCodeLength { get; set; }

        public virtual int LocationId { get; set; }
        public virtual Location Location { get; set; }

        public virtual ICollection<DistributionVoucherCategory> Categories { get; set; }
        public virtual ICollection<DistributionVendor> Vendors { get; set; }
        public virtual ICollection<Voucher> Vouchers { get; set; }

        public virtual ICollection<BeneficiaryDistribution> Beneficiaries { get; set; }
    }
}