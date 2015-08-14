using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Web.Http;
using System.Data.Entity;
using Newtonsoft.Json.Linq;
using System.Security.Cryptography;
using System.Text;
namespace TalonAdmin.Controllers.Api
{
    [RoutePrefix("api/App/VendorProfile")]
    public class VendorProfileController : ApiController
    {
        [Route("Login")]
        public async Task<IHttpActionResult> Login(dynamic request)
        {
            string userName = request.UserName.ToString().ToLower();
            string password = request.Password;
            string deviceName = String.Format("{0} {1} {2}", request.Device.platform, request.Device.model, request.Device.version);
            string deviceUUID = request.Device.UUID;

            if (deviceUUID == null)
                deviceUUID = request.Device.uuid;

            using (var ctx = new Models.Vouchers.Context())
            {
                var vendor = await ctx.Vendors.Where(v => v.UserName.ToLower() == userName).FirstOrDefaultAsync();
                if (vendor == null || deviceUUID == null)
                {
                    return BadRequest("Invalid username or password.");
                }

                if (vendor.ValidatePassword(password) && !ctx.VendorDevices.Where(d => d.UUID == deviceUUID && d.VendorId != vendor.Id).Any())
                {
                    var vendorDevice = await ctx.VendorDevices.Where(d => d.VendorId == vendor.Id && d.UUID == deviceUUID).FirstOrDefaultAsync();

                    var userIPAddress = HttpContext.Current.Request.UserHostAddress;

                    if (HttpContext.Current.Request.Headers.AllKeys.Where(k => k == "X-Forwarded-For").Any())
                        userIPAddress = HttpContext.Current.Request.Headers["X-Forwarded-For"];

                    if (HttpContext.Current.Request.Headers.AllKeys.Where(k => k == "X-Real-IP").Any())
                        userIPAddress = HttpContext.Current.Request.Headers["X-Real-IP"];

                    if (vendorDevice == null)
                    {
                        // Registering device
                        vendorDevice = new Models.Vouchers.VendorDevice
                        {
                            VendorId = vendor.Id,
                            Name = deviceName,
                            Type = request.Device.platform,
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


                    var saltAndHash = vendor.Password.Split(':');
                    var salt = saltAndHash[0];

                    return Ok<JToken>(JToken.FromObject(new
                    {
                        token = vendor.AuthorizationToken,
                    }));
                }
                return BadRequest("Invalid username or password");
            }
        }

        [HttpGet]
        [Route("LoadProfile")]
        public async Task<IHttpActionResult> LoadProfile()
        {
            using (var ctx = new Models.Vouchers.Context())
            using (var admin = new Models.Admin.AdminContext())
            {
                ctx.Configuration.ProxyCreationEnabled = false;
                ctx.Configuration.LazyLoadingEnabled = false;
                admin.Configuration.ProxyCreationEnabled = false;
                admin.Configuration.LazyLoadingEnabled = false;

                if (Request.Headers.Authorization.Scheme.ToLower() != "token")
                {
                    return NotFound();
                }

                var authToken = Request.Headers.Authorization.Parameter;

                var vendor = await ctx.Vendors
                    .Where(v => v.AuthorizationToken == authToken)
                    .FirstOrDefaultAsync();
                var country = await admin.Countries
                    .Where(c => c.Id == vendor.CountryId)
                    .FirstOrDefaultAsync();

                return Ok<JToken>(JToken.FromObject(new
                {
                    vendor.Id,
                    vendor.Name,
                    vendor.UserName,
                    vendor.CountryId,
                    Country = country,
                }));
            }
        }

        [Authorize]
        [Route("UpdatePassword")]
        public async Task<IHttpActionResult> UpdatePassword(dynamic request)
        {
            int vendorId = request.VendorId;
            string password = request.Password;

            using (var ctx = new Models.Vouchers.Context())
            {
                var vendor = await ctx.Vendors.Where(v => v.Id == vendorId).FirstOrDefaultAsync();
                vendor.SetPassword(password);

                await ctx.SaveChangesAsync();

                return Ok();
            }
        }
    }
}