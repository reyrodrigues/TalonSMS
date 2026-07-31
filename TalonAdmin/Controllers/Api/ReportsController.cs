using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ClosedXML.Excel;
using TalonAdmin.Attributes;
using TalonAdmin.Controllers.BindingModels;
using TalonAdmin.Extensions;
using TalonAdmin.Models.Vouchers;
using TalonAdmin.Reporting;

namespace TalonAdmin.Controllers.Api
{
    [Route("api/Reports")]
    public class ReportsController : ControllerBase
    {
        private readonly Models.Vouchers.Context ctx;
        private readonly Models.Admin.AdminContext actx;
        private readonly UserManager<Models.Admin.ApplicationUser> _userManager;
        private readonly RazorViewRenderer _viewRenderer;
        private readonly IPdfRenderer _pdfRenderer;

        public ReportsController(
            Models.Vouchers.Context context,
            Models.Admin.AdminContext adminContext,
            UserManager<Models.Admin.ApplicationUser> userManager,
            RazorViewRenderer viewRenderer,
            IPdfRenderer pdfRenderer)
        {
            this.ctx = context;
            this.actx = adminContext;
            this._userManager = userManager;
            this._viewRenderer = viewRenderer;
            this._pdfRenderer = pdfRenderer;
        }

        [HttpPost("ProgramClosureReport")]
        public async Task<IActionResult> ProgramClosureReport([FromBody] ReportRequestBindingModel request)
        {
            var paperSizeString = "A4";
            if (!String.IsNullOrEmpty(request.PaperSize))
            {
                paperSizeString = request.PaperSize;
            }
            // NOTE: this used to round-trip through System.Drawing.Printing.PaperKind
            // (Enum.Parse then Enum.GetName). That type is Windows-only and the value never left
            // the string domain, so the string is carried straight through now.
            var paperSize = paperSizeString;

            var reportData = new byte[0];
            var content = new StringBuilder();

            var vendorId = request.VendorId ?? 0;
            var programId = request.ProgramId ?? 0;
            var countryId = request.CountryId;
            var organizationId = request.OrganizationId;
            var itemCount = 0;

            Models.Admin.Organization organization = null;
            Models.Admin.Country country = null;
            Func<int, string> createDistributionNumber = (distributionId) =>
            {
                var distribution = ctx.Distributions.Where(d => d.Id == distributionId).First();
                var groupsInOrder = ctx.Distributions.Where(d => d.ProgramId == programId).OrderBy(d => d.CreatedOn).Select(d => d.GroupId).ToList();
                var cycleNumber = groupsInOrder.IndexOf(distribution.GroupId) + 1;
                var distributionNumber = String.Format("{0:D3}-{1:D3}", distribution.Number ?? 0, cycleNumber);
                return distributionNumber;
            };



            organization = await actx.Organizations.AsNoTracking().Where(o => o.Id == organizationId).FirstOrDefaultAsync();
            country = await actx.Countries.AsNoTracking().Where(c => c.Id == countryId).FirstOrDefaultAsync();

            // NOTE: identity resolution is required, not merely an optimisation. The rows are
            // grouped by the Vendor *instance* below, which only works when every row that refers
            // to the same vendor gets the same object back - which EF6's identity map used to do.
            var items = (await ctx.VoucherTransactionRecords
                                .AsNoTrackingWithIdentityResolution()
                                .Include(v => v.Vendor).ThenInclude(v => v.Location)
                                .Include(v => v.Vendor).ThenInclude(v => v.ParentRecord).ThenInclude(p => p.Location)
                                .Where((v) =>
                                v.Voucher.Distribution.ProgramId == programId
                                && (v.Type == 2)
                                && v.ReconciledOn != null
                                && v.IsFinalized == true
                                )
                                .ToArrayAsync())
                                 .Select(v =>
                                 new
                                 {
                                     Vendor = v.Vendor.ParentRecordId == null ? v.Vendor : v.Vendor.ParentRecord,
                                     v.Value,
                                 })
                                 .GroupBy(g => g.Vendor)
                                 .Select(g => new ProgramClosureReportRow
                                 {
                                     Vendor = g.Key,
                                     NumberOfVouchers = g.Count(),
                                     TotalValue = g.Sum(a => a.Value),
                                 })
                                 .OrderBy(a => a.Vendor.Name)
                                 .ToArray();
            itemCount = items.Count();

            int pageSize = 20;
            var pages = new List<ProgramClosureReportPage>();
            var numberOfPages = (int)Math.Ceiling(items.Length / (double)pageSize);

            if (numberOfPages == 0)
            {
                numberOfPages = 1;
            }
            for (int i = 0; i < numberOfPages; i++)
            {
                pages.Add(new ProgramClosureReportPage
                {
                    Page = i + 1,
                    IsLastPage = (i + 1) == numberOfPages,
                    Items = items.Skip(i * pageSize).Take(pageSize).ToArray(),
                    SubTotal = items.Skip(i * pageSize).Take(pageSize).Select(v => v.TotalValue).Sum()
                });
            }
            var user = await _userManager.FindByNameAsync(User.Identity.Name);

            var model = new ProgramClosureReportModel
            {
                User = user,
                Program = ctx.Programs.Where(d => d.Id == programId).First(),
                Organization = organization,
                Country = country,
                Pages = pages,
                Items = items,
                PageSize = pageSize,
                PaperSize = paperSize,
                Vendor = new Vendor(),
                TimezoneOffset = request.TimezoneOffset != null ? request.TimezoneOffset : DateTimeOffset.Now.Offset.TotalHours,
                Total = items.Select(i => i.TotalValue).Sum()
            };

            if (items.Any())
            {

                var spreadsheet = JArray.FromObject(items).Flatten("Vendor").ToDataTable().ToExcelSpreadsheet();
                ctx.ExportedReports.Add(new ExportedReport
                {
                    CountryId = countryId,
                    OrganizationId = organizationId,
                    ProgramId = programId,
                    ReportType = 3,

                    // NOTE: reportData is still empty at this point - the PDF is only rendered
                    // after this block. Pre-existing behaviour, left as-is.
                    OriginalReport = Convert.ToBase64String(reportData),
                    Spreadsheet = Convert.ToBase64String(spreadsheet),

                    Description = ctx.Programs.Where(d => d.Id == programId).First().Name,

                    ReportRunBy = this.GetUserId(),
                    ReportRunOn = DateTime.UtcNow
                });

                ctx.SaveChanges();

            }

            reportData = await GenerateReport("ProgramClosureReport.cshtml", "Program Closure Report", model, paperSize);

            return File(reportData, "application/pdf");
        }

