using System.Text;
using Dhis2UserProvisioning.Models;

namespace Dhis2UserProvisioning.Csv;

/// <summary>
/// Reads user requests from a CSV file. Supports quoted fields (with commas and escaped
/// quotes inside them). List columns use semicolons, for example "Data entry clerk;Tracker user".
/// </summary>
public static class UserRequestCsvReader
{
    private static readonly string[] RequiredColumns =
        { "firstName", "surname", "username", "email", "userRoles", "orgUnits" };

    public static IReadOnlyList<UserRequest> ReadFile(string path) =>
        Read(File.ReadAllText(path, Encoding.UTF8));

    public static IReadOnlyList<UserRequest> Read(string csvText)
    {
        var rows = ParseRows(csvText);
        if (rows.Count == 0)
        {
            throw new FormatException("The CSV file is empty.");
        }

        var header = rows[0].Select(h => h.Trim()).ToList();
        var missing = RequiredColumns
            .Where(c => !header.Contains(c, StringComparer.OrdinalIgnoreCase))
            .ToList();
        if (missing.Count > 0)
        {
            throw new FormatException($"Missing required column(s): {string.Join(", ", missing)}.");
        }

        var index = header
            .Select((name, i) => (name, i))
            .ToDictionary(x => x.name, x => x.i, StringComparer.OrdinalIgnoreCase);

        var requests = new List<UserRequest>();
        for (var r = 1; r < rows.Count; r++)
        {
            var fields = rows[r];
            if (fields.All(string.IsNullOrWhiteSpace))
            {
                continue; // ignore blank lines
            }

            string Get(string column) =>
                index.TryGetValue(column, out var i) && i < fields.Count ? fields[i].Trim() : "";

            requests.Add(new UserRequest
            {
                RowNumber = r + 1, // 1-based, header is row 1
                FirstName = Get("firstName"),
                Surname = Get("surname"),
                Username = Get("username"),
                Email = Get("email"),
                PhoneNumber = Get("phoneNumber"),
                UserRoles = SplitList(Get("userRoles")),
                OrgUnits = SplitList(Get("orgUnits")),
                DataViewOrgUnits = SplitList(Get("dataViewOrgUnits")),
                UserGroups = SplitList(Get("userGroups")),
            });
        }

        return requests;
    }

    internal static IReadOnlyList<string> SplitList(string value) =>
        value.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    /// <summary>Minimal RFC 4180 style parser: quoted fields, "" escapes, CRLF or LF line endings.</summary>
    internal static List<List<string>> ParseRows(string text)
    {
        var rows = new List<List<string>>();
        var row = new List<string>();
        var field = new StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];

            if (inQuotes)
            {
                if (c == '"' && i + 1 < text.Length && text[i + 1] == '"')
                {
                    field.Append('"');
                    i++;
                }
                else if (c == '"')
                {
                    inQuotes = false;
                }
                else
                {
                    field.Append(c);
                }
                continue;
            }

            switch (c)
            {
                case '"':
                    inQuotes = true;
                    break;
                case ',':
                    row.Add(field.ToString());
                    field.Clear();
                    break;
                case '\r':
                    break;
                case '\n':
                    row.Add(field.ToString());
                    field.Clear();
                    rows.Add(row);
                    row = new List<string>();
                    break;
                default:
                    field.Append(c);
                    break;
            }
        }

        if (inQuotes)
        {
            throw new FormatException("The CSV file has an unclosed quoted field.");
        }

        if (field.Length > 0 || row.Count > 0)
        {
            row.Add(field.ToString());
            rows.Add(row);
        }

        // strip a UTF-8 byte order mark from the first header cell, if present
        if (rows.Count > 0 && rows[0].Count > 0)
        {
            rows[0][0] = rows[0][0].TrimStart('﻿');
        }

        return rows;
    }
}
