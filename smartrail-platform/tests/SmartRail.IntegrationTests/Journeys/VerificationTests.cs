using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using SmartRail.IntegrationTests.Database;
using Xunit;

namespace SmartRail.IntegrationTests.Journeys;

/// <summary>Spec 16 F2: FR-52, D-10, NFR-12. A controller verifies, changes or rejects a delay reason; passengers see it only after that.</summary>
public sealed class VerificationTests(PostgresFixture postgres) : JourneyApiTestBase(postgres)
{
    private string ControllerToken => Issuer.Controller();

    private async Task<Guid> DelayAsync(string reason, DateTimeOffset? at = null, int total = 10)
    {
        var when = at ?? Local(8, 28);
        if (when > Clock.GetUtcNow())
        {
            Clock.SetUtcNow(when);
        }

        Assert.Equal(HttpStatusCode.Created, (await ReportDelay(when, reason, total, null, null)).StatusCode);
        return await Database.ScalarAsync<Guid>($"select id from journey_exception where related_event_id in (select id from train_event where event_type = 'DelayReported') order by created_at desc limit 1");
    }

    private async Task<HttpResponseMessage> Post(string path, object body, string? token = null, bool noToken = false)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(body) };
        if (!noToken)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token ?? ControllerToken);
        }

        return await Client.SendAsync(request);
    }

    private Task<HttpResponseMessage> Verify(Guid id, string action, string? reasonCode = null, string? token = null, bool noToken = false) =>
        Post($"/api/v1/exceptions/{id}/verify", new { action, reasonCode }, token, noToken);

    private async Task<JsonElement> StatusAsync()
    {
        var response = await Get($"/api/v1/journeys/{JourneyId}/status");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await ReadAsync(response);
    }

    [Fact]
    public async Task Before_verification_the_public_status_has_no_reason()
    {
        await DelayAsync("Weather");

        var status = await StatusAsync();

        Assert.True(status.TryGetProperty("reason", out var reason) ? reason.ValueKind == JsonValueKind.Null : true);
        Assert.DoesNotContain("Weather", status.GetRawText());
    }

    [Fact]
    public async Task Verify_publishes_the_reported_reason_with_the_time_and_closes_the_exception()
    {
        var id = await DelayAsync("Weather");
        var versionBefore = (await StatusAsync()).GetProperty("version").GetInt32();
        Clock.Advance(TimeSpan.FromMinutes(2));

        var response = await Verify(id, "verify");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var status = await StatusAsync();
        Assert.Equal("Weather", status.GetProperty("reason").GetProperty("code").GetString());
        Assert.Equal(Clock.GetUtcNow(), status.GetProperty("reason").GetProperty("verifiedAt").GetDateTimeOffset());
        Assert.Equal(versionBefore + 1, status.GetProperty("version").GetInt32());
        Assert.Equal("ReasonVerified", await Database.ScalarAsync<string>($"select resolution from journey_exception where id = '{id}'"));
        Assert.Equal("jayasinghe", await Database.ScalarAsync<string>($"select resolved_by_name from journey_exception where id = '{id}'"));
        Assert.Equal("Controller", await Database.ScalarAsync<string>("select source from train_event where event_type = 'ReasonVerified'"));
    }

    [Fact]
    public async Task Change_publishes_the_approved_code_the_controller_chose()
    {
        var id = await DelayAsync("Unknown");

        Assert.Equal(HttpStatusCode.OK, (await Verify(id, "change", "SignalIssue")).StatusCode);

        var status = await StatusAsync();
        Assert.Equal("SignalIssue", status.GetProperty("reason").GetProperty("code").GetString());
        Assert.Equal("ReasonChanged", await Database.ScalarAsync<string>($"select resolution from journey_exception where id = '{id}'"));
    }

    [Theory]
    [InlineData("Unknown", "verify", null)]
    [InlineData("Weather", "change", "Unknown")]
    [InlineData("Weather", "change", "free text")]
    [InlineData("Weather", "change", null)]
    public async Task A_reason_that_is_not_approved_is_refused_and_nothing_changes(string reported, string action, string? code)
    {
        var id = await DelayAsync(reported);

        var response = await Verify(id, action, code);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("reason_not_approved", (await ReadAsync(response)).GetProperty("reason").GetString());
        Assert.Null((await Database.ScalarAsync<string?>($"select resolution from journey_exception where id = '{id}'")));
        Assert.Equal(0, await Database.ScalarAsync<long>("select count(*) from train_event where event_type = 'ReasonVerified'"));
    }

    [Fact]
    public async Task Reject_closes_the_exception_and_publishes_nothing()
    {
        var id = await DelayAsync("Weather");

        var response = await Verify(id, "reject");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Rejected", await Database.ScalarAsync<string>($"select resolution from journey_exception where id = '{id}'"));
        Assert.DoesNotContain("Weather", (await StatusAsync()).GetRawText());
        Assert.Equal(0, await Database.ScalarAsync<long>("select count(*) from train_event where event_type = 'ReasonVerified'"));
    }

    [Fact]
    public async Task A_second_verify_is_a_conflict_and_a_missing_or_other_exception_is_refused()
    {
        var id = await DelayAsync("Weather");
        Assert.Equal(HttpStatusCode.OK, (await Verify(id, "verify")).StatusCode);

        var again = await Verify(id, "verify");
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        Assert.Equal("already_resolved", (await ReadAsync(again)).GetProperty("reason").GetString());

        var missing = await Verify(Guid.NewGuid(), "verify");
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);

        var bad = await Verify(id, "approve");
        Assert.Equal(HttpStatusCode.UnprocessableEntity, bad.StatusCode);
    }

    [Fact]
    public async Task A_new_delay_report_makes_the_reason_unverified_again()
    {
        var id = await DelayAsync("Weather");
        await Verify(id, "verify");
        Clock.Advance(TimeSpan.FromMinutes(1));
        Assert.Equal(HttpStatusCode.Created, (await ReportDelay(Clock.GetUtcNow(), "Obstruction", 20, null, null)).StatusCode);

        var status = await StatusAsync();

        Assert.True(!status.TryGetProperty("reason", out var reason) || reason.ValueKind == JsonValueKind.Null);
    }

    [Fact]
    public async Task Bulk_verify_handles_each_item_on_its_own()
    {
        var weather = await DelayAsync("Weather");
        Clock.Advance(TimeSpan.FromMinutes(1));
        var unknown = await DelayAsync("Unknown", Clock.GetUtcNow(), total: 20);

        var response = await Post("/api/v1/exceptions/verify", new { ids = new[] { unknown, weather, Guid.NewGuid() } });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var results = (await ReadAsync(response)).GetProperty("results");
        Assert.Equal("skipped", results[0].GetProperty("status").GetString());
        Assert.Equal("reason_not_approved", results[0].GetProperty("reason").GetString());
        Assert.Equal("verified", results[1].GetProperty("status").GetString());
        Assert.Equal("exception_not_found", results[2].GetProperty("reason").GetString());
    }

    [Fact]
    public async Task Bulk_verify_needs_between_one_and_fifty_ids()
    {
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await Post("/api/v1/exceptions/verify", new { ids = Array.Empty<Guid>() })).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await Post("/api/v1/exceptions/verify", new { ids = Enumerable.Range(0, 51).Select(_ => Guid.NewGuid()) })).StatusCode);
    }

    [Fact]
    public async Task Only_a_controller_may_verify()
    {
        var id = await DelayAsync("Weather");

        Assert.Equal(HttpStatusCode.Unauthorized, (await Verify(id, "verify", noToken: true)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Verify(id, "verify", token: Issuer.Operator())).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Post("/api/v1/exceptions/verify", new { ids = new[] { id } }, noToken: true)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Post("/api/v1/exceptions/verify", new { ids = new[] { id } }, Issuer.Operator())).StatusCode);
    }

    [Fact]
    public async Task An_early_departure_exception_has_no_reason_to_verify()
    {
        await Report(Stops[0], Local(8, 19), eventType: "Departed");
        var id = await Database.ScalarAsync<Guid>("select id from journey_exception where type = 'EarlyDeparture'");

        var response = await Verify(id, "verify");

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("not_a_verification_exception", (await ReadAsync(response)).GetProperty("reason").GetString());
    }

    [Fact]
    public async Task The_controllers_queue_shows_the_reported_reason_and_whether_it_can_be_published()
    {
        await DelayAsync("Unknown");

        var queue = await ReadAsync(await Get("/api/v1/exceptions?status=open", ControllerToken));

        var item = queue.GetProperty("items")[0];
        Assert.Equal("Unknown", item.GetProperty("reportedReason").GetString());
        Assert.False(item.GetProperty("canVerify").GetBoolean());
    }
}
