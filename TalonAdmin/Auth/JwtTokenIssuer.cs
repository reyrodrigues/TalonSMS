using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using TalonAdmin.Models.Admin;

namespace TalonAdmin.Auth
{
    public class JwtTokenIssuer
    {
        private readonly IConfiguration _configuration;
        private readonly UserManager<ApplicationUser> _userManager;

        public JwtTokenIssuer(IConfiguration configuration, UserManager<ApplicationUser> userManager)
        {
            _configuration = configuration;
            _userManager = userManager;
        }

        public static SymmetricSecurityKey GetSigningKey(IConfiguration configuration)
        {
            var key = configuration["Jwt:SigningKey"];
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new InvalidOperationException(
                    "Jwt:SigningKey is not configured. Set it via user-secrets, an environment " +
                    "variable, or appsettings before starting the application.");
            }

            return new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));
        }

        public async Task<(string AccessToken, int ExpiresInSeconds)> IssueAsync(ApplicationUser user)
        {
            var lifetime = TimeSpan.FromDays(_configuration.GetValue("Jwt:AccessTokenLifetimeDays", 14.0));
            var expires = DateTime.UtcNow.Add(lifetime);

            var claims = new List<Claim>
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Id),
                new Claim(ClaimTypes.NameIdentifier, user.Id),
                new Claim(ClaimTypes.Name, user.UserName),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            };

            if (!string.IsNullOrEmpty(user.Email))
            {
                claims.Add(new Claim(ClaimTypes.Email, user.Email));
            }

            foreach (var role in await _userManager.GetRolesAsync(user))
            {
                claims.Add(new Claim(ClaimTypes.Role, role));
            }

            var credentials = new SigningCredentials(GetSigningKey(_configuration), SecurityAlgorithms.HmacSha256);
            var token = new JwtSecurityToken(
                issuer: _configuration["Jwt:Issuer"],
                audience: _configuration["Jwt:Audience"],
                claims: claims,
                notBefore: DateTime.UtcNow,
                expires: expires,
                signingCredentials: credentials);

            return (new JwtSecurityTokenHandler().WriteToken(token), (int)lifetime.TotalSeconds);
        }
    }
}
