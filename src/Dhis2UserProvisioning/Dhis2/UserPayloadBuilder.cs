using System.Security.Cryptography;
using System.Text.Json.Nodes;
using Dhis2UserProvisioning.Models;

namespace Dhis2UserProvisioning.Dhis2;

/// <summary>Builds the JSON body for POST /api/users (DHIS2 2.36 and later).</summary>
public static class UserPayloadBuilder
{
    public static JsonObject Build(
        UserRequest request,
        string password,
        IEnumerable<string> roleIds,
        IEnumerable<string> orgUnitIds,
        IEnumerable<string> dataViewOrgUnitIds,
        IEnumerable<string> groupIds)
    {
        var payload = new JsonObject
        {
            ["firstName"] = request.FirstName,
            ["surname"] = request.Surname,
            ["email"] = request.Email,
            ["username"] = request.Username,
            ["password"] = password,
            ["userRoles"] = IdArray(roleIds),
            ["organisationUnits"] = IdArray(orgUnitIds),
            ["dataViewOrganisationUnits"] = IdArray(dataViewOrgUnitIds),
            ["userGroups"] = IdArray(groupIds),
        };

        if (!string.IsNullOrWhiteSpace(request.PhoneNumber))
        {
            payload["phoneNumber"] = request.PhoneNumber;
        }

        return payload;
    }

    private static JsonArray IdArray(IEnumerable<string> ids) =>
        new(ids.Select(id => (JsonNode)new JsonObject { ["id"] = id }).ToArray());

    /// <summary>
    /// A random temporary password that meets the default DHIS2 policy:
    /// at least 8 characters with upper case, lower case, a digit and a special character.
    /// </summary>
    public static string GenerateTemporaryPassword(int length = 14)
    {
        const string upper = "ABCDEFGHJKLMNPQRSTUVWXYZ";
        const string lower = "abcdefghijkmnopqrstuvwxyz";
        const string digits = "23456789";
        const string special = "!@#$%&*?";
        const string all = upper + lower + digits + special;

        if (length < 8) throw new ArgumentOutOfRangeException(nameof(length), "Minimum length is 8.");

        var chars = new List<char>
        {
            Pick(upper), Pick(lower), Pick(digits), Pick(special),
        };
        while (chars.Count < length) chars.Add(Pick(all));

        // shuffle so the required characters are not always at the start
        for (var i = chars.Count - 1; i > 0; i--)
        {
            var j = RandomNumberGenerator.GetInt32(i + 1);
            (chars[i], chars[j]) = (chars[j], chars[i]);
        }

        return new string(chars.ToArray());

        static char Pick(string set) => set[RandomNumberGenerator.GetInt32(set.Length)];
    }
}
