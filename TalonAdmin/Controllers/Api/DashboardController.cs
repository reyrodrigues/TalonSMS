using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Data.Entity;
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
            var programs = await ctx.Programs
                .Include("Distributions")
                .FilterCountry(this)
                .FilterOrganization(this)
                .Where(p => p.Distributions.Any(d => d.Vouchers.Any(v => v.Status == 2 || v.Status == 3)))
                .ToArrayAsync();

            var distributionSummary = programs.Select(p => p.Distributions.Select(d => new
            {
                ProgramId = p.Id,
                DistributionId = d.Id,
                LocationId = d.LocationId,
                TotalVouchers = ctx.Vouchers
                    .Where(v => v.DistributionId == d.Id).Count(),
                VouchersUsed = ctx.Vouchers
                    .Where(v => v.DistributionId == d.Id).Where(v => v.Status == 2 || v.Status == 3).Count(),
                UniqueBeneficiaries =
                    ctx.Vouchers
                    .Where(v => v.DistributionId == d.Id)
                    .Where(v => v.Status == 3 || v.Status == 2)
                    .Select(v => v.TransactionRecords.Select(t => t.BeneficiaryId))
                    .SelectMany(r => r)
                    .Distinct()
                    .Count(),
                Beneficiaries =
                    ctx.Vouchers
                    .Where(v => v.DistributionId == d.Id)
                    .Select(v => v.TransactionRecords.Where(t => t.Type == 1).Select(t => t.BeneficiaryId))
                    .SelectMany(r => r)
                    .Distinct()
                    .Count(),
                BeneficiaryIds =
                    ctx.Vouchers
                    .Where(v => v.DistributionId == d.Id)
                    .Select(v => v.TransactionRecords.Select(t => t.BeneficiaryId))
                    .SelectMany(r => r)
                    .Distinct(),
                ClaimedAmount =
                    ctx.Vouchers
                    .Where(v => v.Status == 2 || v.Status == 3)
                    .Where(v => v.DistributionId == d.Id && v.TransactionRecords.Where(t => t.Type == 2).Any())
                    .Select(v => v.TransactionRecords.Where(t => t.Type == 2).Select(t => t.Value ?? 0m).Sum())
                .Sum(),
                IssuedAmount =
                    ctx.Vouchers
                    .Where(v => v.DistributionId == d.Id)
                    .Where(v => v.Value != null && v.TransactionRecords.Any())
                    .Select(v => (v.Value ?? 0))
                .Sum(),
                TotalVendors =
                    ctx.Vouchers
                    .Where(v => v.DistributionId == d.Id)
                    .Where(v => v.Status == 2 || v.Status == 3)
                    .Select(v => v.TransactionRecords.Where(t => t.VendorId != null).Select(t => t.VendorId))
                    .SelectMany(r => r)
                    .Distinct()
                .Sum(),
                VendorIds =
                    ctx.Vouchers
                    .Where(v => v.DistributionId == d.Id)
                    .Where(v => v.Status == 2 || v.Status == 3)
                    .Select(v => v.TransactionRecords.Where(t => t.VendorId != null).Select(t => t.VendorId))
                    .SelectMany(r => r)
                    .Distinct(),
            }).ToArray()).ToArray();

            var dashboardQuery = distributionSummary
                .SelectMany(r => r.Select(d => new
                {
                    Program = ctx.Programs.Where(p => p.Id == d.ProgramId).FirstOrDefault(),
                    Distribution = ctx.Distributions.Where(p => p.Id == d.DistributionId).FirstOrDefault(),
                    Location = ctx.Locations.Where(p => p.Id == d.LocationId).FirstOrDefault(),
                    d.TotalVouchers,
                    d.VouchersUsed,
                    d.UniqueBeneficiaries,
                    d.Beneficiaries,
                    d.BeneficiaryIds,
                    d.ClaimedAmount,
                    d.IssuedAmount,
                    d.TotalVendors,
                    d.VendorIds,
                })).ToArray().GroupBy(k => k.Program).Select(g => new
            {
                Program = new
                {
                    g.Key.Id,
                    g.Key.Name,
                    g.Key.FundCodes
                },
                TotalVouchers = g.Select(d => d.TotalVouchers).Sum(),
                VouchersUsed = g.Select(d => d.VouchersUsed).Sum(),
                UniqueBeneficiaries = g.Select(d => d.UniqueBeneficiaries).Sum(),
                Beneficiaries = g.Select(d => d.BeneficiaryIds).SelectMany(r => r).Distinct().Count(),
                ClaimedAmount = g.Select(d => d.ClaimedAmount).Sum(),
                IssuedAmount = g.Select(d => d.IssuedAmount).Sum(),
                TotalVendors = g.Select(d => d.VendorIds).SelectMany(r => r).Distinct().Count(),
                Distributions = g.ToArray(),
                UsedVsIssued = GenerateUsedVsIssuedReport(g.Key),
                Vendor = GenerateVendorReport(g.Key)
            }).ToArray();

            var dashboardArray = dashboardQuery.ToArray();
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
                .GroupBy(v => v.Vendor.ParentRecordId == null ? v.Vendor : v.Vendor.ParentRecord)
                .ToArray()
                .Select(v => new object[] { v.Key.Name, v.Count() });
        }
        private dynamic GenerateUsedVsIssuedReport(Models.Vouchers.Program program)
        {
            return GenerateUsedVsIssuedReport(program, null, null);
        }

        private dynamic GenerateUsedVsIssuedReport(Models.Vouchers.Program program, DateTime? start, DateTime? end)
        {
            var voucherQuery = ctx.Vouchers.FilterCountry(this).FilterOrganization(this);

            if (start == null)
                start = ctx.VoucherTransactionRecords.Where(d => d.Voucher.Distribution.ProgramId == program.Id).Select(c => c.CreatedOn).ToArray().Select(c => c.ToUniversalTime()).Min();
            if (end == null)
                end = ctx.VoucherTransactionRecords.Where(d => d.Voucher.Distribution.ProgramId == program.Id && d.LastModifiedOn != null).Select(c => c.LastModifiedOn.Value).ToArray().Select(c => c.ToUniversalTime()).Max();


            return Enumerable
                .Range(0, (int)Math.Ceiling((end.Value - start.Value).TotalDays) + 1)
                .Select(d => new
                {
                    Date = DateTime.Parse(start.Value.ToShortDateString()).AddDays(d),
                    DateMinusOne = DateTime.Parse(start.Value.ToShortDateString()).AddDays(d - 1),
                    DatePlusOne = DateTime.Parse(start.Value.ToShortDateString()).AddDays(d + 1)
                })
                .Select(d => new object[] { 
                    d.Date,
                    voucherQuery
                        .Where(v => v.Distribution.ProgramId == program.Id)
                        .Where(v=>v.TransactionRecords.Where(t=> 
                            t.CreatedOn > d.Date && t.CreatedOn < d.DatePlusOne && 
                            (t.LastModifiedOn >= d.Date || t.LastModifiedOn == null))
                        .Any())
                        .Count(),
                    voucherQuery
                        .Where(v => v.Distribution.ProgramId == program.Id)
                        .Where(v=>v.TransactionRecords.Where(t=> 
                           t.CreatedOn < d.DatePlusOne && 
                            (t.LastModifiedOn >= d.Date || t.LastModifiedOn == null))
                        .Any())
                        .Count(),
                    voucherQuery
                        .Where(v => v.Distribution.ProgramId == program.Id)
                        .Where(v=>v.TransactionRecords.Where(t =>
                            t.LastModifiedOn > d.Date && t.LastModifiedOn < d.DatePlusOne 
                            
                            ).Any())
                        .Count()
                });
        }
    }
}