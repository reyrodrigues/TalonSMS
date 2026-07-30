using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TalonAdmin.Auth;

namespace TalonAdmin.Controllers
{
    /// <summary>
    /// Replacement for the OWIN OAuth authorization server that used to serve "/Token".
    /// The request and response shapes are unchanged so existing clients keep working:
    /// a form-encoded resource-owner password grant in, an OAuth token payload out.
    /// </summary>
    [AllowAnonymous]
    [Route("Token")]
    public class TokenController : Controller
    {
        private readonly TalonUserAuthenticator _authenticator;
        private readonly JwtTokenIssuer _tokenIssuer;

        public TokenController(TalonUserAuthenticator authenticator, JwtTokenIssuer tokenIssuer)
        {
            _authenticator = authenticator;
            _tokenIssuer = tokenIssuer;
        }

        [HttpPost]
        [Consumes("application/x-www-form-urlencoded")]
        public async Task<IActionResult> Post(
            [FromForm(Name = "grant_type")] string grantType,
            [FromForm(Name = "username")] string userName,
            [FromForm(Name = "password")] string password)
        {
            if (!string.Equals(grantType, "password"))
            {
                return BadRequest(new
                {
                    error = "unsupported_grant_type",
                    error_description = "Only the resource owner password credentials grant is supported."
                });
            }

            var user = await _authenticator.FindAsync(userName, password);
            if (user == null)
            {
                return BadRequest(new
                {
                    error = "invalid_grant",
                    error_description = "The user name or password is incorrect."
                });
            }

            var token = await _tokenIssuer.IssueAsync(user);

            return Json(new
            {
                access_token = token.AccessToken,
                token_type = "bearer",
                expires_in = token.ExpiresInSeconds,
                userName = user.UserName
            });
        }
    }
}
