﻿using TalonAdmin.Models.BindingModels;
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
                await SendVoucherSms(beneficiaryId, voucherId);
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
                var beneficiaries = await ctx.Beneficiaries
                            .Include("Group")
                            .Where(b => b.GroupId == groupId).AsQueryable().ToListAsync();
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

                transactionRecords.AsParallel().ForAll( t => {
                    SendVoucherSms(t.Beneficiary.Id, t.Voucher.Id).ContinueWith((task)=> task.Dispose());
                });
            }


            return Ok();
        }

        private async Task SendVoucherSms(int beneficiaryId, int voucherId)
        {
            try
            {
                using (var ctx = new Models.Vouchers.Context())
                {
                    var beneficiary = ctx.Beneficiaries.Include("Group").Where(b => b.Id == beneficiaryId).First();
                    var voucher = ctx.Vouchers.Include("Category").Include("Category.Type").Where(b => b.Id == voucherId).First();

                    dynamic request = new JObject();

                    request.MobileNumber = "+" + beneficiary.MobileNumber;
                    request.Name = beneficiary.Name;
                    request.Groups = beneficiary.Group.Name + "," + "Beneficiary";
                    request.Message = String.Format("Your {0} voucher for {1} has been issued: {2}", voucher.Category.Type.Name, voucher.Category.Value, voucher.VoucherCode);

                    await Utils.RescueSMSClient.CreateContactAndSendMessageAsync(request); 
                }
            }
            catch
            {
            }
        }


        [Route("ValidateTransactionSMS")]
        [OverrideAuthentication]
        [AllowAnonymous]
        public IHttpActionResult ValidateTransactionSMS(IncomingSmsBindingModel request)
        {
            string[] codes = request.Message.ToString().Split(' ').Where(s => !String.IsNullOrEmpty(s)).ToArray();
            string from = request.From;
            string voucherCode = "";
            var nationalId = "";

            if (codes.Length >= 2)
            {
                voucherCode = codes[codes.Length - 2];
                nationalId = codes[codes.Length - 1];
            }
            else
            {
                return BadRequest();
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
                                       && vc.TransactionRecords.Any() 
                                       select vc;

                    if (voucherQuery.Count() == 1)
                    {
                        var voucher = voucherQuery.First();
                        var transactionRecord = voucher.TransactionRecords.First();

                        if (voucher.Distribution.Vendors.Count() > 0 &&
                            !voucher.Distribution.Vendors.Where(v => v.VendorId == vendor.Id).Any())
                        {
                            VendorCannotUseVoucher(voucher, vendor);
                        }
                        else if (transactionRecord.Status == 2)
                        {
                            VoucherAlreadyUsed(voucher, vendor);
                        }
                        else if (transactionRecord.Status == 3)
                        {
                            VoucherCancelled(voucher, vendor);
                        }
                        else if (transactionRecord.Beneficiary.NationalId == nationalId)
                        {
                            transactionRecord.VendorId = vendor.Id;
                            transactionRecord.Status = 2;
                            transactionRecord.ConfirmationCode = RandomNumber.RandomLong(7);
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

        private void VoucherCancelled(Models.Vouchers.Voucher voucher, Models.Vouchers.Vendor vendor)
        {
            string voucherCode = voucher.VoucherCode.ToString();
            string vendorName = vendor.OwnerName;
            string vendorMobileNumber = vendor.MobileNumber;
            ThreadPool.QueueUserWorkItem((state) =>
            {
                Utils.RescueSMSClient.CreateContactAndSendMessageAsync(
                    vendorName,
                    vendorMobileNumber,
                    String.Format("This voucher ({0}) was cancelled.", voucherCode),
                    "Vendors").Wait();
            });

            string beneficiaryName = voucher.TransactionRecords.First().Beneficiary.Name;
            string beneficiaryMobileNumber = voucher.TransactionRecords.First().Beneficiary.MobileNumber;
            ThreadPool.QueueUserWorkItem((state) =>
            {
                Utils.RescueSMSClient.CreateContactAndSendMessageAsync(
                    beneficiaryName,
                    beneficiaryMobileNumber,
                    String.Format("This voucher ({0}) was cancelled.", voucherCode),
                    "Beneficiaries").Wait();
            });
        }

        private void VoucherAlreadyUsed(Models.Vouchers.Voucher voucher, Models.Vouchers.Vendor vendor)
        {
            string voucherCode = voucher.VoucherCode.ToString();
            string vendorName = vendor.OwnerName;
            string vendorMobileNumber = vendor.MobileNumber;
            ThreadPool.QueueUserWorkItem((state) =>
            {
                Utils.RescueSMSClient.CreateContactAndSendMessageAsync(
                    vendorName,
                    vendorMobileNumber,
                    String.Format("This voucher ({0}) has already been used.", voucherCode),
                    "Vendors").Wait();
            });

            string beneficiaryName = voucher.TransactionRecords.First().Beneficiary.Name;
            string beneficiaryMobileNumber = voucher.TransactionRecords.First().Beneficiary.MobileNumber;
            ThreadPool.QueueUserWorkItem((state) =>
            {
                Utils.RescueSMSClient.CreateContactAndSendMessageAsync(
                    beneficiaryName,
                    beneficiaryMobileNumber,
                    String.Format("This voucher ({0}) has already been used.", voucherCode),
                    "Beneficiaries").Wait();
            });
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


                if (voucher.TransactionRecords.Any() )
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

        private void VendorCannotUseVoucher(Models.Vouchers.Voucher voucher, Models.Vouchers.Vendor vendor)
        {
            var context = Microsoft.AspNet.SignalR.GlobalHost.ConnectionManager.GetHubContext<Hubs.DashboardHub>();
            context.Clients.All.message("error", "Unauthorized Phone", String.Format("An attempt to claim a voucher was made from {0}, with the incorrect vocuher type ({1}).", vendor.Name, voucher.Category.Type.Name));

            string vendorName = vendor.OwnerName;
            string vendorMobileNumber = vendor.MobileNumber;
            ThreadPool.QueueUserWorkItem((state) =>
            {
                Utils.RescueSMSClient.CreateContactAndSendMessageAsync(
                    vendorName,
                    vendorMobileNumber,
                    "You are not allowed to claim this voucher.",
                    "Vendors").Wait();
            });

            string beneficiaryName = voucher.TransactionRecords.First().Beneficiary.Name;
            string beneficiaryMobileNumber = voucher.TransactionRecords.First().Beneficiary.MobileNumber;
            ThreadPool.QueueUserWorkItem((state) =>
            {
                Utils.RescueSMSClient.CreateContactAndSendMessageAsync(
                    beneficiaryName,
                    beneficiaryMobileNumber,
                    "This vendor is not allowed to take this type of vouchers.",
                    "Beneficiaries").Wait();
            });
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

        private void SendCancelledVoucher(Models.Vouchers.Voucher voucher)
        {
            var name = voucher.TransactionRecords.First().Beneficiary.Name;
            var voucherCode = voucher.VoucherCode;
            var mobileNumber = voucher.TransactionRecords.First().Beneficiary.MobileNumber;


            ThreadPool.QueueUserWorkItem((state) =>
            {
                Utils.RescueSMSClient.CreateContactAndSendMessageAsync(
                    name,
                    mobileNumber,
                    String.Format("This voucher ({0}) has been canceled.", voucherCode),
                    "Beneficiaries").Wait();
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


            string confirmationCode = voucher.TransactionRecords.First().ConfirmationCode.ToString();
            string vendorName = voucher.TransactionRecords.First().Vendor.OwnerName;
            string vendorMobileNumber = voucher.TransactionRecords.First().Vendor.MobileNumber;
            ThreadPool.QueueUserWorkItem((state) =>
            {
                Utils.RescueSMSClient.CreateContactAndSendMessageAsync(
                    vendorName,
                    vendorMobileNumber,
                    String.Format("Your account has been credited. Here is your confirmation code: {0}", confirmationCode),
                    "Vendors").Wait();
            });

            string beneficiaryName = voucher.TransactionRecords.First().Beneficiary.Name;
            string beneficiaryMobileNumber = voucher.TransactionRecords.First().Beneficiary.MobileNumber;
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