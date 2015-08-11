using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Web.Http;
using TalonAdmin.Controllers.Api;
using OfficeOpenXml;
using TalonAdmin.ActionResults;
using System.IO;
using Newtonsoft.Json.Linq;
using System.Data;
using System.Net.Http;
using System.Net;
using System.Text;
using System.Data.Entity;
using TalonAdmin.Extensions;
using TalonAdmin.Attributes;
using Microsoft.AspNet.Identity;
using TalonAdmin.Models.Vouchers;
using System.Web.Hosting;
using Microsoft.AspNet.Identity.Owin;

using TuesPechkin;
using RazorEngine.Templating;
using RazorEngine;
using System.Drawing.Printing;
using TalonAdmin.Controllers.BindingModels;
using System.Security.Cryptography;
using Newtonsoft.Json;
using RazorEngine.Configuration;
using ZXing;


namespace TalonAdmin.Controllers.Api
{
    [RoutePrefix("api/Reports")]
    public class ReportsController : ApiController
    {
        private TemplateServiceConfiguration _config = null;
        private IRazorEngineService _service = null;

        public ReportsController()
        {
            var directories = new string[] { HostingEnvironment.MapPath("~/Reports/") };

            this._config = new TemplateServiceConfiguration();
            _config.TemplateManager = new RazorEngine.Templating.WatchingResolvePathTemplateManager(directories, new InvalidatingCachingProvider());
            this._service = RazorEngineService.Create(_config);
        }

        private ApplicationUserManager UserManager
        {
            get
            {
                return Request.GetOwinContext().GetUserManager<ApplicationUserManager>();
            }
        }

        [Route("GenerateTestSheet")]
        public async Task<IHttpActionResult> GenerateTestSheet([FromUri]int distributionId)
        {
            using (var ctx = new Models.Vouchers.Context())
            {
                var distribution = await ctx.Distributions.Where(d => d.Id == distributionId).FirstAsync();
                var vouchers = await ctx.Vouchers.Where(v => v.DistributionId == distribution.Id).ToListAsync();
                var codeWriter = new ZXing.QrCode.QRCodeWriter();
                var writer = new BarcodeWriter();

                var qrCodes = vouchers.Select(v =>
                {
                    var code = String.IsNullOrEmpty(v.SequentialCode) ? v.VoucherCode : v.SequentialCode;
                    var barcode = codeWriter.encode(code, BarcodeFormat.QR_CODE, 200, 200);
                    var bitmap = writer.Write(barcode);
                    var memoryStream = new MemoryStream();
                    bitmap.Save(memoryStream, System.Drawing.Imaging.ImageFormat.Png);

                    return new
                    {
                        QR = "data:image/png;base64," + Convert.ToBase64String(memoryStream.ToArray()),
                        Beneficiary = v.IssuingTransactionRecord.Beneficiary.Name,
                        Value = v.Value,
                        ValidAfter = v.Category.ValidAfter,
                    };
                }).OrderBy(d => d.Beneficiary).ThenBy(d => d.ValidAfter);


                dynamic model = new
                {
                    Codes = qrCodes.ToArray()
                };

                var reportData = GenerateReport("TestSheet.cshtml", "Test Sheet", (object)model, PaperKind.Letter);

                return this.File(reportData, null, "application/pdf");
            }
        }

