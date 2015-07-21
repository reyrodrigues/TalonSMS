using System;
using System.Collections.Generic;
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
        public virtual DateTime? FinalizedOn { get; set; }

        public virtual string ConfirmationCode { get; set; }

        /// <summary>
        /// 1. Valid
        /// 2. Invalid
        /// </summary>
        public virtual int Status { get; set; }

        public string StatusString
        {
            get
            {
                switch (Status)
                {
                    case 0:
                        return "Unclaimed";
                    case 2:
                        return "Claimed";
                    case 3:
                        return "Cancelled";
                    default:
                        return "";
                }
            }
        }

        public virtual Beneficiary Beneficiary { get; set; }

        public virtual Vendor Vendor { get; set; }
        public virtual Voucher Voucher { get; set; }

    }
}