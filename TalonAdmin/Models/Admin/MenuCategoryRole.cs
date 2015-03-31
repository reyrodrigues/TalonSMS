using Microsoft.AspNet.Identity.EntityFramework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace TalonAdmin.Models.Admin
{
    public class MenuCategoryRole : Entity
    {
        public virtual int CategoryId { get; set; }
        public virtual string RoleId { get; set; }

        public virtual MenuCategory Category { get; set; }
        public virtual IdentityRole Role { get; set; }
    }
}