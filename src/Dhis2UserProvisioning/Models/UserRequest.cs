namespace Dhis2UserProvisioning.Models;

/// <summary>
/// One row from the user request CSV: a person who needs a DHIS2 account.
/// Roles and groups are given by name, organisation units by code, because that is
/// what programme staff fill in on a request form. They are resolved to DHIS2 ids later.
/// </summary>
public sealed record UserRequest
{
    public int RowNumber { get; init; }
    public string FirstName { get; init; } = "";
    public string Surname { get; init; } = "";
    public string Username { get; init; } = "";
    public string Email { get; init; } = "";
    public string PhoneNumber { get; init; } = "";
    public IReadOnlyList<string> UserRoles { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> OrgUnits { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> DataViewOrgUnits { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> UserGroups { get; init; } = Array.Empty<string>();
}
