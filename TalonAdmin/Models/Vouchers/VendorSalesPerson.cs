using System;
using System.ComponentModel.DataAnnotations.Schema;
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
        /// <summary>See <see cref="Beneficiary.PersonalIdentifier"/>.</summary>
        [Column("NationalId")]
        public virtual string PersonalIdentifier { get; set; }

        public virtual Vendor Vendor { get; set; }
    }
}
