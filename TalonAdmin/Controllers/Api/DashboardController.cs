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
                .Select(s => s.Id)
                .ToArray();


            var programs = ctx.Programs
                .Include("Distributions")
                .Include("Distributions.Location")
                .Include("Distributions.Vouchers")
                .Include("Distributions.Vouchers.TransactionRecords")
                .Include("Distributions.Vouchers.Category")
                .FilterCountry(this)
                .FilterOrganization(this);

            var distributionSummary = programs.Select(p =>  p.Distributions.Select(d => new
                                     {
                                         Program = p,
                                         Distribution =  new
                                            {
                                                Id = d.Id,
                                                Title = d.Title,
                                                Location = d.Location != null ? new { 
                                                    Id = d.Location.Id,
                                                    Name = d.Location.Name
                                                } : null,
                                            },
                                         Location = d.Location != null ? d.Location : null,
                                         TotalVouchers = d.Vouchers.Count(),
                                         VouchersUsed = d.Vouchers.Where(v => v.TransactionRecords.Where(t => t.Status == 2).Any()).Count(),
                                         UniqueBeneficiaries =
                                             d.Vouchers
                                             .Where(v => v.TransactionRecords.Where(t => t.Status == 2).Any())
                                             .Select(v => v.TransactionRecords.Select(t => t.BeneficiaryId))
                                             .SelectMany(r => r)
                                             .Distinct()
                                             .Count(),
                                         Beneficiaries =
                                             d.Vouchers
                                             .Select(v => v.TransactionRecords.Select(t => t.BeneficiaryId))
                                             .SelectMany(r => r)
                                             .Distinct()
                                             .Count(),
                                         BeneficiariyIds =
                                             d.Vouchers
                                             .Select(v => v.TransactionRecords.Select(t => t.BeneficiaryId))
                                             .SelectMany(r => r)
                                             .Distinct(),
                                         ClaimedAmount =
                                             d.Vouchers
                                             .Where(v => v.Category.Value != null)
                                             .Where(v => v.TransactionRecords.Where(t => t.Status == 2).Any())
                                             .Select(v => v.Category.Value.Value)
                                         .Sum(),
                                         IssuedAmount =
                                             d.Vouchers
                                             .Where(v => v.Category.Value != null)
                                             .Where(v => v.TransactionRecords.Any())
                                             .Select(v => v.Category.Value.Value)
                                         .Sum(),
                                         TotalVendors =
                                             d.Vouchers
                                             .Where(v => v.TransactionRecords.Where(t => t.Status == 2).Any())
                                             .Select(v => v.TransactionRecords.Where(t => t.VendorId != null).Select(t => t.VendorId))
                                             .SelectMany(r => r)
                                             .Distinct()
                                         .Sum(),
                                         VendorIds =
                                             d.Vouchers
                                             .Where(v => v.TransactionRecords.Where(t => t.Status == 2).Any())
                                             .Select(v => v.TransactionRecords.Where(t => t.VendorId != null).Select(t => t.VendorId))
                                             .SelectMany(r => r)
                                             .Distinct(),
                                     }));

            var dashboardQuery = distributionSummary.SelectMany(r => r).ToArray().GroupBy(k => k.Program).Select(g => new
            {
                Program = new {
                    g.Key.Id,
                    g.Key.Name,
                    g.Key.FundCodes
                },
                TotalVouchers = g.Select(d => d.TotalVouchers).Sum(),
                VouchersUsed = g.Select(d => d.VouchersUsed).Sum(),
                UniqueBeneficiaries = g.Select(d => d.UniqueBeneficiaries).Sum(),
                Beneficiaries = g.Select(d => d.BeneficiariyIds).SelectMany(r => r).Distinct().Count(),
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
                end = ctx.VoucherTransactionRecords.Where(d => d.Voucher.Distribution.ProgramId == program.Id && d.FinalizedOn != null).Select(c => c.FinalizedOn.Value).ToArray().Select(c => c.ToUniversalTime()).Max();


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