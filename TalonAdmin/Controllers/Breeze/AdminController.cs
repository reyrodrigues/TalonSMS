using Breeze.ContextProvider;
using Breeze.ContextProvider.EF6;
using Breeze.WebApi2;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Http;

namespace TalonAdmin.Controllers.Breeze
{
    [BreezeController]
    public class AdminController : ApiController
    {
        readonly EFContextProvider<Models.Admin.AdminContext> _contextProvider =
            new EFContextProvider<Models.Admin.AdminContext>();

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