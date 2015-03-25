using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Web;

namespace TalonAdmin.Models.Vouchers
{
    public class Distribution : TenantEntity
    {
        public Distribution() {
            VoucherCodeLength = 6;
        }

        public virtual string Title { get; set; }
        public virtual DateTime? Date { get; set; }

        public virtual DistributionStatus? Status { get; set; }

        [DefaultValue(6)]
        public virtual int VoucherCodeLength { get; set; }

        public virtual int LocationId { get; set; }
        public virtual Location Location { get; set; }

        public virtual ICollection<DistributionVoucherCategory> Categories { get; set; }
        public virtual ICollection<Voucher> Vouchers { get; set; }

        public virtual ICollection<BeneficiaryDistribution> Beneficiaries { get; set; }
    }

    public enum DistributionStatus
    {
        Created = 1,
        Started = 2,
        Closed = 3
    }
}