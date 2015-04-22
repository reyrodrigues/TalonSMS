using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace TalonAdmin.Models.ViewModels
{
    public class DistributionVendorViewModel
    {
        public Models.Vouchers.Vendor Vendor { get; set; }
        public int NumberOfVouchers { get; set; }
        public decimal? TotalClaimed { get; set; }
    }
}