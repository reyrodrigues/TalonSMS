using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
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
