using System;
using System.Linq;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json.Linq;
using TalonAdmin.Auth;

namespace TalonAdmin.Controllers.Api
{
    [Route("api/App/VendorProfile")]
    public class VendorProfileController : ControllerBase
    {
        private readonly Models.Vouchers.Context ctx;
        private readonly Models.Admin.AdminContext admin;

        public VendorProfileController(Models.Vouchers.Context context, Models.Admin.AdminContext adminContext)
        {
            ctx = context;
            admin = adminContext;
        }

        [HttpPost("Login")]
        [EnableRateLimiting(RateLimitPolicies.Credentials)]
        public async Task<IActionResult> Login([FromBody] JObject request)
        {
            string userName = request["UserName"].ToString().ToLower();
            string password = request.Value<string>("Password");

            var device = request["Device"];
            string deviceName = String.Format("{0} {1} {2}",
                device.Value<string>("platform"), device.Value<string>("model"), device.Value<string>("version"));
            string deviceUUID = device.Value<string>("UUID");

            if (deviceUUID == null)
                deviceUUID = device.Value<string>("uuid");

            var vendor = await ctx.Vendors.Where(v => v.UserName.ToLower() == userName).FirstOrDefaultAsync();
            if (vendor == null || deviceUUID == null)
            {
                return BadRequest("Invalid username or password.");
            }

            if (vendor.ValidatePassword(password) && !ctx.VendorDevices.Where(d => d.UUID == deviceUUID && d.VendorId != vendor.Id).Any())
            {
                var vendorDevice = await ctx.VendorDevices.Where(d => d.VendorId == vendor.Id && d.UUID == deviceUUID).FirstOrDefaultAsync();

                var userIPAddress = HttpContext.Connection.RemoteIpAddress?.ToString();

                if (Request.Headers.ContainsKey("X-Forwarded-For"))
                    userIPAddress = Request.Headers["X-Forwarded-For"];

                if (Request.Headers.ContainsKey("X-Real-IP"))
                    userIPAddress = Request.Headers["X-Real-IP"];

                if (vendorDevice == null)
                {
                    // Registering device
                    vendorDevice = new Models.Vouchers.VendorDevice
                    {
                        VendorId = vendor.Id,
                        Name = deviceName,
                        Type = device.Value<string>("platform"),
                        UUID = deviceUUID,
                        LastAccessed = DateTime.UtcNow,
                        CountryId = vendor.CountryId,
                        IPAddress = userIPAddress,
                    };

                    ctx.VendorDevices.Add(vendorDevice);
                }
                else
                {
                    vendorDevice.IPAddress = userIPAddress;
                    vendorDevice.LastAccessed = DateTime.UtcNow;
                }

                using (var md5 = MD5.Create())
                {
                    vendor.AuthorizationToken = Convert.ToBase64String(md5.ComputeHash(Encoding.UTF8.GetBytes(RandomNumber.RandomLong(10).ToString())));
                }
                await ctx.SaveChangesAsync();

                return Ok(JToken.FromObject(new
                {
                    token = vendor.AuthorizationToken,
                }));
            }

            return BadRequest("Invalid username or password");
        }

        [HttpGet("LoadProfile")]
        public async Task<IActionResult> LoadProfile()
        {
            AuthenticationHeaderValue authorization;
            if (!AuthenticationHeaderValue.TryParse(Request.Headers.Authorization, out authorization)
                || authorization.Scheme.ToLower() != "token")
            {
                return NotFound();
            }

            var authToken = authorization.Parameter;

            var vendor = await ctx.Vendors
                .AsNoTracking()
                .Where(v => v.AuthorizationToken == authToken)
                .FirstOrDefaultAsync();

            // BEHAVIOUR CHANGE: the EF6 version dereferenced `vendor` unconditionally, so an
            // unrecognised token produced a 500 (and a stack trace) on this unauthenticated
            // endpoint. An unknown token is now treated like the malformed-scheme case above.
            if (vendor == null)
            {
                return NotFound();
            }

            var country = await admin.Countries
                .AsNoTracking()
                .Where(c => c.Id == vendor.CountryId)
                .FirstOrDefaultAsync();

            return Ok(JToken.FromObject(new
            {
                vendor.Id,
                vendor.Name,
                vendor.UserName,
                vendor.CountryId,
                Country = country,
            }));
        }

        [Authorize]
        [HttpPost("UpdatePassword")]
        public async Task<IActionResult> UpdatePassword([FromBody] JObject request)
        {
            int vendorId = request.Value<int>("VendorId");
            string password = request.Value<string>("Password");

            var vendor = await ctx.Vendors.Where(v => v.Id == vendorId).FirstOrDefaultAsync();
            vendor.SetPassword(password);

            await ctx.SaveChangesAsync();

            return Ok();
        }
    }
}
