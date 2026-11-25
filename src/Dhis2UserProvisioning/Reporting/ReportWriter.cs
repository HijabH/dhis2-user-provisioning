using System.Text;
using Dhis2UserProvisioning.Models;

namespace Dhis2UserProvisioning.Reporting;

public static class ReportWriter
{
    public static string ToCsv(IEnumerable<ProvisioningResult> results)
    {
        var sb = new StringBuilder();
        sb.AppendLine("row,username,status,message,userId,temporaryPassword");
        foreach (var r in results)
        {
            sb.AppendLine(string.Join(",",
                r.RowNumber,
                Escape(r.Username),
                r.Status,
                Escape(r.Message),
                Escape(r.UserId ?? ""),
                Escape(r.TemporaryPassword ?? "")));
        }
        return sb.ToString();
    }

    public static string Summary(IReadOnlyCollection<ProvisioningResult> results) =>
        string.Join(", ", Enum.GetValues<ProvisioningStatus>()
            .Select(s => (s, n: results.Count(r => r.Status == s)))
            .Where(x => x.n > 0)
            .Select(x => $"{x.s}: {x.n}"));

    internal static string Escape(string value) =>
        value.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0
            ? "\"" + value.Replace("\"", "\"\"") + "\""
            : value;
}