        [Route("ProgramClosureReport")]
        public async Task<IHttpActionResult> ProgramClosureReport([FromBody] ReportRequestBindingModel request)
        {
            var paperSizeString = "A4";
            if (!String.IsNullOrEmpty(request.PaperSize))
            {
                paperSizeString = request.PaperSize;
            }
            var paperSize = (PaperKind)Enum.Parse(typeof(PaperKind), paperSizeString);

            var reportData = new byte[0];
            var content = new StringBuilder();

            var vendorId = request.VendorId ?? 0;
            var programId = request.ProgramId ?? 0;
            var countryId = request.CountryId;
            var organizationId = request.OrganizationId;
            var itemCount = 0;

            using (var ctx = new Models.Vouchers.Context())
            {
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



                using (var actx = new Models.Admin.AdminContext())
                {
                    organization = await actx.Organizations.Where(o => o.Id == organizationId).FirstOrDefaultAsync();
                    country = await actx.Countries.Where(c => c.Id == countryId).FirstOrDefaultAsync();
                }

                var items = (await ctx.VoucherTransactionRecords
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
                                     .Select(g => new
                                     {
                                         Vendor = g.Key,
                                         NumberOfVouchers = g.Count(),
                                         TotalValue = g.Sum(a => a.Value),
                                     })
                                     .OrderBy(a => a.Vendor.Name)
                                     .ToArray();
                itemCount = items.Count();

                int pageSize = 20;
                var pages = new List<dynamic>();
                var numberOfPages = (int)Math.Ceiling(items.Length / (double)pageSize);

                if (numberOfPages == 0)
                {
                    numberOfPages = 1;
                }
                for (int i = 0; i < numberOfPages; i++)
                {
                    pages.Add(new
                    {
                        Page = i + 1,
                        IsLastPage = (i + 1) == numberOfPages,
                        Items = items.Skip(i * pageSize).Take(pageSize).ToArray(),
                        SubTotal = items.Skip(i * pageSize).Take(pageSize).Select(v => v.TotalValue).Sum()
                    });
                }
                var user = UserManager.FindByName(User.Identity.Name);

                dynamic model = new
                {
                    User = user,
                    Program = ctx.Programs.Where(d => d.Id == programId).First(),
                    Organization = organization,
                    Country = country,
                    Pages = pages,
                    Items = items,
                    PageSize = pageSize,
                    PaperSize = Enum.GetName(typeof(PaperKind), paperSize),
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

                        OriginalReport = Convert.ToBase64String(reportData),
                        Spreadsheet = Convert.ToBase64String(spreadsheet),

                        Description = ctx.Programs.Where(d => d.Id == programId).First().Name,

                        ReportRunBy = User.Identity.GetUserId(),
                        ReportRunOn = DateTime.UtcNow
                    });

                    ctx.SaveChanges();

                }

                reportData = GenerateReport("ProgramClosureReport.cshtml", "Program Closure Report", (object)model, paperSize);
            }

            return this.File(reportData, null, "application/pdf");
        }

        [Route("VendorProgramFinancialReport")]
        public async Task<IHttpActionResult> VendorProgramFinancialReport([FromBody] ReportRequestBindingModel request)
        {
            var paperSizeString = "A4";
            if (!String.IsNullOrEmpty(request.PaperSize))
            {
                paperSizeString = request.PaperSize;
            }
            var paperSize = (PaperKind)Enum.Parse(typeof(PaperKind), paperSizeString);

            var reportData = new byte[0];
            var content = new StringBuilder();

            var vendorId = request.VendorId ?? 0;
            var programId = request.ProgramId ?? 0;
            var countryId = request.CountryId;
            var organizationId = request.OrganizationId;
            var itemCount = 0;

            using (var ctx = new Models.Vouchers.Context())
            {
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



                using (var actx = new Models.Admin.AdminContext())
                {
                    organization = await actx.Organizations.Where(o => o.Id == organizationId).FirstOrDefaultAsync();
                    country = await actx.Countries.Where(c => c.Id == countryId).FirstOrDefaultAsync();
                }

                var items = (await ctx.VoucherTransactionRecords
                                    .Where((v) => (v.VendorId == vendorId || v.Vendor.ParentRecordId == vendorId)
                                    && v.Voucher.Distribution.ProgramId == programId
                                    && (v.Type == 2)
                                    && v.ReconciledOn != null
                                    && v.IsFinalized != true
                                    )
                                    .ToArrayAsync())
                                     .Select(v =>
                                     new
                                     {
                                         Name = v.Beneficiary.FirstName + " " + v.Beneficiary.LastName,
                                         v.Beneficiary.MobileNumber,
                                         v.Beneficiary.NationalId,
                                         FinalizedOn = v.LastModifiedOn,
                                         v.Voucher.VoucherCode,
                                         v.ConfirmationCode,
                                         v.Value,
                                         v.VoucherId,
                                         v.Voucher.DistributionId,
                                         DistributionNumber = createDistributionNumber(v.Voucher.DistributionId),
                                     }).OrderBy(a => a.VoucherCode).ToArray();
                itemCount = items.Count();

                int pageSize = 20;
                var pages = new List<dynamic>();
                var numberOfPages = (int)Math.Ceiling(items.Length / (double)pageSize);

                if (numberOfPages == 0)
                {
                    numberOfPages = 1;
                }
                for (int i = 0; i < numberOfPages; i++)
                {
                    pages.Add(new
                    {
                        Page = i + 1,
                        IsLastPage = (i + 1) == numberOfPages,
                        Items = items.Skip(i * pageSize).Take(pageSize).ToArray(),
                        SubTotal = items.Skip(i * pageSize).Take(pageSize).Select(v => v.Value).Sum()
                    });
                }
                var user = UserManager.FindByName(User.Identity.Name);

                dynamic model = new
                {
                    User = user,
                    Program = ctx.Programs.Where(d => d.Id == programId).First(),
                    Organization = organization,
                    Country = country,
                    Pages = pages,
                    Items = items,
                    PageSize = pageSize,
                    PaperSize = Enum.GetName(typeof(PaperKind), paperSize),
                    Vendor = ctx.Vendors.Where(v => v.Id == vendorId).First(),
                    TimezoneOffset = request.TimezoneOffset != null ? request.TimezoneOffset : DateTimeOffset.Now.Offset.TotalHours,
                    Total = items.Select(i => i.Value).Sum()
                };

                reportData = GenerateReport("VendorProgramReceiptReport.cshtml", "Vendor Financial Report", (object)model, paperSize);

                var voucherIds = items.Where(v => v.VoucherId.HasValue).Select(v => v.VoucherId.Value).ToArray();

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

                        ReportRunBy = User.Identity.GetUserId(),
                        ReportRunOn = DateTime.UtcNow
                    });

