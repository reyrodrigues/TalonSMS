using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TalonAdmin.Models.Vouchers
{
    public class CardLoad : TenantEntity
    {
        public virtual int BeneficiaryId { get; set; }
        public virtual int VendorId { get; set; }
        public virtual decimal Amount { get; set; }
        public virtual DateTime Date { get; set; }
        public virtual DateTime DistributionDate { get; set; }
        public virtual string Checksum { get; internal set; }

        public virtual Beneficiary Beneficiary { get; set; }
        public virtual Vendor Vendor { get; set; }
    }
}
