using Dhis2UserProvisioning.Dhis2;
using Xunit;

namespace Dhis2UserProvisioning.Tests;

public class UserPayloadBuilderTests
{
    [Fact]
    public void Builds_payload_with_ids_for_every_reference()
    {
        var request = ValidatorTests.ValidRequest();

        var payload = UserPayloadBuilder.Build(request, "Secret#123a",
            roleIds: new[] { "role1", "role2" },
            orgUnitIds: new[] { "ou1" },
            dataViewOrgUnitIds: new[] { "ou2" },
            groupIds: Array.Empty<string>());

        Assert.Equal("akhan", payload["username"]!.GetValue<string>());
        Assert.Equal("Secret#123a", payload["password"]!.GetValue<string>());
        Assert.Equal("+92 300 1234567", payload["phoneNumber"]!.GetValue<string>());
        Assert.Equal(2, payload["userRoles"]!.AsArray().Count);
        Assert.Equal("role2", payload["userRoles"]![1]!["id"]!.GetValue<string>());
        Assert.Equal("ou1", payload["organisationUnits"]![0]!["id"]!.GetValue<string>());
        Assert.Equal("ou2", payload["dataViewOrganisationUnits"]![0]!["id"]!.GetValue<string>());
        Assert.Empty(payload["userGroups"]!.AsArray());
    }

    [Fact]
    public void Leaves_out_phone_number_when_empty()
    {
        var request = ValidatorTests.ValidRequest() with { PhoneNumber = "" };

        var payload = UserPayloadBuilder.Build(request, "x", new[] { "r" }, new[] { "o" },
            Array.Empty<string>(), Array.Empty<string>());

        Assert.False(payload.ContainsKey("phoneNumber"));
    }

    [Fact]
    public void Temporary_passwords_meet_the_default_dhis2_policy()
    {
        for (var i = 0; i < 200; i++)
        {
            var password = UserPayloadBuilder.GenerateTemporaryPassword();

            Assert.Equal(14, password.Length);
            Assert.Contains(password, char.IsUpper);
            Assert.Contains(password, char.IsLower);
            Assert.Contains(password, char.IsDigit);
            Assert.Contains(password, c => "!@#$%&*?".Contains(c));
        }
    }

    [Fact]
    public void Temporary_passwords_are_not_repeated()
    {
        var passwords = Enumerable.Range(0, 100).Select(_ => UserPayloadBuilder.GenerateTemporaryPassword()).ToList();

        Assert.Equal(passwords.Count, passwords.Distinct().Count());
    }

    [Fact]
    public void Rejects_password_length_below_eight()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => UserPayloadBuilder.GenerateTemporaryPassword(6));
    }
}
