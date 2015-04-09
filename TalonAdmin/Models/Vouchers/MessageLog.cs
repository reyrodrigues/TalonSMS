using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace TalonAdmin.Models.Vouchers
{
    public class MessageLog : Entity
    {
        public virtual string MobileNumber { get; set; }
        public virtual string Message { get; set; }
    }
}