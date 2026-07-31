using System;
using System.Collections.Generic;
using System.Linq;
using TalonAdmin.Models.Admin;

namespace TalonAdmin.Messaging
{
    /// <summary>
    /// The wording a country starts with, applied when its settings are first created.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Talon used to ship none. Every template was blank on a new deployment and nothing said so,
    /// which meant the first distribution issued perfectly valid codes and told nobody: each message
    /// failed with "the message body is empty", visible only to somebody who thought to open the
    /// delivery log. Working wording out of the box is the difference between a misconfiguration
    /// that announces itself and one that silently does not deliver.
    /// </para>
    /// <para>
    /// These are applied **only when a settings row is created**, never to one that already exists.
    /// A country whose staff deliberately cleared a template — because they do not want to pay for
    /// that message — must not have it reinstated by an upgrade, and every deployment already in the
    /// field has settings rows that predate this.
    /// </para>
    /// <para>
    /// Deliberately plain English, and deliberately anonymous: the sending organisation's name
    /// belongs in these messages, but only the deployment knows it. See
    /// <c>docs/MESSAGING.md</c> for what to change before a real distribution.
    /// </para>
    /// </remarks>
    public static class MessageTemplateDefaults
    {
        /// <summary>Template name to starting wording. The names are the stored keys.</summary>
        public static readonly IReadOnlyDictionary<string, string> ByName =
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["Beneficiary Voucher Message"] =
                    "@Model.Beneficiary.FirstName, your voucher code is @Model.Voucher.VoucherCode, " +
                    "worth @Model.Voucher.Value. Show this code at a participating shop.",

                ["Beneficiary Confirmed Message"] =
                    "Your voucher @Model.Voucher.VoucherCode was redeemed at @Model.Vendor.Name. Thank you.",

                ["Vendor Confirmed Message"] =
                    "Accepted: voucher @Model.Voucher.VoucherCode for @Model.Voucher.Value.",

                ["Beneficiary Cancelled Voucher Message"] =
                    "Your voucher @Model.Voucher.VoucherCode has been cancelled. " +
                    "Please contact the programme office.",

                ["Vendor Cancelled Message"] =
                    "Voucher @Model.Voucher.VoucherCode has been cancelled and can no longer be accepted.",

                ["Beneficiary Already Used Message"] =
                    "Voucher @Model.Voucher.VoucherCode has already been redeemed. " +
                    "Please contact the programme office.",

                ["Vendor Already Used Message"] =
                    "Voucher @Model.Voucher.VoucherCode has already been redeemed and cannot be accepted again.",

                ["Beneficiary Cannot Accept Message"] =
                    "Your voucher @Model.Voucher.VoucherCode cannot be used at that shop. " +
                    "Please try another participating shop.",

                ["Vendor Cannot Accept Message"] =
                    "Voucher @Model.Voucher.VoucherCode cannot be accepted at this shop.",

                // The stored name predates the rename to "personal identifier"; the interface
                // relabels it. Renaming the key would orphan the wording countries already have.
                ["Wrong National Id Message"] =
                    "The identifier given does not match voucher @Model.Voucher.VoucherCode. " +
                    "Please check it and try again.",

                // This one fires when the code matched no voucher at all, so it has to reach for
                // the text the vendor actually sent rather than a voucher that is not there.
                ["Invalid Voucher Message"] =
                    "Code @Model.VoucherCode was not recognised. Please check it and try again.",
            };

        /// <summary>
        /// Adds the starting wording to a settings row, leaving alone any template it already has.
        /// </summary>
        /// <returns>The templates that were added.</returns>
        public static IReadOnlyList<CountrySettingsProperty> ApplyTo(CountrySettings settings)
        {
            if (settings == null)
            {
                return Array.Empty<CountrySettingsProperty>();
            }

            settings.PropertyCollection ??= new System.Collections.ObjectModel.ObservableCollection<CountrySettingsProperty>();

            var existing = settings.PropertyCollection
                .Select(p => p.Name)
                .ToHashSet(StringComparer.Ordinal);

            var added = new List<CountrySettingsProperty>();

            foreach (var (name, value) in ByName)
            {
                if (existing.Contains(name))
                {
                    continue;
                }

                var property = new CountrySettingsProperty
                {
                    SettingsId = settings.Id,
                    Name = name,
                    Value = value
                };

                settings.PropertyCollection.Add(property);
                added.Add(property);
            }

            return added;
        }
    }
}
