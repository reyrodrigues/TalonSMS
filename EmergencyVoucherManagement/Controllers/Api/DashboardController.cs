using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Web.Http;
using System.Web.Http.OData;

namespace EmergencyVoucherManagement.Controllers.Api
{
    [Authorize]
    [RoutePrefix("api/Dashboard")]
    public class DashboardController : ApiController
    {
        private Models.Vouchers.Context ctx = new Models.Vouchers.Context();

        public DashboardController()
        {
            ctx.Configuration.LazyLoadingEnabled = false;
            ctx.Configuration.ProxyCreationEnabled = false;
        }

        protected override void Dispose(bool disposing)
        {
            ctx.Dispose();
            base.Dispose(disposing);
        }

        [HttpGet, EnableQuery]
        public IEnumerable<Models.ViewModels.DashboardSummaryViewModel> DashboardSummary()
        {
            var vendorCount = ctx.Vendors.Count();
            var distributions = ctx.Distributions
                .Select(s => s.Id)
                .ToArray()
                .Select(s => new EmergencyVoucherManagement.Models.ViewModels.DashboardSummaryViewModel
            {
                Distribution = ctx.Distributions.Where(d => d.Id == s).Take(1).FirstOrDefault(),
                Location = ctx.Distributions.Include("Location").Where(d => d.Id == s).Select(d => d.Location).Take(1).FirstOrDefault(),
                TotalVouchers = ctx.Vouchers.Where(d => d.DistributionId == s).Count(),
                InactiveVouchers = ctx.Vouchers.Where(d => d.DistributionId == s).Where(v => v.TransactionRecord != null && v.TransactionRecord.Status > 1).Count(),
                Beneficiaries = ctx.Vouchers.Where(d => d.DistributionId == s).Where(v => v.TransactionRecord != null).Count(),
                Vendors = ctx.Vouchers.Where(d => d.DistributionId == s).Where(v => v.TransactionRecord != null && v.TransactionRecord.Vendor != null).Select(v => v.TransactionRecord.VendorId).Distinct().Count(),
                TotalAmount = ctx.Distributions.Where(d => d.Id == s).Select(d => d.Categories.Select(c => c.Value.HasValue ? (c.NumberOfVouchers * c.Value) : 0).Sum()).Sum(),
                IssuedAmount = ctx.Distributions.Where(d => d.Id == s).Select(d => d.Categories.Select(c => c.Value.HasValue ? (c.IssuedVouchers * c.Value) : 0).Sum()).Sum(),
                ClaimedAmount = ctx.VoucherTransactionRecords.Where(d => d.Voucher.DistributionId == s).Where(c => c.Status == 2).Select(d => d.Voucher.Value.HasValue ?  d.Voucher.Value : 0m).Sum(),
            });

            return distributions;
        }
    }
}