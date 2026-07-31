using System.Net.Http.Headers;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace TalonAdmin.Tests.Infrastructure;

/// <summary>
/// Talks to the running application exactly as the AngularJS client does: an OAuth password grant
/// against /Token, the "Token" authorization scheme, tenant headers on every call, and Breeze's own
/// JSON query and save-bundle formats.
/// </summary>
public class TalonApiClient
{
    public const string VoucherNamespace = "TalonAdmin.Models.Vouchers";
    public const string AdminNamespace = "TalonAdmin.Models.Admin";

    private readonly HttpClient _http;
    private string? _token;
    private int _countryId;
    private int _organizationId;

    public TalonApiClient(HttpClient http)
    {
        _http = http;
    }

    public int CountryId => _countryId;
    public int OrganizationId => _organizationId;

    public void UseTenant(int countryId, int organizationId)
    {
        _countryId = countryId;
        _organizationId = organizationId;
    }

    public async Task AuthenticateAsync(string userName, string password)
    {
        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "password",
            ["username"] = userName,
            ["password"] = password
        });

        using var response = await _http.PostAsync("/Token", content);
        var body = await response.Content.ReadAsStringAsync();

        Assert.True(response.IsSuccessStatusCode, $"/Token failed ({(int)response.StatusCode}): {body}");

        _token = JObject.Parse(body).Value<string>("access_token");
        Assert.False(string.IsNullOrEmpty(_token), "/Token returned no access_token.");
    }

    public void UseVendorToken(string token) => _token = token;

    private HttpRequestMessage Request(HttpMethod method, string url)
    {
        var request = new HttpRequestMessage(method, url);

        if (!string.IsNullOrEmpty(_token))
        {
            // The client uses "Token", not "Bearer"; the server accepts both.
            request.Headers.TryAddWithoutValidation("Authorization", "Token " + _token);
        }

        request.Headers.TryAddWithoutValidation("X-Tenant-Country", _countryId.ToString());
        request.Headers.TryAddWithoutValidation("X-Tenant-Organization", _organizationId.ToString());

        return request;
    }

    public async Task<HttpResponseMessage> SendAsync(HttpMethod method, string url, object? jsonBody = null)
    {
        var request = Request(method, url);

        if (jsonBody != null)
        {
            request.Content = new StringContent(
                jsonBody as string ?? JsonConvert.SerializeObject(jsonBody),
                Encoding.UTF8,
                "application/json");
        }

        return await _http.SendAsync(request);
    }

    /// <summary>A raw GET, for tests that assert on a status rather than a body.</summary>
    public Task<HttpResponseMessage> GetAsync(string url) => SendAsync(HttpMethod.Get, url);

    public async Task<JToken> GetJsonAsync(string url)
    {
        using var response = await SendAsync(HttpMethod.Get, url);
        var body = await response.Content.ReadAsStringAsync();

        Assert.True(response.IsSuccessStatusCode, $"GET {url} failed ({(int)response.StatusCode}): {Truncate(body)}");

        return string.IsNullOrWhiteSpace(body) ? JValue.CreateNull() : JToken.Parse(body);
    }

    public async Task<JToken> PostJsonAsync(string url, object body)
    {
        using var response = await SendAsync(HttpMethod.Post, url, body);
        var payload = await response.Content.ReadAsStringAsync();

        Assert.True(response.IsSuccessStatusCode, $"POST {url} failed ({(int)response.StatusCode}): {Truncate(payload)}");

        return string.IsNullOrWhiteSpace(payload) ? JValue.CreateNull() : JToken.Parse(payload);
    }

    public async Task<byte[]> PostForBytesAsync(string url, object body)
    {
        using var response = await SendAsync(HttpMethod.Post, url, body);

        Assert.True(response.IsSuccessStatusCode,
            $"POST {url} failed ({(int)response.StatusCode}): {Truncate(await response.Content.ReadAsStringAsync())}");

        return await response.Content.ReadAsByteArrayAsync();
    }

    // ---- Breeze -----------------------------------------------------------------

    /// <summary>
    /// Runs a Breeze query. The query object uses Breeze's JSON format ({ where, orderBy, take,
    /// expand, inlineCount }) — not OData parameters, which this server ignores.
    /// </summary>
    public async Task<JArray> BreezeQueryAsync(string resource, object? query = null, bool admin = false)
    {
        var url = $"/breeze/{(admin ? "Admin" : "EVM")}/{resource}";
        if (query != null)
        {
            url += "?" + Uri.EscapeDataString(JsonConvert.SerializeObject(query));
        }

        var result = await GetJsonAsync(url);

        // inlineCount queries come back wrapped as { Results, InlineCount }.
        return result as JArray ?? (JArray)result["Results"]!;
    }

    public async Task<(JArray Results, int InlineCount)> BreezeQueryWithCountAsync(
        string resource, object query, bool admin = false)
    {
        var url = $"/breeze/{(admin ? "Admin" : "EVM")}/{resource}?" +
                  Uri.EscapeDataString(JsonConvert.SerializeObject(query));

        var result = await GetJsonAsync(url);

        return ((JArray)result["Results"]!, result.Value<int>("InlineCount"));
    }

    public async Task<BreezeSaveResult> BreezeSaveAsync(IEnumerable<JObject> entities, bool admin = false)
    {
        var bundle = new JObject
        {
            ["entities"] = new JArray(entities.Cast<object>().ToArray()),
            ["saveOptions"] = new JObject()
        };

        var response = await PostJsonAsync($"/breeze/{(admin ? "Admin" : "EVM")}/SaveChanges", bundle.ToString());

        return new BreezeSaveResult((JObject)response);
    }

    /// <summary>Builds the "Added" entity shape a Breeze save bundle expects.</summary>
    public static JObject NewEntity(string typeName, string resourceName, string entityNamespace, int tempId, object values)
    {
        var entity = JObject.FromObject(values);
        entity["Id"] = tempId;
        entity["entityAspect"] = new JObject
        {
            ["entityTypeName"] = $"{typeName}:#{entityNamespace}",
            ["defaultResourceName"] = resourceName,
            ["entityState"] = "Added",
            ["originalValuesMap"] = new JObject(),
            ["autoGeneratedKey"] = new JObject
            {
                ["propertyName"] = "Id",
                ["autoGeneratedKeyType"] = "Identity"
            }
        };

        return entity;
    }

    private static string Truncate(string value) =>
        value.Length <= 600 ? value : value[..600] + "…";
}

/// <summary>Wraps a Breeze SaveResult so tests can resolve the real keys assigned to new rows.</summary>
public class BreezeSaveResult
{
    private readonly Dictionary<int, int> _keyMappings = new();

    public BreezeSaveResult(JObject payload)
    {
        Payload = payload;

        var mappings = payload["KeyMappings"] ?? payload["keyMappings"];
        foreach (var mapping in mappings ?? Enumerable.Empty<JToken>())
        {
            var temp = mapping.Value<int?>("TempValue") ?? mapping.Value<int>("tempValue");
            var real = mapping.Value<int?>("RealValue") ?? mapping.Value<int>("realValue");
            _keyMappings[temp] = real;
        }
    }

    public JObject Payload { get; }

    public int RealId(int tempId)
    {
        Assert.True(_keyMappings.ContainsKey(tempId),
            $"No key mapping for temp id {tempId}. Save result: {Payload}");

        return _keyMappings[tempId];
    }

    public int SavedCount => (Payload["Entities"] ?? Payload["entities"])?.Count() ?? 0;
}
