using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace TalonAdmin.Auth
{
    /// <summary>
    /// Port of the OWIN FormAuthenticationMiddleware. Clients that cannot set request headers
    /// (plain HTML form posts from the field app) smuggle the bearer token and the tenant
    /// selection through form fields; this promotes them back onto the headers before
    /// authentication runs.
    /// </summary>
    public class TenantFormMiddleware
    {
        private readonly RequestDelegate _next;

        public TenantFormMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task Invoke(HttpContext context)
        {
            if (context.Request.HasFormContentType
                && context.Request.ContentType != null
                && context.Request.ContentType.StartsWith("application/x-www-form-urlencoded", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    // The form has to be re-readable by model binding further down the pipeline.
                    context.Request.EnableBuffering();
                    var form = await context.Request.ReadFormAsync();
                    context.Request.Body.Position = 0;

                    if (form.ContainsKey("__authorization") && !context.Request.Headers.ContainsKey("Authorization"))
                    {
                        context.Request.Headers.Authorization = form["__authorization"].ToString();
                    }

                    if (form.ContainsKey("CountryId"))
                    {
                        context.Request.Headers[AuthConstants.CountryHeader] = form["CountryId"].ToString();
                    }

                    if (form.ContainsKey("OrganizationId"))
                    {
                        context.Request.Headers[AuthConstants.OrganizationHeader] = form["OrganizationId"].ToString();
                    }
                }
                catch (InvalidDataException)
                {
                    // Not a readable form; leave the request untouched, exactly as the OWIN version did.
                }
                catch (InvalidOperationException)
                {
                }
            }

            await _next(context);
        }
    }
}
