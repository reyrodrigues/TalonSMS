using TalonAdmin.Models.BindingModels;
using Microsoft.AspNet.Identity;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.Web.Http;
using System.Transactions;
using EntityFramework.BulkInsert.Extensions;
using TalonAdmin.Models.Vouchers;
using System.Data.Entity;
using System.Diagnostics;
using TalonAdmin.Extensions;
using System.Configuration;
using RazorEngine.Templating;
using RazorEngine;
using System.Net;
using System.Net.Http;

using Microsoft.AspNet.Identity.EntityFramework;
using Microsoft.AspNet.Identity.Owin;


namespace TalonAdmin.Controllers.Api
{
    public static class RandomNumber
    {
        private static readonly RNGCryptoServiceProvider _generator = new RNGCryptoServiceProvider();

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
    [RoutePrefix("api/VoucherWorkflow")]
    public class VoucherWorkflowController : ApiController
    {
        private static object syncObj = new Object();

        [Route("AssignVoucherBook")]
        public async Task<IHttpActionResult> AssignVoucherBook(dynamic request)
        {
            int beneficiaryId = request.BeneficiaryId;
            int distributionId = request.DistributionId;
            int serialNumber = request.SerialNumber;

            using (var ctx = new Models.Vouchers.Context())
            {
                var distribution = ctx.Distributions.Where(d => d.Id == distributionId).First();
                var vouchers = await ctx.VoucherTransactionRecords.Where(t =>
                    t.Type == 1 &&
                    t.Voucher.DistributionId == distribution.Id &&
                    t.BeneficiaryId == beneficiaryId
                    )
                    .Select(v => v.Voucher)
                    .Distinct()
                    .ToArrayAsync();

                for (int i = 0; i < vouchers.Count(); i++)
                {
                    var currentSerialNumber = serialNumber + i;
                    var voucher = vouchers[i];
                    voucher.SequentialCode = currentSerialNumber.ToString();
                }

                await ctx.SaveChangesAsync();
            }
            return Ok();
        }

        [Route("GenerateVouchers")]
        public async Task GenerateVouchers(dynamic request)
        {
            int distributionId = request.DistributionId;

            using (var ctx = new Models.Vouchers.Context())
            {
                var distribution = ctx.Distributions.Where(d => d.Id == distributionId).First();
                var totalVouchers = ctx.Distributions.Where(d => d.Id == distributionId)
                    .Select(d => d.Categories.Select(c => c.NumberOfVouchers))
                    .Sum(d => d.Sum());

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
        }

        [Route("ResendSMS")]
        public async Task<IHttpActionResult> ResendSMS(dynamic request)
        {
            int beneficiaryId = request.BeneficiaryId;
            int voucherId = request.VoucherId;

            using (var ctx = new Models.Vouchers.Context())
            {
                SendVoucherSms(beneficiaryId, voucherId);
            }

            return Ok();
        }

        [Route("CancelVoucher")]
        public async Task<IHttpActionResult> CancelVoucher(dynamic request)
        {
            int voucherId = request.VoucherId;

            using (var ctx = new Models.Vouchers.Context())
            {
                var voucher = await ctx.Vouchers.Where(v => v.Id == voucherId).FirstAsync();
                voucher.Status = 4;
                await ctx.SaveChangesAsync();

                SendCancelledVoucher(voucher);
            }

            return Ok();
        }

        [Route("DistributeVouchers")]
        public async Task<IHttpActionResult> DistributeVouchers(DistributeVouchersBindingModel request)
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

            using (var ctx = new Models.Vouchers.Context())
            {
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
                    return InternalServerError();
                }

                return await AssignToGroup(JObject.FromObject(new
                {
                    DistributionId = distribution.Id,
                    GroupId = groupId
                }));
            }
        }

