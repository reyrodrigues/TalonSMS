using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TalonAdmin.Attributes;
using TalonAdmin.Auth;
using TalonAdmin.Extensions;
using TalonAdmin.Models.Vouchers;


namespace TalonAdmin.Controllers.Api
{
    [Route("api/App/MobileClient")]
    public class MobileClientController : ControllerBase
    {
        public readonly string keyContainer;

        private readonly Models.Vouchers.Context ctx;
        private readonly Models.Admin.AdminContext adminCtx;
        private readonly IWebHostEnvironment environment;
        private readonly IHubContext<Hubs.DashboardHub> hubContext;
        private readonly Hubs.DashboardCache dashboardCache;

        public MobileClientController(
            Models.Vouchers.Context ctx,
            Models.Admin.AdminContext adminCtx,
            IWebHostEnvironment environment,
            IHubContext<Hubs.DashboardHub> hubContext,
            Hubs.DashboardCache dashboardCache)
        {
            this.ctx = ctx;
            this.adminCtx = adminCtx;
            this.environment = environment;
            this.hubContext = hubContext;
            this.dashboardCache = dashboardCache;

            keyContainer = Path.Combine(environment.ContentRootPath, "Keys");
        }

        /// <summary>
        /// Noop action that returns whatever is passed in the request. Used to determine if an wifi connection can reach the system.
        /// </summary>
        /// <returns></returns>
        [HttpGet("IsAlive")]
        public IActionResult IsAlive(string echo)
        {

            return Ok(echo);
        }

        #region Provisioning

        [HttpGet("GenerateInitialLoad")]
        public async Task<IActionResult> GenerateInitialLoad(int beneficiaryId)
        {
            Aes rm = KeyForBeneficiary(beneficiaryId);

            // NOTE: the grouping and the aggregates used to run on the server. EF Core cannot
            // translate an aggregate applied on top of a GroupBy projection, so the rows are
            // materialized first and grouped in memory. The result is identical.
            var preQuery = (await ctx.VoucherTransactionRecords
                .AsNoTracking()
                .Where(t =>
                    t.Voucher.Distribution.Program.DistributionMechanism == 2
                    && t.BeneficiaryId == beneficiaryId
                )
                .Select(t => new
                {
                    Voucher = t.VoucherId,
                    Value = t.Type == 1 ? t.Value : -t.Value,
                    CreatedOn = t.CreatedOn,
                })
                .ToArrayAsync())
                .GroupBy(t => t.Voucher);

            if (preQuery.Count() == 0)
            {
                return Ok(JToken.FromObject(EncodeLoad(beneficiaryId, 0, DateTime.UtcNow)));
            }

            var loadQuery = preQuery.Select(t => new
            {
                Voucher = t.Key,
                Value = t.Sum(v => v.Value),
                CreatedOn = t.Min(v => v.CreatedOn)
            });

            var value = loadQuery.Sum(v => v.Value);
            var createdOn = loadQuery.Max(v => v.CreatedOn);

            return Ok(JToken.FromObject(EncodeLoad(beneficiaryId, value, createdOn)));
        }

        [HttpPost("ProvisionBeneficiary")]
        [Authorize]
        public async Task<IActionResult> ProvisionBeneficiary([FromBody]dynamic request)
        {
            int beneficiaryId = request.beneficiaryId;
            string cardId = request.cardId;

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

            return Ok(JToken.FromObject(new
            {
                _id = beneficiary.Id.ToString(),
                BeneficiaryId = beneficiary.Id,
                CardKey = beneficiary.CardKey,
                CardId = beneficiary.CardId,
            }));
        }

