using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace TalonAdmin.Models.Vouchers
{
    public class BeneficiaryDistribution : TenantEntity
    {
        public virtual int DistributionId { get; set; }
        public virtual int BeneficiaryId { get; set; }

        public virtual Beneficiary Beneficiary { get; set; }
        public virtual Distribution Distribution { get; set; }
    }
}