using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TalonAdmin.Extensions;

namespace TalonAdmin.Controllers
{
    public class HomeController : Controller
    {
        private readonly Models.Admin.AdminContext _adminContext;

        public HomeController(Models.Admin.AdminContext adminContext)
        {
            _adminContext = adminContext;
        }

        // The client is a static SPA; "npm ci && npm run build" in ClientApp/ publishes it to
        // wwwroot/app.
        public IActionResult Index()
        {
            return Redirect("~/app/");
        }

        public IActionResult Error()
        {
            return View();
        }

        [Authorize]
        public async Task<IActionResult> NavigationItems()
        {
            var userId = this.GetUserId();

            // Core Identity exposes no ApplicationUser.Roles shortcut on a bare query, so role
            // membership is read from the join table.
            var roles = await _adminContext.UserRoles
                .Where(ur => ur.UserId == userId)
                .Select(ur => ur.RoleId)
                .ToArrayAsync();

            // The sidebar renders three levels: category -> item -> child link. Children have to be
            // loaded explicitly now that lazy loading is gone, or every menu group renders empty.
            var categories = (await _adminContext.MenuCategories
                    .AsNoTracking()
                    .Include(c => c.Roles)
                    .Include(c => c.Items.OrderBy(i => i.Id))
                    .ThenInclude(i => i.Children.OrderBy(child => child.Id))
                    .ToArrayAsync())
                .Where(c => !c.Roles.Any() || c.Roles.Select(r => r.RoleId).Intersect(roles).Any())
                .OrderBy(c => c.SortOrder)
                .ToArray();

            var jsonSerializer = new JsonSerializer
            {
                ReferenceLoopHandling = ReferenceLoopHandling.Ignore
            };

            return Content(JToken.FromObject(categories, jsonSerializer).ToString(), "application/json");
        }
    }
}
