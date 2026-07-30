using Microsoft.AspNetCore.Mvc;

namespace TalonAdmin.Controllers
{
    public class ReportController : Controller
    {
        // NOTE: Views/Report/VoucherReceiptReport.cshtml does not exist in the repository, so this
        // action already returned a view-not-found error before the migration. Kept as-is.
        public IActionResult VoucherReceiptReport()
        {
            return View();
        }
    }
}
