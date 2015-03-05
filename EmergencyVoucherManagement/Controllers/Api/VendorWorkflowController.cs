using EmergencyVoucherManagement.Models.BindingModels;
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

namespace EmergencyVoucherManagement.Controllers.Api
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
                var checkSet = new HashSet<long>(codes);

                foreach (var category in distribution.Categories.Where(c => c.NumberOfVouchers > c.IssuedVouchers))
                {
                    int typeId = category.TypeId;
                    decimal value = category.Value ?? 0;

                    for (int i = 0; i < category.NumberOfVouchers - category.IssuedVouchers; i++)
                    {
                        long curValue = RandomNumber.RandomLong(distribution.VoucherCodeLength);
                        while (checkSet.Contains(curValue) || curValue < 10001)
                        {
                            curValue = RandomNumber.RandomLong(distribution.VoucherCodeLength);
                        }
                        checkSet.Add(curValue);

                        var voucher = new Models.Vouchers.Voucher
                        {
                            TypeId = typeId,
                            Value = value,
                            DistributionId = distributionId,
                            VoucherCode = curValue
                        };

                        ctx.Vouchers.Add(voucher);
                        ctx.SaveChanges();
                    };
                    category.IssuedVouchers = category.NumberOfVouchers;
                }
                await ctx.SaveChangesAsync();

                return Ok();
            }
        }

        [Route("AssignToGroup")]
        public async Task<IHttpActionResult> AssignToGroup(dynamic request)
        {
            int distributionId = request.DistributionId;
            int groupId = request.GroupId;

            using (var ctx = new Models.Vouchers.Context())
            {
                var beneficiaries = ctx.Beneficiaries
                    .Where(b => b.GroupId == groupId && !b.Distributions.Where(d => d.DistributionId == distributionId).Any());
                var distribution = ctx.Distributions.Where(d => d.Id == distributionId).First();

                foreach (var beneficiary in beneficiaries.ToList())
                {
                    foreach (var category in distribution.Categories.ToList())
                    {
                        var transactionRecord = new Models.Vouchers.VoucherTransactionRecord();
                        var voucher = ctx.Vouchers.Where(v => v.DistributionId == distribution.Id &&
                            v.TypeId == category.TypeId &&
                            v.Value == category.Value &&
                            v.TransactionRecord == null).First();

                        transactionRecord.BeneficiaryId = beneficiary.Id;
                        transactionRecord.Status = 0;
                        voucher.TransactionRecord = transactionRecord;

                        ctx.VoucherTransactionRecords.Add(transactionRecord);
                        await SendVoucherSms(beneficiary, voucher);

                    }

                    ctx.BeneficiaryDistributions.Add(new Models.Vouchers.BeneficiaryDistribution
                    {
                        BeneficiaryId = beneficiary.Id,
                        DistributionId = distribution.Id
                    });

                    await ctx.SaveChangesAsync();
                }
            }


            return Ok();
        }

        private async Task SendVoucherSms(Models.Vouchers.Beneficiary beneficiary, Models.Vouchers.Voucher voucher)
        {
            dynamic request = new JObject();

            request.MobileNumber = "+" + beneficiary.MobileNumber;
            request.Name = beneficiary.Name;
            request.Groups = beneficiary.Group.Name + "," + "Beneficiary";
            request.Message = String.Format("Your {0} voucher for {1} has been issued: {2}", voucher.Type.Name, voucher.Value, voucher.VoucherCode);

            await Utils.RescueSMSClient.CreateContactAndSendMessageAsync(request);
        }


        [Route("ValidateTransactionSMS")]
        [OverrideAuthentication]
        [AllowAnonymous]
        public IHttpActionResult ValidateTransactionSMS(IncomingSmsBindingModel request)
        {
            string[] codes = request.Message.ToString().Split(' ').Where(s => !String.IsNullOrEmpty(s)).ToArray();
            string from = request.From;
            long voucherCode = 0L;
            var nationalId = "";

            if (codes.Length >= 2)
            {
                if (!Int64.TryParse(codes[codes.Length - 2], out voucherCode))
                    return BadRequest();

                nationalId = codes[codes.Length - 1];
            }

            var phoneNumber = Regex.Replace(from, "[^\\d]", "").Trim();

            using (var db = new Models.Vouchers.Context())
            {
                var vendorQuery = from v in db.Vendors
                                  where v.MobileNumber == phoneNumber
                                  select v;

                if (vendorQuery.Count() == 1)
                {
                    var vendor = vendorQuery.First();
                    var voucherQuery = from vc in db.Vouchers
                                       where vc.VoucherCode == voucherCode
                                       && vc.TransactionRecord != null
                                       && vc.TransactionRecord.Status < 2
                                       select vc;

                    if (voucherQuery.Count() == 1)
                    {
                        var voucher = voucherQuery.First();
                        if (voucher.TransactionRecord.Beneficiary.NationalId == nationalId)
                        {
                            voucher.TransactionRecord.VendorId = vendor.Id;
                            voucher.TransactionRecord.Status = 2;
                            voucher.TransactionRecord.ConfirmationCode = RandomNumber.RandomLong(7);
                            db.SaveChanges();

                            ConfirmTransaction(voucher);
                        }
                    }
                    else
                    {
                        VoucherIsInvalid(vendor, voucherCode.ToString());
                    }

                }
                else
                {
                    UnauthorizedPhone(phoneNumber);
                }
            }

            return Ok();
        }


        [Route("AssignVoucherToBeneficiary")]
        public IHttpActionResult AssignVoucherToBeneficiary(Models.BindingModels.AssignVoucherBidingModel request)
        {
            using (var db = new Models.Vouchers.Context())
            {
                var beneficiaryQuery = db.Beneficiaries.Where(b => b.Id == request.BeneficiaryId);
                var voucherQuery = db.Vouchers.Where(v => v.Id == request.VoucherId);

                if (!beneficiaryQuery.Any() || !voucherQuery.Any())
                    return BadRequest("Invalid beneficiary or voucher");

                var voucher = voucherQuery.First();
                var beneficiary = beneficiaryQuery.First();


                if (voucher.TransactionRecord != null)
                    return BadRequest("Voucher is being used by another beneficiary");

                var verificationItem = new Models.Vouchers.VoucherTransactionRecord
                {
                    Beneficiary = beneficiary,
                    Status = 1,
                };

                db.VoucherTransactionRecords.Add(verificationItem);
                db.SaveChanges();

                return Ok();
            }
        }

        private void UnauthorizedPhone(string from)
        {
            var context = Microsoft.AspNet.SignalR.GlobalHost.ConnectionManager.GetHubContext<Hubs.DashboardHub>();
            context.Clients.All.message("error", "Unauthorized Phone", String.Format("An attempt to claim a voucher was made from {0} which is not a recognize phone", from));


            ThreadPool.QueueUserWorkItem((state) =>
            {
                Utils.RescueSMSClient.CreateContactAndSendMessageAsync("Unknown", from, "This number is not authorized to claim vouchers").Wait();
            });
        }

        private void VoucherIsInvalid(Models.Vouchers.Vendor vendor, string voucherCode)
        {
            var context = Microsoft.AspNet.SignalR.GlobalHost.ConnectionManager.GetHubContext<Hubs.DashboardHub>();
            context.Clients.All.message("error", "Unauthorized Phone", String.Format("An attempt to claim a voucher was made from {0}, with the incorrect vocuher code {1}", vendor.Name, voucherCode));

            string name = vendor.OwnerName;
            string mobileNumber = vendor.MobileNumber;
            ThreadPool.QueueUserWorkItem((state) =>
            {
                Utils.RescueSMSClient.CreateContactAndSendMessageAsync(
                    name,
                    mobileNumber,
                    "This number is not authorized to claim vouchers",
                    "Vendors").Wait();
            });

        }

        private void ConfirmTransaction(Models.Vouchers.Voucher voucher)
        {
            var context = Microsoft.AspNet.SignalR.GlobalHost.ConnectionManager.GetHubContext<Hubs.DashboardHub>();

            context.Clients.All.message("info", "Incoming voucher", "Confirmed voucher!");
            context.Clients.All.updateDashboard();


            string confirmationCode = voucher.TransactionRecord.ConfirmationCode.ToString();
            string vendorName = voucher.TransactionRecord.Vendor.OwnerName;
            string vendorMobileNumber = voucher.TransactionRecord.Vendor.MobileNumber;
            ThreadPool.QueueUserWorkItem((state) =>
            {
                Utils.RescueSMSClient.CreateContactAndSendMessageAsync(
                    vendorName,
                    vendorMobileNumber,
                    String.Format("Your account has been credited. Here is your confirmation code: {0}", confirmationCode),
                    "Vendors").Wait();
            });

            string beneficiaryName = voucher.TransactionRecord.Beneficiary.Name;
            string beneficiaryMobileNumber = voucher.TransactionRecord.Beneficiary.MobileNumber;
            ThreadPool.QueueUserWorkItem((state) =>
            {
                Utils.RescueSMSClient.CreateContactAndSendMessageAsync(
                    beneficiaryName,
                    beneficiaryMobileNumber,
               String.Format("Your voucher has been debited. Here is your confirmation code: {0}", confirmationCode),
               "Beneficiaries").Wait();
            });
        }

    }
}