        [HttpGet("ListDistributionsForBeneficiary")]
        [Authorize]
        public async Task<IActionResult> ListDistributionsForBeneficiary([FromQuery]int beneficiaryId)
        {
            var distributions = await ctx.VoucherTransactionRecords
                .AsNoTracking()
                .Where(v => v.Type == 1 && v.BeneficiaryId == beneficiaryId && v.Voucher.Distribution.Program.DistributionMechanism == 3)
                .Select(v => v.Voucher.Distribution)
                .Distinct()
                .ToListAsync();

            return Ok(JToken.FromObject(distributions.Select(d => new
            {
                id = d.Id,
                name = d.Title,
            })));
        }



        [HttpPost("SetBeneficiaryPin")]
        [Authorize]
        public async Task<IActionResult> SetBeneficiaryPin([FromBody]dynamic request)
        {
            int beneficiaryId = request.beneficiaryId;
            string pin = request.pin;

            var beneficiary = await ctx.Beneficiaries.Where(b => b.Id == beneficiaryId).FirstOrDefaultAsync();
            if (beneficiary == null)
                return NotFound();

            beneficiary.PIN = pin;

            await ctx.SaveChangesAsync();

            return Ok();
        }

        [HttpPost("AssignVoucherBook")]
        [Authorize]
        public async Task<IActionResult> AssignVoucherBook([FromBody]dynamic request)
        {
            int beneficiaryId = request.beneficiaryId;
            int distributionId = request.distributionId;
            int serialNumber = request.serialNumber;

            // NOTE: controllers are not registered in DI, so the collaborating controller is built
            // through ActivatorUtilities and handed this request's ControllerContext (which carries
            // the HttpContext, the Request and the User the old code copied over one by one).
            var controller = ActivatorUtilities.CreateInstance<VoucherWorkflowController>(HttpContext.RequestServices);
            controller.ControllerContext = ControllerContext;

            return await controller.AssignVoucherBook(JObject.FromObject(new
            {
                DistributionId = distributionId,
                BeneficiaryId = beneficiaryId,
                SerialNumber = serialNumber,
            }));
        }
        #endregion

        #region Beneficiary Keys

        [HttpGet("DownloadBeneficiaryKeys")]
        public async Task<IActionResult> DownloadBeneficiaryKeys()
        {
            return Ok(await DownloadBeneficiaryKeysInternal());

        }

        [HttpGet("DownloadEncryptedBeneficiaryKeys")]
        public async Task<IActionResult> DownloadEncryptedBeneficiaryKeys()
        {
            JToken result = await DownloadBeneficiaryKeysInternal();
            string encrypted = EncryptUsingRSA(result, "VendorPublic.json");

            return Ok(encrypted);
        }

        #endregion

        #region Card Loads

        [HttpGet("GenerateCardLoads")]
        public async Task<IActionResult> GenerateCardLoads()
        {
            JToken result = await GenerateCardLoadsInternal();
            return Ok(result);
        }
        #endregion

        #region Offline Load
        // NOTE: Web API defaulted actions whose name does not start with a verb to POST; the
        // Angular client posts a form to this endpoint, so the verb is pinned to POST here.
        [HttpPost("DownloadOfflinePayload")]
        public async Task<IActionResult> DownloadOfflinePayload()
        {
            var countryId = this.GetCountryId();
            var country = await adminCtx.Countries
                .AsNoTracking()
                .Where(c => c.Id == countryId)
                .FirstOrDefaultAsync();

            JToken cardLoadsJson = await GenerateCardLoadsInternal();
            string cardLoads = EncryptUsingRSA(cardLoadsJson, "VendorPublic.json");

            JToken beneficiaryKeysJson = await DownloadBeneficiaryKeysInternal();
            string beneficiaryKeys = EncryptUsingRSA(beneficiaryKeysJson, "VendorPublic.json");

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

                }

