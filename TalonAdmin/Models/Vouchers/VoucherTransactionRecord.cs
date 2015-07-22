using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Web;

namespace TalonAdmin.Models.Vouchers
{
    public class VoucherTransactionRecord : TenantEntity
    {
        public VoucherTransactionRecord()
        {
            this.CreatedOn = DateTime.UtcNow;
        }

        public virtual int BeneficiaryId { get; set; }

        public virtual int? VoucherId { get; set; }
        public virtual int? VendorId { get; set; }

        public virtual DateTime CreatedOn { get; set; }
        public virtual DateTime? LastModifiedOn { get; set; }

        public virtual string ConfirmationCode { get; set; }

        /// <summary>
        /// 1. Credit
        /// 2. Debit
        /// 3. Cancellation 
        /// </summary>
        [DefaultValue(1)]
        public virtual int Type { get; set; }
        public virtual decimal? Value { get; set; }

        public virtual string ReconciledBy { get; set; }
        public virtual DateTime? ReconciledOn { get; set; }
        public virtual bool? IsFinalized { get; set; }

        public virtual Beneficiary Beneficiary { get; set; }
        public virtual Vendor Vendor { get; set; }
        public virtual Voucher Voucher { get; set; }
    }
}