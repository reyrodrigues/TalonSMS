using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace EmergencyVoucherManagement.Models.Vouchers
{
    public class BeneficiaryGroup : Entity
    {
        public virtual string Name { get; set; }

        public virtual ICollection<Beneficiary> Beneficiaries { get; set; }
    }
}