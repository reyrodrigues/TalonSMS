using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace EmergencyVoucherManagement.Models.Vouchers
{
    public class Voucher : TenantEntity
    {
        public virtual int TypeId { get; set; }
        public virtual int DistributionId { get; set; }

        /// <summary>
        /// I'm assuming that the voucher will have currency amounts, but it could be items instead                    
        /// </summary>
        public virtual decimal? Value { get; set; }

        public virtual long VoucherCode { get; set; }

        public virtual VoucherType Type { get; set; }
        public virtual Distribution Distribution { get; set; }
        public virtual VoucherTransactionRecord TransactionRecord { get; set; }
    }
}