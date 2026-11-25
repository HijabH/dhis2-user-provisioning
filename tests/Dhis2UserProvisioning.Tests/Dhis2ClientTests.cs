using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Dhis2UserProvisioning.Dhis2;
using Xunit;

namespace Dhis2UserProvisioning.Tests;

public class Dhis2ClientTests
{
    /// <summary>Returns canned responses and remembers the requests it received.</summary>
    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _respond;
        public List<HttpRequestMessage> Requests { get; } = new();
        public List<string> Bodies { get; } = new();

        public StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) => _respond = respond;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Requests.Add(request);
            Bodies.Add(request.Content is null ? "" : await request.Content.ReadAsStringAsync(ct));
            return _respond(request);
        }
    }

    private static HttpResponseMessage Json(HttpStatusCode status, string json) =>
        new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    private static Dhis2Client CreateClient(StubHandler handler) =>
        new(new HttpClient(handler), "https://dhis.example.org", "admin", "district");

    [Theory]
    [InlineData("https://dhis.example.org", "https://dhis.example.org/api/")]
    [InlineData("https://dhis.example.org/", "https://dhis.example.org/api/")]
    [InlineData("https://dhis.example.org/api", "https://dhis.example.org/api/")]
    [InlineData("https://host/dhis/api/", "https://host/dhis/api/")]
    public void Normalises_the_api_url(string input, string expected)
    {
        Assert.Equal(expected, Dhis2Client.NormaliseApiUrl(input));
    }

    [Fact]
    public async Task Looks_up_user_roles_by_name_with_basic_auth()
    {
        var handler = new StubHandler(_ => Json(HttpStatusCode.OK,
            """{"userRoles":[{"id":"r1","name":"Data entry clerk"},{"id":"r2","name":"Tracker user"}]}"""));
        var client = CreateClient(handler);

        var ids = await client.GetUserRoleIdsAsync(new[] { "Data entry clerk", "Tracker user", "data entry clerk" });

        Assert.Equal("r1", ids["Data entry clerk"]);
        Assert.Equal("r2", ids["tracker user"]); // lookups are case-insensitive

        var request = Assert.Single(handler.Requests);
        Assert.Equal("Basic", request.Headers.Authorization!.Scheme);
        var url = Uri.UnescapeDataString(request.RequestUri!.ToString());
        Assert.StartsWith("https://dhis.example.org/api/userRoles?", url);
        Assert.Contains("filter=name:in:[Data entry clerk,Tracker user]", url);
    }

    [Fact]
    public async Task Splits_large_lookups_into_batches_of_fifty()
    {
        var handler = new StubHandler(_ => Json(HttpStatusCode.OK, """{"organisationUnits":[]}"""));
        var client = CreateClient(handler);

        await client.GetOrgUnitIdsAsync(Enumerable.Range(1, 120).Select(i => $"OU_{i}"));

        Assert.Equal(3, handler.Requests.Count);
    }

    [Fact]
    public async Task Detects_existing_username()
    {
        var handler = new StubHandler(_ => Json(HttpStatusCode.OK, """{"users":[{"id":"u1"}]}"""));

        Assert.True(await CreateClient(handler).UsernameExistsAsync("akhan"));
    }

    [Fact]
    public async Task Creates_user_and_reads_new_uid()
    {
        var handler = new StubHandler(_ => Json(HttpStatusCode.Created,
            """{"httpStatus":"Created","status":"OK","response":{"uid":"NEWUID12345"}}"""));
        var client = CreateClient(handler);

        var result = await client.CreateUserAsync(new JsonObject { ["username"] = "akhan" });

        Assert.True(result.Success);
        Assert.Equal("NEWUID12345", result.UserId);
        Assert.Equal(HttpMethod.Post, handler.Requests[0].Method);
        Assert.Contains("\"username\":\"akhan\"", handler.Bodies[0]);
    }

    [Fact]
    public async Task Returns_dhis2_error_reports_on_failure()
    {
        var handler = new StubHandler(_ => Json(HttpStatusCode.Conflict,
            """{"status":"ERROR","response":{"errorReports":[{"message":"Username already taken"},{"message":"Invalid email"}]}}"""));

        var result = await CreateClient(handler).CreateUserAsync(new JsonObject());

        Assert.False(result.Success);
        Assert.Equal("HTTP 409: Username already taken; Invalid email", result.Message);
    }

    [Fact]
    public async Task Throws_a_clear_error_when_a_lookup_fails()
    {
        var handler = new StubHandler(_ => Json(HttpStatusCode.Unauthorized, """{"message":"Unauthorized"}"""));

        var ex = await Assert.ThrowsAsync<HttpRequestException>(
            () => CreateClient(handler).GetUserRoleIdsAsync(new[] { "x" }));

        Assert.Contains("401", ex.Message);
        Assert.Contains("Unauthorized", ex.Message);
    }

    [Fact]
    public void Error_message_falls_back_to_raw_body_when_not_json()
    {
        Assert.Equal("<html>Bad gateway</html>", Dhis2Client.ReadErrorMessage("<html>Bad gateway</html>"));
    }
}
