using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Breeze.Persistence.EFCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TalonAdmin.Extensions;

namespace TalonAdmin.Controllers
{
    public class HomeController : Controller
    {
        private readonly Models.Vouchers.Context _voucherContext;
        private readonly Models.Admin.AdminContext _adminContext;
        private readonly IWebHostEnvironment _environment;

        public HomeController(
            Models.Vouchers.Context voucherContext,
            Models.Admin.AdminContext adminContext,
            IWebHostEnvironment environment)
        {
            _voucherContext = voucherContext;
            _adminContext = adminContext;
            _environment = environment;
        }

        // The AngularJS client is a static SPA; "npm install && npm run vendor && npm run build"
        // in Angular/ publishes it to wwwroot/app.
        public IActionResult Index()
        {
            return Redirect("~/app/");
        }

        public IActionResult Error()
        {
            return View();
        }

        public IActionResult ServerScript()
        {
            var appPath = Request.PathBase.HasValue ? Request.PathBase.Value : "";

            if (Request.Headers.ContainsKey("X-Proxy-Path"))
            {
                appPath = Request.Headers["X-Proxy-Path"];
            }

            if (appPath.EndsWith("/"))
            {
                appPath = appPath.Substring(0, appPath.Length - 1);
            }

            ViewBag.AppPath = appPath;

            ViewBag.MainMetadata = new EFPersistenceManager<Models.Vouchers.Context>(_voucherContext).Metadata();
            ViewBag.AdminMetadata = new EFPersistenceManager<Models.Admin.AdminContext>(_adminContext).Metadata();

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

            var categories = (await _adminContext.MenuCategories
                    .AsNoTracking()
                    .Include(c => c.Roles)
                    .Include(c => c.Items)
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

        public IActionResult MetadataJS()
        {
            var buffer = new StringBuilder();
            buffer.AppendFormat("window.ContextMetadata = JSON.stringify({0});\n",
                new EFPersistenceManager<Models.Vouchers.Context>(_voucherContext).Metadata());
            buffer.AppendFormat("window.AdminMetadata = JSON.stringify({0});\n",
                new EFPersistenceManager<Models.Admin.AdminContext>(_adminContext).Metadata());

            return Content(buffer.ToString(), "application/javascript");
        }

        public IActionResult OfflineManifest()
        {
            // Static assets now live under wwwroot, which is what the browser actually fetches.
            var root = _environment.WebRootPath;

            // NOTE: guarded because App/ only exists once the frontend has been built; the EF6
            // version threw a DirectoryNotFoundException on a fresh checkout.
            var files = new[] { "App", "Scripts", "Content" }
                .Select(d => Path.Combine(root, d))
                .Where(Directory.Exists)
                .SelectMany(d => Directory.EnumerateFiles(d, "*.*", SearchOption.AllDirectories))
                .Select(f => f.Replace(root, ""))
                .Select(f => f.Replace(Path.DirectorySeparatorChar, '/'))
                .Select(f => (Request.PathBase + f).Replace("//", "/"))
                .ToArray();

            ViewBag.Files = files;

            return View();
        }
    }
}
