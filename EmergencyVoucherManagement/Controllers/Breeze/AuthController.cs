using Breeze.ContextProvider;
using Breeze.ContextProvider.EF6;
using Breeze.WebApi2;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Http;

namespace EmergencyVoucherManagement.Controllers.Breeze
{
    [BreezeController]
    public class AuthController : ApiController
    {

        readonly EFContextProvider<Models.ApplicationDbContext> _contextProvider =
            new EFContextProvider<Models.ApplicationDbContext>();

        [HttpGet]
        public string Metadata()
        {
            return _contextProvider.Metadata();
        }


        [HttpGet]
        public IQueryable<EmergencyVoucherManagement.Models.ApplicationUser> Users()
        {
            return _contextProvider.Context.Users;
        }

        [HttpGet]
        public IQueryable<Microsoft.AspNet.Identity.EntityFramework.IdentityRole> Roles()
        {
            return _contextProvider.Context.Roles;
        }


        [HttpPost]
        public SaveResult SaveChanges(JObject saveBundle)
        {
            // Not saving anything
            return new SaveResult();
        }

    }
}