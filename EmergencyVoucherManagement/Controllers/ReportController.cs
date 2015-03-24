using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace TalonAdmin.Controllers
{
    public class ReportController : Controller
    {
        public ActionResult VoucherReceiptReport()
        {
            return View();
        }
    }
}