using System.Text.Json.Nodes;
using Dhis2UserProvisioning.Dhis2;

namespace Dhis2UserProvisioning.Tests;

/// <summary>In-memory stand-in for DHIS2 that records what the service asked for.</summary>
internal sealed class FakeDhis2Client : IDhis2Client
{
    public Dictionary<string, string> Roles { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, string> Groups { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, string> OrgUnits { get; } = new(StringComparer.OrdinalIgnoreCase);
    public HashSet<string> ExistingUsernames { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Func<JsonObject, CreateUserResponse> OnCreate { get; set; } =
        p => new CreateUserResponse(true, "uid_" + p["username"]!.GetValue<string>(), "Created");

    public List<JsonObject> CreatedPayloads { get; } = new();
    public int LookupCalls { get; private set; }

    public Task<IReadOnlyDictionary<string, string>> GetUserRoleIdsAsync(IEnumerable<string> names, CancellationToken ct = default) =>
        Lookup(Roles, names);

    public Task<IReadOnlyDictionary<string, string>> GetUserGroupIdsAsync(IEnumerable<string> names, CancellationToken ct = default) =>
        Lookup(Groups, names);

    public Task<IReadOnlyDictionary<string, string>> GetOrgUnitIdsAsync(IEnumerable<string> codes, CancellationToken ct = default) =>
        Lookup(OrgUnits, codes);

    public Task<bool> UsernameExistsAsync(string username, CancellationToken ct = default) =>
        Task.FromResult(ExistingUsernames.Contains(username));

    public Task<CreateUserResponse> CreateUserAsync(JsonObject payload, CancellationToken ct = default)
    {
        CreatedPayloads.Add(payload);
        return Task.FromResult(OnCreate(payload));
    }

    private Task<IReadOnlyDictionary<string, string>> Lookup(Dictionary<string, string> source, IEnumerable<string> keys)
    {
        LookupCalls++;
        IReadOnlyDictionary<string, string> found = keys
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(source.ContainsKey)
            .ToDictionary(k => k, k => source[k], StringComparer.OrdinalIgnoreCase);
        return Task.FromResult(found);
    }
}
