using Dhis2UserProvisioning.Csv;
using Dhis2UserProvisioning.Dhis2;
using Dhis2UserProvisioning.Models;
using Dhis2UserProvisioning.Provisioning;
using Dhis2UserProvisioning.Reporting;

namespace Dhis2UserProvisioning;

public static class Program
{
    private const string Usage = """
        Usage:
          dotnet run --project src/Dhis2UserProvisioning -- --file <users.csv> [--dry-run] [--report <report.csv>]

        Connection settings come from environment variables (never from the command line):
          DHIS2_BASE_URL   e.g. https://play.im.dhis2.org/stable-2-41-x
          DHIS2_USERNAME
          DHIS2_PASSWORD

        --dry-run   validate and check everything against DHIS2, but create nothing
        --report    where to write the results (default: reports/report-<timestamp>.csv)
        """;

    public static async Task<int> Main(string[] args)
    {
        var options = ParseArgs(args);
        if (options is null)
        {
            Console.WriteLine(Usage);
            return 2;
        }

        var baseUrl = Environment.GetEnvironmentVariable("DHIS2_BASE_URL");
        var user = Environment.GetEnvironmentVariable("DHIS2_USERNAME");
        var password = Environment.GetEnvironmentVariable("DHIS2_PASSWORD");
        if (string.IsNullOrWhiteSpace(baseUrl) || string.IsNullOrWhiteSpace(user) || string.IsNullOrWhiteSpace(password))
        {
            Console.Error.WriteLine("Set DHIS2_BASE_URL, DHIS2_USERNAME and DHIS2_PASSWORD first. See .env.example.");
            return 2;
        }

        IReadOnlyList<UserRequest> requests;
        try
        {
            requests = UserRequestCsvReader.ReadFile(options.Value.File);
        }
        catch (Exception e) when (e is IOException or FormatException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine($"Could not read '{options.Value.File}': {e.Message}");
            return 1;
        }

        Console.WriteLine($"Read {requests.Count} request(s) from {options.Value.File}{(options.Value.DryRun ? " (dry run)" : "")}");

        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
        var client = new Dhis2Client(http, baseUrl, user, password);
        var service = new ProvisioningService(client);

        IReadOnlyList<ProvisioningResult> results;
        try
        {
            results = await service.ProvisionAsync(requests, options.Value.DryRun);
        }
        catch (HttpRequestException e)
        {
            Console.Error.WriteLine($"DHIS2 error: {e.Message}");
            return 1;
        }

        foreach (var r in results)
        {
            Console.WriteLine($"  row {r.RowNumber,3}  {r.Username,-25} {r.Status,-11} {r.Message}");
        }

        var reportPath = options.Value.Report
            ?? Path.Combine("reports", $"report-{DateTime.Now:yyyyMMdd-HHmmss}.csv");
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(reportPath))!);
        await File.WriteAllTextAsync(reportPath, ReportWriter.ToCsv(results));

        Console.WriteLine();
        Console.WriteLine($"Summary: {ReportWriter.Summary(results)}");
        Console.WriteLine($"Report written to {reportPath}");
        if (results.Any(r => r.TemporaryPassword is not null))
        {
            Console.WriteLine("The report contains temporary passwords. Share them securely and delete the file afterwards.");
        }

        return results.Any(r => r.Status is ProvisioningStatus.Failed or ProvisioningStatus.Invalid) ? 1 : 0;
    }

    internal static (string File, bool DryRun, string? Report)? ParseArgs(string[] args)
    {
        string? file = null, report = null;
        var dryRun = false;

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--file" when i + 1 < args.Length:
                    file = args[++i];
                    break;
                case "--report" when i + 1 < args.Length:
                    report = args[++i];
                    break;
                case "--dry-run":
                    dryRun = true;
                    break;
                default:
                    return null;
            }
        }

        return file is null ? null : (file, dryRun, report);
    }
}
