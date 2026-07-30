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
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using TalonAdmin.Extensions;
using TalonAdmin.Hubs;
using TalonAdmin.Models.Vouchers;

namespace TalonAdmin.Controllers.Api
{
    public static class RandomNumber
    {
        private static readonly RandomNumberGenerator _generator = RandomNumberGenerator.Create();

        public static long RandomLong(int digits)
        {
            byte[] randomNumber = new byte[sizeof(UInt64)];

            _generator.GetNonZeroBytes(randomNumber);

            var massiveNumber = (UInt64)BitConverter.ToUInt64(randomNumber, 0);

            var massiveNumberString = massiveNumber.ToString();

            return Int64.Parse(massiveNumberString.Substring(new Random().Next(0, massiveNumberString.Length - digits - 1), digits));
        }
    }

    [Authorize]
    [Route("api/VoucherWorkflow")]
    public class VoucherWorkflowController : ControllerBase
    {
        private static object syncObj = new Object();

        /// <summary>
        /// Matches the "@Model.A.B" / "@(Model.A.B)" expressions the stored SMS templates use.
        /// </summary>
        private static readonly Regex modelExpression =
            new Regex(@"@\(?Model((?:\.[A-Za-z_][A-Za-z0-9_]*)+)\)?", RegexOptions.Compiled);

        private readonly Models.Vouchers.Context ctx;
        private readonly Models.Admin.AdminContext adminContext;
        private readonly IHubContext<DashboardHub> dashboardHub;
        private readonly IWebHostEnvironment environment;
        private readonly IConfiguration configuration;

