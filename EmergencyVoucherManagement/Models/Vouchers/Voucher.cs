using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Web;

namespace TalonAdmin.Models.Vouchers
{
    public class Voucher : TenantEntity
    {
        public virtual int CategoryId { get; set; }
        public virtual int DistributionId { get; set; }

        [Index(IsUnique = true), StringLength(30)]
        public virtual string VoucherCode { get; set; }

        public virtual DistributionVoucherCategory Category { get; set; }
        public virtual Distribution Distribution { get; set; }
        public virtual ICollection<VoucherTransactionRecord> TransactionRecords { get; set; }
    }
}