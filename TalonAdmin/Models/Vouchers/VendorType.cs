using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace TalonAdmin.Models.Vouchers
{
    public class VendorType : CountryEntity
    {
        public virtual string Name { get; set; }

        public virtual ICollection<Vendor> Vendors { get; set; }
    }
}