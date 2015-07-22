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
using TalonAdmin.Extensions;
using System.Security.Cryptography.X509Certificates;
using System.IO;
using System.Security;

namespace TalonAdmin.Controllers.Api
{
    [RoutePrefix("api/App/Administration")]
    public class AdministrationController : ApiController
    {
        [HttpGet]
        [Route("GenerateBeneficiaryKey")]
        public async Task<IHttpActionResult> GenerateBeneficiaryKey(int beneficiaryId)
        {
            TripleDESCryptoServiceProvider rm = new TripleDESCryptoServiceProvider();
            var passwordBytes = Encoding.UTF8.GetBytes("1234");
            var iv = new byte[rm.BlockSize / 8];
            Array.Copy(passwordBytes, iv, passwordBytes.Length);
            rm.IV = iv;
            var stream = new MemoryStream();
            var outStream = new MemoryStream();

            using (var cryptStream = new CryptoStream(stream, rm.CreateEncryptor(), CryptoStreamMode.Write))
            {
                var data = new byte[]{
                 0x19, 0x33, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00
                };
                cryptStream.Write(data, 0, data.Length);
            }

            using (var cryptStream = new CryptoStream(new MemoryStream(stream.ToArray()), rm.CreateDecryptor(), CryptoStreamMode.Read))
            using (var reader = new StreamReader(cryptStream))
            {
                var bytes= new List<byte>();
                int currentByte = 0;
                while(( currentByte = cryptStream.ReadByte()) > -1) {
                    bytes.Add((byte)currentByte);
                }
               

                return Ok<string>(Convert.ToBase64String(stream.ToArray()) + reader.ReadToEnd());
            }

        }

        [HttpGet]
        [Authorize]
        [Route("LoadBeneficiary")]
        public async Task<IHttpActionResult> LoadBeneficiary(string nationalId)
        {
            using (var ctx = new Models.Vouchers.Context())
            {
                ctx.Configuration.LazyLoadingEnabled = false;
                ctx.Configuration.ProxyCreationEnabled = false;

                var beneficiary = await ctx.Beneficiaries
                    .FilterCountry(this)
                    .FilterOrganization(this)
                    .Where(b => b.NationalId == nationalId && b.Disabled != true)
                    .FirstOrDefaultAsync();

                if (beneficiary == null)
                {
                    return BadRequest("Invalid National Id");
                }

                return Ok<JToken>(JToken.FromObject(beneficiary));
            }
        }

        [HttpGet]
        [Authorize]
        [Route("LoadVouchers")]
        public async Task<IHttpActionResult> LoadVouchers(string nationalId)
        {
            using (var ctx = new Models.Vouchers.Context())
            {
                ctx.Configuration.LazyLoadingEnabled = false;
                ctx.Configuration.ProxyCreationEnabled = false;

                var beneficiary = await ctx.Beneficiaries
                    .FilterCountry(this)
                    .FilterOrganization(this)
                    .Where(b => b.NationalId == nationalId && b.Disabled != true)
                    .FirstOrDefaultAsync();

                if (beneficiary == null)
                {
                    return BadRequest("Invalid National Id");
                }


                return Ok();
            }
        }
    }
}