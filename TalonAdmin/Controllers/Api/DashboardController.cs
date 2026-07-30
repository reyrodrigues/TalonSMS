using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TalonAdmin.Extensions;

namespace TalonAdmin.Controllers.Api
{
    [Authorize]
    [Route("api/Dashboard")]
    public class DashboardController : ControllerBase
    {
        // NOTE: the action used to carry [EnableQuery] from System.Web.Http.OData. It returns a
        // materialised JToken rather than an IQueryable, so the attribute never had any effect;
        // it is dropped rather than pulling in Microsoft.AspNetCore.OData for a no-op.
        private readonly Models.Vouchers.Context ctx;
        private readonly IWebHostEnvironment _environment;

        public DashboardController(Models.Vouchers.Context context, IWebHostEnvironment environment)
        {
            ctx = context;
            _environment = environment;
        }

        [HttpGet("DashboardSummary")]
        public async Task<IActionResult> DashboardSummary()
        {
            string dirName = Path.Combine(_environment.ContentRootPath, "Dashboards");
            string fileName = Path.Combine(dirName, String.Format("{0}-{1}.json", this.GetOrganizationId(), this.GetCountryId()));

            if (!Directory.Exists(dirName))
            {
                Directory.CreateDirectory(dirName);
            }

            var recreateDashboard = !System.IO.File.Exists(fileName);

            if (System.IO.File.Exists(fileName))
            {
                var timeSinceLastGeneration = DateTime.Now - System.IO.File.GetLastWriteTime(fileName);
                if (timeSinceLastGeneration.TotalMinutes > 10)
                {
                    recreateDashboard = true;
                }
            }

            if (recreateDashboard)
            {
                var programs = await ctx.Programs
                .AsNoTracking()
                .Include(p => p.Distributions)
                .FilterCountry(this)
                .FilterOrganization(this)
                .Where(p => p.Distributions.Any(d => d.Vouchers.Any(v => v.Status == 2 || v.Status == 3)) && p.ShowInDashboard == true)
                .ToArrayAsync();

                var programIds = programs.Select(p => p.Id);

                var vouchers = ctx.Vouchers
                    .AsNoTracking()
                    .FilterCountry(this)
                    .FilterOrganization(this)
                    .Where(v => v.Distribution.ProgramId != null && programIds.Contains(v.Distribution.ProgramId.Value));

                var locations = ctx.Locations
                    .AsNoTracking()
                    .FilterCountry(this);

                var distributionSummary = programs.Select(p => p.Distributions.Select(d => new
                {
                    ProgramId = p.Id,
                    DistributionId = d.Id,
                    LocationId = d.LocationId,
                    TotalVouchers =
                        vouchers
                        .Where(v => v.DistributionId == d.Id).Count(),
                    VouchersUsed =
                        vouchers
                        .Where(v => v.DistributionId == d.Id).Where(v => v.Status == 2 || v.Status == 3).Count(),
                    UniqueBeneficiaries =
                        vouchers
                        .Where(v => v.DistributionId == d.Id)
                        .Where(v => v.Status == 3 || v.Status == 2)
                        .Select(v => v.TransactionRecords.Select(t => t.BeneficiaryId))
                        .SelectMany(r => r)
                        .Distinct()
                        .Count(),
                    Beneficiaries =
                        vouchers
                        .Where(v => v.DistributionId == d.Id)
                        .Select(v => v.TransactionRecords.Where(t => t.Type == 1).Select(t => t.BeneficiaryId))
                        .SelectMany(r => r)
                        .Distinct()
                        .Count(),
                    BeneficiaryIds =
                        vouchers
                        .Where(v => v.DistributionId == d.Id)
                        .Select(v => v.TransactionRecords.Select(t => t.BeneficiaryId))
                        .SelectMany(r => r)
                        .Distinct()
                        .ToArray(),
                    ClaimedAmount =
                        vouchers
                        .Where(v => v.Status == 2 || v.Status == 3)
                        .Where(v => v.DistributionId == d.Id && v.TransactionRecords.Where(t => t.Type == 2).Any())
                        .Select(v => v.TransactionRecords.Where(t => t.Type == 2 && t.Value != null).Select(t => t.Value).Sum())
                    .Sum(),
                    IssuedAmount =
                        vouchers
                        .Where(v => v.DistributionId == d.Id)
                        .Where(v => v.Value != null && v.TransactionRecords.Any())
                        .Select(v => v.Value)
                    .Sum(),
                    TotalVendors =
                        vouchers
                        .Where(v => v.DistributionId == d.Id)
                        .Where(v => v.Status == 2 || v.Status == 3)
                        .Select(v => v.TransactionRecords.Where(t => t.VendorId != null).Select(t => t.VendorId))
                        .SelectMany(r => r)
                        .Distinct()
                    .Sum(),
                    VendorIds =
                        vouchers
                        .Where(v => v.DistributionId == d.Id)
                        .Where(v => v.Status == 2 || v.Status == 3)
                        .Select(v => v.TransactionRecords.Where(t => t.VendorId != null).Select(t => t.VendorId))
                        .SelectMany(r => r)
                        .Distinct()
                        .ToArray(),
                }).ToArray()).ToArray();

                var dashboardQuery = distributionSummary
                    .SelectMany(r => r.Select(d => new
                    {
                        Program = programs.Where(p => p.Id == d.ProgramId).FirstOrDefault(),
                        Distribution = ctx.Distributions.AsNoTracking().Where(p => p.Id == d.DistributionId).FirstOrDefault(),
                        Location = locations.Where(p => p.Id == d.LocationId).FirstOrDefault(),
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

                await System.IO.File.WriteAllTextAsync(fileName, jsonString);
            }

            using (var stream = System.IO.File.OpenRead(fileName))
            using (var reader = new StreamReader(stream))
            using (var jsonReader = new JsonTextReader(reader))
            {
                return Ok(JToken.Load(jsonReader));
            }
        }

        #region Intenal Reporting Funcs
        private dynamic GenerateVendorReport(Models.Vouchers.Program program)
        {
            var transactionRecordQuery = ctx.VoucherTransactionRecords.AsNoTracking().FilterCountry(this).FilterOrganization(this);

            // NOTE: EF Core cannot translate a GroupBy that projects whole entities as the key, so
            // the rows are materialised first and grouped in memory. EF6 evaluated this on the client too.
            var records = transactionRecordQuery
                .Where(v => v.Type == 2 && v.Voucher.Distribution.ProgramId == program.Id && v.VendorId != null)
                .Include(v => v.Vendor)
                .ThenInclude(v => v.ParentRecord)
                .ToArray();

            return records
                .GroupBy(v => v.Vendor.ParentRecordId == null ? v.Vendor : v.Vendor.ParentRecord)
                .Select(v => new object[] { v.Key.Name, v.Count() });
        }

        private dynamic GenerateUsedVsIssuedReport(Models.Vouchers.Program program)
        {
            return GenerateUsedVsIssuedReport(program, null, null);
        }

        private dynamic GenerateUsedVsIssuedReport(Models.Vouchers.Program program, DateTime? start, DateTime? end)
        {
            var voucherQuery = ctx.Vouchers.AsNoTracking().FilterCountry(this).FilterOrganization(this).Where(v => v.Distribution.Program.Id == program.Id);

            var issued = voucherQuery.Where(v => v.TransactionRecords.Where(t => t.Type == 1).Any())
                .Select(v => new { Value = 1, Key = v.TransactionRecords.Select(t => t.CreatedOn).Min() })
                .ToArray()
                .GroupBy(k => k.Key.ToUniversalTime().ToShortDateString())
                .ToDictionary(k => k.Key, v => v.Sum(s => s.Value));

            var used = voucherQuery.Where(v => v.TransactionRecords.Where(t => t.Type == 2).Any())
                .Select(v => new { Value = 1, Key = v.TransactionRecords.Where(t => t.Type == 2).Select(t => t.LastModifiedOn).Min() })
                .ToArray()
                .Where(a => a.Key != null)
                .GroupBy(k => k.Key.Value.ToUniversalTime().ToShortDateString())
                .ToDictionary(k => k.Key, v => v.Sum(s => s.Value));

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
                    issued.ContainsKey(d.Date.ToUniversalTime().ToShortDateString()) ? issued[d.Date.ToUniversalTime().ToShortDateString()] : 0,
                    0,
                    used.ContainsKey(d.Date.ToUniversalTime().ToShortDateString()) ? used[d.Date.ToUniversalTime().ToShortDateString()] : 0,
                });
        }
        #endregion
    }
}
