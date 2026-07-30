using System.Collections.Generic;
using Microsoft.AspNetCore.Identity;

namespace TalonAdmin.Models.Admin
{
    public class ApplicationRole : IdentityRole
    {
        public ApplicationRole()
        {
        }

        public ApplicationRole(string roleName)
            : base(roleName)
        {
        }

        public virtual ICollection<IdentityUserRole<string>> Users { get; set; }
    }
}
