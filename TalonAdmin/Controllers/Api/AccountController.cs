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
using TalonAdmin.Extensions;
using System.Data.Entity;
using TalonAdmin.Models.BindingModels;
using Newtonsoft.Json.Linq;

namespace TalonAdmin.Controllers.Api
{
    [Authorize]
    [RoutePrefix("api/Account")]
    public class AccountController : ApiController
    {
        public ApplicationUserManager UserManager
        {
            get { return Request.GetOwinContext().GetUserManager<ApplicationUserManager>(); }
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
                    var user = (await admin.Users
                        .Include("Countries")
                        .Include("Countries.Country")
                        .Include("Organization")
                        .Where(u => u.Id == userId)
                        .ToListAsync()).First();

                    var jsonUser = JObject.FromObject(user);
                    var role = await RoleManager.FindByNameAsync("System Administrator");
                    jsonUser["IsSystemAdministrator"] = role.Users.Select(r => r.UserId).Contains(user.Id);

                    return Json<JObject>(jsonUser);
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
            var user = await UserManager.FindByNameAsync(model.UserName);

            user.FullName = model.FullName;
            user.Email = model.Email;
            user.UserName = model.UserName;
            user.OrganizationId = model.OrganizationId;

            IdentityResult result = await UserManager.UpdateAsync(user);

            if (!result.Succeeded)
            {
                return GetErrorResult(result);
            }

            if (!String.IsNullOrEmpty(model.Password))
            {
                result = await UserManager.RemovePasswordAsync(user.Id);

                if (!result.Succeeded)
                {
                    return GetErrorResult(result);
                }

                result = await UserManager.AddPasswordAsync(user.Id, model.Password);

                if (!result.Succeeded)
                {
                    return GetErrorResult(result);
                }
            }


            return Ok<ApplicationUser>(user);
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

        [Route("RegisterOrAssignCountry")]
        [Authorize(Roles = "Country Administrator")]
        public async Task<IHttpActionResult> RegisterOrAssignCountry(RegisterBindingModel model)
        {
            var user = await UserManager.FindByNameAsync(model.UserName);

            if (user == null)
            {
                return await Register(model);
            }
            else
            {
                var countries = user.Countries.Select(c => c.CountryId).ToList();
                countries.Add(this.GetCountryId());

                await AssignCountries(user, countries.ToArray());
            }

            return Ok();
        }

        [Route("Register")]
        [Authorize(Roles = "System Administrator, Country Administrator, Organization Administrator")]
        public async Task<IHttpActionResult> Register(RegisterBindingModel model)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var user = new ApplicationUser()
            {
                Id = Guid.NewGuid().ToString(),
                FullName = model.FullName,
                UserName = model.UserName,
                Email = model.Email,
                OrganizationId = model.OrganizationId
            };

            IdentityResult result = await UserManager.CreateAsync(user, model.Password);
            if (!result.Succeeded)
            {
                return GetErrorResult(result);
            }

            return Ok<ApplicationUser>(user);
        }

        [Route("RegisterAdministrator")]
        [Authorize(Roles = "System Administrator")]
        public async Task<IHttpActionResult> RegisterAdministrator(RegisterBindingModel model)
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

            if (!result.Succeeded)
            {
                return GetErrorResult(result);
            }

            result = await UserManager.AddToRoleAsync(user.Id, "System Administrator");

            if (!result.Succeeded)
            {
                return GetErrorResult(result);
            }

            return Ok();
        }

        [Route("AddUserToRole")]
        public async Task<dynamic> AddUserToRole(Models.BindingModels.RoleSubscriptionBindingModel request)
        {
            var user = await UserManager.FindByIdAsync(request.UserId);
            var role = await RoleManager.FindByIdAsync(request.RoleId);
            IdentityResult result = await UserManager.AddToRoleAsync(user.Id, role.Name);

            if (!result.Succeeded)
            {
                return GetErrorResult(result);
            }

            return Ok();
        }

        [Route("RemoveUserFromRole")]
        public async Task<dynamic> RemoveUserFromRole(Models.BindingModels.RoleSubscriptionBindingModel request)
        {
            var user = await UserManager.FindByIdAsync(request.UserId);
            var role = await RoleManager.FindByIdAsync(request.RoleId);
            IdentityResult result = await UserManager.RemoveFromRoleAsync(user.Id, role.Name);

            if (!result.Succeeded)
            {
                return GetErrorResult(result);
            }

            return Ok();
        }

        #region Helpers


        private static async Task AssignCountries(ApplicationUser user, int[] countries)
        {
            using (var adminContext = new Models.Admin.AdminContext())
            {
                var currentCountries = await adminContext.ApplicationUserCountries.Where(a => a.ApplicationUserId == user.Id).ToListAsync();

                var toDelete = currentCountries.Where(c => !countries.Contains(c.CountryId));

                currentCountries = currentCountries.Where(c => countries.Contains(c.CountryId)).ToList();

                adminContext.ApplicationUserCountries.RemoveRange(toDelete.ToArray());

                var toCreate = countries.Where(i => !currentCountries.Select(c => c.CountryId).Contains(i)).Select(i => new ApplicationUserCountry { CountryId = i, ApplicationUserId = user.Id }).ToArray();

                adminContext.ApplicationUserCountries.AddRange(toCreate);

                await adminContext.SaveChangesAsync();
            }
        }

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
