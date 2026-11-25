using System.Text.Json.Nodes;

namespace Dhis2UserProvisioning.Dhis2;

/// <summary>The DHIS2 Web API calls this tool needs. An interface so tests can use a fake.</summary>
public interface IDhis2Client
{
    Task<IReadOnlyDictionary<string, string>> GetUserRoleIdsAsync(IEnumerable<string> names, CancellationToken ct = default);

    Task<IReadOnlyDictionary<string, string>> GetUserGroupIdsAsync(IEnumerable<string> names, CancellationToken ct = default);

    Task<IReadOnlyDictionary<string, string>> GetOrgUnitIdsAsync(IEnumerable<string> codes, CancellationToken ct = default);

    Task<bool> UsernameExistsAsync(string username, CancellationToken ct = default);

    Task<CreateUserResponse> CreateUserAsync(JsonObject payload, CancellationToken ct = default);
}

public sealed record CreateUserResponse(bool Success, string? UserId, string Message);
