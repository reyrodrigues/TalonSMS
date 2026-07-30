using System;
using System.Collections.Generic;
using System.Linq;

namespace TalonAdmin.Models.Vouchers
{
    public class MessageLog : Entity
    {
        public virtual string MobileNumber { get; set; }
        public virtual string Message { get; set; }

        public virtual DateTime DateTime { get; set; }

        public int? CountryId { get; set; }
    }
}