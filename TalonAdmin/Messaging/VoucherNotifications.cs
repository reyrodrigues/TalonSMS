using System;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using TalonAdmin.Models.Vouchers;

namespace TalonAdmin.Messaging
{
    /// <summary>
    /// Every message the voucher workflow sends, and the templates behind them.
    /// </summary>
    /// <remarks>
    /// These used to be private methods on <c>VoucherWorkflowController</c>, which meant the only
    /// way to notify anybody was to be inside that controller — and so the only way to accept a
    /// redemption was to be inside its inbound SMS action. Pulling them out is what lets a
    /// redemption arrive from somewhere other than an SMS gateway.
    /// </remarks>
    public class VoucherNotifications
    {
        /// <summary>Matches the "@Model.A.B" / "@(Model.A.B)" expressions the stored templates use.</summary>
        private static readonly Regex ModelExpression =
            new(@"@\(?Model((?:\.[A-Za-z_][A-Za-z0-9_]*)+)\)?", RegexOptions.Compiled);

        private readonly Models.Admin.AdminContext _adminContext;
        private readonly IOutboundMessageQueue _queue;

        public VoucherNotifications(Models.Admin.AdminContext adminContext, IOutboundMessageQueue queue)
        {
            _adminContext = adminContext;
            _queue = queue;
        }

        // ---- the individual notifications ------------------------------------------------------

        public Task VoucherIssuedAsync(Voucher voucher)
        {
            var record = voucher.IssuingTransactionRecord;
            var model = new { Voucher = voucher, Vendor = record.Vendor, Beneficiary = record.Beneficiary };

            return SendAsync("Beneficiary Voucher Message", record.Beneficiary, voucher, model);
        }

        public Task VoucherCancelledForBeneficiaryAsync(Voucher voucher)
        {
            var record = voucher.IssuingTransactionRecord;
            var model = new { Voucher = voucher, Vendor = record.Vendor, Beneficiary = record.Beneficiary };

            return SendAsync("Beneficiary Cancelled Voucher Message", record.Beneficiary, voucher, model);
        }

        /// <summary>Both parties are told when a redemption completes.</summary>
        public async Task TransactionConfirmedAsync(Voucher voucher)
        {
            var record = voucher.TransactionRecords
                .Where(t => t.Type == 2)
                .OrderByDescending(t => t.LastModifiedOn)
                .FirstOrDefault();

            if (record == null)
            {
                return;
            }

            var model = new
            {
                Voucher = voucher,
                Vendor = record.Vendor,
                Beneficiary = record.Beneficiary,
                TransactionRecord = record
            };

            await SendAsync("Vendor Confirmed Message", record.Vendor, voucher, model);
            await SendAsync("Beneficiary Confirmed Message", record.Beneficiary, voucher, model);
        }

        /// <summary>
        /// A vendor re-sending a code within the grace window gets the same confirmation again
        /// rather than a second redemption.
        /// </summary>
        public Task ConfirmationResentAsync(Voucher voucher)
        {
            var record = voucher.LatestCreditTransactionRecords;
            var model = new
            {
                Voucher = voucher,
                Vendor = record.Vendor,
                Beneficiary = record.Beneficiary,
                TransactionRecord = record
            };

            return SendAsync("Vendor Confirmed Message", record.Vendor, voucher, model);
        }

        public Task VoucherCancelledForVendorAsync(Voucher voucher, Vendor vendor)
        {
            var record = voucher.LatestCreditTransactionRecords;
            var model = new { Voucher = voucher, Vendor = vendor, Beneficiary = record?.Beneficiary };

            return SendAsync("Vendor Cancelled Message", vendor, voucher, model);
        }

        public async Task VoucherAlreadyUsedAsync(Voucher voucher, Vendor vendor)
        {
            var record = voucher.LatestCreditTransactionRecords;
            var model = new
            {
                Voucher = voucher,
                Vendor = record.Vendor,
                Beneficiary = record.Beneficiary,
                TransactionRecord = record
            };

            await SendAsync("Vendor Already Used Message", record.Vendor, voucher, model);
            await SendAsync("Beneficiary Already Used Message", record.Beneficiary, voucher, model);
        }

