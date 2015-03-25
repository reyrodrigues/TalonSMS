using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace TalonAdmin.Models.Vouchers
{
    public class DistributionLog : TenantEntity
    {
        public virtual int DistributionId { get; set; }
    }
}