using Newtonsoft.Json.Linq;
using TalonAdmin.Tests.Infrastructure;

namespace TalonAdmin.Tests;

/// <summary>
/// Walks the whole business workflow against a fresh database, through the same HTTP surface the
/// AngularJS client uses: stand up a country, register vendors and beneficiaries, define a
/// programme, distribute vouchers to a cycle, redeem one by SMS, then check that the dashboard and
/// the reports agree with what happened.
/// </summary>
public class VoucherDistributionScenarioTests : IClassFixture<TalonAppFactory>
{
    private const int BeneficiaryCount = 4;
    private const decimal VoucherValue = 25m;

    private readonly TalonAppFactory _factory;
    private readonly TalonApiClient _api;

    public VoucherDistributionScenarioTests(TalonAppFactory factory)
    {
        _factory = factory;
        _api = new TalonApiClient(factory.CreateClient());
    }

    [Fact]
    public async Task Distributes_vouchers_to_a_cycle_and_redeems_one_by_sms()
    {
        await _api.AuthenticateAsync(TalonAppFactory.AdminUserName, TalonAppFactory.AdminPassword);

        var tenant = await SetUpCountryAsync();
        _api.UseTenant(tenant.CountryId, tenant.OrganizationId);

        var reference = await SetUpCountryReferenceDataAsync();
        var cycle = await RegisterBeneficiariesAsync();
        var programId = await DefineProgrammeAsync(reference);

        // --- distribute ------------------------------------------------------------------
        await _api.PostJsonAsync("/api/VoucherWorkflow/DistributeVouchers", new
        {
            ProgramId = programId,
            GroupId = cycle.GroupId,
            LocationId = reference.LocationId
        });

        var distributions = await _api.BreezeQueryAsync("Distributions",
            new { where = new { ProgramId = programId } });

        var distribution = Assert.Single(distributions);
        var distributionId = distribution.Value<int>("Id");

        // One voucher per beneficiary in the cycle, all unclaimed and issued to a beneficiary.
        var vouchers = await _api.BreezeQueryAsync("Vouchers",
            new { where = new { DistributionId = distributionId }, expand = new[] { "TransactionRecords" } });

        Assert.Equal(BeneficiaryCount, vouchers.Count);
        Assert.All(vouchers, v => Assert.Equal(VoucherValue, v.Value<decimal>("Value")));
        Assert.All(vouchers, v => Assert.Equal(1, v.Value<int>("Status")));
        Assert.All(vouchers, v => Assert.Single(v["TransactionRecords"]!));

        var issuedCodes = vouchers.Select(v => v.Value<string>("VoucherCode")!).ToArray();
        Assert.All(issuedCodes, code => Assert.False(string.IsNullOrWhiteSpace(code)));
        Assert.Equal(issuedCodes.Length, issuedCodes.Distinct().Count());

        // Every issuing record is a credit (type 1) tied to one of our beneficiaries.
        var issued = await _api.BreezeQueryAsync("IssuedVoucherTransactionRecords");
        Assert.Equal(BeneficiaryCount, issued.Count);
        Assert.All(issued, r => Assert.Contains(r.Value<int>("BeneficiaryId"), cycle.BeneficiaryIds));

        // --- redeem one voucher by SMS ---------------------------------------------------
        var redeemed = vouchers.First(v =>
            v["TransactionRecords"]!.Any(t => t.Value<int>("BeneficiaryId") == cycle.BeneficiaryIds[0]));
        var redeemedCode = redeemed.Value<string>("VoucherCode");

        var smsReply = await RedeemBySmsAsync(
            tenant.IsoAlpha2, reference.VendorMobileNumber, redeemedCode!, cycle.NationalIds[0]);

        Assert.DoesNotContain("Invalid", smsReply);
        Assert.DoesNotContain("Unauthorized", smsReply);
        Assert.DoesNotContain("Not a valid country", smsReply);

        // --- the redemption is visible everywhere ----------------------------------------
        var afterRedemption = await _api.BreezeQueryAsync("Vouchers",
            new { where = new { Id = redeemed.Value<int>("Id") }, expand = new[] { "TransactionRecords" } });

        var settled = Assert.Single(afterRedemption);
        Assert.Equal(3, settled.Value<int>("Status"));

        var debits = settled["TransactionRecords"]!.Where(t => t.Value<int>("Type") == 2).ToArray();
        var debit = Assert.Single(debits);
        Assert.Equal(VoucherValue, debit.Value<decimal>("Value"));
        Assert.Equal(reference.VendorId, debit.Value<int>("VendorId"));

        // Remaining vouchers are untouched.
        var stillUnclaimed = await _api.BreezeQueryAsync("Vouchers",
            new { where = new { DistributionId = distributionId, Status = 1 } });
        Assert.Equal(BeneficiaryCount - 1, stillUnclaimed.Count);

        await AssertDashboardReflectsDistributionAsync(programId);
        await AssertDistributionReportListsBeneficiariesAsync(distributionId, cycle.BeneficiaryNames);
    }