        [HttpPost("VendorProgramFinancialReport")]
        public async Task<IActionResult> VendorProgramFinancialReport([FromBody] ReportRequestBindingModel request)
        {
            var paperSizeString = "A4";
            if (!String.IsNullOrEmpty(request.PaperSize))
            {
                paperSizeString = request.PaperSize;
            }
            // NOTE: see ProgramClosureReport - PaperKind is gone, the string is used directly.
            var paperSize = paperSizeString;

            var reportData = new byte[0];
            var content = new StringBuilder();

            var vendorId = request.VendorId ?? 0;
            var programId = request.ProgramId ?? 0;
            var countryId = request.CountryId;
            var organizationId = request.OrganizationId;
            var itemCount = 0;

            Models.Admin.Organization organization = null;
            Models.Admin.Country country = null;
            Func<int, string> createDistributionNumber = (distributionId) =>
            {
                var distribution = ctx.Distributions.Where(d => d.Id == distributionId).First();
                var groupsInOrder = ctx.Distributions.Where(d => d.ProgramId == programId).OrderBy(d => d.CreatedOn).Select(d => d.GroupId).ToList();
                var cycleNumber = groupsInOrder.IndexOf(distribution.GroupId) + 1;
                var distributionNumber = String.Format("{0:D3}-{1:D3}", distribution.Number ?? 0, cycleNumber);
                return distributionNumber;
            };



            organization = await actx.Organizations.AsNoTracking().Where(o => o.Id == organizationId).FirstOrDefaultAsync();
            country = await actx.Countries.AsNoTracking().Where(c => c.Id == countryId).FirstOrDefaultAsync();

            var items = (await ctx.VoucherTransactionRecords
                                .AsNoTracking()
                                .Include(v => v.Beneficiary)
                                .Include(v => v.Voucher)
                                .Where((v) => (v.VendorId == vendorId || v.Vendor.ParentRecordId == vendorId)
                                && v.Voucher.Distribution.ProgramId == programId
                                && (v.Type == 2)
                                && v.ReconciledOn != null
                                && v.IsFinalized != true
                                )
                                .ToArrayAsync())
                                 .Select(v =>
                                 new VendorProgramReceiptReportRow
                                 {
                                     Name = v.Beneficiary.FirstName + " " + v.Beneficiary.LastName,
                                     MobileNumber = v.Beneficiary.MobileNumber,
                                     PersonalIdentifier = v.Beneficiary.PersonalIdentifier,
                                     FinalizedOn = v.LastModifiedOn,
                                     VoucherCode = v.Voucher.VoucherCode,
                                     ConfirmationCode = v.ConfirmationCode,
                                     Value = v.Value,
                                     VoucherId = v.VoucherId,
                                     DistributionId = v.Voucher.DistributionId,
                                     DistributionNumber = createDistributionNumber(v.Voucher.DistributionId),
                                 }).OrderBy(a => a.VoucherCode).ToArray();
            itemCount = items.Count();

            int pageSize = 20;
            var pages = new List<VendorProgramReceiptReportPage>();
            var numberOfPages = (int)Math.Ceiling(items.Length / (double)pageSize);

            if (numberOfPages == 0)
            {
                numberOfPages = 1;
            }
            for (int i = 0; i < numberOfPages; i++)
            {
                pages.Add(new VendorProgramReceiptReportPage
                {
                    Page = i + 1,
                    IsLastPage = (i + 1) == numberOfPages,
                    Items = items.Skip(i * pageSize).Take(pageSize).ToArray(),
                    SubTotal = items.Skip(i * pageSize).Take(pageSize).Select(v => v.Value).Sum()
                });
            }
            var user = await _userManager.FindByNameAsync(User.Identity.Name);

            var model = new VendorProgramReceiptReportModel
            {
                User = user,
                Program = ctx.Programs.Where(d => d.Id == programId).First(),
                Organization = organization,
                Country = country,
                Pages = pages,
                Items = items,
                PageSize = pageSize,
                PaperSize = paperSize,
                Vendor = ctx.Vendors.Where(v => v.Id == vendorId).First(),
                TimezoneOffset = request.TimezoneOffset != null ? request.TimezoneOffset : DateTimeOffset.Now.Offset.TotalHours,
                Total = items.Select(i => i.Value).Sum()
            };

            reportData = await GenerateReport("VendorProgramReceiptReport.cshtml", "Vendor Financial Report", model, paperSize);

            var voucherIds = items.Where(v => v.VoucherId.HasValue).Select(v => v.VoucherId.Value).ToArray();

            // NOTE: deliberately tracked - these entities are updated and saved below.
            var transactionRecords = await ctx.VoucherTransactionRecords
                                .Where((v) => (v.VendorId == vendorId || v.Vendor.ParentRecordId == vendorId)
                                && v.Voucher.Distribution.ProgramId == programId
                                && (v.Type == 2)
                                && v.ReconciledOn != null
                                && v.IsFinalized != true)
                                .ToArrayAsync();

            foreach (var finalizedVoucher in transactionRecords)
            {
                finalizedVoucher.IsFinalized = true;
            }

            ctx.SaveChanges();

            if (items.Any())
            {
                var spreadsheet = JArray.FromObject(items).ToDataTable().ToExcelSpreadsheet();
                ctx.ExportedReports.Add(new ExportedReport
                {
                    CountryId = countryId,
                    OrganizationId = organizationId,
                    ProgramId = programId,
                    ReportType = 1,

                    OriginalReport = Convert.ToBase64String(reportData),

                    Description = ctx.Vendors.Where(v => v.Id == vendorId).First().Name,

                    ReportRunBy = this.GetUserId(),
                    ReportRunOn = DateTime.UtcNow
                });

                ctx.SaveChanges();
            }


            return File(reportData, "application/pdf");
        }

