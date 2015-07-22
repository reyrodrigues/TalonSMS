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

namespace TalonAdmin.Controllers.Api
{
    [RoutePrefix("api/App/Administration")]
    public class AdministrationController : ApiController
    {
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

                if (beneficiary == null) {
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