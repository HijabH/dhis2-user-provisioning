using Dhis2UserProvisioning.Models;
using Dhis2UserProvisioning.Reporting;
using Xunit;

namespace Dhis2UserProvisioning.Tests;

public class ReportWriterTests
{
    [Fact]
    public void Writes_header_and_escapes_commas_and_quotes()
    {
        var results = new[]
        {
            new ProvisioningResult(2, "akhan", ProvisioningStatus.Created, "created", "uid1", "Pa,ss\"1"),
            new ProvisioningResult(3, "bad", ProvisioningStatus.Invalid, "email is required; at least one user role is required"),
        };

        var lines = ReportWriter.ToCsv(results).TrimEnd().Split(Environment.NewLine);

        Assert.Equal("row,username,status,message,userId,temporaryPassword", lines[0]);
        Assert.Equal("2,akhan,Created,created,uid1,\"Pa,ss\"\"1\"", lines[1]);
        Assert.Equal("3,bad,Invalid,email is required; at least one user role is required,,", lines[2]);
    }

    [Fact]
    public void Summary_counts_only_statuses_that_occur()
    {
        var results = new[]
        {
            new ProvisioningResult(2, "a", ProvisioningStatus.Created, ""),
            new ProvisioningResult(3, "b", ProvisioningStatus.Created, ""),
            new ProvisioningResult(4, "c", ProvisioningStatus.Skipped, ""),
        };

        Assert.Equal("Created: 2, Skipped: 1", ReportWriter.Summary(results));
    }
}
