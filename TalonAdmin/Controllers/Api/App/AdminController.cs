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
        /// <summary>
        /// Noop action that just returns true. Used to determine if an wifi connection can reach the system.
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        [Route("IsAlive")]
        public  IHttpActionResult IsAlive()
        {
            return Ok();
        }


        [HttpPost]
        [Route("ProvisionBeneficiary")]
        public async Task<IHttpActionResult> ProvisionBeneficiary([FromBody]dynamic request)
        {
            int beneficiaryId = request.beneficiaryId;
            string cardId = request.cardId;
            using (var ctx = new Models.Vouchers.Context())
            {
                Aes rm = new AesCryptoServiceProvider();
                rm.GenerateKey();
                var beneficiary = await ctx.Beneficiaries.Where(b => b.Id == beneficiaryId).FirstOrDefaultAsync();
                if (beneficiary == null)
                    return NotFound();

                beneficiary.CardKey = Convert.ToBase64String(rm.Key);
                beneficiary.CardId = cardId;
                await ctx.SaveChangesAsync();

                return Ok<JToken>(JToken.FromObject(new
                {
                    _id = beneficiary.Id.ToString(),
                    BeneficiaryId = beneficiary.Id,
                    CardKey = beneficiary.CardKey,
                    CardId = beneficiary.CardId,
                }));
            }
        }

        [HttpGet]
        [Route("DownloadBeneficiaryKeys")]
        public async Task<IHttpActionResult> DownloadBeneficiaryKeys()
        {
            using (var ctx = new Models.Vouchers.Context())
            {
                var cardKeys = await ctx.Beneficiaries
                    .Where(b=> b.CardId != null)
                    .Select(b => new
                {
                    _id = b.Id.ToString(),
                    BeneficiaryId = b.Id,
                    CardKey = b.CardKey,
                    CardId = b.CardId,
                }).ToArrayAsync();

                return Ok<JToken>(JToken.FromObject(cardKeys));
            }
        }

        [HttpGet]
        [Route("GenerateInitialLoad")]
        public async Task<IHttpActionResult> GenerateInitialLoad(int beneficiaryId)
        {
            using (var ctx = new Models.Vouchers.Context())
            {
                Aes rm = KeyForBeneficiary(beneficiaryId);

                var loadQuery = ctx.VoucherTransactionRecords.Where(t =>
                    t.Voucher.Distribution.Program.DistributionMechanism == 2
                    && t.BeneficiaryId == beneficiaryId
                ).Select(t => new
                {
                    Voucher = t.VoucherId,
                    Value = t.Type == 1 ? t.Value : -t.Value,
                    CreatedOn = t.CreatedOn,
                })
                .GroupBy(t => t.Voucher)
                .Select(t => new
                {
                    Voucher = t.Key,
                    Value = t.Sum(v => v.Value),
                    CreatedOn = t.Min(v => v.CreatedOn)
                });

                var value = await loadQuery.SumAsync(v => v.Value);
                var createdOn = await loadQuery.MaxAsync(v => v.CreatedOn);

                return Ok<JToken>(JToken.FromObject(EncodeLoad(beneficiaryId, value, createdOn)));
            }
        }

        [HttpGet]
        [Route("GenerateCardLoads")]
        public async Task<IHttpActionResult> GenerateCardLoads()
        {

            using (var ctx = new Models.Vouchers.Context())
            {
                var loadDictionary = (await ctx.VoucherTransactionRecords.Where(t =>
                    t.Voucher.Distribution.Program.DistributionMechanism == 2
                    && t.Type == 1
                    && t.Beneficiary.CardId != null
                ).Select(t => new
                {
                    Beneficiary = t.Beneficiary,
                    Value = t.Value,
                    CreatedOn = t.CreatedOn,
                })
                .GroupBy(k => k.Beneficiary)
                .Select(l => new
                {
                    CardId = l.Key.CardId,
                    Load = l.Select(b => new {
                        BeneficiaryId = b.Beneficiary.Id,
                        Value = b.Value,
                        CreatedOn = b.CreatedOn,
                    })
                })
                .ToArrayAsync())
                .Select(l => new
                {
                    _id = l.CardId,
                    CardId = l.CardId,
                    Load = l.Load.Select(v => EncodeLoad(v.BeneficiaryId, v.Value, v.CreatedOn)).ToArray(),
                })
                .ToArray();

                return Ok<JToken>(JToken.FromObject(loadDictionary));
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

        private string EncodeLoad(int beneficiaryId, decimal? value, DateTime createdOn)
        {
            int unixTime = (int)(createdOn.ToUniversalTime() - new DateTime(1970, 1, 1)).TotalSeconds;
            var payload = String.Format("1933|{0}|{1:X}", value, unixTime);

            return payload.Encrypt(KeyForBeneficiary(beneficiaryId));
        }

        private Aes KeyForBeneficiary(int beneficiaryId)
        {
            using (var ctx = new Models.Vouchers.Context())
            {
                Aes rm = new AesCryptoServiceProvider();
                var beneficiary = ctx.Beneficiaries.Where(b => b.Id == beneficiaryId).FirstOrDefault();

                var passwordBytes = Encoding.UTF8.GetBytes(beneficiary.PIN);
                var iv = new byte[rm.BlockSize / 8];
                Array.Copy(passwordBytes, iv, passwordBytes.Length < iv.Length ? passwordBytes.Length : iv.Length);

                rm.Key = Convert.FromBase64String(beneficiary.CardKey);
                rm.IV = iv;
                rm.Mode = CipherMode.CBC;
                return rm;
            }
        }
    }
}