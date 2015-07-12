using Microsoft.AspNet.Identity.EntityFramework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace TalonAdmin.Models.Admin
{
    public class ActionRole
    {
        public virtual int Id { get; set; }
        public virtual string RoleId { get; set; }
        public virtual int ActionId { get; set; }

        public virtual Action Action { get; set; }
        public virtual IdentityRole Role { get; set; }
    }
}