using System;
using System.Linq;
using System.Collections.Generic;
using System.Net.Http;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Threading.Tasks;
using System.Web;
using System.Web.Http;
using System.Web.Http.ModelBinding;
using Microsoft.AspNet.Identity;
using Microsoft.AspNet.Identity.EntityFramework;
using Microsoft.AspNet.Identity.Owin;
using Microsoft.Owin.Security;
using Microsoft.Owin.Security.Cookies;
using Microsoft.Owin.Security.OAuth;
using TalonAdmin.Models.Vouchers;
using TalonAdmin.Providers;
using TalonAdmin.Models;
using TalonAdmin.Models.Admin;
using System.Data.Entity;
using TalonAdmin.Models.BindingModels;

namespace TalonAdmin.Controllers
{
    [Authorize]
    [RoutePrefix("api/Account")]
    public class AccountController : ApiController
    {
        public ApplicationUserManager UserManager
        {
            get { return Request.GetOwinContext().GetUserManager<ApplicationUserManager>();  }
        }

        public ApplicationRoleManager RoleManager
        {
            get { return Request.GetOwinContext().GetUserManager<ApplicationRoleManager>(); }
        }

        public AccountController()
        {
        }

        [HostAuthentication(DefaultAuthenticationTypes.ExternalBearer)]
        [Route("Me"), HttpGet]
        public async Task<IHttpActionResult> Me()
        {
            using (var admin = new Models.Admin.AdminContext())
            {
                admin.Configuration.ProxyCreationEnabled = false;
                admin.Configuration.LazyLoadingEnabled = false;

                string userId = User.Identity.GetUserId();
                if (admin.Users.Where(u => u.Id == userId).Any())
                {
                    var user = await admin.Users
                        .Include("Countries")
                        .Include("Countries.Country")
                        .Include("Organization")
                        .Where(u => u.Id == userId)
                        .ToListAsync();

                    return Json<ApplicationUser>(user.First());
                }

                return BadRequest();
            }
        }

        [Route("Logout"), AllowAnonymous]
        public IHttpActionResult Logout()
        {
            Authentication.SignOut(CookieAuthenticationDefaults.AuthenticationType);
            return Ok();
        }

        [Route("ChangePassword")]
        public async Task<IHttpActionResult> ChangePassword(ChangePasswordBindingModel model)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            IdentityResult result = await UserManager.ChangePasswordAsync(User.Identity.GetUserId(), model.OldPassword,
                model.NewPassword);

            if (!result.Succeeded)
            {
                return GetErrorResult(result);
            }

            return Ok();
        }

        [Route("UpdateProfile")]
        public async Task<IHttpActionResult> UpdateProfile(UpdateProfileBindingModel model)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }
            string userId = User.Identity.GetUserId();

            var user = await UserManager.FindByIdAsync(userId);

            user.FullName = model.FullName;
            user.Email = model.Email;

            var result = await UserManager.UpdateAsync(user);

            if (!result.Succeeded)
            {
                return GetErrorResult(result);
            }

            return Ok();
        }

        [Route("SetPassword")]
        public async Task<IHttpActionResult> SetPassword(SetPasswordBindingModel model)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            IdentityResult result = await UserManager.AddPasswordAsync(User.Identity.GetUserId(), model.NewPassword);

            if (!result.Succeeded)
            {
                return GetErrorResult(result);
            }

            return Ok();
        }

        [Route("Register")]
        public async Task<IHttpActionResult> Register(RegisterBindingModel model)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var user = new ApplicationUser()
            {
                FullName = model.FullName,
                UserName = model.UserName,
                Email = model.Email,
                OrganizationId = model.OrganizationId
            };

            IdentityResult result = await UserManager.CreateAsync(user, model.Password);

            if (result.Succeeded)
            {
                using (var ctx = new AdminContext())
                {
                    var countries = model.Countries.Select(c => new ApplicationUserCountry
                    {
                        CountryId = c,
                        ApplicationUserId = user.Id
                    }).ToArray();

                    ctx.ApplicationUserCountries.AddRange(countries);
                    await ctx.SaveChangesAsync();
                }
            }
            if (!result.Succeeded)
            {
                return GetErrorResult(result);
            }

            return Ok();
        }


        [Route("RegisterSystemAdministrator")]
        [Authorize(Roles = "System Administrator")]
        public async Task<IHttpActionResult> RegisterSystemAdministrator(RegisterBindingModel model)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var user = new ApplicationUser()
            {
                FullName = model.FullName,
                UserName = model.UserName,
                Email = model.Email,
                OrganizationId = model.OrganizationId
            };

            IdentityResult result = await UserManager.CreateAsync(user, model.Password);
            var roleResult = await UserManager.AddToRoleAsync(user.Id, "System Administrator");

            if (result.Succeeded)
            {
                using (var ctx = new AdminContext())
                {
                    var countries = model.Countries.Select(c => new ApplicationUserCountry
                    {
                        CountryId = c,
                        ApplicationUserId = user.Id
                    }).ToArray();

                    ctx.ApplicationUserCountries.AddRange(countries);
                    await ctx.SaveChangesAsync();
                }
            }
            if (!result.Succeeded)
            {
                return GetErrorResult(result);
            }

            return Ok();
        }


        [Route("AddUserToRole")]
        public async Task<dynamic> AddUserToRole(Models.BindingModels.AddUserToRoleBindingModel request)
        {

            var user = await UserManager.FindByEmailAsync(request.Email);
            IdentityResult result = await UserManager.AddToRoleAsync(user.Id, request.RoleName);

            if (!result.Succeeded)
            {
                return GetErrorResult(result);
            }

            return Ok();
        }


        [Route("RemoveUserFromRole")]
        public async Task<dynamic> RemoveUserFromRole(Models.BindingModels.RemoveUserFromRoleBindingModel request)
        {

            var user = await UserManager.FindByEmailAsync(request.Email);
            IdentityResult result = await UserManager.RemoveFromRoleAsync(user.Id, request.RoleName);

            if (!result.Succeeded)
            {
                return GetErrorResult(result);
            }

            return Ok();
        }

        #region Helpers

        private IAuthenticationManager Authentication
        {
            get { return Request.GetOwinContext().Authentication; }
        }

        private IHttpActionResult GetErrorResult(IdentityResult result)
        {
            if (result == null)
            {
                return InternalServerError();
            }

            if (!result.Succeeded)
            {
                if (result.Errors != null)
                {
                    foreach (string error in result.Errors)
                    {
                        ModelState.AddModelError("", error);
                    }
                }

                if (ModelState.IsValid)
                {
                    // No ModelState errors are available to send, so just return an empty BadRequest.
                    return BadRequest();
                }

                return BadRequest(ModelState);
            }

            return null;
        }

        #endregion
    }
}
