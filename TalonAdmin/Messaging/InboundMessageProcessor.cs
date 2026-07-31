using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TalonAdmin.Controllers.Api;
using TalonAdmin.Hubs;
using TalonAdmin.Models.Vouchers;
using TalonAdmin.Utils;

namespace TalonAdmin.Messaging
{
    /// <summary>
    /// Redeems a voucher from a message, whatever carried it.
    /// </summary>
    /// <remarks>
    /// This was the body of <c>VoucherWorkflowController.ValidateTransactionSMS</c>. Living on the
    /// controller meant redemption was only reachable by an SMS gateway posting to that one action:
    /// a vendor replying to a Telegram bot, or any future channel, had nowhere to go. The rules are
    /// unchanged — the transport is now just an argument.
    /// </remarks>
    public class InboundMessageProcessor
    {
        /// <summary>
        /// How long after a redemption a repeat of the same code is treated as the vendor not
        /// having seen the confirmation, rather than as a second redemption attempt.
        /// </summary>
        private static readonly TimeSpan ConfirmationGrace = TimeSpan.FromMinutes(30);

        private static readonly Regex NonDigits = new("[^\\d]", RegexOptions.Compiled);

        private readonly Context _context;
        private readonly VoucherNotifications _notifications;
        private readonly IHubContext<DashboardHub> _dashboardHub;
        private readonly DashboardCache _dashboardCache;
        private readonly ILogger<InboundMessageProcessor> _logger;

        public InboundMessageProcessor(
            Context context,
            VoucherNotifications notifications,
            IHubContext<DashboardHub> dashboardHub,
            DashboardCache dashboardCache,
            ILogger<InboundMessageProcessor> logger)
        {
            _context = context;
            _notifications = notifications;
            _dashboardHub = dashboardHub;
            _dashboardCache = dashboardCache;
            _logger = logger;
        }

