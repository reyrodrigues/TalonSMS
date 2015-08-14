using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TalonAdmin.Models.Vouchers
{
    public class VendorSalesPerson : CountryEntity
    {
        public virtual int VendorId { get; set; }

        public virtual string FirstName { get; set; }    
        public virtual string LastName { get; set; }
        public virtual string MobileNumber { get; set; }
        public virtual string NationalId { get; set; }

        public virtual Vendor Vendor { get; set; }
    }
}
