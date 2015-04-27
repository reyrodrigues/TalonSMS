using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace TalonAdmin.Models.Vouchers
{
    public class DistributionLog : TenantEntity
    {
        public virtual int DistributionId { get; set; }
        public virtual DateTime DateTime { get; set; }
        public virtual int AffectedBeneficiaries { get; set; }

        public virtual DateTime? StartedOn { get; set; }
        public virtual DateTime? EndedOn { get; set; }

        public virtual Distribution Distribution { get; set; }
    }
}