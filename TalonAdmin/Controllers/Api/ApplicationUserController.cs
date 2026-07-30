using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json.Linq;
using TalonAdmin.Extensions;
using TalonAdmin.Models.Admin;
using TalonAdmin.Models.BindingModels;

namespace TalonAdmin.Controllers.Api
{
    [Route("api/ApplicationUser")]
    public class ApplicationUserController : ControllerBase
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly AdminContext _admin;

        public ApplicationUserController(
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager,
            AdminContext admin)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _admin = admin;
        }

        [HttpPost]
        public async Task<IActionResult> Post([FromBody] ApplicationUserBindingModel model)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var user = new ApplicationUser
            {
                Id = Guid.NewGuid().ToString(),
                FullName = model.FullName,
                UserName = model.UserName,
                Email = model.Email,
                OrganizationId = model.OrganizationId
            };

            var result = await _userManager.CreateAsync(user, model.Password);

            if (!result.Succeeded)
            {
                return GetErrorResult(result);
            }

            return new JsonResult(user);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Put([FromRoute] string id, [FromBody] ApplicationUserBindingModel model)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var user = await _userManager.FindByIdAsync(id);
            user.FullName = model.FullName;
            user.UserName = model.UserName;
            user.Email = model.Email;
            user.OrganizationId = model.OrganizationId;

            var result = await _userManager.UpdateAsync(user);

            if (!result.Succeeded)
            {
                return GetErrorResult(result);
            }

            if (!String.IsNullOrEmpty(model.Password))
            {
                result = await _userManager.RemovePasswordAsync(user);

                if (!result.Succeeded)
                {
                    return GetErrorResult(result);
                }

                result = await _userManager.AddPasswordAsync(user, model.Password);

                if (!result.Succeeded)
                {
                    return GetErrorResult(result);
                }
            }

            return new JsonResult(user);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete([FromRoute] string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            var result = await _userManager.DeleteAsync(user);

            if (!result.Succeeded)
            {
                return GetErrorResult(result);
            }

            return Ok();
        }

        [HttpGet("Me")]
        public async Task<IActionResult> Me()
        {
            var userId = this.GetUserId();

            var user = await _admin.Users
                .AsNoTracking()
                .Include(u => u.Countries)
                .ThenInclude(c => c.Country)
                .Include(u => u.Roles)
                .Include(u => u.Organization)
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user == null)
            {
                return BadRequest();
            }

            var roles = user.Roles.Select(r => r.RoleId).ToArray();

            var jsonUser = JObject.FromObject(user);

            // Core Identity has no IdentityRole.Users navigation, so membership comes from the join table.
            var systemAdministrator = await _roleManager.FindByNameAsync("System Administrator");
            jsonUser["IsSystemAdministrator"] = systemAdministrator != null
                && await _admin.UserRoles.AnyAsync(ur => ur.RoleId == systemAdministrator.Id && ur.UserId == user.Id);

            var availableActions = await _admin.ActionRoles
                .AsNoTracking()
                .Include(a => a.Action)
                .Where(a => roles.Contains(a.RoleId))
                .Select(a => a.Action)
                .Distinct()
                .ToListAsync();

            jsonUser["AvailableActions"] = JToken.FromObject(availableActions);

            return new JsonResult(jsonUser);
        }

        [HttpPost("{id}/RemoveUserFromRole")]
        public async Task<IActionResult> RemoveUserFromRole([FromRoute] string id, [FromBody] JObject request)
        {
            var roleId = request.Value<string>("roleId");

            var user = await _userManager.FindByIdAsync(id);
            var role = await _roleManager.FindByIdAsync(roleId);

            var result = await _userManager.RemoveFromRoleAsync(user, role.Name);

            if (!result.Succeeded)
            {
                return GetErrorResult(result);
            }

            return Ok();
        }

        [HttpPost("{id}/AddUserToRole")]
        public async Task<IActionResult> AddUserToRole([FromRoute] string id, [FromBody] JObject request)
        {
            var roleId = request.Value<string>("roleId");

            var user = await _userManager.FindByIdAsync(id);
            var role = await _roleManager.FindByIdAsync(roleId);

            var result = await _userManager.AddToRoleAsync(user, role.Name);

            if (!result.Succeeded)
            {
                return GetErrorResult(result);
            }

            return Ok();
        }

        private IActionResult GetErrorResult(IdentityResult result)
        {
            if (result == null)
            {
                return StatusCode(500);
            }

            if (!result.Succeeded)
            {
                if (result.Errors != null)
                {
                    foreach (var error in result.Errors)
                    {
                        ModelState.AddModelError("", error.Description);
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
