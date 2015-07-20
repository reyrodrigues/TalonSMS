using Newtonsoft.Json.Linq;
using RazorEngine;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Drawing.Printing;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using TuesPechkin;
using RazorEngine.Templating;
using Microsoft.AspNet.Identity;
using Microsoft.AspNet.Identity.Owin;
using System.DirectoryServices.AccountManagement;
using System.Globalization;
using Breeze.ContextProvider.EF6;
using Newtonsoft.Json;

namespace TalonAdmin.Controllers
{
    public class HomeController : Controller
    {
        private ApplicationUserManager _userManager;
        public ApplicationUserManager UserManager
        {
            get
            {
                return _userManager ?? Request.GetOwinContext().GetUserManager<ApplicationUserManager>();
            }
            private set
            {
                _userManager = value;
            }
        }

        public ActionResult Index()
        {
            return  Redirect("~/App/");
        }

        public ActionResult TestReport(int vendorId, int distributionId, string period)
        {
            var content = new StringBuilder();
            var report = System.IO.File.ReadAllText(Server.MapPath("~/Reports/VendorReceiptReport.cshtml"));
            var image = System.IO.File.ReadAllBytes(Server.MapPath("~/Content/IRCLogo.svg"));
            var periodDate = DateTime.Parse(period);
            using (var ctx = new Models.Vouchers.Context())
            {
                var items = (from v in ctx.VoucherTransactionRecords
                             where
                             v.VendorId == vendorId && v.Voucher.DistributionId == distributionId
                             orderby v.Beneficiary.Name
                             select v).ToArray();

                int pageSize = 12;
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
                        SubTotal = items.Skip(i * pageSize).Take(pageSize).Select(v => v.Voucher.Category.Value).Sum()
                    });
                }
                var user = UserManager.FindByName(User.Identity.Name);

                var model = new
                {
                    User = user,
                    Distribution = ctx.Distributions.Where(d => d.Id == distributionId).First(),
                    Period = periodDate,
                    Pages = pages,
                    Items = items,
                    PageSize = pageSize,
                    Vendor = ctx.Vendors.Where(v => v.Id == vendorId).First(),
                    Logo = Convert.ToBase64String(image),
                    LogoMimeType = "image/svg+xml",
                    Total = items.Select(i => i.Voucher.Category.Value).Sum()
                };
                var compiled = Engine.Razor.RunCompile(report, Guid.NewGuid().ToString(), null, model);

                content.Append(compiled);

            }

            var document = new HtmlToPdfDocument
            {
                GlobalSettings =
                {
                    ProduceOutline = true,
                    DocumentTitle = "Pretty Websites",
                    PaperSize = PaperKind.A4, // Implicit conversion to PechkinPaperSize
                    Margins =
                    {
                        All = 1,
                        Unit = Unit.Centimeters
                    },
                    Orientation = TuesPechkin.GlobalSettings.PaperOrientation.Landscape
                },
                Objects = {
                    new ObjectSettings { HtmlText = content.ToString() },
                }
            };

            return File(WebApiApplication.converter.Convert(document), "application/pdf");
        }


        public ActionResult TestLDAPS()
        {
            using (PrincipalContext pc = new PrincipalContext(ContextType.Domain, "ldaps://ldaps.rescue.org:636/", "ou=All Users,dc=theirc,dc=org"))
            {
                // validate the credentials
                bool b = pc.ValidateCredentials("otis", "P@ssw0rd");
                return Content("TRUE!");
            }

        }

        public ActionResult ServerScript()
        {
            StringBuilder content = new StringBuilder();
            var isAuthenticated = User.Identity.IsAuthenticated;

            var appPath = Request.ApplicationPath;
            if (appPath == "/")
                appPath = "";

            if (Request.Headers.AllKeys.Contains("X-Proxy-Path"))
            {
                appPath = Request.Headers["X-Proxy-Path"];
            }

            if (appPath.EndsWith("/"))
            {
                appPath = appPath.Substring(0, appPath.Length - 1);
            }

            ViewBag.AppPath = appPath;

            var talonContextProvider = new EFContextProvider<Models.Vouchers.Context>();
            var adminContextProvider = new EFContextProvider<Models.Admin.AdminContext>();

            ViewBag.MainMetadata = talonContextProvider.Metadata();
            ViewBag.AdminMetadata = adminContextProvider.Metadata();


            return View();
        }

        [Authorize]
        public ActionResult NavigationItems()
        {
            var userId = User.Identity.GetUserId();
            using (var ctx = new Models.Admin.AdminContext())
            {
                var user = ctx.Users.Where(u => u.Id == userId).First();
                var roles = user.Roles.Select(r => r.RoleId).ToArray();
                var categories = ctx.MenuCategories.ToArray().Where(c =>!c.Roles.Any() || c.Roles.Select(r => r.RoleId).Intersect(roles).Any())
                    .OrderBy(c => c.SortOrder).ToArray();

                var jsonSerializer  = new JsonSerializer {
                    ReferenceLoopHandling = ReferenceLoopHandling.Ignore
                };

                return Content(JToken.FromObject(categories, jsonSerializer).ToString(), "application/json");
            }
        }

        public ActionResult MetadataJS() {
            var talonContextProvider = new EFContextProvider<Models.Vouchers.Context>();
            var adminContextProvider = new EFContextProvider<Models.Admin.AdminContext>();

            var buffer = new StringBuilder();
            buffer.AppendFormat("window.ContextMetadata = JSON.stringify({0});\n", talonContextProvider.Metadata());
            buffer.AppendFormat("window.AdminMetadata = JSON.stringify({0});\n", adminContextProvider.Metadata());

            return Content(buffer.ToString(), "application/javascript");
        }

        public ActionResult OfflineManifest()
        {

            //throw new HttpException(404, "Some description");

            var files = Directory.EnumerateFiles(Server.MapPath("~/App"), "*.*", SearchOption.AllDirectories)
                                .Union(Directory.EnumerateFiles(Server.MapPath("~/Scripts"), "*.*", SearchOption.AllDirectories))
                                .Union(Directory.EnumerateFiles(Server.MapPath("~/Content"), "*.*", SearchOption.AllDirectories))
                                .Select(f => f.Replace(Server.MapPath("~"), ""))
                                .Select(f => f.Replace(Path.DirectorySeparatorChar, '/'))
                                .Select(f => (Request.ApplicationPath + f).Replace("//", "/"))
                                .ToArray();
            ViewBag.Files = files;


            return View();
        }
    }
}
