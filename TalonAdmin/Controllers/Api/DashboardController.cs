using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Web.Http;
using System.Web.Http.OData;
using TalonAdmin.Extensions;

namespace TalonAdmin.Controllers.Api
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
            var distributions = ctx.Distributions.FilterCountry(this).FilterOrganization(this)
                .Select(s => s.Id)
                .ToArray()
                .Select(s => new TalonAdmin.Models.ViewModels.DashboardSummaryViewModel
            {
                Distribution = ctx.Distributions.FilterCountry(this).FilterOrganization(this).Where(d => d.Id == s).Take(1).FirstOrDefault(),
                Location = ctx.Distributions.Include("Location").FilterCountry(this).FilterOrganization(this).Where(d => d.Id == s).Select(d => d.Location).Take(1).FirstOrDefault(),
                TotalVouchers = ctx.Vouchers.FilterCountry(this).FilterOrganization(this).Where(d => d.DistributionId == s).Count(),
                InactiveVouchers = ctx.Vouchers.FilterCountry(this).FilterOrganization(this).Where(d => d.DistributionId == s).Where(v => v.TransactionRecords.Any()  && v.TransactionRecords.Where(t=>t.Status > 1).Any()).Count(),
                Beneficiaries = ctx.Vouchers.FilterCountry(this).FilterOrganization(this).Where(d => d.DistributionId == s).Where(v => v.TransactionRecords.Any() ).Select(v=>v.TransactionRecords.Select(r=>r.BeneficiaryId)).SelectMany(r=>r).Distinct().Count(),
                Vendors = ctx.Vouchers.FilterCountry(this).FilterOrganization(this).Where(d => d.DistributionId == s).Where(v => v.TransactionRecords.Any()  && v.TransactionRecords.Where(t=>t.Vendor != null).Any()).Select(v => v.TransactionRecords.Select(t=>t.VendorId)).SelectMany(r=> r).Distinct().Count(),
                TotalAmount = ctx.Distributions.FilterCountry(this).FilterOrganization(this).Where(d => d.Id == s).Select(d => d.Categories.Select(c => c.Value.HasValue ? (c.NumberOfVouchers * c.Value) : 0).Sum()).Sum(),
                IssuedAmount = ctx.Distributions.FilterCountry(this).FilterOrganization(this).Where(d => d.Id == s).Select(d => d.Categories.Select(c => c.Value.HasValue ? (c.IssuedVouchers * c.Value) : 0).Sum()).Sum(),
                ClaimedAmount = ctx.VoucherTransactionRecords.FilterCountry(this).FilterOrganization(this).Where(d => d.Voucher.DistributionId == s).Where(c => c.Status == 2).Select(d => d.Voucher.Category.Value.HasValue ? d.Voucher.Category.Value : 0m).Sum(),
            });

            return distributions;
        }
    }
}