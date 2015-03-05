using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;

namespace EmergencyVoucherManagement.Controllers
{
    public class HomeController : Controller
    {
        public ActionResult Index()
        {
            return Redirect("~/App/");
        }

        public async Task<ActionResult> TestRSMS()
        {
            dynamic parameter = new JObject();
            parameter.Name = "Rey Rodrigues 2";
            parameter.MobileNumber = "+380990266649";
            parameter.Message = "Test";
            parameter.Groups = "Beneficiary";

            return Content((await Utils.RescueSMSClient.CreateContactAndSendMessageAsync(parameter)).ToString());
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
