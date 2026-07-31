using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using TalonAdmin.Messaging;
using TalonAdmin.Models.Admin;
using TalonAdmin.Models.Vouchers;
using TalonAdmin.Tests.Infrastructure;

namespace TalonAdmin.Tests;

/// <summary>
/// The wording a country starts with.
/// </summary>
/// <remarks>
/// A default template is worse than no default if it is wrong, because it sends: an expression
/// naming a field that does not exist renders as nothing and the message goes out anyway. So every
/// shipped default is rendered here against a realistic model and checked for the gap that would
/// leave, rather than being eyeballed.
/// </remarks>
public class MessageTemplateDefaultTests : IClassFixture<TalonAppFactory>, IAsyncLifetime
{
    private readonly TalonAppFactory _factory;
    private readonly TalonApiClient _api;

    public MessageTemplateDefaultTests(TalonAppFactory factory)
    {
        _factory = factory;
        _api = new TalonApiClient(factory.CreateClient());
    }

    public async Task InitializeAsync() =>
        await _api.AuthenticateAsync(TalonAppFactory.AdminUserName, TalonAppFactory.AdminPassword);

    public Task DisposeAsync() => Task.CompletedTask;

    private static object Model() => new
    {
        Voucher = new Voucher { VoucherCode = "482913", Value = 50m },
        Vendor = new Vendor { Name = "Al Mahatta Grocery" },
        Beneficiary = new Beneficiary { FirstName = "Tariq", LastName = "Jaber" },
        VoucherCode = "482913"
    };

    [Fact]
    public void Every_default_renders_without_leaving_a_gap()
    {
        foreach (var (name, template) in MessageTemplateDefaults.ByName)
        {
            var rendered = VoucherNotifications.Render(template, Model());

            Assert.False(string.IsNullOrWhiteSpace(rendered), $"{name} rendered empty.");

            // What a misspelled or unreachable field leaves behind. Every default is written to
            // substitute something for every expression it contains.
            Assert.DoesNotContain("  ", rendered);
            Assert.DoesNotContain(" .", rendered);
            Assert.DoesNotContain("@Model", rendered);
        }
    }

    [Fact]
    public void There_is_a_default_for_every_template_the_workflow_asks_for()
    {
        // The names the notifications actually request. A default filed under a name nothing looks
        // up would sit in the settings screen looking configured and never be sent.
        var requested = new[]
        {
            "Beneficiary Voucher Message",
            "Beneficiary Cancelled Voucher Message",
            "Vendor Confirmed Message",
            "Beneficiary Confirmed Message",
            "Vendor Cancelled Message",
            "Vendor Already Used Message",
            "Beneficiary Already Used Message",
            "Vendor Cannot Accept Message",
            "Beneficiary Cannot Accept Message",
            "Wrong National Id Message",
            "Invalid Voucher Message"
        };

        Assert.Equal(
            requested.OrderBy(n => n).ToArray(),
            MessageTemplateDefaults.ByName.Keys.OrderBy(n => n).ToArray());
    }

    [Fact]
    public void The_voucher_default_carries_the_code()
    {
        // The one message that has to work: without the code in it the distribution is undeliverable
        // no matter how well everything else is configured.
        var rendered = VoucherNotifications.Render(
            MessageTemplateDefaults.ByName["Beneficiary Voucher Message"], Model());

        Assert.Contains("482913", rendered);
    }

    [Fact]
    public void Wording_already_there_is_never_replaced()
    {
        var settings = new CountrySettings();
        settings.PropertyCollection.Add(new CountrySettingsProperty
        {
            Name = "Beneficiary Voucher Message",
            Value = "Ours, not yours."
        });

        MessageTemplateDefaults.ApplyTo(settings);

        Assert.Equal(
            "Ours, not yours.",
            settings.PropertyCollection.Single(p => p.Name == "Beneficiary Voucher Message").Value);
    }

    [Fact]
    public void A_template_cleared_on_purpose_stays_cleared()
    {
        // Staff who do not want to pay for a message clear it. Re-applying the defaults — on an
        // upgrade, or on the next save of that country — must not start sending it again.
        var settings = new CountrySettings();
        settings.PropertyCollection.Add(new CountrySettingsProperty
        {
            Name = "Vendor Confirmed Message",
            Value = ""
        });

        MessageTemplateDefaults.ApplyTo(settings);

        Assert.Equal(
            "",
            settings.PropertyCollection.Single(p => p.Name == "Vendor Confirmed Message").Value);
    }

    [Fact]
    public async Task The_seeded_country_can_send_without_anyone_configuring_wording()
    {
        using var scope = _factory.Services.CreateScope();
        var admin = scope.ServiceProvider.GetRequiredService<AdminContext>();

        var settings = await admin.CountrySettings
            .Include(s => s.PropertyCollection)
            .FirstAsync();

        var body = settings.PropertyCollection
            .Single(p => p.Name == "Beneficiary Voucher Message").Value;

        Assert.False(string.IsNullOrWhiteSpace(body));
        Assert.Contains("482913", VoucherNotifications.Render(body, Model()));
    }

    [Fact]
    public async Task A_country_created_from_the_interface_gets_the_wording_too()
    {
        // The path that matters most: only the first country comes from the seeder, and every
        // country added afterwards used to start with eleven blank templates.
        const int settingsTempId = -101;

        var result = await _api.BreezeSaveAsync(
            new[]
            {
                TalonApiClient.NewEntity(
                    "CountrySettings", "CountrySettings", TalonApiClient.AdminNamespace,
                    settingsTempId, new { SmsBackendType = 2 })
            },
            admin: true);

        var newId = result.RealId(settingsTempId);

        using var scope = _factory.Services.CreateScope();
        var admin = scope.ServiceProvider.GetRequiredService<AdminContext>();

        var settings = await admin.CountrySettings
            .AsNoTracking()
            .Include(s => s.PropertyCollection)
            .FirstAsync(s => s.Id == newId);

        Assert.Equal(MessageTemplateDefaults.ByName.Count, settings.PropertyCollection.Count);
        Assert.Contains(
            "482913",
            VoucherNotifications.Render(
                settings.PropertyCollection.Single(p => p.Name == "Beneficiary Voucher Message").Value,
                Model()));
    }
}
