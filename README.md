# DHIS2 User Provisioning

![build-and-test](https://github.com/HijabH/dhis2-user-provisioning/actions/workflows/ci.yml/badge.svg)

A C# / .NET 8 command-line tool that creates [DHIS2](https://dhis2.org) user accounts in bulk from a CSV file.

In national health programmes, new data entry staff are requested on a form: name, email, role and facility. Creating those accounts one by one in the DHIS2 web interface is slow and error-prone. This tool reads the requests, checks every row, looks up the roles and facilities in DHIS2, skips people who already have an account, creates the rest, and writes a report with the results and a temporary password for each new user.

I built a tool like this in my job, where it handled more than 5,000 DHIS2 users and cut the manual work by about 80%. That code belongs to my former employer, so this repository is a **new implementation written from scratch**. It runs against DHIS2's public demo server and contains no real data.

## Features

- Reads a CSV of user requests, with quoted fields and semicolon-separated lists for roles, groups and facilities
- Validates each row before calling DHIS2: required fields, username format, email, phone number, duplicate usernames in the file
- Resolves user role and group names and organisation unit codes to DHIS2 ids in batched requests
- Skips usernames that already exist in DHIS2
- `--dry-run` mode checks everything against DHIS2 without creating anything
- Generates a random temporary password per user that meets the default DHIS2 password policy
- Writes a CSV report and a summary, and returns a non-zero exit code if any row failed
- Credentials come only from environment variables

## Quick start

Requires the [.NET 8 SDK](https://dotnet.microsoft.com/download).

```bash
git clone https://github.com/HijabH/dhis2-user-provisioning.git
cd dhis2-user-provisioning

# DHIS2's public demo server (see .env.example)
export DHIS2_BASE_URL=https://play.im.dhis2.org/stable-2-41-x
export DHIS2_USERNAME=admin
export DHIS2_PASSWORD=district

# check the file first, create nothing
dotnet run --project src/Dhis2UserProvisioning -- --file samples/users.csv --dry-run

# then create the users
dotnet run --project src/Dhis2UserProvisioning -- --file samples/users.csv
```

On Windows PowerShell, set the variables with `$env:DHIS2_BASE_URL="..."`.

The demo server is reset regularly and its version in the URL changes over time. If the URL no longer works, pick a current one from [play.dhis2.org](https://play.dhis2.org).

## CSV format

| Column | Required | Example |
| --- | --- | --- |
| `firstName` | yes | Amina |
| `surname` | yes | Khan |
| `username` | yes | amina.khan |
| `email` | yes | amina.khan@example.org |
| `phoneNumber` | no | +92 300 1234567 |
| `userRoles` | yes | `Data entry clerk;Tracker data analysis` (names) |
| `orgUnits` | yes | `OU_559` (codes; data capture units) |
| `dataViewOrgUnits` | no | `OU_559` (codes) |
| `userGroups` | no | names |

See [`samples/users.csv`](samples/users.csv). It deliberately includes a row with a bad email and a row with an unknown role and facility, so you can see how errors are reported. The role names and facility codes come from the Sierra Leone demo database; if a demo release renames them, the dry run will say exactly which ones are missing.

## Example output

```text
Read 5 request(s) from samples/users.csv
  row   2  amina.khan                Created     created
  row   3  bilal.ahmed               Skipped     a user with this username already exists
  row   4  sara.malik                Created     created
  row   5  usman.raza                Invalid     email 'not-an-email' is not a valid address
  row   6  hina.iqbal                Invalid     unknown user role 'Superhero'; unknown organisation unit 'OU_999'

Summary: Created: 2, Skipped: 1, Invalid: 2
Report written to reports/report-20261119-180512.csv
The report contains temporary passwords. Share them securely and delete the file afterwards.
```

## How it works

```text
users.csv ──> CSV reader ──> local validation ──> batched lookups in DHIS2 ──> per row:
                                  │                 (roles, groups,             unknown reference -> Invalid
                                  │                  org units)                 username exists   -> Skipped
                                  v                                             dry run           -> WouldCreate
                              Invalid rows                                      otherwise POST /api/users
                                                                                         -> Created / Failed
                                                      all results ──> report CSV + summary
```

| Folder | Responsibility |
| --- | --- |
| `Csv/` | Parsing, with no external packages |
| `Validation/` | Checks that need no network |
| `Dhis2/` | Web API client behind an `IDhis2Client` interface, and the JSON payload builder |
| `Provisioning/` | The workflow above |
| `Reporting/` | Report CSV and summary |

The tool itself has no NuGet dependencies. Lookups are sent in batches of 50, so a file with hundreds of users needs only a handful of lookup requests.

## Tests

```bash
dotnet test
```

49 xUnit tests cover CSV parsing, validation, the payload and password rules, the provisioning workflow (with a fake DHIS2 client) and the HTTP client (with a stub message handler). No test calls a real server. GitHub Actions builds the project and runs the tests on every push.

## Limitations and ideas

- Uses basic authentication; personal access tokens (DHIS2 2.38+) would be better for production
- Role and group names containing commas cannot be looked up in one batched filter
- Creates users only; updating or disabling existing accounts would be a natural next step
- Temporary passwords are written to the report; an email invitation flow (`/api/users/invite`) would avoid that

## License

[MIT](LICENSE)