        /// <summary>
        /// Applies an inbound message to the ledger.
        /// </summary>
        /// <param name="from">
        /// The sender's number, used to identify the vendor. A channel that does not carry one —
        /// a chat bot — has to resolve it before calling.
        /// </param>
        public async Task<InboundResult> ProcessAsync(string from, string body, int countryId)
        {
            await LogAsync(from, body, countryId);

            // "TALON 482913 9876543210" and "482913 9876543210" both work: the code and the
            // identifier are the last two words, whatever precedes them.
            var words = (body ?? "").Split(' ').Where(w => !string.IsNullOrEmpty(w)).ToArray();
            if (words.Length < 2)
            {
                return InboundResult.Ignored("Not a talon message");
            }

            var voucherCode = NonDigits.Replace(words[^2], "");
            var personalIdentifier = words[^1];
            var phoneNumber = "+" + NonDigits.Replace(from ?? "", "").Trim();

            var vendor = await _context.Vendors
                .Where(v => (v.MobileNumber == phoneNumber && v.CountryId == countryId)
                    || v.SalesPersons.Any(s => s.MobileNumber == phoneNumber && s.CountryId == countryId))
                .FirstOrDefaultAsync();

            // Lazy loading is off: the checks below walk Category, the issuing record's beneficiary
            // and the latest credit record's vendor.
            var voucher = await _context.Vouchers
                .Include(v => v.Category).ThenInclude(c => c.Type)
                .Include(v => v.TransactionRecords).ThenInclude(t => t.Beneficiary)
                .Include(v => v.TransactionRecords).ThenInclude(t => t.Vendor)
                .Where(v => v.VoucherCode == voucherCode
                    && v.CountryId == countryId
                    && v.TransactionRecords.Any())
                .FirstOrDefaultAsync();

            if (vendor == null && voucher != null)
            {
                // A real code from a number that is not a registered vendor. Deliberately silent:
                // replying would confirm to a stranger that the code is valid.
                _logger.LogWarning("A valid voucher code was presented from an unregistered number.");
                return InboundResult.Ignored("Unauthorized Vendor");
            }

            if (vendor != null && voucher == null)
            {
                await _notifications.VoucherIsInvalidAsync(vendor, voucherCode);
                return InboundResult.Ignored("Invalid Voucher Code");
            }

            if (vendor == null && voucher == null)
            {
                return InboundResult.Ignored("Not a talon message");
            }

            if (voucher.Category.VendorTypeId != null && voucher.Category.VendorTypeId != vendor.TypeId)
            {
                await _notifications.VendorCannotUseVoucherAsync(voucher, vendor);
                return InboundResult.Ignored("Wrong Vendor Type");
            }

            if (voucher.Status == 4)
            {
                await _notifications.VoucherCancelledForVendorAsync(voucher, vendor);
                return InboundResult.Ignored("Voucher Canceled");
            }

            if (voucher.Status == 3)
            {
                var usedAt = voucher.LatestCreditTransactionRecords?.LastModifiedOn;
                var withinGrace = usedAt != null && DateTime.UtcNow - usedAt.Value < ConfirmationGrace;

                if (withinGrace)
                {
                    // On a bad network the confirmation is the part most likely to go missing, so a
                    // repeat inside the window re-sends it rather than refusing.
                    await _notifications.ConfirmationResentAsync(voucher);
                    return InboundResult.Ignored("Confirmation code resent");
                }

                await _notifications.VoucherAlreadyUsedAsync(voucher, vendor);
                return InboundResult.Ignored("Voucher Already Used");
            }

            var issuedTo = voucher.IssuingTransactionRecord?.Beneficiary;
            if (NonDigits.Replace(issuedTo?.PersonalIdentifier ?? "", "") != NonDigits.Replace(personalIdentifier, ""))
            {
                await _notifications.WrongPersonalIdentifierAsync(voucher, vendor);
                return InboundResult.Ignored("Wrong Personal Identifier");
            }

            var transactionRecord = new VoucherTransactionRecord
            {
                VoucherId = voucher.Id,
                BeneficiaryId = voucher.IssuingTransactionRecord.BeneficiaryId,
                VendorId = vendor.Id,
                Type = 2,
                ConfirmationCode = RandomNumber.RandomLong(6).ToString("D6"),
                LastModifiedOn = DateTime.UtcNow,
                CreatedOn = DateTime.UtcNow,
                // A redemption by message claims the voucher's full value; partial claims come from
                // the point-of-sale client.
                Value = voucher.Value,
                CountryId = voucher.CountryId,
                OrganizationId = voucher.OrganizationId
            };

            _context.VoucherTransactionRecords.Add(transactionRecord);
            voucher.Status = 3;

            await _context.SaveChangesAsync();

            // Fire-and-forget, as it always was: a dashboard refresh must not block the redemption.
            _ = MobileClientController.NotifyClients(_dashboardHub, _dashboardCache);

            await _notifications.TransactionConfirmedAsync(voucher);

            return InboundResult.Redeemed(transactionRecord.ConfirmationCode);
        }

        private async Task LogAsync(string from, string body, int countryId)
        {
            try
            {
                _context.MessageLogs.Add(new MessageLog
                {
                    MobileNumber = from,
                    Message = body,
                    DateTime = DateTime.UtcNow,
                    CountryId = countryId
                });

                await _context.SaveChangesAsync();
            }
            catch (Exception exception)
            {
                // Failing to log must not stop a redemption, but it should not vanish either.
                _logger.LogWarning(exception, "Could not record an inbound message.");
            }
        }
    }

    /// <summary>
    /// What happened to an inbound message. The reason is echoed back to the caller, which for an
    /// SMS gateway is diagnostic only — every outcome is still an HTTP 200, because a gateway
    /// treats a non-200 as a delivery failure and retries it.
    /// </summary>
    public sealed class InboundResult
    {
        private InboundResult(bool redeemed, string reason, string confirmationCode)
        {
            WasRedeemed = redeemed;
            Reason = reason;
            ConfirmationCode = confirmationCode;
        }

        public bool WasRedeemed { get; }
        public string Reason { get; }
        public string ConfirmationCode { get; }

        public static InboundResult Redeemed(string confirmationCode) =>
            new(true, "Confirmed", confirmationCode);

        public static InboundResult Ignored(string reason) => new(false, reason, null);
    }
}
