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
        /// ASP.NET Core Identity drops the user/role navigations the old Identity 2 model had.
        /// They are declared here and wired up in <see cref="AdminContext.OnModelCreating"/> so
        /// existing queries and Include("Roles") calls keep working.
        /// </summary>
        public virtual ICollection<IdentityUserRole<string>> Roles { get; set; }
    }
}
