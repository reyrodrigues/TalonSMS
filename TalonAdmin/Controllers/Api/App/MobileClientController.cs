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
using System.Web.Hosting;
using Newtonsoft.Json;
using System.Xml.Linq;
using TalonAdmin.ActionResults;
using TalonAdmin.Utils;
using System.IO.Compression;
using TalonAdmin.Models.Vouchers;
using TalonAdmin.Attributes;
using System.Net.Http;


namespace TalonAdmin.Controllers.Api
{
    [RoutePrefix("api/App/MobileClient")]
    public class MobileClientController : ApiController
    {
        public readonly string keyContainer;

        public MobileClientController()
        {
            keyContainer = HostingEnvironment.MapPath("~/Keys");
        }

        /// <summary>
        /// Noop action that returns whatever is passed in the request. Used to determine if an wifi connection can reach the system.
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        [Route("IsAlive")]
        public IHttpActionResult IsAlive(string echo)
        {

            return Ok<string>(echo);
        }

        #region Provisioning

        [HttpGet]
        [Route("GenerateInitialLoad")]
        public async Task<IHttpActionResult> GenerateInitialLoad(int beneficiaryId)
        {
            using (var ctx = new Models.Vouchers.Context())
            {
                Aes rm = KeyForBeneficiary(beneficiaryId);

                var preQuery = ctx.VoucherTransactionRecords.Where(t =>
                    t.Voucher.Distribution.Program.DistributionMechanism == 2
                    && t.BeneficiaryId == beneficiaryId
                ).Select(t => new
                {
                    Voucher = t.VoucherId,
                    Value = t.Type == 1 ? t.Value : -t.Value,
                    CreatedOn = t.CreatedOn,
                })
                .GroupBy(t => t.Voucher);

                if (preQuery.Count() == 0)
                {
                    return Ok<JToken>(JToken.FromObject(EncodeLoad(beneficiaryId, 0, DateTime.UtcNow)));
                }

                var loadQuery = preQuery.Select(t => new
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

        [HttpPost]
        [Authorize]
        [Route("ProvisionBeneficiary")]
        public async Task<IHttpActionResult> ProvisionBeneficiary([FromBody]dynamic request)
        {
            int beneficiaryId = request.beneficiaryId;
            string cardId = request.cardId;
            using (var ctx = new Models.Vouchers.Context())
            {
                var beneficiary = await ctx.Beneficiaries.Where(b => b.Id == beneficiaryId).FirstOrDefaultAsync();
                var others = await ctx.Beneficiaries.Where(b => b.CardId == cardId).ToListAsync();

                if (beneficiary == null)
                    return NotFound();

                foreach (var other in others)
                {
                    other.CardId = null;
                }

                if (String.IsNullOrEmpty(beneficiary.CardKey))
                {
                    beneficiary.GenerateKey();
                }
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
        [Authorize]
        [Route("ListDistributionsForBeneficiary")]
        public async Task<IHttpActionResult> ListDistributionsForBeneficiary([FromUri]int beneficiaryId)
        {
            using (var ctx = new Models.Vouchers.Context())
            {
                var distributions = await ctx.VoucherTransactionRecords
                    .Where(v => v.Type == 1 && v.BeneficiaryId == beneficiaryId && v.Voucher.Distribution.Program.DistributionMechanism == 3)
                    .Select(v => v.Voucher.Distribution)
                    .Distinct()
                    .ToListAsync();

                return Ok<JToken>(JToken.FromObject(distributions.Select(d => new
                {
                    id = d.Id,
                    name = d.Title,
                })));
            }
        }



        [HttpPost]
        [Authorize]
        [Route("SetBeneficiaryPin")]
        public async Task<IHttpActionResult> SetBeneficiaryPin([FromBody]dynamic request)
        {
            int beneficiaryId = request.beneficiaryId;
            string pin = request.pin;
            using (var ctx = new Models.Vouchers.Context())
            {
                var beneficiary = await ctx.Beneficiaries.Where(b => b.Id == beneficiaryId).FirstOrDefaultAsync();
                if (beneficiary == null)
                    return NotFound();

                beneficiary.PIN = pin;

                await ctx.SaveChangesAsync();

                return Ok();
            }
        }

        [HttpPost]
        [Authorize]
        [Route("AssignVoucherBook")]
        public async Task<IHttpActionResult> AssignVoucherBook([FromBody]dynamic request)
        {
            int beneficiaryId = request.beneficiaryId;
            int distributionId = request.distributionId;
            int serialNumber = request.serialNumber;

            var controller = new VoucherWorkflowController();
            controller.ActionContext = ActionContext;
            controller.Configuration = Configuration;
            controller.Request = Request;
            controller.ControllerContext = ControllerContext;
            controller.User = User;

            return await controller.AssignVoucherBook(new
            {
                DistributionId = distributionId,
                BeneficiaryId = beneficiaryId,
                SerialNumber = serialNumber,
            });
        }
        #endregion

        #region Beneficiary Keys

        [HttpGet]
        [Route("DownloadBeneficiaryKeys")]
        public async Task<IHttpActionResult> DownloadBeneficiaryKeys()
        {
            return Ok<JToken>(await DownloadBeneficiaryKeysInternal());

        }

        [HttpGet]
        [Route("DownloadEncryptedBeneficiaryKeys")]
        public async Task<IHttpActionResult> DownloadEncryptedBeneficiaryKeys()
        {
            JToken result = await DownloadBeneficiaryKeysInternal();
            string encrypted = EncryptUsingRSA(result, "VendorPublic.json");

            return Ok<string>(encrypted);
        }

        #endregion

        #region Card Loads

        [HttpGet]
        [Route("GenerateCardLoads")]
        public async Task<IHttpActionResult> GenerateCardLoads()
        {
            JToken result = await GenerateCardLoadsInternal();
            return Ok<JToken>(result);
        }
        [HttpGet]
        [Route("GenerateQRCodes")]
        public async Task<IHttpActionResult> GenerateQRCodes()
        {
            JToken result = await GenerateQRCodesInternal();
            return Ok<JToken>(result);
        }

        #endregion

        #region Offline Load
        [Route("DownloadOfflinePayload")]
        public async Task<IHttpActionResult> DownloadOfflinePayload()
        {
            using (var ctx = new Models.Admin.AdminContext())
            {
                var countryId = this.GetCountryId();
                var country = await ctx.Countries
                    .Where(c => c.Id == countryId)
                    .FirstOrDefaultAsync();

                JToken cardLoadsJson = await GenerateCardLoadsInternal();
                string cardLoads = EncryptUsingRSA(cardLoadsJson, "VendorPublic.json");

                JToken beneficiaryKeysJson = await DownloadBeneficiaryKeysInternal();
                string beneficiaryKeys = EncryptUsingRSA(beneficiaryKeysJson, "VendorPublic.json");

                JToken qrCodesJson = await DownloadBeneficiaryKeysInternal();
                string qrCodes = EncryptUsingRSA(beneficiaryKeysJson, "VendorPublic.json");
                using (var memoryStream = new MemoryStream())
                {
                    using (var archive = new ZipArchive(memoryStream, ZipArchiveMode.Create, true))
                    {
                        var cardLoadsEntry = archive.CreateEntry("CardLoads.b64");

                        using (var entryStream = cardLoadsEntry.Open())
                        using (var streamWriter = new StreamWriter(entryStream))
                        {
                            streamWriter.Write(cardLoads);
                        }

                        var beneficiaryKeysEntry = archive.CreateEntry("BeneficiaryKeys.b64");

                        using (var entryStream = beneficiaryKeysEntry.Open())
                        using (var streamWriter = new StreamWriter(entryStream))
                        {
                            streamWriter.Write(beneficiaryKeys);
                        }

                        var qrCodesEntry = archive.CreateEntry("QRCodes.b64");

                        using (var entryStream = qrCodesEntry.Open())
                        using (var streamWriter = new StreamWriter(entryStream))
                        {
                            streamWriter.Write(qrCodes);
                        }
                    }

                    using (var outputStream = new MemoryStream())
                    {
                        memoryStream.Seek(0, SeekOrigin.Begin);
                        memoryStream.CopyTo(outputStream);

                        return this.File(outputStream.ToArray(), country.IsoAlpha3 + ".zip", "application/octet-stream");
                    }
                }
            }
        }

        [Route("UploadVendorPayload")]
        public async Task<IHttpActionResult> UploadVendorPayload()
        {
            if (!Request.Content.IsMimeMultipartContent())
            {
                return BadRequest();
            }

            string root = HostingEnvironment.MapPath("~/App_Data/uploads");
            var provider = new MultipartFormDataStreamProvider(root);

            var streamProvider = new MultipartFormDataStreamProvider(root);
            await Request.Content.ReadAsMultipartAsync(streamProvider);

            StringBuilder builder = new StringBuilder();

            foreach (MultipartFileData fileData in streamProvider.FileData)
            {
                var fileBytes = File.ReadAllBytes(fileData.LocalFileName);

                // No need to keep the file lying around
                File.Delete(fileData.LocalFileName);

                using (var memoryStream = new MemoryStream(fileBytes))
                using (var archive = new ZipArchive(memoryStream, ZipArchiveMode.Read))
                {
                    var cardLoadsEntry = archive.GetEntry("cardLoadHistoryDB.b64");
                    var transactionsEntry = archive.GetEntry("transactionHistoryDB.b64");
                    var vendorProfileEntry = archive.GetEntry("vendorProfile.b64");


                    string cardLoadsEncryptedData = "";
                    string transactionsEncryptedData = "";
                    string vendorProfileEncryptedData = "";

                    using (var stream = cardLoadsEntry.Open())
                    {
                        var output = new MemoryStream();
                        stream.CopyTo(output);
                        stream.Close();

                        cardLoadsEncryptedData = Encoding.UTF8.GetString(output.ToArray());
                    }

                    using (var stream = transactionsEntry.Open())
                    {
                        var output = new MemoryStream();
                        stream.CopyTo(output);
                        stream.Close();

                        transactionsEncryptedData = Encoding.UTF8.GetString(output.ToArray());
                    }

                    using (var stream = vendorProfileEntry.Open())
                    {
                        var output = new MemoryStream();
                        stream.CopyTo(output);
                        stream.Close();

                        vendorProfileEncryptedData = Encoding.UTF8.GetString(output.ToArray());
                    }
                    var cardLoads = JToken.Parse(DecryptUsingRSA(cardLoadsEncryptedData, "ServerPrivate.json")) as JArray;
                    var transactions = JToken.Parse(DecryptUsingRSA(transactionsEncryptedData, "ServerPrivate.json")) as JArray;
                    var vendorProfile = JToken.Parse(DecryptUsingRSA(vendorProfileEncryptedData, "ServerPrivate.json"));

                    var vendorObject = vendorProfile.ToObject<Models.Vouchers.Vendor>();

                    await ProcessCardLoadsInternal(cardLoads, vendorObject);
                    await ProcessTransactionsInternal(transactions, vendorObject);
                }
            }
            return Ok<string>(builder.ToString());
        }
        #endregion

        #region Cached Transactions

        [Route("UploadCardLoads")]
        [AuthorizeVendor]
        public async Task<IHttpActionResult> UploadCardLoads([FromBody]dynamic cardLoads)
        {
            if (cardLoads == null || String.IsNullOrEmpty(cardLoads.ToString()))
            {
                return Ok();
            }

            var vendor = await VendorFromRequest();
            await ProcessCardLoadsInternal(cardLoads, vendor);

            return Ok();
        }

        [Route("UploadTransactions")]
        [AuthorizeVendor]
        public async Task<IHttpActionResult> UploadTransactions([FromBody]JArray transactions)
        {
            if (transactions == null || String.IsNullOrEmpty(transactions.ToString()))
            {
                return Ok();
            }

            var vendor = await VendorFromRequest();
            await ProcessTransactionsInternal(transactions, vendor);

            return Ok();
        }
        #endregion

        [HttpPost]
        [AuthorizeVendor]
        [Route("ProcessNFCTransaction")]
        public async Task<IHttpActionResult> ProcessNFCTransaction([FromBody]dynamic request)
        {
            var vendor = await VendorFromRequest();
            dynamic result = new JObject();
            decimal amountCredited = request.amountCredited;
            int beneficiaryId = request.beneficiaryId;

            return Ok<JToken>(await ProccessNFCTransactionInternal(vendor, beneficiaryId, amountCredited));
        }

        private async Task<JToken> ProccessNFCTransactionInternal(Vendor vendor, int beneficiaryId, decimal amountCredited)
        {
            return await ProccessNFCTransactionInternal(vendor, beneficiaryId, amountCredited, DateTime.UtcNow);
        }

        private async Task<JToken> ProccessNFCTransactionInternal(Vendor vendor, int beneficiaryId, decimal amountCredited, DateTime transactionDate)
        {
            var confirmationCodes = new List<string>();
            using (var db = new Models.Vouchers.Context())
            {
                var vouchers = await db.VoucherTransactionRecords
                    .Where(v => v.Voucher.Status != null &&
                        new int[] { 1, 2 }.Contains(v.Voucher.Status.Value) &&
                        v.Voucher.Distribution.Program.DistributionMechanism == 2 &&
                        v.BeneficiaryId == beneficiaryId)
                    .Select(v => new
                    {
                        Voucher = v.Voucher,
                        Amount = v.Type == 1 ? v.Value : -v.Value
                    })
                    .GroupBy(v => v.Voucher)
                    .Select(v => new
                    {
                        Voucher = v.Key,
                        Amount = v.Sum(a => a.Amount ?? 0)
                    })
                    .ToListAsync();
                foreach (var voucherData in vouchers.OrderBy(v => v.Voucher.Distribution.CreatedOn))
                {
                    var voucher = voucherData.Voucher;

                    if (amountCredited > voucherData.Amount)
                    {
                        var confirmationCode = RandomNumber.RandomLong(6).ToString("D6");
                        var transactionRecord = new VoucherTransactionRecord()
                        {
                            VoucherId = voucher.Id,
                            BeneficiaryId = voucher.IssuingTransactionRecord.BeneficiaryId,
                            VendorId = vendor.Id,
                            Type = 2,
                            ConfirmationCode = confirmationCode,
                            LastModifiedOn = transactionDate,
                            CreatedOn = transactionDate,
                            Value = voucherData.Amount, // Spending out the remaining amount in this voucher
                            CountryId = voucher.CountryId,
                            OrganizationId = voucher.OrganizationId
                        };

                        db.VoucherTransactionRecords.Add(transactionRecord);
                        voucher.Status = 3;
                        amountCredited = amountCredited - voucherData.Amount;

                        confirmationCodes.Add(confirmationCode);

                        continue;
                    }
                    if (amountCredited <= voucherData.Amount)
                    {

                        var confirmationCode = RandomNumber.RandomLong(6).ToString("D6");
                        var transactionRecord = new VoucherTransactionRecord()
                        {
                            VoucherId = voucher.Id,
                            BeneficiaryId = voucher.IssuingTransactionRecord.BeneficiaryId,
                            VendorId = vendor.Id,
                            Type = 2,
                            ConfirmationCode = confirmationCode,
                            LastModifiedOn = transactionDate,
                            CreatedOn = transactionDate,
                            Value = amountCredited,
                            CountryId = voucher.CountryId,
                            OrganizationId = voucher.OrganizationId
                        };

                        confirmationCodes.Add(confirmationCode);
                        db.VoucherTransactionRecords.Add(transactionRecord);
                        voucher.Status = 2;
                        amountCredited = 0;

                        break;
                    }
                }

                await db.SaveChangesAsync();

                return JToken.FromObject(new
                {
                    Success = true,
                    ConfirmationCode = String.Join(",", confirmationCodes)
                });
            }
        }

        [HttpPost]
        [AuthorizeVendor]
        [Route("ProcessQRTransaction")]
        public async Task<IHttpActionResult> ProcessQRTransaction([FromBody]dynamic request)
        {
            var vendor = await VendorFromRequest();
            dynamic result = new JObject();
            string voucherCode = request.voucherCode;
            return Ok<JToken>(await ProcessQRTransactionInternal(vendor, voucherCode));
        }

        private async Task<JToken> ProcessQRTransactionInternal(Vendor vendor, string voucherCode)
        {
            return await ProcessQRTransactionInternal(vendor, voucherCode, DateTime.UtcNow);
        }

        private async Task<JToken> ProcessQRTransactionInternal(Vendor vendor, string voucherCode, DateTime transactionDate)
        {
            using (var db = new Models.Vouchers.Context())
            {
                var voucher = await db.Vouchers
                    .Where(v => voucherCode == v.SequentialCode || voucherCode == v.VoucherCode)
                    .FirstOrDefaultAsync();

                if (voucher == null || voucher.Status != 1)
                {
                    return JToken.FromObject(new
                    {
                        Success = false,
                        Message = "Voucher Already Used",
                        VoucherCode = voucherCode
                    });
                }
                var confirmationCode = RandomNumber.RandomLong(6).ToString("D6");
                var transactionRecord = new VoucherTransactionRecord()
                {
                    VoucherId = voucher.Id,
                    BeneficiaryId = voucher.IssuingTransactionRecord.BeneficiaryId,
                    VendorId = vendor.Id,
                    Type = 2,
                    ConfirmationCode = confirmationCode,
                    LastModifiedOn = transactionDate,
                    CreatedOn = transactionDate,
                    Value = voucher.Value, // QR claims full price,
                    CountryId = voucher.CountryId,
                    OrganizationId = voucher.OrganizationId
                };

                db.VoucherTransactionRecords.Add(transactionRecord);
                voucher.Status = 3;

                await db.SaveChangesAsync();

                return JToken.FromObject(new
                {
                    Success = true,
                    VoucherCode = voucher.VoucherCode,
                    ConfirmationCode = confirmationCode
                });
            }
        }

        [HttpPost]
        [Authorize]
        [Route("ProcessQuarantinedTransaction")]
        public async Task<IHttpActionResult> ProcessQuarantinedTransaction(int transactionId)
        {
            using (var ctx = new Models.Vouchers.Context())
            {
                var transaction = await ctx.TransactionLogItems.Include("Vendor").Where(t => t.Id == transactionId).FirstOrDefaultAsync();
                if (transaction == null)
                {
                    return Ok<JToken>(JToken.FromObject(new
                    {
                        Success = false,
                        Message = "Invalid transaction id."
                    }));
                }
                dynamic result = new JObject() { };
                if (transaction.Type == 2)
                {
                    //NFC
                    result = await ProccessNFCTransactionInternal(transaction.Vendor, transaction.BeneficiaryId, transaction.AmountCredited, transaction.Date);
                }
                else
                {
                    // QR And SMS should be the same?
                    result = await ProcessQRTransactionInternal(transaction.Vendor, transaction.VoucherCode, transaction.Date);
                }
                var me = await this.WhoAmI();

                if (result.Success == true)
                {
                    transaction.Processed = true;
                    transaction.Quarantine = false;
                    transaction.ProcessedOn = DateTime.UtcNow;
                    transaction.ProcessedBy = me.UserName;
                    transaction.ConfirmationCode = result.ConfirmationCode;
                }

                await ctx.SaveChangesAsync();

                return Ok<JToken>(result);
            }
        }


        [HttpGet]
        [Route("DownloadKeyset")]
        public async Task<IHttpActionResult> DownloadKeyset()
        {
            /// TODO: PLEASE REMOVE ME FROM THE FILE SYSTEM SOMEHOW
            GenerateKeys(keyContainer);

            var serverPublic = ExportPublicKey(JsonToRSAParameters(JToken.Parse(File.ReadAllText(Path.Combine(keyContainer, "ServerPublic.json")))));
            var vendorPrivate = ExportPrivateKey(JsonToRSAParameters(JToken.Parse(File.ReadAllText(Path.Combine(keyContainer, "VendorPrivate.json")))));

            return Ok<JToken>(JToken.FromObject(new
            {
                Server = serverPublic,
                Vendor = vendorPrivate
            }));
        }

        #region Private Methods
        private async Task ProcessTransactionsInternal(JArray transactions, Vendor vendor)
        {
            using (var ctx = new Models.Vouchers.Context())
            {
                var mapped = transactions.OfType<JObject>().Select(s => new
                {
                    Type = s.ValueIfExists<int?>("type"),
                    BeneficiaryId = s.ValueIfExists<int?>("beneficiaryId"),
                    AmountCredited = s.ValueIfExists<decimal?>("amountCredited"),
                    AmountRemaining = s.ValueIfExists<decimal?>("amountRemaining"),
                    Date = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(s.ValueIfExists<int>("date")),
                    Checksum = s.ValueIfExists<string>("checksum"),
                    TransactionCode = s.ValueIfExists<string>("transactionCode"),
                    Quarantine = s.ValueIfExists<bool?>("quarantine"),
                    ConfirmationCode = s.ValueIfExists<string>("confirmationCode"),
                    VoucherCode = s.ValueIfExists<string>("voucherCode"),
                })
                .Select(s => new TransactionLogItem
                {
                    Type = s.Type.Value,
                    BeneficiaryId = s.BeneficiaryId.Value,
                    AmountCredited = s.AmountCredited ?? 0m,
                    AmountRemaining = s.AmountRemaining ?? 0m,
                    Date = s.Date,
                    Checksum = s.Checksum,
                    TransactionCode = s.TransactionCode,
                    Quarantine = s.Quarantine.Value,
                    VendorId = vendor.Id,
                    ConfirmationCode = s.ConfirmationCode,
                    VoucherCode = s.VoucherCode,

                    CountryId = ctx.Beneficiaries.Where(b => b.Id == s.BeneficiaryId.Value).First().CountryId,
                    OrganizationId = ctx.Beneficiaries.Where(b => b.Id == s.BeneficiaryId.Value).First().OrganizationId,
                });


                var transactionCodes = mapped.Select(t => t.TransactionCode);
                var existing = ctx.TransactionLogItems
                    .Where(t => transactionCodes.Contains(t.TransactionCode) && t.VendorId == vendor.Id)
                    .Select(t => t.TransactionCode)
                    .ToArray();
                var filtered = mapped.Where(t => !existing.Contains(t.TransactionCode)).ToArray();

                ctx.TransactionLogItems.AddRange(filtered);
                await ctx.SaveChangesAsync();
            }
        }

        private async Task ProcessCardLoadsInternal(JArray cardLoads, Vendor vendor)
        {
            using (var ctx = new Models.Vouchers.Context())
            using (var md5 = new MD5CryptoServiceProvider())
            {

                var mapped = cardLoads.OfType<JObject>().Select(s => new
                {
                    BeneficiaryId = s.ValueIfExists<int?>("beneficiaryId"),
                    Amount = s.ValueIfExists<decimal?>("amount"),
                    Date = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(s.ValueIfExists<int>("date")),
                    DistributionDate = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(s.ValueIfExists<int>("distributionDate")),
                    Checksum = Convert.ToBase64String(md5.ComputeHash(Encoding.ASCII.GetBytes(s.ValueIfExists<int>("date").ToString()))),
                })
                .Select(s => new CardLoad
                {
                    BeneficiaryId = s.BeneficiaryId.Value,
                    Amount = s.Amount.Value,
                    Date = s.Date,
                    DistributionDate = s.DistributionDate,
                    VendorId = vendor.Id,
                    Checksum = s.Checksum,


                    CountryId = ctx.Beneficiaries.Where(b => b.Id == s.BeneficiaryId.Value).First().CountryId,
                    OrganizationId = ctx.Beneficiaries.Where(b => b.Id == s.BeneficiaryId.Value).First().OrganizationId,
                });

                var cardLoadDates = mapped.Select(t => t.Checksum);
                var existing = ctx.CardLoads
                    .Where(t => cardLoadDates.Contains(t.Checksum) && t.VendorId == vendor.Id)
                    .Select(t => t.Checksum)
                    .ToArray();
                var filtered = mapped.Where(t => !existing.Contains(t.Checksum)).ToArray();

                ctx.CardLoads.AddRange(filtered);

                await ctx.SaveChangesAsync();
            }
        }

        public async Task<JToken> DownloadBeneficiaryKeysInternal()
        {
            using (var ctx = new Models.Vouchers.Context())
            {
                var cardKeys = await ctx.Beneficiaries
                    .FilterCountry(this)
                    .Where(b => b.CardKey != null)
                    .Select(b => new
                    {
                        _id = b.Id.ToString(),
                        BeneficiaryId = b.Id,
                        CardKey = b.CardKey,
                        CardId = b.CardId,
                    }).ToArrayAsync();

                return JToken.FromObject(cardKeys);
            }
        }

        /// <summary>
        /// Does the meat of generating the card loads.
        /// </summary>
        /// <param name="result"></param>
        /// <returns></returns>
        private async Task<JToken> GenerateCardLoadsInternal()
        {
            using (var ctx = new Models.Vouchers.Context())
            {
                var loadDictionary = (await ctx.VoucherTransactionRecords
                    .FilterCountry(this)
                    .Where(t =>
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
                    Load = l.Select(b => new
                    {
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

                var result = JToken.FromObject(loadDictionary);
                return result;

            }
        }

        /// <summary>
        /// Does the meat of generating the data needed to process QR Codes
        /// QR Code loads have the beneficiary id not encrypted and the voucher code is in the client and in the physical voucher
        /// </summary>
        /// <returns></returns>
        private async Task<JToken> GenerateQRCodesInternal()
        {
            using (var ctx = new Models.Vouchers.Context())
            {
                var loadDictionary = (await ctx.VoucherTransactionRecords
                    .FilterCountry(this)
                    .Where(t =>
                    t.Voucher.Distribution.Program.DistributionMechanism == 3
                    && t.Voucher.Status == 1
                    && t.Type == 1
                    && t.Beneficiary.CardKey != null
                ).Select(t => new
                {
                    BeneficiaryId = t.Beneficiary.Id,
                    VoucherCode = t.Voucher.SequentialCode == null ? t.Voucher.VoucherCode : t.Voucher.SequentialCode,
                    Value = t.Value,
                    ValidAfter = t.Voucher.Category.ValidAfter,
                })
                .ToArrayAsync())
                .Select(l => new
                {
                    _id = l.VoucherCode,
                    VoucherCode = l.VoucherCode,
                    BeneficiaryId = l.BeneficiaryId,
                    Payload = EncodeQRLoad(l.BeneficiaryId, l.Value, l.ValidAfter, l.VoucherCode)
                })
                .ToArray();

                var result = JToken.FromObject(loadDictionary);
                return result;

            }
        }


        #endregion

        #region Helper Methods
        private string DecryptUsingRSA(string payload, string keyName)
        {
            using (var rsa = new RSACryptoServiceProvider())
            {
                dynamic serverPrivate = JToken.Parse(File.ReadAllText(Path.Combine(keyContainer, keyName)));

                RSAParameters privateKey = new RSAParameters
                {
                    D = serverPrivate.D,
                    DP = serverPrivate.DP,
                    DQ = serverPrivate.DQ,
                    Exponent = serverPrivate.Exponent,
                    InverseQ = serverPrivate.InverseQ,
                    Modulus = serverPrivate.Modulus,
                    P = serverPrivate.P,
                    Q = serverPrivate.Q,
                };
                rsa.ImportParameters(privateKey);

                string[] arguments = payload.Split('|');
                var key = rsa.Decrypt(Convert.FromBase64String(arguments[1]), true);
                var iv = rsa.Decrypt(Convert.FromBase64String(arguments[2]), true);

                using (var aes = new AesCryptoServiceProvider())
                {
                    aes.Key = key;
                    aes.IV = iv;

                    return Encoding.UTF8.GetString(arguments[0].Decrypt(iv, key));
                }

            }
        }
        private string EncryptUsingRSA(JToken result, string keyName)
        {
            string encrypted = "";
            using (var rsa = new RSACryptoServiceProvider())
            {
                rsa.PersistKeyInCsp = false;
                dynamic serverPublic = JToken.Parse(File.ReadAllText(Path.Combine(keyContainer, keyName)));

                RSAParameters publicKey = new RSAParameters
                {
                    Exponent = serverPublic.Exponent,
                    Modulus = serverPublic.Modulus,
                };

                rsa.ImportParameters(publicKey);

                using (var aes = new AesCryptoServiceProvider())
                {
                    encrypted = result.ToString().Encrypt(aes);
                    encrypted += "|" + Convert.ToBase64String(rsa.Encrypt(aes.Key, true));
                    encrypted += "|" + Convert.ToBase64String(rsa.Encrypt(aes.IV, true));

                }
            }
            return encrypted;
        }

        private static RSAParameters JsonToRSAParameters(JToken keyJson)
        {

            dynamic key = keyJson;

            RSAParameters rsaParameters = new RSAParameters
            {
                D = key.D,
                DP = key.DP,
                DQ = key.DQ,
                Exponent = key.Exponent,
                InverseQ = key.InverseQ,
                Modulus = key.Modulus,
                P = key.P,
                Q = key.Q,
            };

            return rsaParameters;
        }


        private async Task<Vendor> VendorFromRequest()
        {

            if (Request.Headers.Authorization.Scheme.ToLower() != "token")
            {
                return null;
            }

            var authToken = Request.Headers.Authorization.Parameter;
            using (var ctx = new Models.Vouchers.Context())
            {
                return await ctx.Vendors
                    .Where(v => v.AuthorizationToken == authToken).FirstOrDefaultAsync();
            }
        }

        private static void GenerateKeys(string keyContainer)
        {
            if (!Directory.Exists(keyContainer))
                Directory.CreateDirectory(keyContainer);

            if (!File.Exists(Path.Combine(keyContainer, "VendorPublic.json")) || !File.Exists(Path.Combine(keyContainer, "VendorPrivate.json")))
            {
                using (RSACryptoServiceProvider rsa = new RSACryptoServiceProvider())
                {
                    rsa.PersistKeyInCsp = false;
                    string publicKey = rsa.ToXmlString(false);
                    string privateKey = rsa.ToXmlString(true);


                    var jsonPublicKey = JToken.Parse(JsonConvert.SerializeXNode(XDocument.Parse(publicKey)));
                    var jsonPrivateKey = JToken.Parse(JsonConvert.SerializeXNode(XDocument.Parse(privateKey)));
                    File.WriteAllText(Path.Combine(keyContainer, "VendorPublic.json"), jsonPublicKey["RSAKeyValue"].ToString());
                    File.WriteAllText(Path.Combine(keyContainer, "VendorPrivate.json"), jsonPrivateKey["RSAKeyValue"].ToString());
                }
            }

            if (!File.Exists(Path.Combine(keyContainer, "ServerPublic.json")) || !File.Exists(Path.Combine(keyContainer, "ServerPrivate.json")))
            {
                using (RSACryptoServiceProvider rsa = new RSACryptoServiceProvider())
                {
                    rsa.PersistKeyInCsp = false;
                    string publicKey = rsa.ToXmlString(false);
                    string privateKey = rsa.ToXmlString(true);


                    var jsonPublicKey = JToken.Parse(JsonConvert.SerializeXNode(XDocument.Parse(publicKey)));
                    var jsonPrivateKey = JToken.Parse(JsonConvert.SerializeXNode(XDocument.Parse(privateKey)));
                    File.WriteAllText(Path.Combine(keyContainer, "ServerPublic.json"), jsonPublicKey["RSAKeyValue"].ToString());
                    File.WriteAllText(Path.Combine(keyContainer, "ServerPrivate.json"), jsonPrivateKey["RSAKeyValue"].ToString());
                }
            }
        }

        private string EncodeLoad(int beneficiaryId, decimal? value, DateTime createdOn)
        {
            int unixTime = (int)(createdOn.ToUniversalTime() - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;
            var payload = String.Format("1933|{0}|{1:X}", value, unixTime);

            return payload.Encrypt(KeyForBeneficiary(beneficiaryId));
        }


        private string EncodeQRLoad(int beneficiaryId, decimal? value, DateTime? validAfter, string voucherCode)
        {
            int unixTime = validAfter != null ? (int)(validAfter.Value.ToUniversalTime() - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds : 0;
            var payload = String.Format("1933|{0}|{1:X}|{2}", value, unixTime, voucherCode);

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

        private static string ExportPrivateKey(RSAParameters parameters)
        {
            TextWriter outputStream = new StringWriter();
            using (var stream = new MemoryStream())
            {
                var writer = new BinaryWriter(stream);
                writer.Write((byte)0x30); // SEQUENCE
                using (var innerStream = new MemoryStream())
                {
                    var innerWriter = new BinaryWriter(innerStream);
                    EncodeIntegerBigEndian(innerWriter, new byte[] { 0x00 }); // Version
                    EncodeIntegerBigEndian(innerWriter, parameters.Modulus);
                    EncodeIntegerBigEndian(innerWriter, parameters.Exponent);
                    EncodeIntegerBigEndian(innerWriter, parameters.D);
                    EncodeIntegerBigEndian(innerWriter, parameters.P);
                    EncodeIntegerBigEndian(innerWriter, parameters.Q);
                    EncodeIntegerBigEndian(innerWriter, parameters.DP);
                    EncodeIntegerBigEndian(innerWriter, parameters.DQ);
                    EncodeIntegerBigEndian(innerWriter, parameters.InverseQ);
                    var length = (int)innerStream.Length;
                    EncodeLength(writer, length);
                    writer.Write(innerStream.GetBuffer(), 0, length);
                }

                var base64 = Convert.ToBase64String(stream.GetBuffer(), 0, (int)stream.Length).ToCharArray();
                outputStream.WriteLine("-----BEGIN RSA PRIVATE KEY-----");
                // Output as Base64 with lines chopped at 64 characters
                for (var i = 0; i < base64.Length; i += 64)
                {
                    outputStream.WriteLine(base64, i, Math.Min(64, base64.Length - i));
                }
                outputStream.WriteLine("-----END RSA PRIVATE KEY-----");
            }
            return outputStream.ToString();
        }

        public static String ExportPublicKey(RSAParameters parameters)
        {
            TextWriter outputStream = new StringWriter();

            using (var stream = new MemoryStream())
            {
                var writer = new BinaryWriter(stream);
                writer.Write((byte)0x30); // SEQUENCE
                using (var innerStream = new MemoryStream())
                {
                    var innerWriter = new BinaryWriter(innerStream);
                    EncodeIntegerBigEndian(innerWriter, parameters.Modulus);
                    EncodeIntegerBigEndian(innerWriter, parameters.Exponent);


                    var length = (int)innerStream.Length;
                    EncodeLength(writer, length);
                    writer.Write(innerStream.GetBuffer(), 0, length);
                }

                var base64 = Convert.ToBase64String(stream.GetBuffer(), 0, (int)stream.Length).ToCharArray();
                outputStream.WriteLine("-----BEGIN RSA PUBLIC KEY-----");
                // Output as Base64 with lines chopped at 64 characters
                for (var i = 0; i < base64.Length; i += 64)
                {
                    outputStream.WriteLine(base64, i, Math.Min(64, base64.Length - i));
                }
                outputStream.WriteLine("-----END RSA PUBLIC KEY-----");

                return outputStream.ToString();

            }
        }


        private static void EncodeLength(BinaryWriter stream, int length)
        {
            if (length < 0) throw new ArgumentOutOfRangeException("length", "Length must be non-negative");
            if (length < 0x80)
            {
                // Short form
                stream.Write((byte)length);
            }
            else
            {
                // Long form
                var temp = length;
                var bytesRequired = 0;
                while (temp > 0)
                {
                    temp >>= 8;
                    bytesRequired++;
                }
                stream.Write((byte)(bytesRequired | 0x80));
                for (var i = bytesRequired - 1; i >= 0; i--)
                {
                    stream.Write((byte)(length >> (8 * i) & 0xff));
                }
            }
        }

        private static void EncodeIntegerBigEndian(BinaryWriter stream, byte[] value, bool forceUnsigned = true)
        {
            stream.Write((byte)0x02); // INTEGER
            var prefixZeros = 0;
            for (var i = 0; i < value.Length; i++)
            {
                if (value[i] != 0) break;
                prefixZeros++;
            }
            if (value.Length - prefixZeros == 0)
            {
                EncodeLength(stream, 1);
                stream.Write((byte)0);
            }
            else
            {
                if (forceUnsigned && value[prefixZeros] > 0x7f)
                {
                    // Add a prefix zero to force unsigned if the MSB is 1
                    EncodeLength(stream, value.Length - prefixZeros + 1);
                    stream.Write((byte)0);
                }
                else
                {
                    EncodeLength(stream, value.Length - prefixZeros);
                }
                for (var i = prefixZeros; i < value.Length; i++)
                {
                    stream.Write(value[i]);
                }
            }
        }

        #endregion
    }
}