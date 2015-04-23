using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace TalonAdmin.Models.Vouchers
{
    public class Vendor : CountryEntity
    {
        public virtual string Name { get; set; }
        public virtual string FirstName { get; set; }
        public virtual string LastName { get; set; }
        public virtual string MobileNumber { get; set; }
        public virtual string NationalId { get; set; }

        public virtual int? LocationId { get; set; }
        public virtual int? TypeId { get; set; }

        public virtual string GPSCoordinates { get; set; }

        public virtual VendorType Type { get; set; }
        public virtual Location Location { get; set; }

        public virtual int? ParentRecordId { get; set; }
        public virtual Vendor ParentRecord { get; set; }
    }
}