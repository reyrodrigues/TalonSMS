using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace TalonAdmin.Models.Vouchers
{
    public class Program : TenantEntity
    {
        public string Name { get; set; }
        public string FundCodes { get; set; }
    }
}