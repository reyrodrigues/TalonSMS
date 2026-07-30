using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using TalonAdmin.Models.Admin;

namespace TalonAdmin.Auth
{
    /// <summary>
    /// Credential check that preserves the Identity 2 behaviour of the old ApplicationUserManager:
    /// accept either a user name or an e-mail address, and fall back to the upstream directory
    /// service, provisioning a local user on first successful directory login.
    /// </summary>
    public class TalonUserAuthenticator
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;
        private readonly ILogger<TalonUserAuthenticator> _logger;

        public TalonUserAuthenticator(
            UserManager<ApplicationUser> userManager,
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration,
            ILogger<TalonUserAuthenticator> logger)
        {
            _userManager = userManager;
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<ApplicationUser> FindAsync(string userName, string password)
        {
            if (string.IsNullOrEmpty(userName) || string.IsNullOrEmpty(password))
            {
                return null;
            }

            var user = userName.IndexOf('@') > -1
                ? await _userManager.FindByEmailAsync(userName)
                : await _userManager.FindByNameAsync(userName);

            if (user != null && await _userManager.CheckPasswordAsync(user, password))
            {
                return user;
            }

            if (!await AuthenticateAgainstDirectoryAsync(userName, password))
            {
                return null;
            }

            user = await _userManager.FindByNameAsync(userName);
            if (user == null)
            {
                var emailDomain = _configuration["ActiveDirectory:EmailDomain"] ?? "theirc.org";
                user = new ApplicationUser
                {
                    UserName = userName,
                    Email = string.Format("{0}@{1}", userName, emailDomain)
                };

                var created = await _userManager.CreateAsync(user, password);
                if (!created.Succeeded)
                {
                    _logger.LogWarning("Could not provision directory user {UserName}", userName);
                    return null;
                }

                return user;
            }

            // Directory accepted the password, so re-stamp the local copy to match.
            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var reset = await _userManager.ResetPasswordAsync(user, token, password);

            return reset.Succeeded ? user : null;
        }

        private async Task<bool> AuthenticateAgainstDirectoryAsync(string userName, string password)
        {
            if (!_configuration.GetValue("ActiveDirectory:Enabled", false))
            {
                return false;
            }

            var url = _configuration["ActiveDirectory:AuthenticationUrl"];
            if (string.IsNullOrEmpty(url))
            {
                return false;
            }

            try
            {
                var client = _httpClientFactory.CreateClient();
                var content = new FormUrlEncodedContent(new[]
                {
                    new System.Collections.Generic.KeyValuePair<string, string>("username", userName),
                    new System.Collections.Generic.KeyValuePair<string, string>("password", password)
                });

                var response = await client.PostAsync(url, content);
                if (!response.IsSuccessStatusCode)
                {
                    return false;
                }

                var result = JToken.Parse(await response.Content.ReadAsStringAsync());
                return result["result"] != null && result["result"].Value<bool>();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Directory authentication call failed for {UserName}", userName);
                return false;
            }
        }
    }
}
