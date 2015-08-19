using Microsoft.AspNet.Identity.Owin;
using Microsoft.Owin;
using Microsoft.Owin.Security.Cookies;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using System.Security.Claims;

namespace TalonAdmin.Middleware
{
    public class BasicAuthenticationMiddleware : OwinMiddleware
    {
        public BasicAuthenticationMiddleware(OwinMiddleware next)
            : base(next)
        {
        }

        public override async Task Invoke(IOwinContext context)
        {
            var request = context.Request;
            var response = context.Response;

            response.OnSendingHeaders(state =>
            {
                var resp = (OwinResponse)state;

                if (resp.StatusCode == 401)
                {
                    resp.Headers["WWW-Authenticate"] = "Basic";
                }
            }, response);

            if (request.Headers.ContainsKey("Authorization") && !String.IsNullOrEmpty(request.Headers["Authorization"]))
            {
                var header = AuthenticationHeaderValue.Parse(request.Headers["Authorization"]);

                if (header != null && "Basic".Equals(header.Scheme,
                                         StringComparison.OrdinalIgnoreCase))
                {
                    string parameter = Encoding.UTF8.GetString(
                                          Convert.FromBase64String(
                                                header.Parameter));
                    var parts = parameter.Split(':');

                    string userName = parts[0];
                    string password = parts[1];



                    if (context.Request.User == null || !context.Request.User.Identity.IsAuthenticated)
                    {
                        var manager = context.GetUserManager<ApplicationUserManager>();
                        var user = await manager.FindAsync(userName, password);
                        if (user != null)
                        {
                            var basicIdentity = await user.GenerateUserIdentityAsync(manager, "Basic");
                            var basicPrincipal = new ClaimsPrincipal(basicIdentity);

                            context.Request.User = basicPrincipal;
                        }
                    }
                }
            }

            await Next.Invoke(context);
        }
    }
}
