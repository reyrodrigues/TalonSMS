using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace TalonAdmin.Models.Vouchers
{
    public class VendorDevice : CountryEntity
    {
        public virtual int VendorId { get; set; }
        public virtual string UUID { get; set; }
        public virtual string Type { get; set; }
        public virtual string Name { get; set; }
        public virtual string IPAddress { get; set; }
        public virtual DateTime LastAccessed { get; set; }

        public virtual Vendor Vendor { get; set; }
    }
}