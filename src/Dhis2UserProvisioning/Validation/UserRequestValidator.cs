using System.Text.RegularExpressions;
using Dhis2UserProvisioning.Models;

namespace Dhis2UserProvisioning.Validation;

/// <summary>
/// Checks a request before anything is sent to DHIS2, so bad rows are reported
/// clearly instead of failing halfway through a bulk upload.
/// </summary>
public static partial class UserRequestValidator
{
    // DHIS2 usernames: 4 to 255 characters; letters, digits and . _ - @ allowed
    [GeneratedRegex(@"^[A-Za-z0-9._@-]{4,255}$")]
    private static partial Regex UsernamePattern();

    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$")]
    private static partial Regex EmailPattern();

    [GeneratedRegex(@"^\+?[0-9 ()-]{6,20}$")]
    private static partial Regex PhonePattern();

    public static IReadOnlyList<string> Validate(UserRequest request)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(request.FirstName)) errors.Add("firstName is required");
        if (string.IsNullOrWhiteSpace(request.Surname)) errors.Add("surname is required");

        if (string.IsNullOrWhiteSpace(request.Username))
        {
            errors.Add("username is required");
        }
        else if (!UsernamePattern().IsMatch(request.Username))
        {
            errors.Add("username must be 4 to 255 characters: letters, digits, '.', '_', '-' or '@'");
        }

        if (string.IsNullOrWhiteSpace(request.Email))
        {
            errors.Add("email is required");
        }
        else if (!EmailPattern().IsMatch(request.Email))
        {
            errors.Add($"email '{request.Email}' is not a valid address");
        }

        if (!string.IsNullOrWhiteSpace(request.PhoneNumber) && !PhonePattern().IsMatch(request.PhoneNumber))
        {
            errors.Add($"phoneNumber '{request.PhoneNumber}' is not valid");
        }

        if (request.UserRoles.Count == 0) errors.Add("at least one user role is required");
        if (request.OrgUnits.Count == 0) errors.Add("at least one organisation unit is required");

        return errors;
    }

    /// <summary>Finds usernames that appear more than once in the same file (case-insensitive).</summary>
    public static IReadOnlySet<string> FindDuplicateUsernames(IEnumerable<UserRequest> requests) =>
        requests
            .Where(r => !string.IsNullOrWhiteSpace(r.Username))
            .GroupBy(r => r.Username, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
}
