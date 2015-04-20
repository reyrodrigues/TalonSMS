using Microsoft.Owin;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Web;

namespace TalonAdmin.Middleware
{
    public class FormAuthenticationMiddleware : OwinMiddleware
    {
        public FormAuthenticationMiddleware(OwinMiddleware next)
            : base(next)
        {
        }

        public async override Task Invoke(IOwinContext context)
        {
            if (context.Request.ContentType == "application/x-www-form-urlencoded" &&
                (context.Request.User == null || !context.Request.User.Identity.IsAuthenticated))
            {
                try
                {
                    var stringReader = new System.IO.StreamReader(context.Request.Body);
                    var body = stringReader.ReadToEnd();
                    stringReader.Close();
                    stringReader.Dispose();

                    context.Request.Body.Seek(0, System.IO.SeekOrigin.Begin);
                    var form = HttpUtility.ParseQueryString(body);
                    if (form.AllKeys.Contains("__authorization"))
                    {
                        context.Request.Headers["Authorization"] = form["__authorization"];
                    }
                }
                catch
                {
                }
            }

            await Next.Invoke(context);
        }
    }
}