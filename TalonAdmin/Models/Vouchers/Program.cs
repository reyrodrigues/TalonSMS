using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Web;

namespace TalonAdmin.Models.Vouchers
{
    public class Program : TenantEntity
    {
        public string Name { get; set; }
        public string FundCodes { get; set; }

        /// <summary>
        /// 1. SMS
        /// 2. NFC Card Load
        /// 3. QR Code - Paper
        /// </summary>
        public virtual int DistributionMechanism { get; set; }

        [DefaultValue(6)]
        public virtual int VoucherCodeLength { get; set; }

        public virtual string CreatedBy { get; set; }
        public virtual string ModifiedBy { get; set; }
        public virtual DateTime CreatedOn { get; set; }
        public virtual DateTime ModifiedOn { get; set; }

        public virtual ICollection<Distribution> Distributions { get; set; }
        public virtual ICollection<ProgramVoucherCategory> Categories { get; set; }

    }
}