        [Route("AssignToGroup")]
        public async Task<IHttpActionResult> AssignToGroup(dynamic request)
        {
            int distributionId = request.DistributionId;
            int groupId = request.GroupId;

            var context = Microsoft.AspNet.SignalR.GlobalHost.ConnectionManager.GetHubContext<Hubs.DashboardHub>();

            context.Clients.All.lockAssignment(distributionId);

            using (var ctx = new Models.Vouchers.Context())
            {

                var isAssigning = await ctx.DistributionLogs.Where(l => l.EndedOn == null && l.DistributionId == distributionId).AnyAsync();

                if (isAssigning)
                {
                    return Ok("Already assigning");
                }

                var count = await ctx.Beneficiaries
                            .Include("Group")
                            .Where(b => b.GroupId == groupId && b.Disabled != true).CountAsync();
                var distribution = ctx.Distributions.Include("Program").Where(d => d.Id == distributionId).First();
                foreach (var category in distribution.Categories)
                {
                    category.NumberOfVouchers += count;
                }

                var beneficiaries = await ctx.Beneficiaries
                            .Include("Group")
                            .Where(b => b.GroupId == groupId && b.Disabled != true).AsQueryable().ToListAsync();

                beneficiaries.Where(b => String.IsNullOrEmpty(b.CardKey)).ToList().ForEach(b => b.GenerateKey());

                await ctx.SaveChangesAsync();

                await GenerateVouchers(new { DistributionId = distributionId });



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
                                   group v by v.Category into g
                                   select g;

                var voucherDictionary = voucherQuery.ToDictionary(k => k.Key, v => v.Take(beneficiaries.Count()));
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
            }

            using (var ctx = new Models.Vouchers.Context())
            {
                var finishedDistribution = ctx.DistributionLogs.Where(l => l.EndedOn == null && l.DistributionId == distributionId).First();
                finishedDistribution.EndedOn = DateTime.UtcNow;

                ctx.SaveChanges();
            }


            context.Clients.All.unlockAssignment(distributionId);

            return Ok();
        }

        [Route("ValidateTransactionSMS/{countryCode}/")]
        [OverrideAuthentication]
        [AllowAnonymous]
        public async Task<IHttpActionResult> ValidateTransactionSMS([FromBody]IncomingSmsBindingModel request,
            string countryCode,
            [FromUri] string secret = "")
        {
            var systemSecret = ConfigurationManager.AppSettings["SystemSecret"] ?? "";
            int countryId = 0;

            using (var adminContext = new Models.Admin.AdminContext())
            {
                countryId = adminContext.Countries.AsNoTracking().Where(o => o.IsoAlpha2.ToLower() == countryCode.ToLower().Trim()).Select(o => o.Id).FirstOrDefault();
            }

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
                using (var ctx = new Models.Vouchers.Context())
                {
                    ctx.MessageLogs.Add(new MessageLog
                    {
                        MobileNumber = request.From,
                        Message = request.Message,
                        DateTime = DateTime.UtcNow,
                        CountryId = countryId
                    });

                    ctx.SaveChanges();
                }
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

            using (var db = new Models.Vouchers.Context())
            {
                var vendorQuery = from v in db.Vendors
                                  where (v.MobileNumber == phoneNumber && v.CountryId == countryId) ||
                                  v.SalesPersons.Where(s=>s.MobileNumber == phoneNumber && s.CountryId == countryId).Any()
                                  select v;


                var voucherQuery = from vc in db.Vouchers.Include("TransactionRecords")
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

                    Ok("Confirmed");
                }
            }

            return Ok();
        }


        #region Private Functions and Methods

        private DateTime? CalculateOffset(DateTime date, int? offsetType, int? offset)
        {
            if (offsetType == null || offset == null)
                return null;

            if (offsetType == 2)
                return date.AddDays((double)offset);
            else if (offsetType == 3)
                return date.AddDays((double)offset * 7d);

            return date;
        }

