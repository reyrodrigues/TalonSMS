using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNet.Identity;
using Microsoft.AspNet.Identity.EntityFramework;
using Microsoft.AspNet.Identity.Owin;
using System.Collections.Generic;

namespace EmergencyVoucherManagement.Models.Admin
{
    public class ApplicationUser : IdentityUser
    {
        public virtual string FullName { get; set; }

        public virtual int OrganizationId { get; set; }

        public virtual Organization Organization { get; set; }
        public virtual IEnumerable<ApplicationUserCountry> Countries { get; set; }

        public async Task<ClaimsIdentity> GenerateUserIdentityAsync(UserManager<ApplicationUser> manager, string authenticationType)
        {
            // Note the authenticationType must match the one defined in CookieAuthenticationOptions.AuthenticationType
            var userIdentity = await manager.CreateIdentityAsync(this, authenticationType);
            // Add custom user claims here
            return userIdentity;
        }
    }
}