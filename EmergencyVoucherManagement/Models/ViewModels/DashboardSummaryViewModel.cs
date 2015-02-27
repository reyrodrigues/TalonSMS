using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace EmergencyVoucherManagement.Models.ViewModels
{
    public class DashboardSummaryViewModel
    {
        public Models.Vouchers.Distribution Distribution { get; set; }
        public int TotalVouchers { get; set; }
        public int InactiveVouchers { get; set; }

        public int Beneficiaries { get; set; }

        public int Vendors { get; set; }

        public Vouchers.Location Location { get; set; }

        public decimal? TotalAmount { get; set; }

        public decimal? IssuedAmount { get; set; }

        public decimal? ClaimedAmount { get; set; }
    }
}