        public VoucherWorkflowController(
            Models.Vouchers.Context ctx,
            Models.Admin.AdminContext adminContext,
            IHubContext<DashboardHub> dashboardHub,
            IWebHostEnvironment environment,
            IConfiguration configuration)
        {
            this.ctx = ctx;
            this.adminContext = adminContext;
            this.dashboardHub = dashboardHub;
            this.environment = environment;
            this.configuration = configuration;
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
        public async Task<IActionResult> ValidateTransactionSMS([FromBody]IncomingSmsBindingModel request,
            string countryCode,
            [FromQuery] string secret = "")
        {
            var systemSecret = configuration["SystemSecret"] ?? "";
            int countryId = 0;

            countryId = adminContext.Countries.AsNoTracking().Where(o => o.IsoAlpha2.ToLower() == countryCode.ToLower().Trim()).Select(o => o.Id).FirstOrDefault();

            if (countryId == 0)
            {
                return Ok("Not a valid country");
            }

            if (systemSecret != secret && !String.IsNullOrEmpty(systemSecret))
            {
                return BadRequest("Secret is invalid.");
            }

            try
            {
                // NOTE: the message log used to be written through its own DbContext, so a failure here
                // could not affect the rest of the request. The context is request scoped now, which
                // means a failed SaveChanges leaves the log entry pending on the shared context.
                ctx.MessageLogs.Add(new MessageLog
                {
                    MobileNumber = request.From,
                    Message = request.Message,
                    DateTime = DateTime.UtcNow,
                    CountryId = countryId
                });

                ctx.SaveChanges();
            }
            catch
            {
            }

            var onlyNumbers = new Regex("[^\\d]");
            string numericalMessage = onlyNumbers.Replace(request.Message, "");

            string[] codes = request.Message.ToString().Split(' ').Where(s => !String.IsNullOrEmpty(s)).ToArray();
            string from = request.From;
            string voucherCode = "";
            var nationalId = "";

            if (codes.Length >= 2)
            {
                voucherCode = onlyNumbers.Replace(codes[codes.Length - 2], "");
                nationalId = codes[codes.Length - 1];
            }

            var phoneNumber = "+" + Regex.Replace(from, "[^\\d]", "").Trim();

            var db = ctx;

            var vendorQuery = from v in db.Vendors
                              where (v.MobileNumber == phoneNumber && v.CountryId == countryId) ||
                              v.SalesPersons.Where(s => s.MobileNumber == phoneNumber && s.CountryId == countryId).Any()
                              select v;

            // Lazy loading is off: the checks below walk Category, the issuing transaction record's
            // beneficiary and the latest credit transaction record's vendor.
            var voucherQuery = from vc in db.Vouchers
                                    .Include(v => v.Category).ThenInclude(c => c.Type)
                                    .Include(v => v.TransactionRecords).ThenInclude(t => t.Beneficiary)
                                    .Include(v => v.TransactionRecords).ThenInclude(t => t.Vendor)
                               where vc.VoucherCode == voucherCode && vc.CountryId == countryId
                               && vc.TransactionRecords.Any()
                               select vc;

            var vendor = await vendorQuery.FirstOrDefaultAsync();
            var voucher = await voucherQuery.FirstOrDefaultAsync();

            if (vendor == null && voucher != null)
            {
                // valid voucher invalid vendor
                UnauthorizedPhone(phoneNumber);

                return Ok("Unauthorized Vendor");
            }

            if (vendor != null && voucher == null)
            {
                // Valid Vendor invalid Voucher
                VoucherIsInvalid(vendor, voucherCode.ToString());

                return Ok("Invalid Voucher Code");
            }

            if (vendor == null && voucher == null)
            {
                return Ok("Not a talon message");
            }



            if (voucher.Category.VendorTypeId != null &&
                voucher.Category.VendorTypeId != vendor.TypeId)
            {
                // Voucher limited to another category that doesn't match vendor
                VendorCannotUseVoucher(voucher, vendor);

                return Ok("Wrong Vendor Type");
            }
            else if (voucher.Status == 3 && (DateTime.UtcNow - voucher.LatestCreditTransactionRecords.LastModifiedOn.Value).TotalMinutes > 30)
            {
                // Voucher used already
                VoucherAlreadyUsed(voucher, vendor);

                return Ok("Voucher Already Used");
            }
            else if (voucher.Status == 3 && (DateTime.UtcNow - voucher.LatestCreditTransactionRecords.LastModifiedOn.Value).TotalMinutes < 30)
            {
                // Voucher used already
                ResendConfirmationCode(voucher, vendor);

                return Ok("Confirmation code resent");
            }
            else if (voucher.Status == 4)
            {
                // Voucher canceled
                VoucherCancelled(voucher, vendor);

                return Ok("Voucher Canceled");
            }
            else if (onlyNumbers.Replace(voucher.IssuingTransactionRecord.Beneficiary.NationalId, "") != onlyNumbers.Replace(nationalId, ""))
            {
                // Wrong national id
                WrongNationalId(voucher, vendor);

                return Ok("Wrong National Id Canceled");
            }
            else
            {
                var transactionRecord = new VoucherTransactionRecord()
                {
                    VoucherId = voucher.Id,
                    BeneficiaryId = voucher.IssuingTransactionRecord.BeneficiaryId,
                    VendorId = vendor.Id,
                    Type = 2,
                    ConfirmationCode = RandomNumber.RandomLong(6).ToString("D6"),
                    LastModifiedOn = DateTime.UtcNow,
                    CreatedOn = DateTime.UtcNow,
                    Value = voucher.Value, // SMS claims full price,
                    CountryId = voucher.CountryId,
                    OrganizationId = voucher.OrganizationId
                };

                db.VoucherTransactionRecords.Add(transactionRecord);
                voucher.Status = 3;

                db.SaveChanges();

                ConfirmTransaction(voucher);

                // NOTE: pre-existing bug, the result of this call is discarded and the method falls
                // through to the Ok() below.
                Ok("Confirmed");
            }

            return Ok();
        }


        #region Private Functions and Methods

        private Task NotifyClients()
        {
            // Still delegates to MobileClientController's helper as it always did; that helper now
            // takes the hub context and hosting environment because ASP.NET Core SignalR has no
            // GlobalHost equivalent to reach for.
            return MobileClientController.NotifyClients(dashboardHub, environment);
        }

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

        private void ResendConfirmationCode(Voucher voucher, Vendor vendor)
        {
            var transactionRecord = voucher.LatestCreditTransactionRecords;
            var model = new
            {
                Voucher = voucher,
                Vendor = transactionRecord.Vendor,
                Beneficiary = transactionRecord.Beneficiary,
                TransactionRecord = transactionRecord
            };

            var vendorMessage = CompileMessage("Vendor Confirmed Message", voucher.CountryId, voucher.OrganizationId, model);

            SendAsyncMessage(transactionRecord.Vendor.MobileNumber, transactionRecord.Vendor.Name, vendorMessage, transactionRecord.CountryId, transactionRecord.OrganizationId);
        }

        private void SendVoucherSms(int beneficiaryId, int voucherId)
        {
            var beneficiary = ctx.Beneficiaries.Include("Group").Where(b => b.Id == beneficiaryId).First();
            // The issuing transaction record and its beneficiary/vendor are walked below, so they are
            // included explicitly now that lazy loading is off.
            var voucher = ctx.Vouchers
                .Include(v => v.Category).ThenInclude(c => c.Type)
                .Include(v => v.TransactionRecords).ThenInclude(t => t.Beneficiary)
                .Include(v => v.TransactionRecords).ThenInclude(t => t.Vendor)
                .Where(b => b.Id == voucherId).First();

            var transactionRecord = voucher.IssuingTransactionRecord;
            var model = new { Voucher = voucher, Vendor = transactionRecord.Vendor, Beneficiary = transactionRecord.Beneficiary };

            var beneficiaryMessage = CompileMessage("Beneficiary Voucher Message", voucher.CountryId, voucher.OrganizationId, model);

            SendAsyncMessage(transactionRecord.Beneficiary.MobileNumber, transactionRecord.Beneficiary.Name, beneficiaryMessage, transactionRecord.CountryId, transactionRecord.OrganizationId);
        }

        private void SendCancelledVoucher(Models.Vouchers.Voucher voucher)
        {
            var transactionRecord = voucher.IssuingTransactionRecord;
            var model = new { Voucher = voucher, Vendor = transactionRecord.Vendor, Beneficiary = transactionRecord.Beneficiary };

            var message = CompileMessage("Beneficiary Cancelled Voucher Message", voucher.CountryId, voucher.OrganizationId, model);

            SendAsyncMessage(transactionRecord.Beneficiary.MobileNumber, transactionRecord.Beneficiary.Name, message, transactionRecord.CountryId, transactionRecord.OrganizationId);
        }

        private void VoucherCancelled(Models.Vouchers.Voucher voucher, Models.Vouchers.Vendor vendor)
        {
            var transactionRecord = voucher.LatestCreditTransactionRecords;
            var model = new { Voucher = voucher, Vendor = vendor, Beneficiary = transactionRecord.Beneficiary };

            var vendorMessage = CompileMessage("Vendor Cancelled Message", voucher.CountryId, voucher.OrganizationId, model);

            SendAsyncMessage(vendor.MobileNumber, vendor.Name, vendorMessage, vendor.CountryId, voucher.OrganizationId);
        }

        private void VoucherAlreadyUsed(Models.Vouchers.Voucher voucher, Models.Vouchers.Vendor vendor)
        {
            var transactionRecord = voucher.LatestCreditTransactionRecords;
            var model = new { Voucher = voucher, Vendor = transactionRecord.Vendor, Beneficiary = transactionRecord.Beneficiary };

            var vendorMessage = CompileMessage("Vendor Already Used Message", voucher.CountryId, voucher.OrganizationId, model);
            var beneficiaryMessage = CompileMessage("Beneficiary Already Used Message", voucher.CountryId, voucher.OrganizationId, model);

            SendAsyncMessage(transactionRecord.Vendor.MobileNumber, transactionRecord.Vendor.Name, vendorMessage, transactionRecord.CountryId, transactionRecord.OrganizationId);
            SendAsyncMessage(transactionRecord.Beneficiary.MobileNumber, transactionRecord.Beneficiary.Name, beneficiaryMessage, transactionRecord.CountryId, transactionRecord.OrganizationId);
        }

        private void VendorCannotUseVoucher(Models.Vouchers.Voucher voucher, Models.Vouchers.Vendor vendor)
        {
            var transactionRecord = voucher.IssuingTransactionRecord;
            var model = new { Voucher = voucher, Vendor = vendor, Beneficiary = transactionRecord.Beneficiary };

            var vendorMessage = CompileMessage("Vendor Cannot Accept Message", voucher.CountryId, voucher.OrganizationId, model);
            var beneficiaryMessage = CompileMessage("Beneficiary Cannot Accept Message", voucher.CountryId, voucher.OrganizationId, model);

            SendAsyncMessage(vendor.MobileNumber, vendor.Name, vendorMessage, transactionRecord.CountryId, transactionRecord.OrganizationId);
            SendAsyncMessage(transactionRecord.Beneficiary.MobileNumber, transactionRecord.Beneficiary.Name, beneficiaryMessage, transactionRecord.CountryId, transactionRecord.OrganizationId);
        }

        private void UnauthorizedPhone(string from)
        {
        }

        private void WrongNationalId(Voucher voucher, Vendor vendor)
        {
            var model = new { Voucher = voucher, Vendor = vendor, Beneficiary = new { } };

            var message = CompileMessage("Wrong National Id Message", vendor.CountryId, voucher.OrganizationId, model);

            SendAsyncMessage(vendor.MobileNumber, vendor.Name, message, vendor.CountryId, voucher.OrganizationId);
        }

        private void VoucherIsInvalid(Models.Vouchers.Vendor vendor, string voucherCode)
        {
            var model = new { Voucher = new { }, Vendor = vendor, Beneficiary = new { }, VoucherCode = voucherCode };

            var message = CompileMessage("Invalid Voucher Message", vendor.CountryId, null, model);

            SendAsyncMessage(vendor.MobileNumber, vendor.Name, message, vendor.CountryId);
        }

        private void ConfirmTransaction(Models.Vouchers.Voucher voucher)
        {
            // Fire-and-forget, as the EF6 version was: a dashboard refresh must not block the caller.
            _ = NotifyClients();

            var transactionRecord = voucher.TransactionRecords.Where(t => t.Type == 2).OrderByDescending(o => o.LastModifiedOn).FirstOrDefault();
            var model = new
            {
                Voucher = voucher,
                Vendor = transactionRecord.Vendor,
                Beneficiary = transactionRecord.Beneficiary,
                TransactionRecord = transactionRecord
            };

            var beneficiaryMessage = CompileMessage("Beneficiary Confirmed Message", voucher.CountryId, voucher.OrganizationId, model);
            var vendorMessage = CompileMessage("Vendor Confirmed Message", voucher.CountryId, voucher.OrganizationId, model);

            SendAsyncMessage(transactionRecord.Vendor.MobileNumber, transactionRecord.Vendor.Name, vendorMessage, transactionRecord.CountryId, transactionRecord.OrganizationId);
            SendAsyncMessage(transactionRecord.Beneficiary.MobileNumber, transactionRecord.Beneficiary.Name, beneficiaryMessage, transactionRecord.CountryId, transactionRecord.OrganizationId);
        }

        private string CompileMessage(string key, int countryId, int? organizationId, object model)
        {
            // Settings/PropertyCollection are read below, so both have to be included explicitly.
            var country = adminContext.Countries.AsNoTracking()
                .Include("Settings")
                .Include("Settings.PropertyCollection")
                .Where(c => c.Id == countryId).FirstOrDefault();
            string organizationMessage = "";
            string countryMessage = "";

            if (organizationId != null)
            {
                var organization = adminContext.OrganizationCountries.AsNoTracking()
                    .Include("Settings")
                    .Include("Settings.PropertyCollection")
                    .Where(o => o.OrganizationId == organizationId && o.CountryId == countryId).FirstOrDefault();
                if (organization != null)
                    organizationMessage = organization.Settings.PropertyCollection.Where(p => p.Name == key).Select(p => p.Value).FirstOrDefault();
            }

            countryMessage = country.Settings.PropertyCollection.Where(p => p.Name == key).Select(p => p.Value).FirstOrDefault();
            try
            {

                var message = String.IsNullOrEmpty(organizationMessage) ? countryMessage : organizationMessage;

                // NOTE: RazorEngine has no build for current .NET, so the stored templates are no longer
                // compiled as Razor. The substitution below covers the "@Model.A.B" expressions these
                // one-line SMS templates use; anything relying on Razor control flow will not render.
                return RenderTemplate(message, model);
            }
            catch
            {
                return "";
            }
        }

        /// <summary>
        /// Minimal stand-in for RazorEngine: replaces "@Model.A.B" / "@(Model.A.B)" with the value
        /// read off the model by reflection.
        /// </summary>
        private static string RenderTemplate(string template, object model)
        {
            if (String.IsNullOrEmpty(template))
                return template;

            return modelExpression.Replace(template, match =>
            {
                object current = model;

                foreach (var member in match.Groups[1].Value.Split('.').Where(m => !String.IsNullOrEmpty(m)))
                {
                    if (current == null)
                        return "";

                    PropertyInfo property = current.GetType().GetProperty(member);
                    if (property == null)
                        return "";

                    current = property.GetValue(current);
                }

                return current == null ? "" : Convert.ToString(current, CultureInfo.CurrentCulture);
            });
        }

        private void SendAsyncMessage(string to, string name, string message, int countryId, int? organizationId = null)
        {
            string senderName = "Talon";
            Models.Admin.Country country = null;

            try
            {

                if (organizationId != null)
                {
                    senderName = adminContext.Organizations.Where(o => o.Id == organizationId.Value).First().Abbreviation;
                }

                country = adminContext.Countries.Include("Settings")
                    .Include("Settings.PropertyCollection")
                    .Where(c => c.Id == countryId).FirstOrDefault();
            }
            catch
            {
            }

            if (country == null)
                return;

            try
            {
                if (country.Settings.SmsBackendType == 0)
                {
                    // Sending through RSMS
                    var baseUrl = country.Settings.ServiceUrl;
                    var user = country.Settings.ServiceUser;
                    var password = country.Settings.ServicePassword;

                    if (String.IsNullOrEmpty(baseUrl) || String.IsNullOrEmpty(user) || String.IsNullOrEmpty(password))
                    {
                        // NOTE: RescueSMSClient no longer has a parameterless constructor that falls back to
                        // ConfigurationManager.AppSettings, so the same fallback is read from configuration here.
                        baseUrl = configuration["RescueSMS:URL"];
                        user = configuration["RescueSMS:User"];
                        password = configuration["RescueSMS:Password"];
                    }

#pragma warning disable 4014
                    Task.Factory.StartNew(() =>
                    {
                        try
                        {
                            var client = new Utils.RescueSMSClient(baseUrl, user, password);

                            client.CreateContactAndSendMessageAsync(
                                name,
                                to,
                                message,
                                ""
                             ).ConfigureAwait(false);
                        }
                        catch { }
                    }).ConfigureAwait(false);
#pragma warning restore 4014
                }
                else if (country.Settings.SmsBackendType == 1)
                {
                    // Sending through TurboSMS
                    var service = new Soap.TurboSMS.Service();
                    var cookieJar = new CookieContainer();
                    service.CookieContainer = cookieJar;

                    var signature = country.Settings.PropertyCollection.Where(s => s.Name == "TurboSMS Signature").Select(s => s.Value).FirstOrDefault() ?? "Talon";

                    var authResponse = service.Auth(country.Settings.ServiceUser, country.Settings.ServicePassword);
                    var result = service.SendSMS(signature, to, message, "");
                }
                else if (country.Settings.SmsBackendType == 2)
                {
                    // Clickatell
                    var isUnicode = message.Any(c => c > 255);

                    if (isUnicode)
                    {
                        var bytes = System.Text.Encoding.GetEncoding("UTF-16BE").GetBytes(message);
                        message = string.Concat(bytes.Select(b => b.ToString("X2")));
                    }

                    var urlCall = String.Format(country.Settings.ServiceUrl + (isUnicode ? "&unicode=1" : ""), to, message);
                    HttpClient client = new HttpClient();

                    var result = client.GetAsync(urlCall).Result;
                }

            }
            catch
            {
            }
        }
        #endregion
    }
}
