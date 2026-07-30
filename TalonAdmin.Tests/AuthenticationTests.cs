using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Newtonsoft.Json.Linq;
using TalonAdmin.Tests.Infrastructure;

namespace TalonAdmin.Tests;

/// <summary>
/// The migration replaced OWIN cookies and OAuth with ASP.NET Core Identity and JWT while keeping
/// the contract the existing clients rely on. These tests pin that contract down.
/// </summary>
public class AuthenticationTests : IClassFixture<SeededTalonAppFactory>
{
    private readonly SeededTalonAppFactory _factory;

    public AuthenticationTests(SeededTalonAppFactory factory) => _factory = factory;

    private async Task<string> GetTokenAsync()
    {
        using var response = await _factory.CreateClient().PostAsync("/Token", new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["grant_type"] = "password",
                ["username"] = TalonAppFactory.AdminUserName,
                ["password"] = TalonAppFactory.AdminPassword
            }));

        return JObject.Parse(await response.Content.ReadAsStringAsync()).Value<string>("access_token")!;
    }

    [Fact]
    public async Task Token_endpoint_keeps_the_oauth_password_grant_shape()
    {
        using var response = await _factory.CreateClient().PostAsync("/Token", new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["grant_type"] = "password",
                ["username"] = TalonAppFactory.AdminUserName,
                ["password"] = TalonAppFactory.AdminPassword
            }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = JObject.Parse(await response.Content.ReadAsStringAsync());

        Assert.False(string.IsNullOrEmpty(payload.Value<string>("access_token")));
        Assert.Equal("bearer", payload.Value<string>("token_type"));
        Assert.True(payload.Value<int>("expires_in") > 0);
        Assert.Equal(TalonAppFactory.AdminUserName, payload.Value<string>("userName"));
    }

    [Fact]
    public async Task Bad_credentials_are_rejected_with_the_oauth_error_shape()
    {
        using var response = await _factory.CreateClient().PostAsync("/Token", new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["grant_type"] = "password",
                ["username"] = TalonAppFactory.AdminUserName,
                ["password"] = "not-the-password"
            }));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("invalid_grant", JObject.Parse(await response.Content.ReadAsStringAsync()).Value<string>("error"));
    }

    [Fact]
    public async Task Protected_endpoints_answer_401_rather_than_redirecting_to_a_login_page()
    {
        using var response = await _factory.CreateClient().GetAsync("/breeze/EVM/Metadata");

        // AddIdentity points the default challenge at its cookie handler, which answers 302 to a
        // login page this application does not have. The schemes are reset in Program.cs.
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("Token")]
    [InlineData("Bearer")]
    public async Task Both_the_token_and_bearer_schemes_are_accepted(string scheme)
    {
        var token = await GetTokenAsync();

        // The AngularJS client sends "Token"; under OWIN that worked only because /Token also set
        // a cookie. Both are supported now so the client did not have to change.
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/ApplicationUser/Me");
        request.Headers.TryAddWithoutValidation("Authorization", $"{scheme} {token}");

        using var response = await _factory.CreateClient().SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(TalonAppFactory.AdminUserName,
            JObject.Parse(await response.Content.ReadAsStringAsync()).Value<string>("UserName"));
    }

    [Fact]
    public async Task Basic_authentication_still_works_for_staff_tooling()
    {
        var credentials = Convert.ToBase64String(
            Encoding.UTF8.GetBytes($"{TalonAppFactory.AdminUserName}:{TalonAppFactory.AdminPassword}"));

        var request = new HttpRequestMessage(HttpMethod.Get, "/api/ApplicationUser/Me");
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);

        using var response = await _factory.CreateClient().SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task A_vendor_signs_in_and_uses_its_own_token_scheme()
    {
        var client = _factory.CreateClient();

        using var login = await client.PostAsync("/api/App/VendorProfile/Login", new StringContent(
            new JObject
            {
                ["UserName"] = "vendor",
                ["Password"] = TalonAppFactory.AdminPassword,
                ["Device"] = new JObject
                {
                    ["platform"] = "iOS",
                    ["model"] = "iPhone",
                    ["version"] = "17",
                    ["UUID"] = Guid.NewGuid().ToString()
                }
            }.ToString(), Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.OK, login.StatusCode);

        var vendorToken = JObject.Parse(await login.Content.ReadAsStringAsync()).Value<string>("token");
        Assert.False(string.IsNullOrEmpty(vendorToken));

        var request = new HttpRequestMessage(HttpMethod.Get, "/api/App/VendorProfile/LoadProfile");
        request.Headers.TryAddWithoutValidation("Authorization", $"Token {vendorToken}");

        using var profile = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, profile.StatusCode);
        Assert.Equal("Demo Vendor", JObject.Parse(await profile.Content.ReadAsStringAsync()).Value<string>("Name"));
    }

    [Fact]
    public async Task An_unknown_vendor_token_is_refused_rather_than_throwing()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/App/VendorProfile/LoadProfile");
        request.Headers.TryAddWithoutValidation("Authorization", "Token not-a-real-token");

        using var response = await _factory.CreateClient().SendAsync(request);

        // The EF6 version dereferenced a null vendor and returned a 500 with a stack trace on this
        // unauthenticated endpoint.
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task A_foreign_type_discriminator_is_refused_on_deserialisation()
    {
        var token = await GetTokenAsync();

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/ApplicationRole");
        request.Headers.TryAddWithoutValidation("Authorization", $"Token {token}");
        request.Content = new StringContent(
            """{"$type":"System.Diagnostics.Process, System.Diagnostics.Process","Name":"pwn"}""",
            Encoding.UTF8, "application/json");

        using var response = await _factory.CreateClient().SendAsync(request);

        // TypeNameHandling is on for Breeze's sake, so the serialization binder has to keep a
        // request from naming an arbitrary type. See TalonSerializationBinder.
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