                    ctx.SaveChanges();
                }
            }


            return this.File(reportData, null, "application/pdf");
        }

        [Route("DistributionReport")]
        public async Task<IHttpActionResult> DistributionReport([FromBody] ReportRequestBindingModel request)
        {
            var paperSizeString = "A4";
            if (!String.IsNullOrEmpty(request.PaperSize))
            {
                paperSizeString = request.PaperSize;
            }
            var paperSize = (PaperKind)Enum.Parse(typeof(PaperKind), paperSizeString);

            var content = new StringBuilder();

            var distributionId = request.DistributionId ?? 0;
            var countryId = request.CountryId;
            var organizationId = request.OrganizationId;


            using (var ctx = new Models.Vouchers.Context())
            {
                Models.Admin.Organization organization = null;
                Models.Admin.Country country = null;
                Func<int?, string> fetchLatestConfirmationCode = (i) => ctx.VoucherTransactionRecords
                                                        .Where(t => t.VoucherId == i && t.Type == 2)
                                                        .OrderByDescending(t => t.LastModifiedOn)
                                                        .Select(t => t.ConfirmationCode)
                                                        .FirstOrDefault();


                using (var actx = new Models.Admin.AdminContext())
                {
                    organization = await actx.Organizations.Where(o => o.Id == organizationId).FirstOrDefaultAsync();
                    country = await actx.Countries.Where(c => c.Id == countryId).FirstOrDefaultAsync();
                }
                var items = (await ctx.VoucherTransactionRecords
                                    .Where((v) => v.Voucher.DistributionId == distributionId
                                     && v.Type == 1
                                    )
                                    .ToArrayAsync())
                                    .Select(v => new
                                    {
                                        Name = v.Beneficiary.FirstName + " " + v.Beneficiary.LastName,
                                        Sex = v.Beneficiary.Sex == 0 ? "Male" : "Female",
                                        v.Beneficiary.NationalId,
                                        v.Beneficiary.MobileNumber,
                                        Location = v.Beneficiary.Location != null ? v.Beneficiary.Location.Name : "",
                                        v.Voucher.VoucherCode,
                                        v.Voucher.Value,
                                        v.Voucher.StatusString,
                                        ConfirmationCode = fetchLatestConfirmationCode(v.VoucherId),
                                    }).OrderBy(a => a.Name).ToArray();

                int pageSize = 20;
                var pages = new List<dynamic>();
                var numberOfPages = (int)Math.Ceiling(items.Count() / (double)pageSize);

                if (numberOfPages == 0)
                {
                    numberOfPages = 1;
                }
                for (int i = 0; i < numberOfPages; i++)
                {
                    pages.Add(new
                    {
                        Page = i + 1,
                        IsLastPage = (i + 1) == numberOfPages,
                        Items = items.Skip(i * pageSize).Take(pageSize).ToArray(),
                        SubTotal = items.Skip(i * pageSize).Take(pageSize).Select(v => v.Value).Sum()
                    });
                }
                var user = UserManager.FindByName(User.Identity.Name);
                var distribution = ctx.Distributions.Where(d => d.Id == distributionId).First();
                var groupsInOrder = ctx.Distributions.Where(d => d.ProgramId == distribution.ProgramId).OrderBy(d => d.CreatedOn).Select(d => d.GroupId).ToList();
                var cycleNumber = groupsInOrder.IndexOf(distribution.GroupId) + 1;
                var distributionNumber = String.Format("{0:D3}-{1:D3}", distribution.Number ?? 0, cycleNumber);

                var model = new
                {
                    User = user,
                    Distribution = distribution,
                    DistributionNumber = distributionNumber,
                    Organization = organization,
                    Country = country,
                    Pages = pages,
                    Items = items,
                    PageSize = pageSize,
                    PaperSize = Enum.GetName(typeof(PaperKind), paperSize),
                    TimezoneOffset = request.TimezoneOffset != null ? request.TimezoneOffset : DateTimeOffset.Now.Offset.TotalHours,
                    Total = items.Select(i => i.Value).Sum()
                };

                var reportData = GenerateReport("DistributionReport.cshtml", "Distribution Report", (object)model, paperSize);

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

                        ReportRunBy = User.Identity.GetUserId(),
                        ReportRunOn = DateTime.UtcNow
                    });

                    ctx.SaveChanges();

                }

                return this.File(reportData, null, "application/pdf");
            }
        }

        [Route("PaymentScheduleReport")]
        public async Task<IHttpActionResult> PaymentScheduleReport([FromBody] ReportRequestBindingModel request)
        {
            using (var ctx = new Models.Vouchers.Context())
            {
                ctx.Configuration.LazyLoadingEnabled = false;
                ctx.Configuration.ProxyCreationEnabled = false;

                var periodStart = DateTime.SpecifyKind(DateTime.Parse(request.PeriodStart.Replace("\"", "").Split('T')[0]), DateTimeKind.Utc);
                var periodEnd = DateTime.SpecifyKind(DateTime.Parse(request.PeriodEnd.Replace("\"", "").Split('T')[0]), DateTimeKind.Utc);

                var query = from v in ctx.VoucherTransactionRecords
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

                return this.File(dataTable.ToExcelSpreadsheet(), "Report.xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
            }
        }

        private byte[] GenerateReport(string reportName, string reportTitle, object model, PaperKind paperKind = PaperKind.A4)
        {
            byte[] reportData = null;
            var content = new StringBuilder();

            if (!_service.IsTemplateCached(reportName, null))
            {
                _service.Compile(reportName);
            }

            var compiled = _service.RunCompile(reportName, null, model);

            content.Append(compiled);


            var document = new HtmlToPdfDocument
            {
                GlobalSettings =
                {
                    ProduceOutline = true,
                    DocumentTitle = reportTitle,
                    PaperSize = PaperKind.A4,
                    Margins =
                    {
                        All = 1,
                        Unit = Unit.Centimeters
                    },
                    Orientation = TuesPechkin.GlobalSettings.PaperOrientation.Landscape
                },
                Objects = {
                    new ObjectSettings {
                        HtmlText = content.ToString(),
                        WebSettings = new WebSettings {
                            DefaultEncoding = "utf-8"
                        }
                    },
                }
            };

            reportData = WebApiApplication.converter.Convert(document);
            return reportData;
        }
    }
}