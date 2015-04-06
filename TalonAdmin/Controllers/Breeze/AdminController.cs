using Breeze.ContextProvider;
using Breeze.ContextProvider.EF6;
using Breeze.WebApi2;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Http;
using Microsoft.AspNet.Identity.Owin;
using Microsoft.AspNet.Identity;
using System.Threading.Tasks;

using TalonAdmin.Extensions;
using TalonAdmin.Attributes;
namespace TalonAdmin.Controllers.Breeze
{
    [BreezeController, EnableBreezeQuery(MaxExpansionDepth = 5)]
    public class AdminController : ApiController
    {
        readonly EFContextProvider<Models.Admin.AdminContext> _contextProvider =
            new EFContextProvider<Models.Admin.AdminContext>();

        public ApplicationRoleManager RoleManager { get { return HttpContext.Current.GetOwinContext().GetUserManager<ApplicationRoleManager>(); } }


        [HttpGet]
        public string Metadata()
        {
            return _contextProvider.Metadata();
        }


        [HttpGet]
        public IQueryable<TalonAdmin.Models.Admin.ApplicationUser> Users()
        {
            return _contextProvider.Context.Users;
        }

        [HttpGet]
        [Authorize(Roles = "System Administrator")]
        public async Task<IQueryable<TalonAdmin.Models.Admin.ApplicationUser>> Administrators()
        {
            var systemAdmins = await RoleManager.FindByNameAsync("System Administrator");
            var orgAdmins = await RoleManager.FindByNameAsync("Organization Administrator");

            var users = systemAdmins.Users.Select(r => r.UserId).Union(orgAdmins.Users.Select(u => u.UserId)).Distinct().ToArray();

            return _contextProvider.Context.Users.Where(u => users.Contains(u.Id));
        }

        [HttpGet]
        [Authorize(Roles = "Organization Administrator,System Administrator"), AuthorizeTenant]
        public async Task<IQueryable<TalonAdmin.Models.Admin.ApplicationUser>> OrganizationUsers()
        {
            var role = await RoleManager.FindByNameAsync("System Administrator");
            var users = role.Users.Select(r => r.UserId).ToArray();

            return _contextProvider.Context.Users
                .FilterOrganization(this)
                .Where(u => !users.Contains(u.Id));
        }

        [HttpGet]
        [Authorize(Roles = "Country Administrator, Organization Administrator,System Administrator"), AuthorizeTenant]
        public async Task<IQueryable<TalonAdmin.Models.Admin.ApplicationUser>> CountryUsers()
        {

            var sysAdmin = await RoleManager.FindByNameAsync("System Administrator");
            var orgAdmin = await RoleManager.FindByNameAsync("Organization Administrator");
            var users = sysAdmin.Users.Select(r => r.UserId).Union(orgAdmin.Users.Select(u=> u.UserId)).Distinct().ToArray();

            var headers = Request.Headers.AsEnumerable().ToDictionary(k => k.Key, v => v.Value);
            int tenantCountry = Convert.ToInt32(headers.ContainsKey("X-Tenant-Country") ? headers["X-Tenant-Country"].First() : "0");
            var userQuery = _contextProvider.Context.Users.FilterOrganization(this).Select(u=>u.Id);

            return _contextProvider.Context.Users
                .FilterOrganization(this)
                .Where(u => u.Countries.Select(c=>c.CountryId == tenantCountry).Any())
                .Where(u => !users.Contains(u.Id));
        }

        [HttpGet]
        [Authorize(Roles = "Organization Administrator,System Administrator"), AuthorizeTenant]
        public async Task<IQueryable<TalonAdmin.Models.Admin.OrganizationCountry>> OrganizationCountries()
        {
            return _contextProvider.Context.OrganizationCountries
                .Include("Country")
                .Include("Settings")
                .FilterOrganization(this);
        }

        [HttpGet]
        public IQueryable<Microsoft.AspNet.Identity.EntityFramework.IdentityRole> Roles()
        {
            return _contextProvider.Context.Roles;
        }

        [HttpGet]
        public IQueryable<TalonAdmin.Models.Admin.Organization> Organizations()
        {
            return _contextProvider.Context.Organizations;
        }

        [HttpGet]
        public IQueryable<TalonAdmin.Models.Admin.Country> Countries()
        {
            return _contextProvider.Context.Countries;
        }

        [HttpPost]
        public SaveResult SaveChanges(JObject saveBundle)
        {
            return _contextProvider.SaveChanges(saveBundle);
        }
    }
}