        public async Task VendorCannotUseVoucherAsync(Voucher voucher, Vendor vendor)
        {
            var record = voucher.IssuingTransactionRecord;
            var model = new { Voucher = voucher, Vendor = vendor, Beneficiary = record.Beneficiary };

            await SendAsync("Vendor Cannot Accept Message", vendor, voucher, model);
            await SendAsync("Beneficiary Cannot Accept Message", record.Beneficiary, voucher, model);
        }

        public Task WrongPersonalIdentifierAsync(Voucher voucher, Vendor vendor)
        {
            var model = new { Voucher = voucher, Vendor = vendor, Beneficiary = new { } };

            return SendAsync("Wrong National Id Message", vendor, voucher, model);
        }

        public Task VoucherIsInvalidAsync(Vendor vendor, string voucherCode)
        {
            var model = new { Voucher = new { }, Vendor = vendor, Beneficiary = new { }, VoucherCode = voucherCode };

            return SendAsync("Invalid Voucher Message", vendor.MobileNumber, vendor.Name,
                vendor.CountryId, null, model);
        }

        // ---- composition -----------------------------------------------------------------------

        private Task SendAsync(string templateKey, Beneficiary beneficiary, Voucher voucher, object model) =>
            beneficiary == null
                ? Task.CompletedTask
                : SendAsync(templateKey, beneficiary.MobileNumber, beneficiary.Name,
                    voucher.CountryId, voucher.OrganizationId, model);

        private Task SendAsync(string templateKey, Vendor vendor, Voucher voucher, object model) =>
            vendor == null
                ? Task.CompletedTask
                : SendAsync(templateKey, vendor.MobileNumber, vendor.Name,
                    voucher.CountryId, voucher.OrganizationId, model);

        private async Task SendAsync(
            string templateKey,
            string to,
            string name,
            int countryId,
            int? organizationId,
            object model)
        {
            var message = await CompileAsync(templateKey, countryId, organizationId, model);

            await _queue.EnqueueAsync(to, name, message, countryId, organizationId ?? 0);
        }

        /// <summary>
        /// Resolves a template for a country — an organization's own wording overrides the
        /// country's — and substitutes the model into it.
        /// </summary>
        public async Task<string> CompileAsync(string key, int countryId, int? organizationId, object model)
        {
            var countryTemplate = await _adminContext.Countries
                .AsNoTracking()
                .Include(c => c.Settings)
                .ThenInclude(s => s.PropertyCollection)
                .Where(c => c.Id == countryId)
                .Select(c => c.Settings.PropertyCollection
                    .Where(p => p.Name == key)
                    .Select(p => p.Value)
                    .FirstOrDefault())
                .FirstOrDefaultAsync();

            string organizationTemplate = null;
            if (organizationId != null)
            {
                organizationTemplate = await _adminContext.OrganizationCountries
                    .AsNoTracking()
                    .Include(o => o.Settings)
                    .ThenInclude(s => s.PropertyCollection)
                    .Where(o => o.OrganizationId == organizationId && o.CountryId == countryId)
                    .Select(o => o.Settings.PropertyCollection
                        .Where(p => p.Name == key)
                        .Select(p => p.Value)
                        .FirstOrDefault())
                    .FirstOrDefaultAsync();
            }

            var template = string.IsNullOrEmpty(organizationTemplate) ? countryTemplate : organizationTemplate;

            try
            {
                return Render(template, model);
            }
            catch
            {
                // A template referring to something the model does not carry should not stop a
                // redemption; the empty body then fails visibly in the delivery log.
                return "";
            }
        }

        /// <summary>
        /// Minimal stand-in for RazorEngine, which has no build for current .NET: replaces
        /// "@Model.A.B" / "@(Model.A.B)" with the value read off the model by reflection. Anything
        /// relying on Razor control flow will not render.
        /// </summary>
        public static string Render(string template, object model)
        {
            if (string.IsNullOrEmpty(template))
            {
                return template;
            }

            return ModelExpression.Replace(template, match =>
            {
                object current = model;

                foreach (var member in match.Groups[1].Value.Split('.').Where(m => !string.IsNullOrEmpty(m)))
                {
                    if (current == null)
                    {
                        return "";
                    }

                    PropertyInfo property = current.GetType().GetProperty(member);
                    if (property == null)
                    {
                        return "";
                    }

                    current = property.GetValue(current);
                }

                return current == null ? "" : Convert.ToString(current, CultureInfo.CurrentCulture);
            });
        }
    }
}