    // ---- workflow steps ---------------------------------------------------------------

    private async Task<(int CountryId, int OrganizationId, string IsoAlpha2)> SetUpCountryAsync()
    {
        // The organisation is created by the application's own start-up seeding.
        var organizations = await _api.BreezeQueryAsync("Organizations", admin: true);
        var organizationId = Assert.Single(organizations).Value<int>("Id");

        // A Country needs its CountrySettings row; both go in one save bundle and Breeze resolves
        // the temporary key into the real foreign key.
        const int settingsTempId = -1;
        const int countryTempId = -2;
        var isoAlpha2 = "T" + Random.Shared.Next(0, 9);

        var save = await _api.BreezeSaveAsync(new[]
        {
            TalonApiClient.NewEntity("CountrySettings", "CountrySettings", TalonApiClient.AdminNamespace, settingsTempId, new
            {
                SmsBackendType = 0,
                ServiceUrl = "http://localhost/sms/",
                ServiceUser = "test",
                ServicePassword = "test"
            }),
            TalonApiClient.NewEntity("Country", "Countries", TalonApiClient.AdminNamespace, countryTempId, new
            {
                Name = "Testland",
                IsoAlpha2 = isoAlpha2,
                IsoAlpha3 = "TST",
                CurrencyIsoCode = "USD",
                CurrencyUnicodeSymbol = "$",
                CountryCallingCode = "99",
                LanguageCode = "en",
                SettingsId = settingsTempId
            })
        }, admin: true);

        var countryId = save.RealId(countryTempId);
        var settingsId = save.RealId(settingsTempId);

        await _api.BreezeSaveAsync(new[]
        {
            TalonApiClient.NewEntity("OrganizationCountry", "OrganizationCountries", TalonApiClient.AdminNamespace, -1, new
            {
                OrganizationId = organizationId,
                CountryId = countryId,
                SettingsId = settingsId
            })
        }, admin: true);

        return (countryId, organizationId, isoAlpha2);
    }

    private async Task<(int LocationId, int VendorTypeId, int VoucherTypeId, int VendorId, string VendorMobileNumber)>
        SetUpCountryReferenceDataAsync()
    {
        var tenant = new { CountryId = _api.CountryId, OrganizationId = _api.OrganizationId };

        var save = await _api.BreezeSaveAsync(new[]
        {
            TalonApiClient.NewEntity("Location", "Locations", TalonApiClient.VoucherNamespace, -1, new
            {
                Name = "Test District",
                tenant.CountryId
            }),
            TalonApiClient.NewEntity("VendorType", "VendorTypes", TalonApiClient.VoucherNamespace, -2, new
            {
                Name = "General Store",
                tenant.CountryId
            }),
            TalonApiClient.NewEntity("VoucherType", "VoucherTypes", TalonApiClient.VoucherNamespace, -3, new
            {
                Name = "Food",
                tenant.CountryId,
                tenant.OrganizationId
            })
        });

        var locationId = save.RealId(-1);
        var vendorTypeId = save.RealId(-2);
        var voucherTypeId = save.RealId(-3);

        const string vendorMobile = "+9955500001";
        var vendorSave = await _api.BreezeSaveAsync(new[]
        {
            TalonApiClient.NewEntity("Vendor", "Vendors", TalonApiClient.VoucherNamespace, -1, new
            {
                Name = "Test Vendor",
                FirstName = "Vera",
                LastName = "Vendor",
                MobileNumber = vendorMobile,
                NationalId = "V-0001",
                UserName = "testvendor",
                LocationId = locationId,
                TypeId = vendorTypeId,
                tenant.CountryId
            })
        });

        return (locationId, vendorTypeId, voucherTypeId, vendorSave.RealId(-1), vendorMobile);
    }

