using System.Linq;
using System.Threading.Tasks;
using Breeze.AspNetCore;
using Breeze.Persistence;
using Breeze.Persistence.EFCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json.Linq;
using TalonAdmin.Attributes;
using TalonAdmin.Extensions;

namespace TalonAdmin.Controllers.Breeze
{
    [BreezeQueryFilter(MaxDepth = 5)]
    [Route("breeze/Admin/[action]")]
    public class AdminController : Controller
    {
        private readonly EFPersistenceManager<Models.Admin.AdminContext> _persistenceManager;

        public AdminController(Models.Admin.AdminContext context)
        {
            _persistenceManager = new EFPersistenceManager<Models.Admin.AdminContext>(context);
        }

        private Models.Admin.AdminContext Db
        {
            get { return _persistenceManager.Context; }
        }

        /// <summary>
        /// Core Identity has no IdentityRole.Users navigation and does not eagerly load one, so
        /// membership is resolved by querying the join table directly.
        /// </summary>
        private async Task<string[]> UserIdsInRolesAsync(params string[] roleNames)
        {
            var roleIds = await Db.Roles
                .Where(r => roleNames.Contains(r.Name))
                .Select(r => r.Id)
                .ToArrayAsync();

            return await Db.UserRoles
                .Where(ur => roleIds.Contains(ur.RoleId))
                .Select(ur => ur.UserId)
                .Distinct()
                .ToArrayAsync();
        }

        [HttpGet]
        public string Metadata()
        {
            return _persistenceManager.Metadata();
        }

        [HttpGet]
        [Authorize(Roles = "System Administrator")]
        public async Task<IQueryable<Models.Admin.ApplicationUser>> Administrators()
        {
            var users = await UserIdsInRolesAsync("System Administrator", "Organization Administrator");

            return Db.Users.Where(u => users.Contains(u.Id));
        }

        [HttpGet]
        [Authorize(Roles = "Organization Administrator,System Administrator"), AuthorizeTenant]
        public async Task<IQueryable<Models.Admin.ApplicationUser>> OrganizationUsers()
        {
            var users = await UserIdsInRolesAsync("System Administrator");

            return Db.Users
                .FilterOrganization(this)
                .Where(u => !users.Contains(u.Id));
        }

        [HttpGet]
        [Authorize(Roles = "Country Administrator, Organization Administrator,System Administrator"), AuthorizeTenant]
        public async Task<IQueryable<Models.Admin.ApplicationUser>> CountryUsers()
        {
            var users = await UserIdsInRolesAsync("System Administrator", "Organization Administrator");
            var tenantCountry = this.GetCountryId();

            return Db.Users
                .FilterOrganization(this)
                .Where(u => u.Countries.Any(c => c.CountryId == tenantCountry))
                .Where(u => !users.Contains(u.Id));
        }

        [HttpGet]
        [Authorize(Roles = "Organization Administrator,System Administrator")]
        public IQueryable<Models.Admin.OrganizationCountry> OrganizationCountries()
        {
            return Db.OrganizationCountries
                .Include(o => o.Country)
                .Include(o => o.Settings)
                .FilterOrganization(this);
        }

        [HttpGet]
        [Authorize(Roles = "Organization Administrator,System Administrator")]
        public IQueryable<Models.Admin.OrganizationCountry> UnfilteredOrganizationCountries()
        {
            return Db.OrganizationCountries
                .Include(o => o.Country)
                .Include(o => o.Settings);
        }

        [HttpGet]
        public IQueryable<Models.Admin.Organization> Organizations()
        {
            return Db.Organizations;
        }

        [HttpGet]
        public IQueryable<Models.Admin.Country> Countries()
        {
            return Db.Countries;
        }

        [HttpGet]
        public IQueryable<Models.Admin.Action> Actions()
        {
            return Db.Actions;
        }

        [HttpGet]
        public IQueryable<Models.Admin.ActionRole> ActionRoles()
        {
            return Db.ActionRoles;
        }

        [HttpGet]
        public IQueryable<Models.Admin.MenuCategory> MenuCategories()
        {
            return Db.MenuCategories;
        }

        [HttpGet]
        public IQueryable<Models.Admin.MenuCategoryRole> MenuCategoryRoles()
        {
            return Db.MenuCategoryRoles;
        }

        [HttpGet]
        public IQueryable<Models.Admin.MenuItem> MenuItems()
        {
            return Db.MenuItems;
        }

        [HttpGet]
        public IQueryable<Models.Admin.ApplicationUser> Users()
        {
            return Db.Users;
        }

        [HttpGet]
        public IQueryable<IdentityRole> Roles()
        {
            return Db.Roles;
        }

        [HttpGet]
        public IQueryable<IdentityUserRole<string>> UserRoles()
        {
            return Db.UserRoles;
        }

        [HttpGet]
        public IQueryable<Models.Admin.ApplicationUserCountry> ApplicationUserCountries()
        {
            return Db.ApplicationUserCountries;
        }

        [HttpPost]
        public SaveResult SaveChanges([FromBody] JObject saveBundle)
        {
            return _persistenceManager.SaveChanges(saveBundle);
        }
    }
}
