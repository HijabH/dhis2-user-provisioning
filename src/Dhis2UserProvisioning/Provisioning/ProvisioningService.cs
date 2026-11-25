using Dhis2UserProvisioning.Dhis2;
using Dhis2UserProvisioning.Models;
using Dhis2UserProvisioning.Validation;

namespace Dhis2UserProvisioning.Provisioning;

/// <summary>
/// Runs the whole process for a batch of requests:
/// validate, resolve names and codes to DHIS2 ids, skip existing users, then create the rest.
/// </summary>
public sealed class ProvisioningService
{
    private readonly IDhis2Client _client;
    private readonly Func<string> _passwordGenerator;

    public ProvisioningService(IDhis2Client client, Func<string>? passwordGenerator = null)
    {
        _client = client;
        _passwordGenerator = passwordGenerator ?? (() => UserPayloadBuilder.GenerateTemporaryPassword());
    }

    public async Task<IReadOnlyList<ProvisioningResult>> ProvisionAsync(
        IReadOnlyList<UserRequest> requests, bool dryRun, CancellationToken ct = default)
    {
        var results = new List<ProvisioningResult>();
        var duplicates = UserRequestValidator.FindDuplicateUsernames(requests);

        var valid = new List<UserRequest>();
        foreach (var request in requests)
        {
            var errors = UserRequestValidator.Validate(request).ToList();
            if (duplicates.Contains(request.Username))
            {
                errors.Add($"username '{request.Username}' appears more than once in the file");
            }

            if (errors.Count > 0)
            {
                results.Add(Invalid(request, string.Join("; ", errors)));
            }
            else
            {
                valid.Add(request);
            }
        }

        if (valid.Count == 0)
        {
            return Ordered(results);
        }

        var roleIds = await _client.GetUserRoleIdsAsync(valid.SelectMany(r => r.UserRoles), ct);
        var groupIds = await _client.GetUserGroupIdsAsync(valid.SelectMany(r => r.UserGroups), ct);
        var orgUnitIds = await _client.GetOrgUnitIdsAsync(
            valid.SelectMany(r => r.OrgUnits.Concat(r.DataViewOrgUnits)), ct);

        foreach (var request in valid)
        {
            var unknown = Unknown("user role", request.UserRoles, roleIds)
                .Concat(Unknown("user group", request.UserGroups, groupIds))
                .Concat(Unknown("organisation unit", request.OrgUnits.Concat(request.DataViewOrgUnits), orgUnitIds))
                .ToList();

            if (unknown.Count > 0)
            {
                results.Add(Invalid(request, string.Join("; ", unknown)));
                continue;
            }

            if (await _client.UsernameExistsAsync(request.Username, ct))
            {
                results.Add(new ProvisioningResult(request.RowNumber, request.Username,
                    ProvisioningStatus.Skipped, "a user with this username already exists"));
                continue;
            }

            if (dryRun)
            {
                results.Add(new ProvisioningResult(request.RowNumber, request.Username,
                    ProvisioningStatus.WouldCreate, "valid; would be created"));
                continue;
            }

            var password = _passwordGenerator();
            var payload = UserPayloadBuilder.Build(
                request,
                password,
                request.UserRoles.Select(n => roleIds[n]),
                request.OrgUnits.Select(c => orgUnitIds[c]),
                request.DataViewOrgUnits.Select(c => orgUnitIds[c]),
                request.UserGroups.Select(n => groupIds[n]));

            var response = await _client.CreateUserAsync(payload, ct);
            results.Add(response.Success
                ? new ProvisioningResult(request.RowNumber, request.Username, ProvisioningStatus.Created,
                    "created", response.UserId, password)
                : new ProvisioningResult(request.RowNumber, request.Username, ProvisioningStatus.Failed,
                    response.Message));
        }

        return Ordered(results);
    }

    private static IEnumerable<string> Unknown(
        string kind, IEnumerable<string> keys, IReadOnlyDictionary<string, string> found) =>
        keys.Where(k => !found.ContainsKey(k)).Select(k => $"unknown {kind} '{k}'");

    private static ProvisioningResult Invalid(UserRequest r, string message) =>
        new(r.RowNumber, r.Username, ProvisioningStatus.Invalid, message);

    private static IReadOnlyList<ProvisioningResult> Ordered(List<ProvisioningResult> results) =>
        results.OrderBy(r => r.RowNumber).ToList();
}
