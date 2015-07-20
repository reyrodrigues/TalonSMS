using Microsoft.AspNet.Identity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Http;
using TalonAdmin.Models;
using TalonAdmin.Models.Admin;
using Microsoft.Owin.Security;
using Microsoft.Owin.Security.Cookies;
using Microsoft.Owin.Security.OAuth;
using Microsoft.AspNet.Identity;
using Microsoft.AspNet.Identity.EntityFramework;
using Microsoft.AspNet.Identity.Owin;
using TalonAdmin.Models.BindingModels;

namespace TalonAdmin.Controllers.Api
{
    public class ApplicationUserController : ApiController
    {
        public async Task<IHttpActionResult> Post(ApplicationUserBindingModel model)
        {
            var UserManager = Request.GetOwinContext().GetUserManager<ApplicationUserManager>();
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

            return Json<ApplicationUser>(user, new Newtonsoft.Json.JsonSerializerSettings { ReferenceLoopHandling = Newtonsoft.Json.ReferenceLoopHandling.Ignore });
        }

        public async Task<IHttpActionResult> Put([FromUri]string id, [FromBody] ApplicationUserBindingModel model)
        {
            IdentityResult result;

            var UserManager = Request.GetOwinContext().GetUserManager<ApplicationUserManager>();
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var user = await UserManager.FindByIdAsync(id);
            user.FullName = model.FullName;
            user.UserName = model.UserName;
            user.Email = model.Email;
            user.OrganizationId = model.OrganizationId;

            result = await UserManager.UpdateAsync(user);

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


            return Json<ApplicationUser>(user, new Newtonsoft.Json.JsonSerializerSettings {  ReferenceLoopHandling  = Newtonsoft.Json.ReferenceLoopHandling.Ignore });
        }

        public async Task<IHttpActionResult> Delete([FromUri]string id)
        {
            var UserManager = Request.GetOwinContext().GetUserManager<ApplicationUserManager>();

            var user = await UserManager.FindByIdAsync(id);
            var result = await UserManager.DeleteAsync(user);

            if (!result.Succeeded)
            {
                return GetErrorResult(result);
            }

            return Ok();
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

    }
}