        private async Task<TalonAdmin.Models.Admin.ApplicationUser> CurrentUser()
        {
            var userManager = Request.GetOwinContext().GetUserManager<ApplicationUserManager>();

            using (var admin = new Models.Admin.AdminContext())
            {
                admin.Configuration.ProxyCreationEnabled = false;
                admin.Configuration.LazyLoadingEnabled = false;

                string userId = User.Identity.GetUserId();
                if (admin.Users.Where(u => u.Id == userId).Any())
                {
                    var user = (await admin.Users
                        .Include("Countries")
                        .Include("Roles")
                        .Include("Countries.Country")
                        .Include("Organization")
                        .Where(u => u.Id == userId)
                        .ToListAsync()).First();

                    return user;
                }
            }

            return null;
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
            using (var ctx = new Models.Vouchers.Context())
            {
                var beneficiary = ctx.Beneficiaries.Include("Group").Where(b => b.Id == beneficiaryId).First();
                var voucher = ctx.Vouchers.Include("Category").Include("Category.Type").Where(b => b.Id == voucherId).First();

                var transactionRecord = voucher.IssuingTransactionRecord;
                var model = new { Voucher = voucher, Vendor = transactionRecord.Vendor, Beneficiary = transactionRecord.Beneficiary };

                var beneficiaryMessage = CompileMessage("Beneficiary Voucher Message", voucher.CountryId, voucher.OrganizationId, model);

                SendAsyncMessage(transactionRecord.Beneficiary.MobileNumber, transactionRecord.Beneficiary.Name, beneficiaryMessage, transactionRecord.CountryId, transactionRecord.OrganizationId);
            }
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
            var context = Microsoft.AspNet.SignalR.GlobalHost.ConnectionManager.GetHubContext<Hubs.DashboardHub>();

            context.Clients.All.message("success", "Incoming voucher", "Confirmed voucher!");
            context.Clients.All.updateDashboard();

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
            using (var ctx = new Models.Admin.AdminContext())
            {
                var country = ctx.Countries.AsNoTracking().Where(c => c.Id == countryId).FirstOrDefault();
                string organizationMessage = "";
                string countryMessage = "";

                if (organizationId != null)
                {
                    var organization = ctx.OrganizationCountries.AsNoTracking().Where(o => o.OrganizationId == organizationId && o.CountryId == countryId).FirstOrDefault();
                    if (organization != null)
                        organizationMessage = organization.Settings.PropertyCollection.Where(p => p.Name == key).Select(p => p.Value).FirstOrDefault();
                }

                countryMessage = country.Settings.PropertyCollection.Where(p => p.Name == key).Select(p => p.Value).FirstOrDefault();
                try
                {

                    var message = String.IsNullOrEmpty(organizationMessage) ? countryMessage : organizationMessage;
                    var hash = Convert.ToBase64String(MD5CryptoServiceProvider.Create().ComputeHash(System.Text.Encoding.UTF8.GetBytes(message)));
                    if (!Engine.Razor.IsTemplateCached(hash, null))
                    {
                        Engine.Razor.Compile(message, hash);
                    }

                    return Engine.Razor.RunCompile(hash, null, model);
                }
                catch
                {
                    return "";
                }
            }
        }

        private void SendAsyncMessage(string to, string name, string message, int countryId, int? organizationId = null)
        {
            string senderName = "Talon";
            Models.Admin.Country country = null;

            try
            {

                using (var ctx = new Models.Admin.AdminContext())
                {
                    if (organizationId != null)
                    {
                        senderName = ctx.Organizations.Where(o => o.Id == organizationId.Value).First().Abbreviation;
                    }

                    country = ctx.Countries.Include("Settings")
                        .Include("Settings.PropertyCollection")
                        .Where(c => c.Id == countryId).FirstOrDefault();
                }
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

#pragma warning disable 4014
                    Task.Factory.StartNew(() =>
                    {
                        try
                        {
                            var client = new Utils.RescueSMSClient();
                            if (!String.IsNullOrEmpty(baseUrl) && !String.IsNullOrEmpty(user) && !String.IsNullOrEmpty(password))
                            {
                                client.BaseUrl = baseUrl;
                                client.User = user;
                                client.Password = password;
                            }

                            client.CreateContactAndSendMessageAsync(
                                name,
                                to,
                                message,
                                ""
                             ).ConfigureAwait(false);
                        }
                        catch { }
                    }).ConfigureAwait(false);
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