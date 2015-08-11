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
using Microsoft.AspNet.Identity.EntityFramework;
using Microsoft.AspNet.Identity.Owin;
using TalonAdmin.Models.BindingModels;


namespace TalonAdmin.Controllers.Api
{
    public class ApplicationRoleController : ApiController
    {
        public async Task<IHttpActionResult> Post(ApplicationRoleBindingModel model)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var RoleManager = Request.GetOwinContext().GetUserManager<ApplicationRoleManager>();

            var role = new IdentityRole()
            {
                Name = model.Name
            };

            var result = await RoleManager.CreateAsync(role);

            if (!result.Succeeded)
            {
                return GetErrorResult(result);
            }

            return Json<IdentityRole>(role, new Newtonsoft.Json.JsonSerializerSettings { ReferenceLoopHandling = Newtonsoft.Json.ReferenceLoopHandling.Ignore });

        }

        public async Task<IHttpActionResult> Put([FromUri]string id, [FromBody] ApplicationRoleBindingModel model)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var RoleManager = Request.GetOwinContext().GetUserManager<ApplicationRoleManager>();

            var role = await RoleManager.FindByIdAsync(id);

            role.Name = model.Name;

            var result = await RoleManager.UpdateAsync(role);

            if (!result.Succeeded)
            {
                return GetErrorResult(result);
            }

            return Json<IdentityRole>(role, new Newtonsoft.Json.JsonSerializerSettings { ReferenceLoopHandling = Newtonsoft.Json.ReferenceLoopHandling.Ignore });
        }

        public async Task<IHttpActionResult> Delete([FromUri]string id)
        {
            var RoleManager = Request.GetOwinContext().GetUserManager<ApplicationRoleManager>();

            var role = await RoleManager.FindByIdAsync(id);
            var result = await RoleManager.DeleteAsync(role);

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