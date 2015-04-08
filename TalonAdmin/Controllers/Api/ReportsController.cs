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
using Microsoft.AspNet.Identity;
using Microsoft.AspNet.Identity.Owin;

using TuesPechkin;
using RazorEngine.Templating;
using RazorEngine;
using System.Drawing.Printing;

namespace TalonAdmin.Controllers.Api
{
    [Authorize]
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


        [HttpGet]
        [Route("VendorFinancialReport")]
        public async Task<IHttpActionResult> VendorFinancialReport(string period, int distributionId, int vendorId)
        {
            var report = System.IO.File.ReadAllText(HostingEnvironment.MapPath("~/Reports/VendorReceiptReport.cshtml"));
            var image = new byte[0];
            var content = new StringBuilder();

            var periodStartDate = DateTime.Parse(period);
            periodStartDate = new DateTime(periodStartDate.Year, periodStartDate.Month, 1);

            var periodEndDate = periodStartDate.AddMonths(1).AddSeconds(-1);
            using (var ctx = new Models.Vouchers.Context())
            {
                Models.Admin.Organization organization = null;
                Models.Admin.Country country = null;
                var organizationId = this.GetOrganizationId();
                var countryId = this.GetCountryId();
                using (var actx = new Models.Admin.AdminContext())
                {
                    organization = await actx.Organizations.Where(o => o.Id == organizationId).FirstOrDefaultAsync();
                    country = await actx.Countries.Where(c => c.Id == countryId).FirstOrDefaultAsync();
                }

                var items =( await (from v in ctx.VoucherTransactionRecords
                                   where
                                   (v.VendorId == vendorId && v.Voucher.DistributionId == distributionId) &&
                                   (v.FinalizedOn >= periodStartDate && v.FinalizedOn <= periodEndDate)
                                   select new {
                                       Name = v.Beneficiary.FirstName + " " + v.Beneficiary.LastName, 
                                       v.Beneficiary.MobileNumber, 
                                       v.FinalizedOn, 
                                       v.Voucher.VoucherCode,
                                       v.ConfirmationCode,
                                       v.Voucher.Category.Value
                                   }).ToArrayAsync()).OrderBy(a => a.Name).ToArray();

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
                    Period = periodStartDate,
                    Pages = pages,
                    Items = items,
                    PageSize = pageSize,
                    Vendor = ctx.Vendors.Where(v => v.Id == vendorId).First(),
                    Total = items.Select(i => i.Value).Sum()
                };
                var compiled = Engine.Razor.RunCompile(report, Guid.NewGuid().ToString(), null, model);

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

            return this.File(WebApiApplication.converter.Convert(document), "Report.pdf", "application/pdf");
        }
    }
}