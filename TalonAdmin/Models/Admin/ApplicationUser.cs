using System.Collections.Generic;
using Microsoft.AspNetCore.Identity;

namespace TalonAdmin.Models.Admin
{
    public class ApplicationUser : IdentityUser
    {
        public virtual string FullName { get; set; }

        public virtual int OrganizationId { get; set; }

        public virtual Organization Organization { get; set; }

        public virtual ICollection<ApplicationUserCountry> Countries { get; set; }

        /// <summary>
        /// Identity 2's IdentityUser carried Roles, Claims and Logins navigations; ASP.NET Core
        /// Identity dropped all three. The admin screens still expand them, so they are declared
        /// here and wired up in <see cref="AdminContext.OnModelCreating"/>. Without them a query
        /// such as expand=["Organization","Roles","Claims","Logins"] fails outright, because EF
        /// Core rejects an include path it cannot resolve.
        /// </summary>
        public virtual ICollection<IdentityUserRole<string>> Roles { get; set; }

        public virtual ICollection<IdentityUserClaim<string>> Claims { get; set; }

        public virtual ICollection<IdentityUserLogin<string>> Logins { get; set; }
    }
}
