using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Dhis2UserProvisioning.Dhis2;

/// <summary>
/// Talks to the DHIS2 Web API with basic authentication.
/// Lookups are sent in batches so a file with hundreds of rows needs only a few requests.
/// </summary>
public sealed class Dhis2Client : IDhis2Client
{
    private const int BatchSize = 50;
    private readonly HttpClient _http;

    public Dhis2Client(HttpClient http, string baseUrl, string username, string password)
    {
        _http = http;
        _http.BaseAddress = new Uri(NormaliseApiUrl(baseUrl));
        var token = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{username}:{password}"));
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", token);
        _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    /// <summary>Accepts "https://host/dhis" or "https://host/dhis/api" and returns ".../api/".</summary>
    public static string NormaliseApiUrl(string baseUrl)
    {
        var url = baseUrl.Trim().TrimEnd('/');
        if (!url.EndsWith("/api", StringComparison.OrdinalIgnoreCase))
        {
            url += "/api";
        }
        return url + "/";
    }

    public Task<IReadOnlyDictionary<string, string>> GetUserRoleIdsAsync(IEnumerable<string> names, CancellationToken ct = default) =>
        LookupAsync("userRoles", "name", names, ct);

    public Task<IReadOnlyDictionary<string, string>> GetUserGroupIdsAsync(IEnumerable<string> names, CancellationToken ct = default) =>
        LookupAsync("userGroups", "name", names, ct);

    public Task<IReadOnlyDictionary<string, string>> GetOrgUnitIdsAsync(IEnumerable<string> codes, CancellationToken ct = default) =>
        LookupAsync("organisationUnits", "code", codes, ct);

    public async Task<bool> UsernameExistsAsync(string username, CancellationToken ct = default)
    {
        var url = $"users?fields=id&paging=false&filter=username:eq:{Uri.EscapeDataString(username)}";
        using var doc = await GetJsonAsync(url, ct);
        return doc.RootElement.TryGetProperty("users", out var users) && users.GetArrayLength() > 0;
    }

    public async Task<CreateUserResponse> CreateUserAsync(JsonObject payload, CancellationToken ct = default)
    {
        using var content = new StringContent(payload.ToJsonString(), Encoding.UTF8, "application/json");
        using var response = await _http.PostAsync("users", content, ct);
        var body = await response.Content.ReadAsStringAsync(ct);

        if (response.IsSuccessStatusCode)
        {
            return new CreateUserResponse(true, ReadCreatedUid(body), "Created");
        }

        return new CreateUserResponse(false, null, $"HTTP {(int)response.StatusCode}: {ReadErrorMessage(body)}");
    }

    private async Task<IReadOnlyDictionary<string, string>> LookupAsync(
        string resource, string keyField, IEnumerable<string> keys, CancellationToken ct)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var distinct = keys.Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        foreach (var batch in distinct.Chunk(BatchSize))
        {
            var filter = $"{keyField}:in:[{string.Join(",", batch)}]";
            var url = $"{resource}?fields=id,{keyField}&paging=false&filter={Uri.EscapeDataString(filter)}";
            using var doc = await GetJsonAsync(url, ct);

            if (!doc.RootElement.TryGetProperty(resource, out var items)) continue;
            foreach (var item in items.EnumerateArray())
            {
                if (item.TryGetProperty(keyField, out var key) && item.TryGetProperty("id", out var id)
                    && key.GetString() is { } k && id.GetString() is { } i)
                {
                    result[k] = i;
                }
            }
        }

        return result;
    }

    private async Task<JsonDocument> GetJsonAsync(string relativeUrl, CancellationToken ct)
    {
        using var response = await _http.GetAsync(relativeUrl, ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"DHIS2 request '{Uri.UnescapeDataString(relativeUrl)}' failed with HTTP {(int)response.StatusCode}: {ReadErrorMessage(body)}");
        }
        return JsonDocument.Parse(body);
    }

    /// <summary>DHIS2 2.36+ returns {"response":{"uid":"..."}}; older versions may differ.</summary>
    internal static string? ReadCreatedUid(string body)
    {
        try
        {
            var node = JsonNode.Parse(body);
            return node?["response"]?["uid"]?.GetValue<string>()
                ?? node?["uid"]?.GetValue<string>();
        }
        catch (JsonException)
        {
            return null;
        }
    }

    internal static string ReadErrorMessage(string body)
    {
        try
        {
            var node = JsonNode.Parse(body);
            var errorReports = node?["response"]?["errorReports"]?.AsArray();
            if (errorReports is { Count: > 0 })
            {
                return string.Join("; ", errorReports
                    .Select(e => e?["message"]?.GetValue<string>())
                    .Where(m => !string.IsNullOrWhiteSpace(m)));
            }
            return node?["message"]?.GetValue<string>() ?? Truncate(body);
        }
        catch (Exception e) when (e is JsonException or InvalidOperationException)
        {
            return Truncate(body);
        }
    }

    private static string Truncate(string s) => s.Length <= 200 ? s : s[..200] + "...";
}
