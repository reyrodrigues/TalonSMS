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

                var query = ctx.VoucherTransactionRecords
                    .Include("Beneficiary")
                    .Include("Voucher")
                    .Include("Voucher.Category")
                    .Where(v => v.Status < 2);

                using (MD5 md5Hash = MD5.Create())
                {
                    var records = (await query.ToArrayAsync())
                        .Where(v => v.Beneficiary != null)
                        .Select(v =>
                        {
                            var iv = new byte[16];
                            _generator.GetBytes(iv);
                            byte[] key = md5Hash.ComputeHash(Encoding.UTF8.GetBytes(v.Beneficiary.NationalId + ":" + v.Voucher.VoucherCode));
                            return new
                            {
                                IV = Convert.ToBase64String(iv),
                                RecordKey = Convert.ToBase64String(key),
                                Encrypted = String.Format(
                                    "{0}:{1}:{2}", 
                                    v.Voucher.VoucherCode, 
                                    v.Voucher.Category.Value,
                                    v.CreatedOn.ToBinary()
                                ).Encrypt(iv, HashPin(v.Beneficiary)),
                                Date = v.CreatedOn
                            };
                        })
                        .OrderByDescending(v => v.Date);


                    return Ok<JToken>(JToken.FromObject(records));
                }
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