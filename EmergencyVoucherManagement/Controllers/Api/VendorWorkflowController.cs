using EmergencyVoucherManagement.Models.BindingModels;
using Microsoft.AspNet.Identity;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
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

                foreach (var category in distribution.Categories.Where(c=>c.NumberOfVouchers > c.IssuedVouchers))
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

                foreach (var beneficiary in beneficiaries)
                {
                    foreach (var category in distribution.Categories)
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
                    }

                    ctx.BeneficiaryDistributions.Add(new Models.Vouchers.BeneficiaryDistribution { 
                        BeneficiaryId = beneficiary.Id,
                        DistributionId = distribution.Id
                    });
                }

                await ctx.SaveChangesAsync();
            }            


            return Ok();
        }


        [Route("ValidateTransactionSMS")]
        [OverrideAuthentication]
        [AllowAnonymous]
        public IHttpActionResult ValidateTransactionSMS(IncomingSmsBindingModel request)
        {
            string[] codes = request.Message.ToString().Split(' ');
            string from = request.From;
            var voucherCode = 0L;
            var nationalId = "";

            if (codes.Length >= 2)
            {
                voucherCode = Int64.Parse(codes[codes.Length - 2]);
                nationalId = codes[codes.Length - 1];
            }

            var phoneNumber = Regex.Replace(from, "[^\\d]", "").Trim();

            lock (syncObj)
            {
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
                                db.SaveChanges();

                                ConfirmTransaction();
                            }
                        }
                        else
                        {
                            VoucherIsInvalid();
                        }

                    }
                    else
                    {
                        UnauthorizedPhone();
                    }
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

        private void UnauthorizedPhone()
        {
        }

        private void VoucherIsInvalid()
        {
        }

        private void ConfirmTransaction()
        {
        }

    }
}