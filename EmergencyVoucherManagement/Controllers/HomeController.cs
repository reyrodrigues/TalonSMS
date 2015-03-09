using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Drawing.Printing;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using TuesPechkin;

namespace EmergencyVoucherManagement.Controllers
{
    public class HomeController : Controller
    {
        public ActionResult Index()
        {
            return View(); // Redirect("~/App/");
        }

        public ActionResult TestReport()
        {
            var loopbackURL = ConfigurationManager.AppSettings["Loopback.URL"];

            var content = new StringBuilder();
            content.Append("<html>");
            content.Append("<head>");
            content.Append("<style>");
            content.Append("body { color: Red; }");
            content.Append("</style>");
            content.Append("</head>");
            content.Append("<body>");
            content.AppendFormat("<img src=\"{0}Content/IRCLogo.svg\"/><h1>IS this red?</h1>{0}Content/IRCLogo.svg", loopbackURL);
            content.Append("</body>");
            content.Append("</html>");

            var document = new HtmlToPdfDocument
            {
                GlobalSettings =
                {
                    ProduceOutline = true,
                    DocumentTitle = "Pretty Websites",
                    PaperSize = PaperKind.A4, // Implicit conversion to PechkinPaperSize
                    Margins =
                    {
                        All = 1.375,
                        Unit = Unit.Centimeters
                    }
                },
                Objects = {
                    new ObjectSettings { HtmlText = content.ToString() },
                }
            };

            return File(WebApiApplication.converter.Convert(document), "application/pdf");
        }

        public ActionResult ServerScript()
        {
            StringBuilder content = new StringBuilder();

            content.AppendFormat("window.BaseUrl = '{0}/'\n", Request.ApplicationPath);


            return Content(content.ToString());
        }


        public ActionResult OfflineManifest()
        {

            throw new HttpException(404, "Some description");
            /*
            var files = Directory.EnumerateFiles(Server.MapPath("~/App"), "*.*", SearchOption.AllDirectories)
                                .Select(f=> f.Replace(Server.MapPath("~"), ""))
                                .Union(Directory.EnumerateFiles(Server.MapPath("~/Scripts"), "*.*", SearchOption.AllDirectories)
                                .Select(f => f.Replace(Server.MapPath("~"), "")))
                                .Select(f=> f.Replace(Path.DirectorySeparatorChar, '/'))
                                .Select(f=> (Request.ApplicationPath + f).Replace("//", "/"))
                                .ToArray();
            ViewBag.Files = files;


            return View();*/
        }
    }
}
