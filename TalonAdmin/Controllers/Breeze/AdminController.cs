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

namespace TalonAdmin.Controllers.Breeze
{
    [BreezeController]
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
        public async Task<IQueryable<TalonAdmin.Models.Admin.ApplicationUser>> SystemAdministrators()
        {
            var role = await RoleManager.FindByNameAsync("System Administrator");
            var users = role.Users.Select(r => r.UserId).ToArray();

            return _contextProvider.Context.Users.Where(u => users.Contains(u.Id));
        }

        [HttpGet]
        [Authorize(Roles = "Organization Administrator,System Administrator")]
        public async Task<IQueryable<TalonAdmin.Models.Admin.ApplicationUser>> OrganizationAdministrators()
        {
            var role = await RoleManager.FindByNameAsync("Organization Administrator");
            var users = role.Users.Select(r => r.UserId).ToArray();

            return _contextProvider.Context.Users.Where(u => users.Contains(u.Id));
        }

        [HttpGet]
        [Authorize(Roles = "Country Administrator,Organization Administrator,System Administrator")]
        public async Task<IQueryable<TalonAdmin.Models.Admin.ApplicationUser>> CountryAdministrators()
        {
            var role = await RoleManager.FindByNameAsync("Country Administrator");
            var users = role.Users.Select(r => r.UserId).ToArray();

            return _contextProvider.Context.Users.Where(u => users.Contains(u.Id));
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