using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using TalonAdmin.Models.Admin;

namespace TalonAdmin.Auth
{
    /// <summary>
    /// Credential check that preserves the Identity 2 behaviour of the old ApplicationUserManager:
    /// accept either a user name or an e-mail address.
    /// </summary>
    public class TalonUserAuthenticator
    {
        private readonly UserManager<ApplicationUser> _userManager;

        public TalonUserAuthenticator(UserManager<ApplicationUser> userManager)
        {
            _userManager = userManager;
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

            return null;
        }
    }
}
