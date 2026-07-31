using TalonAdmin.Models.BindingModels;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using TalonAdmin.Auth;
using TalonAdmin.Extensions;
using TalonAdmin.Hubs;
using TalonAdmin.Models.Vouchers;

namespace TalonAdmin.Controllers.Api
{
    /// <summary>
    /// Draws the random numbers behind voucher codes and SMS confirmation codes.
    /// </summary>
    /// <remarks>
    /// <para>
    /// These values are bearer instruments: whoever holds a voucher code can redeem it, so the
    /// numbers have to be unpredictable and evenly spread across the whole code space.
    /// </para>
    /// <para>
    /// The original implementation drew eight cryptographic bytes, rendered them as a decimal
    /// string, and cut <c>digits</c> characters from a randomly chosen offset. That leaked
    /// uniformity in three ways, all measured against the replacement below:
    /// </para>
    /// <list type="bullet">
    /// <item><description>
    /// It called <c>GetNonZeroBytes</c>, which never yields 0x00, so the underlying 64-bit value
    /// was drawn from a biased subset and its decimal rendering was 19 or 20 digits ~95% of the
    /// time rather than uniformly distributed.
    /// </description></item>
    /// <item><description>
    /// Cutting a substring inherits the leading-digit skew of that rendering. Over 500,000 draws
    /// of a 6-digit code, codes beginning with '1' came up 13.3% of the time against 10% expected,
    /// while '0', '8' and '9' were each under-represented — a measurable narrowing of the space an
    /// attacker has to search.
    /// </description></item>
    /// <item><description>
    /// The offset was chosen with <c>new Random()</c>, a non-cryptographic PRNG, and its upper
    /// bound was <c>Length - digits - 1</c>. That is one lower than the largest usable offset, so
    /// the final digit position was never selected, and the expression goes negative once
    /// <c>digits</c> approaches the rendering's length: at a code length of 18 the constructor
    /// threw <c>ArgumentOutOfRangeException</c> on 5% of calls.
    /// </description></item>
    /// </list>
    /// <para>
    /// Rejection sampling replaces all of it. Draws that fall in the final, partial block of the
    /// random range are discarded rather than folded back in with a modulo, which is what keeps
    /// every code equally likely. The loop is expected to run about once and is bounded in
    /// practice: the discarded fraction is always under half the range.
    /// </para>
    /// </remarks>
    public static class RandomNumber
    {
        /// <summary>
        /// Beyond 18 digits a code no longer fits in the <see cref="long"/> the callers and the
        /// VoucherCode column use, so reject it here rather than overflow silently.
        /// </summary>
        private const int MaxDigits = 18;

        public static long RandomLong(int digits)
        {
            if (digits < 1 || digits > MaxDigits)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(digits), digits, $"Code length must be between 1 and {MaxDigits} digits.");
            }

            // The number of distinct codes of this length, e.g. 1,000,000 for the default 6.
            ulong codeSpace = 1;
            for (var i = 0; i < digits; i++)
            {
                codeSpace *= 10;
            }

            // Reducing a 64-bit draw modulo the code space would favour the low end of the range,
            // because the draw range is not an exact multiple of it. Discarding the final, partial
            // block leaves a whole number of cycles, which every code is then equally likely to land
            // in. A power of ten never divides a power of two, so that block is never empty and the
            // arithmetic below never yields a zero bound.
            var largestWholeMultiple = ulong.MaxValue - (ulong.MaxValue % codeSpace);

            ulong draw;
            do
            {
                draw = NextUInt64();
            }
            while (draw >= largestWholeMultiple);

