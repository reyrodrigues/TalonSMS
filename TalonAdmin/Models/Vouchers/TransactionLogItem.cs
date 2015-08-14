using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TalonAdmin.Models.Vouchers
{
    public class TransactionLogItem : TenantEntity
    {
        public virtual int BeneficiaryId { get; set; }
        public virtual int VendorId { get; set; }
        /// <summary>
        /// Maps to the program distribution mechanism
        /// 
        /// 1. Sms which will not be used 
        /// 2. NFC
        /// 3. QR
        /// </summary>
        public virtual int Type { get; set; }
        public virtual decimal AmountCredited { get; set; }
        public virtual decimal AmountRemaining { get; set; }
        public virtual DateTime Date { get; set; }
        public virtual string Checksum { get; set; }
        public virtual string TransactionCode { get; set; }
        public virtual bool Quarantine { get; set; }
        public virtual string ConfirmationCode { get;  set; }
        public virtual string VoucherCode { get;  set; }


        public virtual bool Processed { get; set; }
        public virtual DateTime? ProcessedOn { get; set; }
        public virtual string ProcessedBy { get; set; }

        public virtual Beneficiary Beneficiary { get; set; }
        public virtual Vendor Vendor { get; set; }
    }
}
