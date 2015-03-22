using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace EmergencyVoucherManagement.Models.Admin
{
    public class ApplicationUserCountry : Entity
    {
        public virtual int ApplicationUserId { get; set; }
        public virtual int CountryId { get; set; }

        public virtual ApplicationUser ApplicationUser { get; set; }
        public virtual Country Country { get; set; }
    }
}