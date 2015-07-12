using Microsoft.AspNet.Identity.EntityFramework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace TalonAdmin.Models.Admin
{
    public class ApplicationRole : IdentityRole<string, ApplicationUserRole>
    {
    }
}