            return (long)(draw % codeSpace);
        }

        private static ulong NextUInt64()
        {
            Span<byte> buffer = stackalloc byte[sizeof(ulong)];

            // Fill, unlike the GetNonZeroBytes the original used, draws every byte from the full
            // 0-255 range. Excluding zero is what skewed the old distribution.
            RandomNumberGenerator.Fill(buffer);

            return BitConverter.ToUInt64(buffer);
        }
    }

    [Authorize]
    [Route("api/VoucherWorkflow")]
    public class VoucherWorkflowController : ControllerBase
    {
        private static object syncObj = new Object();

        private readonly Models.Vouchers.Context ctx;
        private readonly Models.Admin.AdminContext adminContext;
        private readonly IConfiguration configuration;
        private readonly Messaging.VoucherNotifications notifications;
        private readonly Messaging.InboundMessageProcessor inboundMessages;

        public VoucherWorkflowController(
            Models.Vouchers.Context ctx,
            Models.Admin.AdminContext adminContext,
            IConfiguration configuration,
            Messaging.VoucherNotifications notifications,
            Messaging.InboundMessageProcessor inboundMessages)
        {
            this.ctx = ctx;
            this.adminContext = adminContext;
            this.configuration = configuration;
            this.notifications = notifications;
            this.inboundMessages = inboundMessages;
        }

        [HttpPost("AssignVoucherBook")]
        public async Task<IActionResult> AssignVoucherBook([FromBody]JObject request)
        {
            int beneficiaryId = request.Value<int>("BeneficiaryId");
            int distributionId = request.Value<int>("DistributionId");
            int serialNumber = request.Value<int>("SerialNumber");

            var distribution = ctx.Distributions.Where(d => d.Id == distributionId).First();

            // NOTE: this used to project the vouchers out of VoucherTransactionRecords and then call
            // Distinct(). EF Core drops an Include that sits behind a projection, and the ordering
            // below needs Category, so the same set of vouchers is selected directly from Vouchers
            // (which is already distinct, hence no Distinct() call).
            var vouchers = await ctx.Vouchers
                .Include(v => v.Category)
                .Where(v => v.DistributionId == distribution.Id &&
                    v.TransactionRecords.Any(t => t.Type == 1 && t.BeneficiaryId == beneficiaryId))
                .ToArrayAsync();

            vouchers = vouchers.OrderBy(v => v.Category.ValidAfter).ToArray();

            for (int i = 0; i < vouchers.Count(); i++)
            {
                var currentSerialNumber = serialNumber + i;
                var voucher = vouchers[i];
                voucher.SequentialCode = currentSerialNumber.ToString();
            }

            await ctx.SaveChangesAsync();

            return Ok();
        }

        [HttpPost("GenerateVouchers")]
        public async Task GenerateVouchers([FromBody]JObject request)
        {
            int distributionId = request.Value<int>("DistributionId");

            var distribution = ctx.Distributions
                .Include(d => d.Categories)
                .Include(d => d.Vouchers)
                .Where(d => d.Id == distributionId).First();

            // NOTE: the original projected the per-category counts and summed them client side; that
            // shape is not translatable by EF Core, the nested Sum below produces the same number.
            var totalVouchers = ctx.Distributions.Where(d => d.Id == distributionId)
                .Sum(d => d.Categories.Sum(c => c.NumberOfVouchers));

            if (distribution.Vouchers.Count() == totalVouchers)
            {
                return;
            }

            var codes = ctx.Vouchers.Select(c => c.VoucherCode).ToArray();
            int numberOfVouchers = distribution.Categories.Select(c => c.NumberOfVouchers - c.IssuedVouchers).Sum();
            if (numberOfVouchers < 0)
            {
                return;
            }

            var checkSet = new HashSet<string>(codes);
            var allCodes = new HashSet<string>(
                    Enumerable.Range(0, numberOfVouchers)
                        .AsParallel()
                        .Select(c => RandomNumber.RandomLong(distribution.VoucherCodeLength).ToString("D" + distribution.VoucherCodeLength))
                    );

            allCodes = new HashSet<string>(allCodes.Except(checkSet).Take(numberOfVouchers));
            checkSet = new HashSet<string>(allCodes.Union(checkSet));

            while (allCodes.Count() < numberOfVouchers)
            {
                long curValue = RandomNumber.RandomLong(distribution.VoucherCodeLength);
                string stringValue = curValue.ToString("D" + distribution.VoucherCodeLength);
                while (checkSet.Contains(stringValue))
                {
                    curValue = RandomNumber.RandomLong(distribution.VoucherCodeLength);
                    stringValue = curValue.ToString("D" + distribution.VoucherCodeLength);
                }
                checkSet.Add(stringValue);
                allCodes.Add(stringValue);
            }

            var codeStack = new Stack<string>(allCodes);

            foreach (var category in distribution.Categories.Where(c => c.NumberOfVouchers > c.IssuedVouchers).ToArray())
            {
                int typeId = category.TypeId;
                decimal value = category.Value ?? 0;

                var vouchers = Enumerable.Range(0, category.NumberOfVouchers - category.IssuedVouchers).Select((i) =>
                    new Models.Vouchers.Voucher
                    {
                        CategoryId = category.Id,
                        Value = value,
                        DistributionId = distributionId,
                        VoucherCode = codeStack.Pop(),
                        CountryId = distribution.CountryId,
                        OrganizationId = distribution.OrganizationId,
                        Status = 1,
                    }
                ).ToArray();

                ctx.Vouchers.AddRange(vouchers);

                category.IssuedVouchers = category.NumberOfVouchers;
            }

            await ctx.SaveChangesAsync();
        }

        [HttpPost("ResendSMS")]
        public async Task<IActionResult> ResendSMS([FromBody]JObject request)
        {
            int beneficiaryId = request.Value<int>("BeneficiaryId");
            int voucherId = request.Value<int>("VoucherId");

            SendVoucherSms(beneficiaryId, voucherId);

            return Ok();
        }

        [HttpPost("CancelVoucher")]
        public async Task<IActionResult> CancelVoucher([FromBody]JObject request)
        {
            int voucherId = request.Value<int>("VoucherId");

            // Lazy loading is off: the cancellation message walks the issuing transaction record,
            // its beneficiary and its vendor, so the graph has to be loaded up front.
            var voucher = await ctx.Vouchers
                .Include(v => v.Category).ThenInclude(c => c.Type)
                .Include(v => v.TransactionRecords).ThenInclude(t => t.Beneficiary)
                .Include(v => v.TransactionRecords).ThenInclude(t => t.Vendor)
                .Where(v => v.Id == voucherId).FirstAsync();
            voucher.Status = 4;
            await ctx.SaveChangesAsync();

            SendCancelledVoucher(voucher);

            return Ok();
        }

        [HttpPost("DistributeVouchers")]
        public async Task<IActionResult> DistributeVouchers([FromBody]DistributeVouchersBindingModel request)
        {
            int programId = request.ProgramId;
            int groupId = request.GroupId;
            int locationId = request.LocationId;
            var user = await CurrentUser();

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            if (user == null)
            {
                return Unauthorized();
            }

            var program = await ctx.Programs
                .Include("Categories")
                .Where(v => v.Id == programId).FirstAsync();

            var group = await ctx.BeneficiaryGroups.Where(g => g.Id == groupId).FirstAsync();
            var location = await ctx.Locations.Where(g => g.Id == locationId).FirstOrDefaultAsync();
            var distributionCount = await ctx.Distributions.Where(d => d.GroupId == groupId && d.ProgramId == programId).CountAsync();

            var distribution = new Distribution
            {
                Title = String.Format("Distribution {0} - {1}", distributionCount + 1, group.Name),

                CountryId = program.CountryId,
                OrganizationId = program.OrganizationId,
                Number = distributionCount + 1,

                ProgramId = program.Id,
                GroupId = groupId,
                Location = location,
                VoucherCodeLength = program.VoucherCodeLength,



                CreatedBy = user.UserName,
                ModifiedBy = user.UserName,
                CreatedOn = DateTime.UtcNow,
                ModifiedOn = DateTime.UtcNow,

                Categories = program.Categories.ToArray().Select(c => new DistributionVoucherCategory
                {
                    CountryId = c.CountryId,
                    OrganizationId = c.OrganizationId,
                    TypeId = c.TypeId,
                    Value = c.Value,
                    VendorTypeId = c.VendorTypeId,
                    IssuedVouchers = 0,
                    NumberOfVouchers = 0,
                    ValidAfter = CalculateOffset(DateTime.Now, c.ValidAfterOffsetType, c.ValidAfterOffset)
                }).ToList()
            };
            ctx.Distributions.Add(distribution);

            int result = await ctx.SaveChangesAsync();
            if (result == 0)
            {
                return StatusCode(500);
            }

            return await AssignToGroup(JObject.FromObject(new
            {
                DistributionId = distribution.Id,
                GroupId = groupId
            }));
        }

        [HttpPost("AssignToGroup")]
        public async Task<IActionResult> AssignToGroup([FromBody]JObject request)
        {
            int distributionId = request.Value<int>("DistributionId");
            int groupId = request.Value<int>("GroupId");


            var isAssigning = await ctx.DistributionLogs.Where(l => l.EndedOn == null && l.DistributionId == distributionId).AnyAsync();

            if (isAssigning)
            {
                return Ok("Already assigning");
            }

            var count = await ctx.Beneficiaries
                        .Include("Group")
                        .Where(b => b.GroupId == groupId && b.Disabled != true).CountAsync();
            // Categories is walked right below, so it has to be included now that lazy loading is off.
            var distribution = ctx.Distributions.Include("Program").Include("Categories").Where(d => d.Id == distributionId).First();
            foreach (var category in distribution.Categories)
            {
                category.NumberOfVouchers += count;
            }

            var beneficiaries = await ctx.Beneficiaries
                        .Include("Group")
                        .Where(b => b.GroupId == groupId && b.Disabled != true).AsQueryable().ToListAsync();

            beneficiaries.Where(b => String.IsNullOrEmpty(b.CardKey)).ToList().ForEach(b => b.GenerateKey());

            await ctx.SaveChangesAsync();

            await GenerateVouchers(JObject.FromObject(new { DistributionId = distributionId }));



            var distributionLog = new DistributionLog
            {
                DistributionId = distributionId,
                DateTime = DateTime.UtcNow,
                AffectedBeneficiaries = beneficiaries.Count(),
                CountryId = this.GetCountryId(),
                OrganizationId = this.GetOrganizationId(),
                StartedOn = DateTime.UtcNow
            };

            ctx.DistributionLogs.Add(distributionLog);
            await ctx.SaveChangesAsync();

            var voucherQuery = from v in ctx.Vouchers.AsNoTracking()
                               where v.DistributionId == distribution.Id &&
                               v.TransactionRecords.Count() == 0
                               select v;

            // NOTE: the original grouped by the Category entity in the query. EF Core cannot translate a
            // GroupBy that projects the groups themselves, and the loop below only uses the key to index
            // the dictionary, so the grouping is done in memory on CategoryId.
            var voucherDictionary = voucherQuery.AsEnumerable()
                .GroupBy(v => v.CategoryId)
                .ToDictionary(k => k.Key, v => v.Take(beneficiaries.Count()));
            var transactionRecords = new List<VoucherTransactionRecord>();

            foreach (var category in voucherDictionary.Keys)
            {
                var voucherStack = new Stack<Voucher>(voucherDictionary[category]);
                foreach (var beneficiary in beneficiaries.ToList())
                {
                    if (!voucherStack.Any()) break;

                    var voucherId = voucherStack.Pop().Id;
                    var voucher = await ctx.Vouchers.Where(v => v.Id == voucherId).FirstAsync();
                    var transactionRecord = new Models.Vouchers.VoucherTransactionRecord();

                    transactionRecord.Beneficiary = beneficiary;
                    transactionRecord.BeneficiaryId = beneficiary.Id;
                    transactionRecord.Type = 1;
                    transactionRecord.VoucherId = voucher.Id;
                    transactionRecord.Voucher = voucher;
                    transactionRecord.Value = voucher.Value;
                    transactionRecord.OrganizationId = voucher.OrganizationId;
                    transactionRecord.CountryId = voucher.CountryId;


                    transactionRecords.Add(transactionRecord);
                }
            }

            ctx.VoucherTransactionRecords.AddRange(transactionRecords);

            await ctx.SaveChangesAsync();

            if (distribution.Program.DistributionMechanism == 1) // SMS
            {
                try
                {
                    foreach (var tr in transactionRecords)
                    {
                        SendVoucherSms(tr.Beneficiary.Id, tr.Voucher.Id);
                    }
                }
                catch
                {

                }
            }

            var finishedDistribution = ctx.DistributionLogs.Where(l => l.EndedOn == null && l.DistributionId == distributionId).First();
            finishedDistribution.EndedOn = DateTime.UtcNow;

            ctx.SaveChanges();


            return Ok();
        }

        // NOTE: [OverrideAuthentication] has no ASP.NET Core equivalent; [AllowAnonymous] alone
        // already lets an unauthenticated gateway post to this endpoint.
        // NOTE: the legacy route template ended in a '/', which Core routing does not need since it
        // matches request paths with or without the trailing slash.
        [HttpPost("ValidateTransactionSMS/{countryCode}")]
        [AllowAnonymous]
        [EnableRateLimiting(RateLimitPolicies.SmsRedemption)]
        public async Task<IActionResult> ValidateTransactionSMS([FromBody]IncomingSmsBindingModel request,
            string countryCode,
            [FromQuery] string secret = "")
        {
            // Checked before the country is looked up. The other order let an unauthenticated caller
            // discover which countries a deployment runs by watching which codes came back
            // "Not a valid country" and which got as far as asking for a secret.
            switch (InboundWebhookSecret.Check(configuration, secret))
            {
                case InboundWebhookSecret.Result.NotConfigured:
                    return StatusCode(
                        StatusCodes.Status503ServiceUnavailable,
                        InboundWebhookSecret.NotConfiguredMessage);

                case InboundWebhookSecret.Result.Invalid:
                    return BadRequest(InboundWebhookSecret.InvalidMessage);
            }

            var countryId = adminContext.Countries.AsNoTracking()
                .Where(o => o.IsoAlpha2.ToLower() == countryCode.ToLower().Trim())
                .Select(o => o.Id)
                .FirstOrDefault();

            if (countryId == 0)
            {
                return Ok("Not a valid country");
            }

            var result = await inboundMessages.ProcessAsync(request.From, request.Message, countryId);

            // Every outcome is a 200: an SMS gateway treats a non-200 as a delivery failure and
            // redelivers, which would replay the redemption.
            return Ok(result.Reason);
        }


        #region Private Functions and Methods

        private DateTime? CalculateOffset(DateTime date, int? offsetType, int? offset)
        {
            if (offsetType == null || offset == null)
                return date;

            if (offsetType == 2)
                return date.AddDays((double)offset);
            else if (offsetType == 3)
                return date.AddDays((double)offset * 7d);

            return date;
        }

        private async Task<TalonAdmin.Models.Admin.ApplicationUser> CurrentUser()
        {
            // WhoAmI loads the same graph the old query did (Countries, Countries.Country, Roles,
            // Organization) and returns null when the id does not resolve to a user.
            return await this.WhoAmI(adminContext);
        }

        /// <summary>
        /// Composes and queues a beneficiary's voucher code. The template lookup, substitution and
        /// queueing all live in <see cref="Messaging.VoucherNotifications"/> now, so the same
        /// wording is used no matter which path issued the voucher.
        /// </summary>
        private void SendVoucherSms(int beneficiaryId, int voucherId)
        {
            var voucher = ctx.Vouchers
                .Include(v => v.Category).ThenInclude(c => c.Type)
                .Include(v => v.TransactionRecords).ThenInclude(t => t.Beneficiary)
                .Include(v => v.TransactionRecords).ThenInclude(t => t.Vendor)
                .First(v => v.Id == voucherId);

            notifications.VoucherIssuedAsync(voucher).GetAwaiter().GetResult();
        }

        private void SendCancelledVoucher(Models.Vouchers.Voucher voucher)
        {
            notifications.VoucherCancelledForBeneficiaryAsync(voucher).GetAwaiter().GetResult();
        }


        #endregion
    }
}
