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
using TalonAdmin.Extensions;
using System.Net;
using System.Net.Http;

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

        [Route("GenerateVouchers")]
        public async Task<IHttpActionResult> GenerateVouchers(dynamic request)
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
                    return BadRequest("This distribution has been already fulfilled.");
                }

                var codes = ctx.Vouchers.Select(c => c.VoucherCode).ToArray();
                int numberOfVouchers = distribution.Categories.Select(c => c.NumberOfVouchers - c.IssuedVouchers).Sum();
                if (numberOfVouchers < 0)
                    numberOfVouchers = 0;

                var checkSet = new HashSet<string>(codes);
                var allCodes = new HashSet<string>(
                        Enumerable.Range(0, (int)Math.Pow(10, distribution.VoucherCodeLength))
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
                            DistributionId = distributionId,
                            VoucherCode = codeStack.Pop(),
                            CountryId = distribution.CountryId,
                            OrganizationId = distribution.OrganizationId
                        }
                    ).ToArray();

                    ctx.BulkInsert(vouchers);

                    category.IssuedVouchers = category.NumberOfVouchers;
                }

                await ctx.SaveChangesAsync();

                return Ok();
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
                var voucher = ctx.Vouchers.Where(v => v.Id == voucherId).First();
                SendCancelledVoucher(voucher);
            }

            return Ok();
        }

        [Route("AssignToGroup")]
        public async Task<IHttpActionResult> AssignToGroup(dynamic request)
        {
            int distributionId = request.DistributionId;
            int groupId = request.GroupId;

            using (var ctx = new Models.Vouchers.Context())
            {
                var count = await ctx.Beneficiaries
                            .Include("Group")
                            .Where(b => b.GroupId == groupId && b.Disabled != true).CountAsync();
                var distribution = ctx.Distributions.Where(d => d.Id == distributionId).First();
                foreach (var category in distribution.Categories)
                {
                    category.NumberOfVouchers += count;
                }

                await ctx.SaveChangesAsync();
            }

            await GenerateVouchers(new { DistributionId = distributionId });

            using (var ctx = new Models.Vouchers.Context())
            {
                var beneficiaries = await ctx.Beneficiaries
                            .Include("Group")
                            .Where(b => b.GroupId == groupId && b.Disabled != true).AsQueryable().ToListAsync();

                var distributionLog = new DistributionLog
                {
                    DistributionId = distributionId,
                    DateTime = DateTime.UtcNow,
                    AffectedBeneficiaries = beneficiaries.Count(),
                    CountryId = this.GetCountryId(),
                    OrganizationId = this.GetOrganizationId(),
                };

                ctx.DistributionLogs.Add(distributionLog);
                await ctx.SaveChangesAsync();

                var distribution = ctx.Distributions.Where(d => d.Id == distributionId).First();
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

                        var voucher = voucherStack.Pop();
                        var transactionRecord = new Models.Vouchers.VoucherTransactionRecord();

                        transactionRecord.Beneficiary = beneficiary;

                        transactionRecord.BeneficiaryId = beneficiary.Id;
                        transactionRecord.Status = 0;
                        transactionRecord.VoucherId = voucher.Id;
                        transactionRecord.Voucher = voucher;
                        transactionRecord.OrganizationId = voucher.OrganizationId;
                        transactionRecord.CountryId = voucher.CountryId;


                        transactionRecords.Add(transactionRecord);
                    }
                }

                ctx.BulkInsert(transactionRecords);

                ctx.BulkInsert(beneficiaries.Select(b =>
                    new Models.Vouchers.BeneficiaryDistribution
                    {
                        BeneficiaryId = b.Id,
                        DistributionId = distribution.Id
                    }));

                await ctx.SaveChangesAsync();

                foreach (var tr in transactionRecords)
                {
                    SendVoucherSms(tr.Beneficiary.Id, tr.Voucher.Id);
                }
            }


            return Ok();
        }

        [Route("ValidateTransactionSMS")]
        [OverrideAuthentication]
        [AllowAnonymous]
        public async Task<IHttpActionResult> ValidateTransactionSMS([FromBody]IncomingSmsBindingModel request, [FromUri] string secret = "")
        {
            var systemSecret = ConfigurationManager.AppSettings["SystemSecret"] ?? "";

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
                        DateTime = DateTime.Now
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
                                  where v.MobileNumber == phoneNumber
                                  select v;


                var voucherQuery = from vc in db.Vouchers
                                   where vc.VoucherCode == voucherCode
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

                var transactionRecord = voucher.TransactionRecords.First();
                transactionRecord.LastModifiedOn = DateTime.UtcNow;
                db.SaveChanges();

                if (voucher.Category.VendorTypeId != null &&
                    voucher.Category.VendorTypeId != vendor.TypeId)
                {
                    // Voucher limited to another category that doesn't match vendor
                    VendorCannotUseVoucher(voucher, vendor);

                    return Ok("Wrong Vendor Type");
                }
                else if (transactionRecord.Status == 2)
                {
                    // Voucher used already
                    VoucherAlreadyUsed(voucher, vendor);

                    return Ok("Voucher Already Used");
                }
                else if (transactionRecord.Status == 3)
                {
                    // Voucher canceled
                    VoucherCancelled(voucher, vendor);

                    return Ok("Voucher Canceled");
                }
                else if (onlyNumbers.Replace(transactionRecord.Beneficiary.NationalId, "") != onlyNumbers.Replace(nationalId, ""))
                {
                    // Wrong national id
                    WrongNationalId(voucher, vendor);

                    return Ok("Wrong National Id Canceled");
                }
                else
                {
                    transactionRecord.VendorId = vendor.Id;
                    transactionRecord.Status = 2;
                    transactionRecord.ConfirmationCode = RandomNumber.RandomLong(6).ToString("D6");
                    transactionRecord.FinalizedOn = DateTime.UtcNow;
                    db.SaveChanges();

                    ConfirmTransaction(voucher);

                    Ok("Confirmed");
                }
            }

            return Ok();
        }

        private void SendVoucherSms(int beneficiaryId, int voucherId)
        {
            using (var ctx = new Models.Vouchers.Context())
            {
                var beneficiary = ctx.Beneficiaries.Include("Group").Where(b => b.Id == beneficiaryId).First();
                var voucher = ctx.Vouchers.Include("Category").Include("Category.Type").Where(b => b.Id == voucherId).First();

                var transactionRecord = voucher.TransactionRecords.First();
                var model = new { Voucher = voucher, Vendor = transactionRecord.Vendor, Beneficiary = transactionRecord.Beneficiary };

                var beneficiaryMessage = CompileMessage("Beneficiary Voucher Message", voucher.CountryId, voucher.OrganizationId, model);

                SendAsyncMessage(transactionRecord.Beneficiary.MobileNumber, transactionRecord.Beneficiary.Name, beneficiaryMessage, transactionRecord.CountryId, transactionRecord.OrganizationId);
            }
        }

        private void SendCancelledVoucher(Models.Vouchers.Voucher voucher)
        {
            var transactionRecord = voucher.TransactionRecords.First();
            var model = new { Voucher = voucher, Vendor = transactionRecord.Vendor, Beneficiary = transactionRecord.Beneficiary };

            var message = CompileMessage("Beneficiary Canceled Voucher Message", voucher.CountryId, voucher.OrganizationId, model);

            SendAsyncMessage(transactionRecord.Beneficiary.MobileNumber, transactionRecord.Beneficiary.Name, message, transactionRecord.CountryId, transactionRecord.OrganizationId);
        }


        private void VoucherCancelled(Models.Vouchers.Voucher voucher, Models.Vouchers.Vendor vendor)
        {
            var transactionRecord = voucher.TransactionRecords.First();
            var model = new { Voucher = voucher, Vendor = vendor, Beneficiary = transactionRecord.Beneficiary };

            var vendorMessage = CompileMessage("Vendor Cancelled Message", voucher.CountryId, voucher.OrganizationId, model);

            SendAsyncMessage(vendor.MobileNumber, vendor.Name, vendorMessage, vendor.CountryId, voucher.OrganizationId);
        }

        private void VoucherAlreadyUsed(Models.Vouchers.Voucher voucher, Models.Vouchers.Vendor vendor)
        {
            var transactionRecord = voucher.TransactionRecords.First();
            var model = new { Voucher = voucher, Vendor = transactionRecord.Vendor, Beneficiary = transactionRecord.Beneficiary };

            var vendorMessage = CompileMessage("Vendor Already Used Message", voucher.CountryId, voucher.OrganizationId, model);
            var beneficiaryMessage = CompileMessage("Beneficiary Already Used Message", voucher.CountryId, voucher.OrganizationId, model);

            SendAsyncMessage(transactionRecord.Vendor.MobileNumber, transactionRecord.Vendor.Name, vendorMessage, transactionRecord.CountryId, transactionRecord.OrganizationId);
            SendAsyncMessage(transactionRecord.Beneficiary.MobileNumber, transactionRecord.Beneficiary.Name, beneficiaryMessage, transactionRecord.CountryId, transactionRecord.OrganizationId);
        }

        private void VendorCannotUseVoucher(Models.Vouchers.Voucher voucher, Models.Vouchers.Vendor vendor)
        {
            var transactionRecord = voucher.TransactionRecords.First();
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

            var transactionRecord = voucher.TransactionRecords.First();
            var model = new { 
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
                    var organization = ctx.OrganizationCountries.AsNoTracking().Where(o => o.Id == organizationId && o.CountryId == countryId).FirstOrDefault();
                    organizationMessage = organization.Settings.PropertyCollection.Where(p=>p.Name == key).Select(p=> p.Value).FirstOrDefault();
                }

                countryMessage = country.Settings.PropertyCollection.Where(p => p.Name == key).Select(p => p.Value).FirstOrDefault();
                try
                {

                    var message = String.IsNullOrEmpty(organizationMessage) ? countryMessage : organizationMessage;

                    return Engine.Razor.RunCompile(message, Guid.NewGuid().ToString(), null, model);
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
                if (country.Settings.SmsBackendType == 0) {
                    // Sending through RSMS

                    ThreadPool.QueueUserWorkItem((state) =>
                    {
                        try
                        {
                            Utils.RescueSMSClient.CreateContactAndSendMessageAsync(
                                name,
                                to,
                                message,
                                ""
                             ).Wait();
                        }
                        catch { }
                    });
                }
                else if (country.Settings.SmsBackendType == 1)
                {
                    // Sending through TurboSMS

                    var service = new Soap.TurboSMS.Service();
                    var cookieJar = new CookieContainer();
                    service.CookieContainer = cookieJar;

                    var authResponse = service.Auth(country.Settings.ServiceUser, country.Settings.ServicePassword);
                    var result = service.SendSMS("Talon", to, message, "");
                }
                else if (country.Settings.SmsBackendType == 2) {

                    var urlCall = String.Format(country.Settings.ServiceUrl, to, message);
                    HttpClient client = new HttpClient();

                    var result = client.GetAsync(urlCall).Result;
                }

            } 
            catch 
            { 
            }
        }
    }
}