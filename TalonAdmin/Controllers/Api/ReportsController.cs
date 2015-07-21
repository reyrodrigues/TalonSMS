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
                    var cycleNumber = distribution.Group != null ? distribution.Group.Number : 0;
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
                    //&& v.Voucher.ReconciledOn != null
                    //&& v.Voucher.IsFinalized != true
                                    )
                                    .ToArrayAsync())
                                     .Select(v =>
                                     new
                                    {
                                        Name = v.Beneficiary.FirstName + " " + v.Beneficiary.LastName,
                                        v.Beneficiary.MobileNumber,
                                        v.Beneficiary.NationalId,
                                        v.FinalizedOn,
                                        v.Voucher.VoucherCode,
                                        v.ConfirmationCode,
                                        v.Voucher.Category.Value,
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

                var model = new
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
                    TimezoneOffset = request.TimezoneOffset != null? request.TimezoneOffset : DateTimeOffset.Now.Offset.TotalHours,
                    Total = items.Select(i => i.Value).Sum()
                };


                if (!_service.IsTemplateCached("VendorProgramReceiptReport.cshtml", null))
                {
                    _service.Compile("VendorProgramReceiptReport.cshtml");
                }

                var compiled = _service.RunCompile("VendorProgramReceiptReport.cshtml", null, model);

                content.Append(compiled);


                var document = new HtmlToPdfDocument
                {
                    GlobalSettings =
                    {
                        ProduceOutline = true,
                        DocumentTitle = "Vendor Financial Report",
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

                var voucherIds = items.Where(v => v.VoucherId.HasValue).Select(v => v.VoucherId.Value).ToArray();

                foreach (var finalizedVoucher in ctx.Vouchers.Where(v => voucherIds.Contains(v.Id)))
                {
                    finalizedVoucher.IsFinalized = true;
                }

                ctx.SaveChanges();
            }


            using (var ctx = new Models.Vouchers.Context())
            {
                if (itemCount > 0)
                {
                    ctx.ProgramVendorReconciliations.Add(new ProgramVendorReconciliation
                    {
                        CountryId = countryId,
                        OrganizationId = organizationId,
                        ReconciledOn = DateTime.Now,
                        OriginalReport = Convert.ToBase64String(reportData),
                        VendorId = vendorId,
                        ProgramId = programId,

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

                using (var actx = new Models.Admin.AdminContext())
                {
                    organization = await actx.Organizations.Where(o => o.Id == organizationId).FirstOrDefaultAsync();
                    country = await actx.Countries.Where(c => c.Id == countryId).FirstOrDefaultAsync();
                }
                var items = (await ctx.VoucherTransactionRecords
                                    .Where((v) => v.Voucher.DistributionId == distributionId)
                                    .ToArrayAsync())
                                    .Select(v => new
                                    {
                                        Name = v.Beneficiary.FirstName + " " + v.Beneficiary.LastName,
                                        Sex = v.Beneficiary.Sex == 0 ? "Male" : "Female",
                                        v.Beneficiary.NationalId,
                                        v.Beneficiary.MobileNumber,
                                        Location = v.Beneficiary.Location != null ? v.Beneficiary.Location.Name : "",
                                        v.Voucher.VoucherCode,
                                        v.Voucher.Category.Value,
                                        v.StatusString,
                                        v.ConfirmationCode,
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
                var cycleNumber = distribution.Group != null ? distribution.Group.Number : 0;
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

                if (!_service.IsTemplateCached("DistributionReport.cshtml", null))
                {
                    _service.Compile("DistributionReport.cshtml");
                }

                var compiled = _service.RunCompile("DistributionReport.cshtml", null, model);

                content.Append(compiled);
            }

            var document = new HtmlToPdfDocument
            {
                GlobalSettings =
                {
                    ProduceOutline = true,
                    DocumentTitle = "Vendor Financial Report",
                    PaperSize = paperSize,
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

            return this.File(WebApiApplication.converter.Convert(document), null, "application/pdf");
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
                                && v.Voucher.ReconciledOn != null
                                && v.Voucher.ReconciledOn.Value > periodStart
                                && v.Voucher.ReconciledOn.Value < periodEnd
                                && v.Voucher.IsFinalized == true
                            select
                                new
                                {
                                    v.Voucher.ReconciledOn,
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

    }
}