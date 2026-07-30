using System;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TalonAdmin.Models.Admin;

namespace TalonAdmin.Auth
{
    /// <summary>
    /// Replaces the OWIN BasicAuthenticationMiddleware. Registered as a real authentication
    /// handler so that [Authorize] and the challenge/401 flow work the same as for bearer tokens.
    /// </summary>
    public class BasicAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        private readonly TalonUserAuthenticator _authenticator;
        private readonly UserManager<ApplicationUser> _userManager;

        public BasicAuthenticationHandler(
            IOptionsMonitor<AuthenticationSchemeOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder,
            TalonUserAuthenticator authenticator,
            UserManager<ApplicationUser> userManager)
            : base(options, logger, encoder)
        {
            _authenticator = authenticator;
            _userManager = userManager;
        }

        protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            string headerValue = Request.Headers.Authorization;
            if (string.IsNullOrEmpty(headerValue))
            {
                return AuthenticateResult.NoResult();
            }

            AuthenticationHeaderValue header;
            if (!AuthenticationHeaderValue.TryParse(headerValue, out header)
                || !AuthConstants.BasicScheme.Equals(header.Scheme, StringComparison.OrdinalIgnoreCase)
                || string.IsNullOrEmpty(header.Parameter))
            {
                return AuthenticateResult.NoResult();
            }

            string userName;
            string password;
            try
            {
                var parameter = Encoding.UTF8.GetString(Convert.FromBase64String(header.Parameter));
                var separator = parameter.IndexOf(':');
                if (separator < 0)
                {
                    return AuthenticateResult.Fail("Malformed Basic credentials.");
                }

                userName = parameter.Substring(0, separator);
                password = parameter.Substring(separator + 1);
            }
            catch (FormatException)
            {
                return AuthenticateResult.Fail("Malformed Basic credentials.");
            }

            var user = await _authenticator.FindAsync(userName, password);
            if (user == null)
            {
                return AuthenticateResult.Fail("Invalid user name or password.");
            }

            var identity = new ClaimsIdentity(AuthConstants.BasicScheme);
            identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, user.Id));
            identity.AddClaim(new Claim(ClaimTypes.Name, user.UserName));
            foreach (var role in await _userManager.GetRolesAsync(user))
            {
                identity.AddClaim(new Claim(ClaimTypes.Role, role));
            }

            var principal = new ClaimsPrincipal(identity);
            return AuthenticateResult.Success(new AuthenticationTicket(principal, AuthConstants.BasicScheme));
        }

        protected override Task HandleChallengeAsync(AuthenticationProperties properties)
        {
            Response.StatusCode = 401;
            Response.Headers.WWWAuthenticate = "Basic";
            return Task.CompletedTask;
        }
    }
}