    private async Task<(int GroupId, int[] BeneficiaryIds, string[] NationalIds, string[] BeneficiaryNames)>
        RegisterBeneficiariesAsync()
    {
        var tenant = new { CountryId = _api.CountryId, OrganizationId = _api.OrganizationId };

        var groupSave = await _api.BreezeSaveAsync(new[]
        {
            TalonApiClient.NewEntity("BeneficiaryGroup", "BeneficiaryGroups", TalonApiClient.VoucherNamespace, -1, new
            {
                Name = "Cycle 1",
                tenant.CountryId,
                tenant.OrganizationId
            })
        });

        var groupId = groupSave.RealId(-1);

        var nationalIds = Enumerable.Range(1, BeneficiaryCount)
            .Select(i => $"NID{i:D5}").ToArray();

        var beneficiaries = Enumerable.Range(1, BeneficiaryCount).Select(i =>
            TalonApiClient.NewEntity("Beneficiary", "Beneficiaries", TalonApiClient.VoucherNamespace, -i, new
            {
                FirstName = "Test",
                LastName = $"Beneficiary {i:D2}",
                NationalId = nationalIds[i - 1],
                MobileNumber = $"+995551{i:D4}",
                BirthYear = (1980 + i).ToString(),
                Sex = i % 2 == 0 ? 1 : 2,
                PIN = $"{1000 + i}",
                GroupId = groupId,
                tenant.CountryId,
                tenant.OrganizationId
            })).ToArray();

        var save = await _api.BreezeSaveAsync(beneficiaries);
        var ids = Enumerable.Range(1, BeneficiaryCount).Select(i => save.RealId(-i)).ToArray();
        var names = Enumerable.Range(1, BeneficiaryCount).Select(i => $"Test Beneficiary {i:D2}").ToArray();

        return (groupId, ids, nationalIds, names);
    }

    private async Task<int> DefineProgrammeAsync(
        (int LocationId, int VendorTypeId, int VoucherTypeId, int VendorId, string VendorMobileNumber) reference)
    {
        var tenant = new { CountryId = _api.CountryId, OrganizationId = _api.OrganizationId };

        var save = await _api.BreezeSaveAsync(new[]
        {
            TalonApiClient.NewEntity("Program", "Programs", TalonApiClient.VoucherNamespace, -1, new
            {
                Name = "Test Cash Programme",
                FundCodes = "TST-001",
                DistributionMechanism = 1,
                VoucherCodeLength = 6,
                ShowInDashboard = true,
                CreatedOn = DateTime.UtcNow,
                ModifiedOn = DateTime.UtcNow,
                tenant.CountryId,
                tenant.OrganizationId
            })
        });

        var programId = save.RealId(-1);

        await _api.BreezeSaveAsync(new[]
        {
            TalonApiClient.NewEntity("ProgramVoucherCategory", "ProgramVoucherCategories", TalonApiClient.VoucherNamespace, -1, new
            {
                ProgramId = programId,
                TypeId = reference.VoucherTypeId,
                VendorTypeId = reference.VendorTypeId,
                Value = VoucherValue,
                NumberOfVouchers = 0,
                IssuedVouchers = 0,
                tenant.CountryId,
                tenant.OrganizationId
            })
        });

        return programId;
    }

    private async Task<string> RedeemBySmsAsync(string isoAlpha2, string vendorMobile, string voucherCode, string nationalId)
    {
        // The gateway posts the vendor's number as the sender and "<voucherCode> <nationalId>" as
        // the body; the endpoint is anonymous and keyed by the country's ISO code.
        using var response = await _factory.CreateClient().PostAsync(
            $"/api/VoucherWorkflow/ValidateTransactionSMS/{isoAlpha2}",
            new StringContent(
                new JObject
                {
                    ["to"] = vendorMobile,
                    ["from"] = vendorMobile,
                    ["message"] = $"{voucherCode} {nationalId}"
                }.ToString(),
                System.Text.Encoding.UTF8,
                "application/json"));

        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, $"SMS webhook failed ({(int)response.StatusCode}): {body}");

        return body;
    }

    // ---- verification -----------------------------------------------------------------

    private async Task AssertDashboardReflectsDistributionAsync(int programId)
    {
        var dashboard = await _api.GetJsonAsync("/api/Dashboard/DashboardSummary");

        var programme = Assert.Single(dashboard, p => p["Program"]!.Value<int>("Id") == programId);

        Assert.Equal(BeneficiaryCount, programme.Value<int>("TotalVouchers"));
        Assert.Equal(1, programme.Value<int>("VouchersUsed"));
        Assert.Equal(VoucherValue * BeneficiaryCount, programme.Value<decimal>("IssuedAmount"));
        Assert.Equal(VoucherValue, programme.Value<decimal>("ClaimedAmount"));
    }

    private async Task AssertDistributionReportListsBeneficiariesAsync(int distributionId, string[] beneficiaryNames)
    {
        _factory.PdfRenderer.Clear();

        var pdf = await _api.PostForBytesAsync("/api/Reports/DistributionReport", new
        {
            DistributionId = distributionId,
            CountryId = _api.CountryId,
            OrganizationId = _api.OrganizationId,
            PaperSize = "A4",
            TimezoneOffset = 0d
        });

        Assert.StartsWith("%PDF", System.Text.Encoding.ASCII.GetString(pdf[..4]));

        // The renderer captured the HTML the Razor template produced, so the report's contents can
        // be asserted directly rather than through the PDF binary.
        var html = _factory.PdfRenderer.LastHtml;

        Assert.Contains("Distribution Report", html);
        Assert.Contains("Test Cash Programme", html);
        Assert.All(beneficiaryNames, name => Assert.Contains(name, html));
    }
}
