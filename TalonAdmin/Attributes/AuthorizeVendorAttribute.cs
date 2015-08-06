using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Http;

namespace TalonAdmin.Attributes
{
    public class AuthorizeVendorAttribute : AuthorizeAttribute
    {
        protected override bool IsAuthorized(System.Web.Http.Controllers.HttpActionContext actionContext)
        {
            var headers = actionContext.Request.Headers.ToDictionary(k => k.Key, v => v.Value);

            if (actionContext.Request.Headers.Authorization.Scheme.ToLower() != "token")
            {
                return false;
            }

            var authToken = actionContext.Request.Headers.Authorization.Parameter;
            using (var ctx = new Models.Vouchers.Context())
            {
                return ctx.Vendors
                    .Where(v => v.AuthorizationToken == authToken).Any();
            }
        }
    }
}