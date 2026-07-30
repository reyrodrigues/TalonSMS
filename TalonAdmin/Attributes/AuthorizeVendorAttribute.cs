using System;
using System.Linq;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using TalonAdmin.Auth;

namespace TalonAdmin.Attributes
{
    /// <summary>
    /// Authenticates a point-of-sale device by its vendor token
    /// (<c>Authorization: Token &lt;value&gt;</c>) and publishes the resolved vendor id on the
    /// request so downstream actions can read it.
    /// </summary>
    public class AuthorizeVendorAttribute : TypeFilterAttribute
    {
        public AuthorizeVendorAttribute()
            : base(typeof(AuthorizeVendorFilter))
        {
        }

        private class AuthorizeVendorFilter : IAsyncAuthorizationFilter
        {
            private readonly Models.Vouchers.Context _context;

            public AuthorizeVendorFilter(Models.Vouchers.Context context)
            {
                _context = context;
            }

            public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
            {
                string headerValue = context.HttpContext.Request.Headers.Authorization;

                AuthenticationHeaderValue header;
                if (string.IsNullOrEmpty(headerValue)
                    || !AuthenticationHeaderValue.TryParse(headerValue, out header)
                    || !"token".Equals(header.Scheme, StringComparison.OrdinalIgnoreCase)
                    || string.IsNullOrEmpty(header.Parameter))
                {
                    context.Result = new UnauthorizedResult();
                    return;
                }

                var vendorId = await _context.Vendors
                    .Where(v => v.AuthorizationToken == header.Parameter)
                    .Select(v => (int?)v.Id)
                    .FirstOrDefaultAsync();

                if (vendorId == null)
                {
                    context.Result = new UnauthorizedResult();
                    return;
                }

                context.HttpContext.Request.Headers[AuthConstants.VendorIdHeader] = vendorId.Value.ToString();
            }
        }
    }
}