        [HttpPost("DistributionReport")]
        public async Task<IActionResult> DistributionReport([FromBody] ReportRequestBindingModel request)
        {
            var paperSizeString = "A4";
            if (!String.IsNullOrEmpty(request.PaperSize))
            {
                paperSizeString = request.PaperSize;
            }
            // NOTE: see ProgramClosureReport - PaperKind is gone, the string is used directly.
            var paperSize = paperSizeString;

            var content = new StringBuilder();

            var distributionId = request.DistributionId ?? 0;
            var countryId = request.CountryId;
            var organizationId = request.OrganizationId;


            Models.Admin.Organization organization = null;
            Models.Admin.Country country = null;
            Func<int?, string> fetchLatestConfirmationCode = (i) => ctx.VoucherTransactionRecords
                                                    .Where(t => t.VoucherId == i && t.Type == 2)
                                                    .OrderByDescending(t => t.LastModifiedOn)
                                                    .Select(t => t.ConfirmationCode)
                                                    .FirstOrDefault();


            organization = await actx.Organizations.AsNoTracking().Where(o => o.Id == organizationId).FirstOrDefaultAsync();
            country = await actx.Countries.AsNoTracking().Where(c => c.Id == countryId).FirstOrDefaultAsync();

            var items = (await ctx.VoucherTransactionRecords
                                .AsNoTracking()
                                .Include(v => v.Beneficiary).ThenInclude(b => b.Location)
                                .Include(v => v.Voucher)
                                .Where((v) => v.Voucher.DistributionId == distributionId
                                 && v.Type == 1
                                )
                                .ToArrayAsync())
                                .Select(v => new DistributionReportRow
                                {
                                    Name = v.Beneficiary.FirstName + " " + v.Beneficiary.LastName,
                                    Sex = v.Beneficiary.Sex == 0 ? "Male" : "Female",
                                    PersonalIdentifier = v.Beneficiary.PersonalIdentifier,
                                    MobileNumber = v.Beneficiary.MobileNumber,
                                    Location = v.Beneficiary.Location != null ? v.Beneficiary.Location.Name : "",
                                    VoucherCode = v.Voucher.VoucherCode,
                                    Value = v.Voucher.Value,
                                    StatusString = v.Voucher.StatusString,
                                    ConfirmationCode = fetchLatestConfirmationCode(v.VoucherId),
                                }).OrderBy(a => a.Name).ToArray();

            int pageSize = 20;
            var pages = new List<DistributionReportPage>();
            var numberOfPages = (int)Math.Ceiling(items.Count() / (double)pageSize);

            if (numberOfPages == 0)
            {
                numberOfPages = 1;
            }
            for (int i = 0; i < numberOfPages; i++)
            {
                pages.Add(new DistributionReportPage
                {
                    Page = i + 1,
                    IsLastPage = (i + 1) == numberOfPages,
                    Items = items.Skip(i * pageSize).Take(pageSize).ToArray(),
                    SubTotal = items.Skip(i * pageSize).Take(pageSize).Select(v => v.Value).Sum()
                });
            }
            var user = await _userManager.FindByNameAsync(User.Identity.Name);
            var distribution = ctx.Distributions.AsNoTracking().Include(d => d.Program).Where(d => d.Id == distributionId).First();
            var groupsInOrder = ctx.Distributions.Where(d => d.ProgramId == distribution.ProgramId).OrderBy(d => d.CreatedOn).Select(d => d.GroupId).ToList();
            var cycleNumber = groupsInOrder.IndexOf(distribution.GroupId) + 1;
            var distributionNumber = String.Format("{0:D3}-{1:D3}", distribution.Number ?? 0, cycleNumber);

            var model = new DistributionReportModel
            {
                User = user,
                Distribution = distribution,
                DistributionNumber = distributionNumber,
                Organization = organization,
                Country = country,
                Pages = pages,
                Items = items,
                PageSize = pageSize,
                PaperSize = paperSize,
                TimezoneOffset = request.TimezoneOffset != null ? request.TimezoneOffset : DateTimeOffset.Now.Offset.TotalHours,
                Total = items.Select(i => i.Value).Sum()
            };

            var reportData = await GenerateReport("DistributionReport.cshtml", "Distribution Report", model, paperSize);

            if (items.Any())
            {
                var spreadsheet = JArray.FromObject(items).ToDataTable().ToExcelSpreadsheet();
                ctx.ExportedReports.Add(new ExportedReport
                {
                    CountryId = countryId,
                    OrganizationId = organizationId,
                    ProgramId = distribution.ProgramId ?? 0,
                    ReportType = 2,

                    OriginalReport = Convert.ToBase64String(reportData),
                    Spreadsheet = Convert.ToBase64String(spreadsheet),

                    Description = distribution.Title,

                    ReportRunBy = this.GetUserId(),
                    ReportRunOn = DateTime.UtcNow
                });

                ctx.SaveChanges();

            }

            return File(reportData, "application/pdf");
        }

