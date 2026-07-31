using Newtonsoft.Json.Linq;
using TalonAdmin.Tests.Infrastructure;

namespace TalonAdmin.Tests;

/// <summary>
/// The sidebar and every create/edit button are data-driven. Both sets of rows were missing after
/// the migration, which left the application looking read-only with a one-item menu.
/// </summary>
public class NavigationAndPermissionTests : IClassFixture<SeededTalonAppFactory>
{
    private readonly TalonApiClient _api;

    public NavigationAndPermissionTests(SeededTalonAppFactory factory)
    {
        _api = new TalonApiClient(factory.CreateClient());
        _api.UseTenant(1, 1);
    }

    private async Task AuthenticateAsync() =>
        await _api.AuthenticateAsync(TalonAppFactory.AdminUserName, TalonAppFactory.AdminPassword);

    [Fact]
    public async Task The_sidebar_is_populated_with_categories_groups_and_links()
    {
        await AuthenticateAsync();

        var categories = (JArray)await _api.GetJsonAsync("/Home/NavigationItems");

        Assert.True(categories.Count >= 5, $"Expected the full menu, got {categories.Count} categories.");

        foreach (var category in categories)
        {
            Assert.False(string.IsNullOrWhiteSpace(category.Value<string>("Name")));

            var items = category["Items"];
            Assert.NotNull(items);
            Assert.NotEmpty(items!);

            foreach (var group in items!)
            {
                // Children carry the ui-router state the link navigates to. EF6 lazy-loaded these
                // during serialisation; without an explicit ThenInclude every group renders empty.
                var children = group["Children"];
                Assert.NotNull(children);
                Assert.NotEmpty(children!);

                Assert.All(children!, child =>
                    Assert.False(string.IsNullOrWhiteSpace(child.Value<string>("State"))));
                Assert.All(children!, child =>
                    Assert.False(string.IsNullOrWhiteSpace(child.Value<string>("Title"))));
            }
        }
    }

    [Fact]
    public async Task Every_menu_link_points_at_a_state_the_client_defines()
    {
        await AuthenticateAsync();

        var categories = (JArray)await _api.GetJsonAsync("/Home/NavigationItems");

        var states = categories
            .SelectMany(c => c["Items"]!)
            .SelectMany(i => i["Children"]!)
            .Select(child => child.Value<string>("State")!)
            .ToArray();

        Assert.NotEmpty(states);

        // ui-router state names, e.g. "beneficiaries.list" — a bare url or an "app."-prefixed name
        // silently renders a dead link.
        Assert.All(states, state =>
        {
            Assert.DoesNotContain("/", state);
            Assert.Contains(".", state);
            Assert.False(state.StartsWith("app."), $"'{state}' is not a state this client defines.");
        });

        Assert.Contains("beneficiaries.list", states);
        Assert.Contains("distributions.list", states);
        Assert.Contains("vendors.list", states);
        Assert.Contains("sys-admin.users.list", states);
    }

    /// <summary>
    /// Creating a country writes four rows, and the client has to send all four. The generic CRUD
    /// form sent only the Country and failed on the CountrySettings foreign key; sending three
    /// rows saved, but the new country never appeared in the country switcher because that reads
    /// ApplicationUserCountry. This pins the whole bundle down.
    /// </summary>
    [Fact]
    public async Task Creating_a_country_grants_the_organisation_and_the_creating_user_access_to_it()
    {
        await AuthenticateAsync();

        var me = (JObject)await _api.GetJsonAsync("/api/ApplicationUser/Me");
        var userId = me.Value<string>("Id")!;
        var organizationId = me.Value<int>("OrganizationId");
        var countriesBefore = ((JArray)me["Countries"]!).Count;

        const int settingsTempId = -1;
        const int countryTempId = -2;

        var save = await _api.BreezeSaveAsync(new[]
        {
            TalonApiClient.NewEntity("CountrySettings", "CountrySettings", TalonApiClient.AdminNamespace,
                settingsTempId, new { SmsBackendType = 1 }),
            TalonApiClient.NewEntity("Country", "Countries", TalonApiClient.AdminNamespace, countryTempId, new
            {
                Name = "Testland",
                IsoAlpha2 = "T9",
                IsoAlpha3 = "TS9",
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
            TalonApiClient.NewEntity("OrganizationCountry", "OrganizationCountries",
                TalonApiClient.AdminNamespace, -1,
                new { OrganizationId = organizationId, CountryId = countryId, SettingsId = settingsId }),
            TalonApiClient.NewEntity("ApplicationUserCountry", "ApplicationUserCountries",
                TalonApiClient.AdminNamespace, -2,
                new { ApplicationUserId = userId, CountryId = countryId })
        }, admin: true);

        // Without the ApplicationUserCountry row the country exists but is unreachable: the
        // switcher offers only the countries this endpoint returns.
        var after = (JObject)await _api.GetJsonAsync("/api/ApplicationUser/Me");
        var countries = (JArray)after["Countries"]!;

        Assert.Equal(countriesBefore + 1, countries.Count);
        Assert.Contains(countries, c => c["Country"]!.Value<string>("Name") == "Testland");
    }

    [Fact]
    public async Task Administration_categories_are_restricted_by_role()
    {
        await AuthenticateAsync();

        var categories = (JArray)await _api.GetJsonAsync("/Home/NavigationItems");

        var restricted = categories.Where(c => c["Roles"]!.Any()).ToArray();

        Assert.NotEmpty(restricted);
        Assert.Contains(restricted, c => c.Value<string>("Name") == "System Administration");
    }

    [Fact]
    public async Task The_signed_in_user_is_granted_the_actions_the_ui_gates_on()
    {
        await AuthenticateAsync();

        var me = await _api.GetJsonAsync("/api/ApplicationUser/Me");

        Assert.True(me.Value<bool>("IsSystemAdministrator"));

        var actions = me["AvailableActions"]!.Select(a => a.Value<string>("Name")).ToHashSet();

        // $rootScope.canI gates every create/edit/delete button on these rows; with none present
        // the create states bounce straight back to their list.
        Assert.Contains("Create Beneficiaries", actions);
        Assert.Contains("Edit Beneficiaries", actions);
        Assert.Contains("Delete Beneficiaries", actions);
        Assert.Contains("Distribute Vouchers", actions);
        Assert.Contains("Export Beneficiaries", actions);
        Assert.Contains("Generate Test Sheet", actions);
    }

    [Fact]
    public async Task The_signed_in_user_carries_its_organisation_and_countries()
    {
        await AuthenticateAsync();

        var me = await _api.GetJsonAsync("/api/ApplicationUser/Me");

        // The client reads these straight after login to pick the active tenant; they only appear
        // if the Include chain survived the move off lazy loading.
        Assert.NotNull(me["Organization"]);
        Assert.False(string.IsNullOrWhiteSpace(me["Organization"]!.Value<string>("Name")));

        var countries = me["Countries"]!;
        Assert.NotEmpty(countries);
        Assert.All(countries, c => Assert.NotNull(c["Country"]));
    }
}
