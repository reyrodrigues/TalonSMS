using Newtonsoft.Json.Linq;
using TalonAdmin.Tests.Infrastructure;

namespace TalonAdmin.Tests;

/// <summary>
/// Guards the wire contract between breeze-client 1.5 and the Breeze 7 server. Every assertion here
/// corresponds to a way the grids broke during the .NET 10 migration, and each failure mode was
/// silent — rows rendered, but blank, or unpaged. See MIGRATION.md.
/// </summary>
public class BreezeContractTests : IClassFixture<SeededTalonAppFactory>
{
    private readonly TalonApiClient _api;

    public BreezeContractTests(SeededTalonAppFactory factory)
    {
        _api = new TalonApiClient(factory.CreateClient());
        _api.UseTenant(1, 1);
    }

    private async Task AuthenticateAsync() =>
        await _api.AuthenticateAsync(TalonAppFactory.AdminUserName, TalonAppFactory.AdminPassword);

    [Fact]
    public async Task Entities_are_serialised_with_the_clr_property_names()
    {
        await AuthenticateAsync();

        var beneficiaries = await _api.BreezeQueryAsync("Beneficiaries");
        var beneficiary = (JObject)beneficiaries.First();

        // ASP.NET Core's default camelCase resolver would leave every property unset on the client,
        // because Breeze matches incoming JSON against the PascalCase names in its metadata.
        Assert.Contains("FirstName", beneficiary.Properties().Select(p => p.Name));
        Assert.DoesNotContain("firstName", beneficiary.Properties().Select(p => p.Name));
    }

    [Fact]
    public async Task Expanded_entities_carry_a_type_discriminator()
    {
        await AuthenticateAsync();

        var beneficiaries = await _api.BreezeQueryAsync("Beneficiaries",
            new { expand = new[] { "Location" }, take = 1 });

        var location = Assert.Single(beneficiaries)["Location"];

        Assert.NotNull(location);

        // Without $type the client cannot materialise a reference navigation and throws
        // "Cannot read properties of undefined (reading 'entityState')".
        Assert.Contains("TalonAdmin.Models.Vouchers.Location", location!.Value<string>("$type"));
    }

    [Fact]
    public async Task Metadata_property_names_match_the_payload_property_names()
    {
        await AuthenticateAsync();

        var metadata = JObject.Parse((await _api.GetJsonAsync("/breeze/EVM/Metadata")).ToString());
        var beneficiaryType = metadata["structuralTypes"]!
            .First(t => t.Value<string>("shortName") == "Beneficiary");

        var metadataNames = beneficiaryType["dataProperties"]!
            .Select(p => p.Value<string>("name") ?? p.Value<string>("nameOnServer")!)
            .ToHashSet();

        var payload = (JObject)(await _api.BreezeQueryAsync("Beneficiaries", new { take = 1 })).First();

        // The client keys the payload off the metadata, so the two have to use the same casing.
        Assert.Contains("FirstName", metadataNames);
        Assert.Contains("FirstName", payload.Properties().Select(p => p.Name));
    }

    [Fact]
    public async Task Take_and_skip_are_applied_by_the_server()
    {
        await AuthenticateAsync();

        var all = await _api.BreezeQueryAsync("Beneficiaries");
        Assert.True(all.Count > 2, "The seeded programme should have more than two beneficiaries.");

        var page = await _api.BreezeQueryAsync("Beneficiaries", new { take = 2, orderBy = new[] { "Id" } });
        Assert.Equal(2, page.Count);

        var second = await _api.BreezeQueryAsync("Beneficiaries", new { skip = 2, take = 2, orderBy = new[] { "Id" } });
        Assert.Empty(page.Select(p => p.Value<int>("Id")).Intersect(second.Select(p => p.Value<int>("Id"))));
    }

    [Fact]
    public async Task InlineCount_returns_the_unpaged_total_in_an_envelope()
    {
        await AuthenticateAsync();

        var all = await _api.BreezeQueryAsync("Beneficiaries");

        var (results, inlineCount) = await _api.BreezeQueryWithCountAsync(
            "Beneficiaries", new { take = 1, inlineCount = true });

        Assert.Single(results);

        // The grid's paging footer reads this; without it the client reports "NaN total entries".
        Assert.Equal(all.Count, inlineCount);
    }

    [Fact]
    public async Task Where_clauses_are_translated_to_sql()
    {
        await AuthenticateAsync();

        var claimed = await _api.BreezeQueryAsync("Vouchers", new { where = new { Status = 3 } });
        var all = await _api.BreezeQueryAsync("Vouchers");

        Assert.NotEmpty(claimed);
        Assert.True(claimed.Count < all.Count, "The filter should not have returned every voucher.");
        Assert.All(claimed, v => Assert.Equal(3, v.Value<int>("Status")));
    }

    [Fact]
    public async Task Expand_populates_reference_and_collection_navigations()
    {
        await AuthenticateAsync();

        var vouchers = await _api.BreezeQueryAsync("Vouchers",
            new { expand = new[] { "Category", "TransactionRecords" }, take = 1 });

        var voucher = Assert.Single(vouchers);

        Assert.NotNull(voucher["Category"]);
        Assert.NotNull(voucher["Category"]!["Id"]);
        Assert.NotEmpty(voucher["TransactionRecords"]!);
    }

    [Fact]
    public async Task Both_breeze_endpoints_publish_metadata()
    {
        await AuthenticateAsync();

        foreach (var endpoint in new[] { "/breeze/EVM/Metadata", "/breeze/Admin/Metadata" })
        {
            var metadata = JObject.Parse((await _api.GetJsonAsync(endpoint)).ToString());
            Assert.NotEmpty(metadata["structuralTypes"]!);
        }
    }

    [Fact]
    public async Task Saving_a_change_round_trips_through_the_save_bundle()
    {
        await AuthenticateAsync();

        var before = (JObject)(await _api.BreezeQueryAsync("Beneficiaries", new { take = 1, orderBy = new[] { "Id" } })).First();
        var id = before.Value<int>("Id");

        var edited = (JObject)before.DeepClone();
        edited.Remove("$type");
        edited["LastName"] = "Renamed By Test";
        edited["entityAspect"] = new JObject
        {
            ["entityTypeName"] = $"Beneficiary:#{TalonApiClient.VoucherNamespace}",
            ["defaultResourceName"] = "Beneficiaries",
            ["entityState"] = "Modified",
            ["originalValuesMap"] = new JObject { ["LastName"] = before.Value<string>("LastName") },
            ["autoGeneratedKey"] = new JObject
            {
                ["propertyName"] = "Id",
                ["autoGeneratedKeyType"] = "Identity"
            }
        };

        var result = await _api.BreezeSaveAsync(new[] { edited });
        Assert.Equal(1, result.SavedCount);

        var after = (JObject)(await _api.BreezeQueryAsync("Beneficiaries", new { where = new { Id = id } })).First();
        Assert.Equal("Renamed By Test", after.Value<string>("LastName"));
    }
}
