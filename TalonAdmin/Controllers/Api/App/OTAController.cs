using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Web.Http;
using System.Web.Http.OData;
using TalonAdmin.Extensions;
using System.Data.Entity;

namespace TalonAdmin.Controllers.Api
{
    [RoutePrefix("api/App/OTA")]
    public class OTAController : ApiController
    {
        private static readonly RNGCryptoServiceProvider _generator = new RNGCryptoServiceProvider();

        [HttpGet]
        [Route("DownloadOpenTransactions")]
        public async Task<IHttpActionResult> DownloadOpenTransactions()
        {
            using (var ctx = new Models.Vouchers.Context())
            {
                ctx.Configuration.LazyLoadingEnabled = false;
                ctx.Configuration.ProxyCreationEnabled = false;
                // Filter Country

                return Ok();
            }
        }


        private byte[] HashPin(Models.Vouchers.Beneficiary beneficiary)
        {
            var paddedSalt = beneficiary.NationalId.PadLeft(8, '0');

            var passwordHasher = new System.Security.Cryptography.Rfc2898DeriveBytes(beneficiary.PIN, Encoding.UTF8.GetBytes(paddedSalt), 1000);
            return passwordHasher.GetBytes(16);
        }
    }
}