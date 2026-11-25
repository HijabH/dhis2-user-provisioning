using Dhis2UserProvisioning.Models;
using Dhis2UserProvisioning.Validation;
using Xunit;

namespace Dhis2UserProvisioning.Tests;

public class ValidatorTests
{
    internal static UserRequest ValidRequest(string username = "akhan", int row = 2) => new()
    {
        RowNumber = row,
        FirstName = "Amina",
        Surname = "Khan",
        Username = username,
        Email = "amina@example.org",
        PhoneNumber = "+92 300 1234567",
        UserRoles = new[] { "Data entry clerk" },
        OrgUnits = new[] { "OU_001" },
    };

    [Fact]
    public void Valid_request_has_no_errors()
    {
        Assert.Empty(UserRequestValidator.Validate(ValidRequest()));
    }

    [Fact]
    public void Reports_every_missing_required_field()
    {
        var errors = UserRequestValidator.Validate(new UserRequest { RowNumber = 2 });

        Assert.Contains("firstName is required", errors);
        Assert.Contains("surname is required", errors);
        Assert.Contains("username is required", errors);
        Assert.Contains("email is required", errors);
        Assert.Contains("at least one user role is required", errors);
        Assert.Contains("at least one organisation unit is required", errors);
    }

    [Theory]
    [InlineData("abc")]          // too short
    [InlineData("has space")]
    [InlineData("bad/char")]
    public void Rejects_invalid_usernames(string username)
    {
        var errors = UserRequestValidator.Validate(ValidRequest(username));

        Assert.Contains(errors, e => e.StartsWith("username must be"));
    }

    [Theory]
    [InlineData("amina.khan")]
    [InlineData("a_khan-01")]
    [InlineData("amina@cmu")]
    public void Accepts_valid_usernames(string username)
    {
        Assert.Empty(UserRequestValidator.Validate(ValidRequest(username)));
    }

    [Theory]
    [InlineData("not-an-email")]
    [InlineData("a@b")]
    [InlineData("two@@example.org")]
    public void Rejects_invalid_emails(string email)
    {
        var errors = UserRequestValidator.Validate(ValidRequest() with { Email = email });

        Assert.Contains(errors, e => e.StartsWith("email"));
    }

    [Fact]
    public void Phone_number_is_optional_but_checked_when_given()
    {
        Assert.Empty(UserRequestValidator.Validate(ValidRequest() with { PhoneNumber = "" }));

        var errors = UserRequestValidator.Validate(ValidRequest() with { PhoneNumber = "call me" });
        Assert.Contains(errors, e => e.StartsWith("phoneNumber"));
    }

    [Fact]
    public void Finds_duplicate_usernames_ignoring_case()
    {
        var requests = new[] { ValidRequest("akhan", 2), ValidRequest("AKhan", 3), ValidRequest("other", 4) };

        var duplicates = UserRequestValidator.FindDuplicateUsernames(requests);

        Assert.Single(duplicates);
        Assert.Contains("akhan", duplicates);
    }
}
