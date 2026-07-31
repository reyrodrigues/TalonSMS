using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using TalonAdmin.Models.Vouchers;
using TalonAdmin.Tests.Infrastructure;

namespace TalonAdmin.Tests;

/// <summary>
/// The identifier a beneficiary gives a vendor at redemption.
/// </summary>
/// <remarks>
/// It used to be called NationalId and was expected to hold a government document number, which
/// suits neither the caseload — many people a cash programme reaches have no such document — nor
/// the protection risk of writing a real one into a voucher database. A programme can issue one
/// instead.
/// </remarks>
public class PersonalIdentifierTests : IClassFixture<SeededTalonAppFactory>
{
    private readonly SeededTalonAppFactory _factory;
    private readonly TalonApiClient _api;

    public PersonalIdentifierTests(SeededTalonAppFactory factory)
    {
        _factory = factory;
        _api = new TalonApiClient(factory.CreateClient());
        _api.UseTenant(1, 1);
    }

    private async Task AuthenticateAsync() =>
        await _api.AuthenticateAsync(TalonAppFactory.AdminUserName, TalonAppFactory.AdminPassword);

    [Fact]
    public async Task A_generated_identifier_has_the_requested_number_of_digits()
    {
        await AuthenticateAsync();

        foreach (var length in new[] { 8, 9, 10 })
        {
            var result = (JObject)await _api.GetJsonAsync($"/api/PersonalIdentifier/Generate?length={length}");
            var identifier = result.Value<string>("Identifier")!;

            Assert.Matches("^[0-9]+$", identifier);
            Assert.Equal(length, identifier.Length);

            // Never a leading zero: "0000004217" is a valid draw but reads like a mistake and
            // invites somebody to trim it down.
            Assert.NotEqual('0', identifier[0]);
        }
    }

    [Fact]
    public async Task Lengths_outside_the_supported_range_are_refused()
    {
        await AuthenticateAsync();

        foreach (var length in new[] { 4, 7, 11, 20 })
        {
            using var response = await _api.GetAsync($"/api/PersonalIdentifier/Generate?length={length}");
            Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
        }
    }

    [Fact]
    public async Task Generated_identifiers_do_not_repeat()
    {
        await AuthenticateAsync();

        var seen = new HashSet<string>();
        for (var i = 0; i < 25; i++)
        {
            var result = (JObject)await _api.GetJsonAsync("/api/PersonalIdentifier/Generate");
            Assert.True(seen.Add(result.Value<string>("Identifier")!), "A draw repeated within 25 calls.");
        }
    }

    [Fact]
    public async Task An_identifier_already_in_use_is_never_handed_out_again()
    {
        await AuthenticateAsync();

        // Two beneficiaries sharing an identifier would let a vendor redeem one person's voucher
        // against the other's, so the generator checks before it answers.
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<Context>();

        var taken = new HashSet<string>(
            await context.Beneficiaries
                .Where(b => b.CountryId == 1 && b.PersonalIdentifier != null)
                .Select(b => b.PersonalIdentifier!)
                .ToListAsync());

        Assert.NotEmpty(taken);

        for (var i = 0; i < 10; i++)
        {
            var result = (JObject)await _api.GetJsonAsync("/api/PersonalIdentifier/Generate");
            Assert.DoesNotContain(result.Value<string>("Identifier")!, taken);
        }
    }

    [Fact]
    public async Task The_renamed_property_still_reads_the_column_it_always_did()
    {
        await AuthenticateAsync();

        // The CLR property is PersonalIdentifier; the column is still NationalId, because this
        // application has no migration framework and a rename would break existing databases.
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<Context>();

        var column = context.Model
            .FindEntityType(typeof(Beneficiary))!
            .FindProperty(nameof(Beneficiary.PersonalIdentifier))!
            .GetColumnName();

        Assert.Equal("NationalId", column);

        // And it round-trips through the API under the new name.
        var beneficiaries = await _api.BreezeQueryAsync("Beneficiaries",
            new { where = new { CountryId = 1 }, take = 1 });

        var beneficiary = Assert.Single(beneficiaries);
        Assert.NotNull(beneficiary["PersonalIdentifier"]);
        Assert.Null(beneficiary["NationalId"]);
    }
}
