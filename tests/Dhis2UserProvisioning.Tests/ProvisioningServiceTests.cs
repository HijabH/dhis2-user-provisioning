using Dhis2UserProvisioning.Dhis2;
using Dhis2UserProvisioning.Models;
using Dhis2UserProvisioning.Provisioning;
using Xunit;

namespace Dhis2UserProvisioning.Tests;

public class ProvisioningServiceTests
{
    private readonly FakeDhis2Client _client = new();
    private readonly ProvisioningService _service;

    public ProvisioningServiceTests()
    {
        _client.Roles["Data entry clerk"] = "roleClerk";
        _client.Groups["TB team"] = "groupTb";
        _client.OrgUnits["OU_001"] = "ouIslamabad";
        _client.OrgUnits["OU_002"] = "ouLahore";
        _service = new ProvisioningService(_client, () => "Fixed#Pass1");
    }

    [Fact]
    public async Task Creates_valid_users_and_returns_their_ids_and_passwords()
    {
        var request = ValidatorTests.ValidRequest() with
        {
            UserGroups = new[] { "TB team" },
            DataViewOrgUnits = new[] { "OU_002" },
        };

        var results = await _service.ProvisionAsync(new[] { request }, dryRun: false);

        var r = Assert.Single(results);
        Assert.Equal(ProvisioningStatus.Created, r.Status);
        Assert.Equal("uid_akhan", r.UserId);
        Assert.Equal("Fixed#Pass1", r.TemporaryPassword);

        var payload = Assert.Single(_client.CreatedPayloads);
        Assert.Equal("roleClerk", payload["userRoles"]![0]!["id"]!.GetValue<string>());
        Assert.Equal("ouLahore", payload["dataViewOrganisationUnits"]![0]!["id"]!.GetValue<string>());
        Assert.Equal("groupTb", payload["userGroups"]![0]!["id"]!.GetValue<string>());
    }

    [Fact]
    public async Task Dry_run_checks_everything_but_creates_nothing()
    {
        var results = await _service.ProvisionAsync(new[] { ValidatorTests.ValidRequest() }, dryRun: true);

        Assert.Equal(ProvisioningStatus.WouldCreate, Assert.Single(results).Status);
        Assert.Empty(_client.CreatedPayloads);
    }

    [Fact]
    public async Task Invalid_rows_are_reported_and_never_sent()
    {
        var bad = ValidatorTests.ValidRequest() with { Email = "nope" };

        var results = await _service.ProvisionAsync(new[] { bad }, dryRun: false);

        var r = Assert.Single(results);
        Assert.Equal(ProvisioningStatus.Invalid, r.Status);
        Assert.Contains("email", r.Message);
        Assert.Empty(_client.CreatedPayloads);
        Assert.Equal(0, _client.LookupCalls); // nothing valid, so no network calls at all
    }

    [Fact]
    public async Task Unknown_roles_and_org_units_make_the_row_invalid()
    {
        var request = ValidatorTests.ValidRequest() with
        {
            UserRoles = new[] { "Data entry clerk", "Superhero" },
            OrgUnits = new[] { "OU_999" },
        };

        var results = await _service.ProvisionAsync(new[] { request }, dryRun: false);

        var r = Assert.Single(results);
        Assert.Equal(ProvisioningStatus.Invalid, r.Status);
        Assert.Contains("unknown user role 'Superhero'", r.Message);
        Assert.Contains("unknown organisation unit 'OU_999'", r.Message);
        Assert.Empty(_client.CreatedPayloads);
    }

    [Fact]
    public async Task Existing_usernames_are_skipped()
    {
        _client.ExistingUsernames.Add("akhan");

        var results = await _service.ProvisionAsync(new[] { ValidatorTests.ValidRequest() }, dryRun: false);

        Assert.Equal(ProvisioningStatus.Skipped, Assert.Single(results).Status);
        Assert.Empty(_client.CreatedPayloads);
    }

    [Fact]
    public async Task Duplicate_usernames_in_the_file_are_all_rejected()
    {
        var requests = new[] { ValidatorTests.ValidRequest("akhan", 2), ValidatorTests.ValidRequest("AKHAN", 3) };

        var results = await _service.ProvisionAsync(requests, dryRun: false);

        Assert.All(results, r => Assert.Equal(ProvisioningStatus.Invalid, r.Status));
        Assert.Empty(_client.CreatedPayloads);
    }

    [Fact]
    public async Task Failed_creation_is_reported_with_the_dhis2_message()
    {
        _client.OnCreate = _ => new CreateUserResponse(false, null, "HTTP 409: Email already taken");

        var results = await _service.ProvisionAsync(new[] { ValidatorTests.ValidRequest() }, dryRun: false);

        var r = Assert.Single(results);
        Assert.Equal(ProvisioningStatus.Failed, r.Status);
        Assert.Equal("HTTP 409: Email already taken", r.Message);
        Assert.Null(r.TemporaryPassword);
    }

    [Fact]
    public async Task Lookups_are_batched_once_per_type_for_the_whole_file()
    {
        var requests = Enumerable.Range(0, 25)
            .Select(i => ValidatorTests.ValidRequest($"user{i:00}", i + 2))
            .ToList();

        var results = await _service.ProvisionAsync(requests, dryRun: true);

        Assert.Equal(25, results.Count);
        Assert.Equal(3, _client.LookupCalls); // roles, groups, org units
    }

    [Fact]
    public async Task Results_come_back_in_file_order()
    {
        var requests = new[]
        {
            ValidatorTests.ValidRequest("first", 2),
            ValidatorTests.ValidRequest("x", 3), // invalid username, handled in an earlier pass
            ValidatorTests.ValidRequest("third", 4),
        };

        var results = await _service.ProvisionAsync(requests, dryRun: true);

        Assert.Equal(new[] { 2, 3, 4 }, results.Select(r => r.RowNumber));
    }
}
