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

namespace TalonAdmin.Controllers.Api
{
    [RoutePrefix("api/Reports")]
    public class ReportsController : ApiController
    {
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
            var reportData = new byte[0];
            var report = System.IO.File.ReadAllText(HostingEnvironment.MapPath("~/Reports/VendorProgramReceiptReport.cshtml"));
            var image = new byte[0];
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



                using (var actx = new Models.Admin.AdminContext())
                {
                    organization = await actx.Organizations.Where(o => o.Id == organizationId).FirstOrDefaultAsync();
                    country = await actx.Countries.Where(c => c.Id == countryId).FirstOrDefaultAsync();
                }

                var items = (await (from v in ctx.VoucherTransactionRecords
                                    where
                                    (v.VendorId == vendorId || v.Vendor.ParentRecordId == vendorId)
                                    && v.Voucher.Distribution.ProgramId == programId
                                    && v.Voucher.ReconciledOn != null
                                    && v.Voucher.IsFinalized != true
                                    // && (v.FinalizedOn >= periodStartDate && v.FinalizedOn <= periodEndDate)
                                    select new
                                    {
                                        Name = v.Beneficiary.FirstName + " " + v.Beneficiary.LastName,
                                        v.Beneficiary.MobileNumber,
                                        v.FinalizedOn,
                                        v.Voucher.VoucherCode,
                                        v.ConfirmationCode,
                                        v.Voucher.Category.Value,
                                        v.VoucherId,
                                        v.Voucher.DistributionId,
                                    }).ToArrayAsync()).OrderBy(a => a.VoucherCode).ToArray();
                itemCount = items.Count();

                int pageSize = 15;
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
                    Vendor = ctx.Vendors.Where(v => v.Id == vendorId).First(),
                    Total = items.Select(i => i.Value).Sum()
                };


                var hash = Convert.ToBase64String(MD5CryptoServiceProvider.Create().ComputeHash(System.Text.Encoding.UTF8.GetBytes(report)));
                if (!Engine.Razor.IsTemplateCached(hash, null))
                {
                    Engine.Razor.Compile(report, hash);
                }

                var compiled = Engine.Razor.RunCompile(hash, null, model);

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


        [Route("VendorFinancialReport")]
        public async Task<IHttpActionResult> VendorFinancialReport([FromBody] ReportRequestBindingModel request)
        {
            var reportData = new byte[0];
            var report = System.IO.File.ReadAllText(HostingEnvironment.MapPath("~/Reports/VendorReceiptReport.cshtml"));
            var image = new byte[0];
            var content = new StringBuilder();

            var vendorId = request.VendorId ?? 0;
            var distributionId = request.DistributionId ?? 0;
            var countryId = request.CountryId;
            var organizationId = request.OrganizationId;
            var itemCount = 0;

            using (var ctx = new Models.Vouchers.Context())
            {
                Models.Admin.Organization organization = null;
                Models.Admin.Country country = null;


                using (var actx = new Models.Admin.AdminContext())
                {
                    organization = await actx.Organizations.Where(o => o.Id == organizationId).FirstOrDefaultAsync();
                    country = await actx.Countries.Where(c => c.Id == countryId).FirstOrDefaultAsync();
                }

                var items = (await (from v in ctx.VoucherTransactionRecords
                                    where
                                    v.VendorId == vendorId
                                    && v.Voucher.DistributionId == distributionId
                                    && v.Voucher.ReconciledOn != null
                                    && v.Voucher.IsFinalized != true
                                    // && (v.FinalizedOn >= periodStartDate && v.FinalizedOn <= periodEndDate)
                                    select new
                                    {
                                        Name = v.Beneficiary.FirstName + " " + v.Beneficiary.LastName,
                                        v.Beneficiary.MobileNumber,
                                        v.FinalizedOn,
                                        v.Voucher.VoucherCode,
                                        v.ConfirmationCode,
                                        v.Voucher.Category.Value,
                                        v.VoucherId
                                    }).ToArrayAsync()).OrderBy(a => a.Name).ToArray();
                itemCount = items.Count();

                int pageSize = 15;
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
                    Distribution = ctx.Distributions.Where(d => d.Id == distributionId).First(),
                    Organization = organization,
                    Country = country,
                    Pages = pages,
                    Items = items,
                    PageSize = pageSize,
                    Vendor = ctx.Vendors.Where(v => v.Id == vendorId).First(),
                    Total = items.Select(i => i.Value).Sum()
                };

                var hash = Convert.ToBase64String(MD5CryptoServiceProvider.Create().ComputeHash(System.Text.Encoding.UTF8.GetBytes(report)));
                if (!Engine.Razor.IsTemplateCached(hash, null))
                {
                    Engine.Razor.Compile(report, hash);
                }

                var compiled = Engine.Razor.RunCompile(hash, null, model);


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
                    ctx.DistributionVendorReconciliations.Add(new DistributionVendorReconciliation
                    {
                        CountryId = countryId,
                        OrganizationId = organizationId,
                        ReconciledOn = DateTime.Now,
                        OriginalReport = Convert.ToBase64String(reportData),
                        VendorId = vendorId,
                        DistributionId = distributionId,

                        ReportRunBy = User.Identity.GetUserId(),
                        ReportRunOn = DateTime.Now
                    });

                    ctx.SaveChanges();

                }
            }

            return this.File(reportData, null, "application/pdf");
        }

        [Route("DistributionReport")]
        public async Task<IHttpActionResult> DistributionReport([FromBody] ReportRequestBindingModel request)
        {
            var report = System.IO.File.ReadAllText(HostingEnvironment.MapPath("~/Reports/DistributionReport.cshtml"));
            var image = new byte[0];
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

                var items = (await (from v in ctx.VoucherTransactionRecords
                                    where
                                    (v.Voucher.DistributionId == distributionId)
                                    select new
                                    {
                                        Name = v.Beneficiary.FirstName + " " + v.Beneficiary.LastName,
                                        Sex = v.Beneficiary.Sex == 0 ? "Male" : "Female",
                                        v.Beneficiary.NationalId,
                                        v.Beneficiary.MobileNumber,
                                        Location = v.Beneficiary.Location != null ? v.Beneficiary.Location.Name : "",
                                        v.Voucher.VoucherCode,
                                        v.Voucher.Category.Value
                                    }).ToArrayAsync()).OrderBy(a => a.Name).ToArray();

                int pageSize = 18;
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
                    Distribution = ctx.Distributions.Where(d => d.Id == distributionId).First(),
                    Organization = organization,
                    Country = country,
                    Pages = pages,
                    Items = items,
                    PageSize = pageSize,
                    Total = items.Select(i => i.Value).Sum()
                };

                var hash = Convert.ToBase64String(MD5CryptoServiceProvider.Create().ComputeHash(System.Text.Encoding.UTF8.GetBytes(report)));
                if (!Engine.Razor.IsTemplateCached(hash, null))
                {
                    Engine.Razor.Compile(report, hash);
                }

                var compiled = Engine.Razor.RunCompile(hash, null, model);

                content.Append(compiled);

            }

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

            return this.File(WebApiApplication.converter.Convert(document), null, "application/pdf");
        }
    }
}