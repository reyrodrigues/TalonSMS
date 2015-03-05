﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Web;

namespace EmergencyVoucherManagement.Models.Vouchers
{
    public class VoucherTransactionRecord : Entity
    {
        public VoucherTransactionRecord() {
            this.CreatedOn = DateTime.UtcNow;
        }

        public virtual int BeneficiaryId { get; set; }

        public virtual int? VendorId { get; set; }

        public virtual DateTime CreatedOn { get; set; }
        public virtual DateTime? LastModifiedOn { get; set; }
        public virtual DateTime? FinalizedOn { get; set; }

        public virtual long? ConfirmationCode { get; set; }
        /// <summary>
        /// 1. Valid
        /// 2. Invalid
        /// </summary>
        public virtual int Status { get; set; }

        public virtual Beneficiary Beneficiary { get; set; }

        public virtual Vendor Vendor { get; set; }
        public virtual Voucher Voucher { get; set; }

    }
}