        [HttpPost("PaymentScheduleReport")]
        public async Task<IActionResult> PaymentScheduleReport([FromBody] ReportRequestBindingModel request)
        {
            var periodStart = DateTime.SpecifyKind(DateTime.Parse(request.PeriodStart.Replace("\"", "").Split('T')[0]), DateTimeKind.Utc);
            var periodEnd = DateTime.SpecifyKind(DateTime.Parse(request.PeriodEnd.Replace("\"", "").Split('T')[0]), DateTimeKind.Utc);

            var query = from v in ctx.VoucherTransactionRecords.AsNoTracking()
                        where
                            v.Voucher.Distribution.ProgramId == request.ProgramId
                            && v.ReconciledOn != null
                            && v.ReconciledOn.Value > periodStart
                            && v.ReconciledOn.Value < periodEnd
                            && v.IsFinalized == true
                        select
                            new
                            {
                                v.ReconciledOn,
                                VoucherName = v.Vendor.ParentRecordId == null ? v.Vendor.Name : v.Vendor.ParentRecord.Name,
                                v.Voucher.Category.Value
                            };

            var groupedQuery = (await query.ToArrayAsync())
                .GroupBy(s => s.ReconciledOn.Value.ToShortDateString())
                .Select(g => new
                {
                    g.Key,
                    Vendors = g.GroupBy(s => s.VoucherName).Select(v => new { v.Key, Vouchers = v.Sum(v1 => v1.Value) })
                }).Select(t => t.Vendors.Select(t1 => new object[] { t.Key, t1.Key, t1.Vouchers }))
            .SelectMany(v => v);

            // NOTE: this serializes the IQueryable, which re-runs the query, and neither
            // jsonString nor jsonCollection is used afterwards. Pre-existing, kept as-is.
            var jsonString = JsonConvert.SerializeObject(query, Formatting.Indented,
                new JsonSerializerSettings
                {
                    ReferenceLoopHandling = ReferenceLoopHandling.Ignore
                });

            var jsonCollection = JToken.Parse(jsonString) as JArray;

            var dataTable = new DataTable("Report");
            dataTable.Columns.Add("Date");
            dataTable.Columns.Add("Vendor");
            dataTable.Columns.Add("Amount");

            foreach (var row in groupedQuery)
            {
                dataTable.Rows.Add(row.ToArray());
            }

            return File(dataTable.ToExcelSpreadsheet(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "Report.xlsx");
        }

        // NOTE: the old template was "LiveBeneficiaryReport/{programId}/". ASP.NET Core rejects a
        // route template ending in '/', and it ignores a trailing slash when matching anyway, so
        // the URL shape is unchanged.
        [Authorize, HttpGet("LiveBeneficiaryReport/{programId}")]
        public async Task<IActionResult> LiveBeneficiaryReport([FromRoute]int programId) {
            var program = ctx.Programs.AsNoTracking().Where(t => t.Id == programId).First();
            var transactions = ctx.VoucherTransactionRecords.AsNoTracking().Where(t => t.Voucher.Distribution.ProgramId == programId);

            // NOTE: EF Core cannot materialise a bare GroupBy, so each aggregate is projected
            // before the dictionary is built. The aggregates themselves are unchanged.
            var voucherUsedByBeneficiaries = await transactions.Where(t=>t.Type==2)
                .GroupBy(g => g.BeneficiaryId)
                .Select(g => new { g.Key, Aggregate = g.Select(v => v.VoucherId).Distinct().Count() })
                .ToDictionaryAsync(k => k.Key, g => g.Aggregate);

            var amountUsedByBeneficiaries = await transactions
                .Where(t => t.Type == 2)
                .GroupBy(g => g.BeneficiaryId)
                .Select(g => new { g.Key, Aggregate = g.Select(v => v.Value).Sum() })
                .ToDictionaryAsync(k => k.Key, g => g.Aggregate);

            var voucherIssuedToBeneficiaries = await transactions.Where(t => t.Type == 1)
                .GroupBy(g => g.BeneficiaryId)
                .Select(g => new { g.Key, Aggregate = g.Select(v => v.VoucherId).Count() })
                .ToDictionaryAsync(k => k.Key, g => g.Aggregate);

            var amountIssuedToBeneficiaries = await transactions
                .Where(t => t.Type == 1)
                .GroupBy(g => g.BeneficiaryId)
                .Select(g => new { g.Key, Aggregate = g.Select(v => v.Value).Sum() })
                .ToDictionaryAsync(k => k.Key, g => g.Aggregate);

            var affectedBeneficiaries = voucherIssuedToBeneficiaries.Keys
                .Union(amountIssuedToBeneficiaries.Keys)
                .Union(voucherIssuedToBeneficiaries.Keys)
                .Union(amountIssuedToBeneficiaries.Keys)
                .Distinct();

            var beneficiaries = JArray.FromObject(await ctx.Beneficiaries.AsNoTracking()
                .Include(b => b.AdditionalData)
                .Include(b => b.Location)
                .Where(b => affectedBeneficiaries.Contains(b.Id))
                .ToArrayAsync(), new JsonSerializer { ReferenceLoopHandling = ReferenceLoopHandling.Ignore });

            var groups = await ctx.BeneficiaryGroups.AsNoTracking().Where(g => g.CountryId == program.CountryId && g.OrganizationId == program.OrganizationId).ToArrayAsync();

            foreach (var beneficiary in beneficiaries.OfType<JObject>())
            {
                var groupId = beneficiary.ValueIfExists<int?>("GroupId");
                if (groupId != null)
                {
                    beneficiary["Group"] = JToken.FromObject(groups.Where(g => g.Id == groupId.Value).First());
                }
                beneficiary["Sex"] = beneficiary["Sex"].Type != JTokenType.Null ? (beneficiary["Sex"].ToString() == "0" ? "Male" : "Female") : "";
            }


            beneficiaries.Flatten("AdditionalDataObject", "Additional Data/")
                .Flatten("Group", "Cycle/")
                .Flatten("Location", "Location/");
            beneficiaries.RemoveProperties("Cycle/Beneficiaries", "Cycle/OrganizationId", "Cycle/CountryId");
            beneficiaries.RemoveProperties("Location/CountryId");
            beneficiaries.RemoveProperties("Group", "Location", "AdditionalDataObject", "AdditionalData");

            foreach (dynamic beneficiary in beneficiaries)
            {
                int beneficiaryId = beneficiary.Id;
                beneficiary.VouchersIssued = voucherIssuedToBeneficiaries.ContainsKey(beneficiaryId) ? voucherIssuedToBeneficiaries[beneficiaryId] : 0;
                beneficiary.VouchersUsed = voucherUsedByBeneficiaries.ContainsKey(beneficiaryId) ? voucherUsedByBeneficiaries[beneficiaryId] : 0;
                beneficiary.AmountIssued = amountIssuedToBeneficiaries.ContainsKey(beneficiaryId) ? amountIssuedToBeneficiaries[beneficiaryId] : 0;
                beneficiary.AmountUsed = amountUsedByBeneficiaries.ContainsKey(beneficiaryId) ? amountUsedByBeneficiaries[beneficiaryId] : 0;
            }

            var datatable = beneficiaries.ToDataTable();

            #region Somewhat Necessary Garbage Collection
            beneficiaries = null;
            groups = null;
            GC.Collect();
            #endregion

            return File(datatable.ToExcelSpreadsheet(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "LiveData.xlsx");
        }

        private async Task<byte[]> GenerateReport<TModel>(string reportName, string reportTitle, TModel model, string paperSize = "A4")
        {
            byte[] reportData = null;
            var content = new StringBuilder();

            var compiled = await _viewRenderer.RenderAsync("/Views/Reports/" + reportName, model);

            content.Append(compiled);

            // NOTE: the TuesPechkin GlobalSettings this replaces hard-coded PaperSize = A4 and a
            // landscape orientation and never looked at the paper size the caller passed in - the
            // requested size only ever reached the templates through the model. Same behaviour
            // here; ProduceOutline and DocumentTitle ("@reportTitle") have no equivalent on
            // IPdfRenderer, and the 1cm margins are the renderer's own concern now.
            reportData = await _pdfRenderer.RenderAsync(content.ToString(), "A4", PdfOrientation.Landscape);
            return reportData;
        }
    }
}