                using (var outputStream = new MemoryStream())
                {
                    memoryStream.Seek(0, SeekOrigin.Begin);
                    memoryStream.CopyTo(outputStream);

                    return File(outputStream.ToArray(), "application/octet-stream", country.IsoAlpha3 + ".zip");
                }
            }
        }

        [HttpPost("UploadVendorPayload")]
        public async Task<IActionResult> UploadVendorPayload()
        {
            if (!Request.HasFormContentType
                || Request.ContentType == null
                || !Request.ContentType.StartsWith("multipart/", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest();
            }

            // NOTE: MultipartFormDataStreamProvider is gone; ASP.NET Core buffers the uploaded
            // files for us, so nothing is written to ~/App_Data/uploads anymore.
            var form = await Request.ReadFormAsync();

            StringBuilder builder = new StringBuilder();

            foreach (var fileData in form.Files)
            {
                byte[] fileBytes;
                using (var uploadStream = new MemoryStream())
                {
                    await fileData.CopyToAsync(uploadStream);
                    fileBytes = uploadStream.ToArray();
                }

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
            return Ok(builder.ToString());
        }
        #endregion

        #region Cached Transactions

        [HttpPost("UploadCardLoads")]
        [AuthorizeVendor]
        public async Task<IActionResult> UploadCardLoads([FromBody]dynamic cardLoads)
        {
            if (cardLoads == null || String.IsNullOrEmpty(cardLoads.ToString()))
            {
                return Ok();
            }

            var vendor = await VendorFromRequest();
            await ProcessCardLoadsInternal(cardLoads, vendor);

            return Ok();
        }

        [HttpPost("UploadTransactions")]
        [AuthorizeVendor]
        public async Task<IActionResult> UploadTransactions([FromBody]JArray transactions)
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

        [HttpPost("ProcessNFCTransaction")]
        [AuthorizeVendor]
        public async Task<IActionResult> ProcessNFCTransaction([FromBody]dynamic request)
        {
            await NotifyClients(hubContext, dashboardCache);

            var vendor = await VendorFromRequest();
            dynamic result = new JObject();
            decimal amountCredited = request.amountCredited;
            int beneficiaryId = request.beneficiaryId;

            return Ok(await ProccessNFCTransactionInternal(vendor, beneficiaryId, amountCredited));
        }

        private async Task<JToken> ProccessNFCTransactionInternal(Vendor vendor, int beneficiaryId, decimal amountCredited)
        {
            return await ProccessNFCTransactionInternal(vendor, beneficiaryId, amountCredited, DateTime.UtcNow);
        }

        private async Task<JToken> ProccessNFCTransactionInternal(Vendor vendor, int beneficiaryId, decimal amountCredited, DateTime transactionDate)
        {
            var confirmationCodes = new List<string>();

            // NOTE: lazy loading is gone, so Distribution (ordering) and TransactionRecords
            // (IssuingTransactionRecord) are eagerly loaded. The grouping by voucher moved to
            // memory because EF Core cannot group by an entity and project it back out.
            var vouchers = (await ctx.VoucherTransactionRecords
                .Include(v => v.Voucher).ThenInclude(v => v.Distribution)
                .Include(v => v.Voucher).ThenInclude(v => v.TransactionRecords)
                .Where(v => v.Voucher.Status != null &&
                    new int[] { 1, 2 }.Contains(v.Voucher.Status.Value) &&
                    v.Voucher.Distribution.Program.DistributionMechanism == 2 &&
                    v.BeneficiaryId == beneficiaryId)
                .ToListAsync())
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
                .ToList();
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

                    ctx.VoucherTransactionRecords.Add(transactionRecord);
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
                    ctx.VoucherTransactionRecords.Add(transactionRecord);
                    voucher.Status = 2;
                    amountCredited = 0;

                    break;
                }
            }

            await ctx.SaveChangesAsync();

            return JToken.FromObject(new
            {
                Success = true,
                ConfirmationCode = String.Join(",", confirmationCodes)
            });
        }

        /// <summary>
        /// Redeems a voucher code presented at the point of sale.
        /// </summary>
        /// <remarks>
        /// The route keeps its name because deployed vendor apps call it, but this is not the paper
        /// QR path that was removed — it is how the point-of-sale client redeems any voucher code,
        /// and it is shared with the offline transaction sync.
        /// </remarks>
        [HttpPost("ProcessQRTransaction")]
        [AuthorizeVendor]
        public async Task<IActionResult> ProcessQRTransaction([FromBody]dynamic request)
        {
            await NotifyClients(hubContext, dashboardCache);

            var vendor = await VendorFromRequest();
            dynamic result = new JObject();
            string voucherCode = request.voucherCode;
            return Ok(await RedeemVoucherCodeInternal(vendor, voucherCode));
        }

        private async Task<JToken> RedeemVoucherCodeInternal(Vendor vendor, string voucherCode)
        {
            return await RedeemVoucherCodeInternal(vendor, voucherCode, DateTime.UtcNow);
        }

        private async Task<JToken> RedeemVoucherCodeInternal(Vendor vendor, string voucherCode, DateTime transactionDate)
        {
            // NOTE: TransactionRecords is included because IssuingTransactionRecord walks it and
            // lazy loading is no longer available.
            var voucher = await ctx.Vouchers
                .Include(v => v.TransactionRecords)
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
                Value = voucher.Value, // a code redemption claims the full value
                CountryId = voucher.CountryId,
                OrganizationId = voucher.OrganizationId
            };

            ctx.VoucherTransactionRecords.Add(transactionRecord);
            voucher.Status = 3;

            await ctx.SaveChangesAsync();

            return JToken.FromObject(new
            {
                Success = true,
                VoucherCode = voucher.VoucherCode,
                ConfirmationCode = confirmationCode
            });
        }

        [HttpPost("ProcessQuarantinedTransaction")]
        [Authorize]
        public async Task<IActionResult> ProcessQuarantinedTransaction(int transactionId)
        {
            var transaction = await ctx.TransactionLogItems.Include(t => t.Vendor).Where(t => t.Id == transactionId).FirstOrDefaultAsync();
            if (transaction == null)
            {
                return Ok(JToken.FromObject(new
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
                result = await RedeemVoucherCodeInternal(transaction.Vendor, transaction.VoucherCode, transaction.Date);
            }
            var me = await this.WhoAmI(adminCtx);

            if (result.Success == true)
            {
                transaction.Processed = true;
                transaction.Quarantine = false;
                transaction.ProcessedOn = DateTime.UtcNow;
                transaction.ProcessedBy = me.UserName;
                transaction.ConfirmationCode = result.ConfirmationCode;
            }

            await ctx.SaveChangesAsync();

            return Ok((JToken)result);
        }


        [HttpGet("DownloadKeyset")]
        public async Task<IActionResult> DownloadKeyset()
        {
            /// TODO: PLEASE REMOVE ME FROM THE FILE SYSTEM SOMEHOW
            GenerateKeys(keyContainer);

            var serverPublic = ExportPublicKey(JsonToRSAParameters(JToken.Parse(System.IO.File.ReadAllText(Path.Combine(keyContainer, "ServerPublic.json")))));
            var vendorPrivate = ExportPrivateKey(JsonToRSAParameters(JToken.Parse(System.IO.File.ReadAllText(Path.Combine(keyContainer, "VendorPrivate.json")))));

            return Ok(JToken.FromObject(new
            {
                Server = serverPublic,
                Vendor = vendorPrivate
            }));
        }

        #region Private Methods
        // NOTE: this used to be a parameterless static that reached for the SignalR GlobalHost and
        // the hosting environment. Both are DI services now, so callers pass them in.
        internal static async Task NotifyClients(IHubContext<Hubs.DashboardHub> hubContext, Hubs.DashboardCache cache)
        {
            // Drop the stale summary before telling anyone to refetch, or the refetch races the
            // invalidation and the client redraws the figures it already had.
            cache.Invalidate();

            await hubContext.Clients.All.SendAsync("updateDashboard");
        }

        private async Task ProcessTransactionsInternal(JArray transactions, Vendor vendor)
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
            })
            // NOTE: materialized up front so the per-beneficiary lookups above do not run while the
            // context is executing the existence query below.
            .ToArray();


            var transactionCodes = mapped.Select(t => t.TransactionCode).ToArray();
            var existing = ctx.TransactionLogItems
                .Where(t => transactionCodes.Contains(t.TransactionCode) && t.VendorId == vendor.Id)
                .Select(t => t.TransactionCode)
                .ToArray();
            var filtered = mapped.Where(t => !existing.Contains(t.TransactionCode)).ToArray();

            ctx.TransactionLogItems.AddRange(filtered);
            await ctx.SaveChangesAsync();
        }

        private async Task ProcessCardLoadsInternal(JArray cardLoads, Vendor vendor)
        {
            using (var md5 = MD5.Create())
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
                })
                // NOTE: materialized up front so the per-beneficiary lookups above do not run while
                // the context is executing the existence query below.
                .ToArray();

                var cardLoadDates = mapped.Select(t => t.Checksum).ToArray();
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
            var cardKeys = await ctx.Beneficiaries
                .AsNoTracking()
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

        /// <summary>
        /// Does the meat of generating the card loads.
        /// </summary>
        /// <param name="result"></param>
        /// <returns></returns>
        private async Task<JToken> GenerateCardLoadsInternal()
        {
            // NOTE: the grouping by beneficiary was pushed to memory; EF Core cannot group by an
            // entity and then project the group's elements. The rows and the output are the same.
            var loadDictionary = (await ctx.VoucherTransactionRecords
                .AsNoTracking()
                .FilterCountry(this)
                .Where(t =>
                t.Voucher.Distribution.Program.DistributionMechanism == 2
                && t.Type == 1
                && t.Beneficiary.CardId != null
            ).Select(t => new
            {
                BeneficiaryId = t.Beneficiary.Id,
                CardId = t.Beneficiary.CardId,
                Value = t.Value,
                CreatedOn = t.CreatedOn,
            })
            .ToArrayAsync())
            .GroupBy(k => k.BeneficiaryId)
            .Select(l => new
            {
                CardId = l.First().CardId,
                Load = l.Select(b => new
                {
                    BeneficiaryId = b.BeneficiaryId,
                    Value = b.Value,
                    CreatedOn = b.CreatedOn,
                })
            })
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
        #endregion

        #region Helper Methods
        private string DecryptUsingRSA(string payload, string keyName)
        {
            // NOTE: RSACryptoServiceProvider is Windows/CAPI bound on .NET; RSA.Create() is the
            // portable equivalent and OaepSHA1 is exactly what Decrypt(..., fOAEP: true) used.
            using (var rsa = RSA.Create())
            {
                dynamic serverPrivate = JToken.Parse(System.IO.File.ReadAllText(Path.Combine(keyContainer, keyName)));

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
                var key = rsa.Decrypt(Convert.FromBase64String(arguments[1]), RSAEncryptionPadding.OaepSHA1);
                var iv = rsa.Decrypt(Convert.FromBase64String(arguments[2]), RSAEncryptionPadding.OaepSHA1);

                using (var aes = Aes.Create())
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
            using (var rsa = RSA.Create())
            {
                // NOTE: PersistKeyInCsp is a CAPI-only concept; RSA.Create() keys are never persisted.
                dynamic serverPublic = JToken.Parse(System.IO.File.ReadAllText(Path.Combine(keyContainer, keyName)));

                RSAParameters publicKey = new RSAParameters
                {
                    Exponent = serverPublic.Exponent,
                    Modulus = serverPublic.Modulus,
                };

                rsa.ImportParameters(publicKey);

                using (var aes = Aes.Create())
                {
                    encrypted = result.ToString().Encrypt(aes);
                    encrypted += "|" + Convert.ToBase64String(rsa.Encrypt(aes.Key, RSAEncryptionPadding.OaepSHA1));
                    encrypted += "|" + Convert.ToBase64String(rsa.Encrypt(aes.IV, RSAEncryptionPadding.OaepSHA1));

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
            // NOTE: [AuthorizeVendor] already resolved the "Authorization: Token <token>" header and
            // published the vendor id on the request, so the token lookup happens there now.
            var vendorIdHeader = Request.Headers[AuthConstants.VendorIdHeader];
            int vendorId;

            if (vendorIdHeader.Count == 0 || !Int32.TryParse(vendorIdHeader[0], out vendorId))
            {
                return null;
            }

            return await ctx.Vendors
                .Where(v => v.Id == vendorId).FirstOrDefaultAsync();
        }

        private static void GenerateKeys(string keyContainer)
        {
            if (!Directory.Exists(keyContainer))
                Directory.CreateDirectory(keyContainer);

            if (!System.IO.File.Exists(Path.Combine(keyContainer, "VendorPublic.json")) || !System.IO.File.Exists(Path.Combine(keyContainer, "VendorPrivate.json")))
            {
                using (RSA rsa = RSA.Create())
                {
                    string publicKey = rsa.ToXmlString(false);
                    string privateKey = rsa.ToXmlString(true);


                    var jsonPublicKey = JToken.Parse(JsonConvert.SerializeXNode(XDocument.Parse(publicKey)));
                    var jsonPrivateKey = JToken.Parse(JsonConvert.SerializeXNode(XDocument.Parse(privateKey)));
                    System.IO.File.WriteAllText(Path.Combine(keyContainer, "VendorPublic.json"), jsonPublicKey["RSAKeyValue"].ToString());
                    System.IO.File.WriteAllText(Path.Combine(keyContainer, "VendorPrivate.json"), jsonPrivateKey["RSAKeyValue"].ToString());
                }
            }

            if (!System.IO.File.Exists(Path.Combine(keyContainer, "ServerPublic.json")) || !System.IO.File.Exists(Path.Combine(keyContainer, "ServerPrivate.json")))
            {
                using (RSA rsa = RSA.Create())
                {
                    string publicKey = rsa.ToXmlString(false);
                    string privateKey = rsa.ToXmlString(true);


                    var jsonPublicKey = JToken.Parse(JsonConvert.SerializeXNode(XDocument.Parse(publicKey)));
                    var jsonPrivateKey = JToken.Parse(JsonConvert.SerializeXNode(XDocument.Parse(privateKey)));
                    System.IO.File.WriteAllText(Path.Combine(keyContainer, "ServerPublic.json"), jsonPublicKey["RSAKeyValue"].ToString());
                    System.IO.File.WriteAllText(Path.Combine(keyContainer, "ServerPrivate.json"), jsonPrivateKey["RSAKeyValue"].ToString());
                }
            }
        }

        private string EncodeLoad(int beneficiaryId, decimal? value, DateTime createdOn)
        {
            int unixTime = (int)(createdOn.ToUniversalTime() - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;
            var payload = String.Format("1933|{0}|{1:X}", value, unixTime);

            return payload.Encrypt(KeyForBeneficiary(beneficiaryId));
        }


        private Aes KeyForBeneficiary(int beneficiaryId)
        {
            Aes rm = Aes.Create();
            var beneficiary = ctx.Beneficiaries.AsNoTracking().Where(b => b.Id == beneficiaryId).FirstOrDefault();

            var passwordBytes = Encoding.UTF8.GetBytes(beneficiary.PIN);
            var iv = new byte[rm.BlockSize / 8];
            Array.Copy(passwordBytes, iv, passwordBytes.Length < iv.Length ? passwordBytes.Length : iv.Length);

            rm.Key = Convert.FromBase64String(beneficiary.CardKey);
            rm.IV = iv;
            rm.Mode = CipherMode.CBC;
            return rm;
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
