using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Http;
using Microsoft.AspNet.Identity;

namespace TalonAdmin.Attributes
{
    public class AuthorizeTenantAttribute : AuthorizeAttribute
    {
        protected override bool IsAuthorized(System.Web.Http.Controllers.HttpActionContext actionContext)
        {
            using (var ctx = new Models.Admin.AdminContext())
            {
                var userId = actionContext.RequestContext.Principal.Identity.GetUserId();
                if (!String.IsNullOrEmpty(userId))
                {
                    var user = ctx.Users.Where(u => u.Id == userId).FirstOrDefault();
                    if (user != null)
                    {
                        if (actionContext.Request.Headers.GetValues("X-Tenant-Country").Any() && actionContext.Request.Headers.GetValues("X-Tenant-Organization").Any())
                        {
                            int attemptedCountry = Convert.ToInt32(actionContext.Request.Headers.GetValues("X-Tenant-Country").First() ?? "0");
                            int attemptedOrganization = Convert.ToInt32(actionContext.Request.Headers.GetValues("X-Tenant-Organization").First() ?? "0");

                            return true;// user.Organization.Id == attemptedOrganization && user.Countries.Where(c => c.Id == attemptedCountry).Any();
                        }
                    }
                }
            }

            return false;
        }
    }
}