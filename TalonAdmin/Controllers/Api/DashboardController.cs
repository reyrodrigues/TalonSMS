using Newtonsoft.Json;
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
        public async Task<IHttpActionResult> DashboardSummary()
        {
            var vendorCount = ctx.Vendors.Count();
            var voucherQuery = ctx.Vouchers.FilterCountry(this).FilterOrganization(this);
            var distributionQuery = ctx.Distributions.Include("Program").Include("Location").FilterCountry(this).FilterOrganization(this);
            var transactionRecordQuery = ctx.VoucherTransactionRecords.FilterCountry(this).FilterOrganization(this);

            var distributions = ctx.Distributions
                .FilterCountry(this)
                .FilterOrganization(this)
                .Where(d => d.IsClosed != true)
                .Select(s => s.Id)
                .ToArray();

            var programs = ctx.Distributions.Where(d => d.IsClosed != true).Select(d => d.Program).Distinct().ToArray();

            var programStatistics = programs.Select(p => new
            {
                Program = p,
                Distributions = distributionQuery.Where(d => d.ProgramId == p.Id)
                .Select(s => new
                {
                    Distribution = distributionQuery.Where(d => d.Id == s.Id).Take(1).FirstOrDefault(),
                    Location = distributionQuery.Where(d => d.Id == s.Id).Select(d => d.Location).Take(1).FirstOrDefault(),
                    TotalVouchers = voucherQuery.Where(d => d.DistributionId == s.Id).Count(),
                    VouchersUsed = voucherQuery.Where(d => d.DistributionId == s.Id).Where(v => v.TransactionRecords.Any() && v.TransactionRecords.Where(t => t.Status == 2).Any()).Count(),
                    UniqueBeneficiaries = voucherQuery.Where(d => d.DistributionId == s.Id).Where(v => v.TransactionRecords.Any()).Select(v => v.TransactionRecords.Where(t => t.Status == 2).Select(r => r.BeneficiaryId)).SelectMany(r => r).Distinct().Count(),
                    Beneficiaries = voucherQuery.Where(d => d.DistributionId == s.Id).Where(v => v.TransactionRecords.Any()).Select(v => v.TransactionRecords.Select(r => r.BeneficiaryId)).SelectMany(r => r).Distinct().Count(),
                    Vendors = voucherQuery.Where(d => d.DistributionId == s.Id).Where(v => v.TransactionRecords.Any() && v.TransactionRecords.Where(t => t.Vendor != null).Any()).Select(v => v.TransactionRecords.Select(t => t.VendorId)).SelectMany(r => r).Distinct().Count(),
                    IssuedAmount = transactionRecordQuery.Where(d => d.Voucher.DistributionId == s.Id).Select(d => d.Voucher.Category.Value.HasValue ? d.Voucher.Category.Value : 0m).Sum() ?? 0,
                    ClaimedAmount = transactionRecordQuery.Where(d => d.Voucher.DistributionId == s.Id).Where(c => c.Status == 2).Select(d => d.Voucher.Category.Value.HasValue ? d.Voucher.Category.Value : 0m).Sum() ?? 0,
                }),
                Start = ctx.VoucherTransactionRecords.Where(d => d.Voucher.Distribution.ProgramId == p.Id).Select(c => c.CreatedOn).ToArray().Select(c => c.ToUniversalTime()).Min(),
                End = ctx.VoucherTransactionRecords.Where(d => d.Voucher.Distribution.ProgramId == p.Id && d.FinalizedOn != null).Select(c => c.FinalizedOn.Value).ToArray().Select(c => c.ToUniversalTime()).Max(),
            });

            var dashboardArray = programStatistics.Select(s => new
            {
                s.Program,
                TotalVouchers = s.Distributions.Select(d => d.TotalVouchers).Sum(),
                VouchersUsed = s.Distributions.Select(d => d.VouchersUsed).Sum(),
                UniqueBeneficiaries = voucherQuery.Where(d => d.Distribution.ProgramId == s.Program.Id).Where(v => v.TransactionRecords.Any()).Select(v => v.TransactionRecords.Where(t => t.Status == 2).Select(r => r.BeneficiaryId)).SelectMany(r => r).Distinct().Count(),
                Beneficiaries = voucherQuery.Where(d => d.Distribution.ProgramId == s.Program.Id).Where(v => v.TransactionRecords.Any()).Select(v => v.TransactionRecords.Select(r => r.BeneficiaryId)).SelectMany(r => r).Distinct().Count(),
                ClaimedAmount = s.Distributions.Select(d => d.ClaimedAmount).Sum(),
                IssuedAmount = s.Distributions.Select(d => d.IssuedAmount).Sum(),
                TotalVendors = voucherQuery.Where(d => d.Distribution.ProgramId == s.Program.Id).Where(v => v.TransactionRecords.Any() && v.TransactionRecords.Where(t => t.Vendor != null).Any()).Select(v => v.TransactionRecords.Select(t => t.VendorId)).SelectMany(r => r).Distinct().Count(),
                s.Distributions,
                UsedVsIssued = GenerateUsedVsIssuedReport(s.Program, s.Start, s.End),
                Vendor = GenerateVendorReport(s.Program)
            });
            var jsonString = JsonConvert.SerializeObject(dashboardArray,
                Formatting.Indented,
                new JsonSerializerSettings
                {
                    ReferenceLoopHandling = ReferenceLoopHandling.Ignore,

                });


            return Ok<JToken>(JToken.Parse(jsonString));
        }
        private dynamic GenerateVendorReport(Models.Vouchers.Program program)
        {
            var transactionRecordQuery = ctx.VoucherTransactionRecords.FilterCountry(this).FilterOrganization(this);

            return transactionRecordQuery
                .Where(v => v.Voucher.Distribution.ProgramId == program.Id && v.VendorId != null)
                .GroupBy(v=> v.Vendor.ParentRecordId == null ? v.Vendor : v.Vendor.ParentRecord)
                .ToArray()
                .Select(v=> new object[]{ v.Key.Name, v.Count() });
        }


        private dynamic GenerateUsedVsIssuedReport(Models.Vouchers.Program program, DateTime start, DateTime end)
        {
            var voucherQuery = ctx.Vouchers.FilterCountry(this).FilterOrganization(this);

            return Enumerable
                .Range(0, (int)Math.Ceiling((end - start).TotalDays) + 1)
                .Select(d => new
                {
                    Date = DateTime.Parse(start.ToShortDateString()).AddDays(d),
                    DateMinusOne = DateTime.Parse(start.ToShortDateString()).AddDays(d - 1),
                    DatePlusOne = DateTime.Parse(start.ToShortDateString()).AddDays(d + 1)
                })
                .Select(d => new object[] { 
                    d.Date,
                    voucherQuery
                        .Where(v => v.Distribution.ProgramId == program.Id)
                        .Where(v=>v.TransactionRecords.Where(t=> 
                            t.CreatedOn > d.Date && t.CreatedOn < d.DatePlusOne && 
                            (t.FinalizedOn >= d.Date || t.FinalizedOn == null))
                        .Any())
                        .Count(),
                    voucherQuery
                        .Where(v => v.Distribution.ProgramId == program.Id)
                        .Where(v=>v.TransactionRecords.Where(t=> 
                           t.CreatedOn < d.DatePlusOne && 
                            (t.FinalizedOn >= d.Date || t.FinalizedOn == null))
                        .Any())
                        .Count(),
                    voucherQuery
                        .Where(v => v.Distribution.ProgramId == program.Id)
                        .Where(v=>v.TransactionRecords.Where(t =>
                            t.FinalizedOn > d.Date && t.FinalizedOn < d.DatePlusOne 
                            
                            ).Any())
                        .Count()
                });
        }
    }
}