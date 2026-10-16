using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using SmartRail.IntegrationTests.Database;
using Xunit;

namespace SmartRail.IntegrationTests.Journeys;

/// <summary>
/// Spec 07, slice B1: AC-E05-02 (1, 2, 5, 6), FR-51. A delay report or an early departure raises one exception; a controller
/// lists the open ones and resolves them. Train 8058 leaves Veyangoda at 08:20.
/// </summary>
public sealed class ExceptionsQueueTests(PostgresFixture postgres) : JourneyApiTestBase(postgres)
{
    private string ControllerToken => Issuer.Controller();

    private void AdvanceTo(DateTimeOffset at)
    {
        if (at > Clock.GetUtcNow())
        {
            Clock.SetUtcNow(at);
        }
    }

    private async Task<HttpResponseMessage> Delay(DateTimeOffset at, string reason, Guid? id = null, int total = 10)
    {
        AdvanceTo(at);
        return await ReportDelay(at, reason, total, null, id);
    }

    private async Task<HttpResponseMessage> Resolve(Guid id, string? action, string? token = null, bool noToken = false)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/exceptions/{id}/resolve") { Content = JsonContent.Create(new { action }) };
        if (!noToken)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token ?? ControllerToken);
        }

        return await Client.SendAsync(request);
    }

    private async Task<System.Text.Json.JsonElement> OpenAsync()
    {
        var response = await Get("/api/v1/exceptions?status=open", ControllerToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await ReadAsync(response);
    }

    // ---- raising ----

    [Fact]
    public async Task A_TechnicalFault_delay_after_Veyangoda_raises_one_Critical_item_with_train_and_station()
    {
        AdvanceTo(Local(8, 21));
        await Report(Stops[0], Local(8, 21), eventType: "Departed");
        await Delay(Local(8, 28), "TechnicalFault");

        var queue = await OpenAsync();

        Assert.Equal(1, queue.GetProperty("openCount").GetInt32());
        var item = queue.GetProperty("items")[0];
        Assert.Equal("Critical", item.GetProperty("severity").GetString());
        Assert.Equal("ReasonAwaitingVerification", item.GetProperty("type").GetString());
        Assert.Equal("8058", item.GetProperty("trainNumber").GetString());
        Assert.Equal("Veyangoda", item.GetProperty("stationName").GetString());
    }

    [Fact]
    public async Task A_delay_before_anything_is_reported_has_no_station()
    {
        await Delay(Local(8, 28), "SignalIssue");

        var item = (await OpenAsync()).GetProperty("items")[0];

        Assert.Equal("High", item.GetProperty("severity").GetString());
        Assert.Equal(System.Text.Json.JsonValueKind.Null, item.GetProperty("stationName").ValueKind);
    }

    [Fact]
    public async Task Replaying_the_same_report_keeps_one_exception()
    {
        var id = Guid.NewGuid();
        await Delay(Local(8, 28), "Weather", id);
        var second = await Delay(Local(8, 28), "Weather", id);

        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Equal(1, await Database.ScalarAsync<long>("select count(*) from journey_exception"));
    }

    [Fact]
    public async Task A_report_the_server_rejects_raises_nothing()
    {
        var response = await ReportDelay(Local(8, 28), reason: "SignalIssue", total: null, unknown: null); // no total, not unknown: 422

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal(0, await Database.ScalarAsync<long>("select count(*) from journey_exception"));
    }

    [Fact]
    public async Task A_Critical_item_sorts_before_an_older_High_one()
    {
        await Delay(Local(8, 24), "SignalIssue"); // older, High
        await Delay(Local(8, 28), "Obstruction", total: 15); // newer, Critical

        var items = (await OpenAsync()).GetProperty("items");

        Assert.Equal("Critical", items[0].GetProperty("severity").GetString());
        Assert.Equal("High", items[1].GetProperty("severity").GetString());
    }

    [Fact]
    public async Task A_departure_at_least_30_seconds_early_raises_an_EarlyDeparture()
    {
        AdvanceTo(Local(8, 19, 30));
        await Report(Stops[0], Local(8, 19, 30), eventType: "Departed"); // exactly 30 s before 08:20

        var item = (await OpenAsync()).GetProperty("items")[0];

        Assert.Equal("EarlyDeparture", item.GetProperty("type").GetString());
        Assert.Equal("High", item.GetProperty("severity").GetString());
        Assert.Equal("Veyangoda", item.GetProperty("stationName").GetString());
    }

    [Fact]
    public async Task A_departure_29_seconds_early_and_an_on_time_one_raise_nothing()
    {
        AdvanceTo(Local(8, 19, 31));
        await Report(Stops[0], Local(8, 19, 31), eventType: "Departed");
        AdvanceTo(Local(8, 31));
        await Report(Stops[1], Local(8, 31), eventType: "Departed");

        Assert.Equal(0, await Database.ScalarAsync<long>("select count(*) from journey_exception"));
    }

    // ---- resolving ----

    [Fact]
    public async Task Resolving_removes_the_item_and_records_who_and_what()
    {
        await Delay(Local(8, 28), "Weather");
        var id = (await OpenAsync()).GetProperty("items")[0].GetProperty("id").GetGuid();

        var response = await Resolve(id, "Acknowledged");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(0, (await OpenAsync()).GetProperty("openCount").GetInt32());
        Assert.Equal("d0d0d0d0-0000-4000-8000-000000000003", await Database.ScalarAsync<string>("select resolved_by from journey_exception"));
        Assert.Equal("Acknowledged", await Database.ScalarAsync<string>("select resolution from journey_exception"));
    }

    [Fact]
    public async Task Resolving_twice_is_409_already_resolved()
    {
        await Delay(Local(8, 28), "Weather");
        var id = (await OpenAsync()).GetProperty("items")[0].GetProperty("id").GetGuid();
        await Resolve(id, "Escalated");

        var second = await Resolve(id, "NoActionNeeded");

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        Assert.Equal("already_resolved", (await ReadAsync(second)).GetProperty("reason").GetString());
        Assert.Equal("Escalated", await Database.ScalarAsync<string>("select resolution from journey_exception")); // the first stays
    }

    [Fact]
    public async Task An_unknown_id_is_404_and_a_bad_action_is_422()
    {
        await Delay(Local(8, 28), "Weather");
        var id = (await OpenAsync()).GetProperty("items")[0].GetProperty("id").GetGuid();

        var missing = await Resolve(Guid.NewGuid(), "Acknowledged");
        var badAction = await Resolve(id, "Ignore");

        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Equal("exception_not_found", (await ReadAsync(missing)).GetProperty("reason").GetString());
        Assert.Equal(HttpStatusCode.UnprocessableEntity, badAction.StatusCode);
        Assert.Equal(1, (await OpenAsync()).GetProperty("openCount").GetInt32()); // still open
    }

    // ---- who may use it ----

    [Fact]
    public async Task Only_a_controller_may_read_or_resolve()
    {
        await Delay(Local(8, 28), "Weather");
        var id = (await OpenAsync()).GetProperty("items")[0].GetProperty("id").GetGuid();

        Assert.Equal(HttpStatusCode.Unauthorized, (await Get("/api/v1/exceptions")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Get("/api/v1/exceptions", OperatorToken)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Resolve(id, "Acknowledged", noToken: true)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Resolve(id, "Acknowledged", OperatorToken)).StatusCode);
        Assert.Equal(1, (await OpenAsync()).GetProperty("openCount").GetInt32());
    }

    [Fact]
    public async Task Any_status_other_than_open_or_resolved_is_422()
    {
        var response = await Get("/api/v1/exceptions?status=closed", ControllerToken);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task The_app_role_can_update_the_table()
    {
        await Delay(Local(8, 28), "Weather");

        await Database.ExecuteAsync("update journey_exception set resolved_at = now(), resolved_by = 'x', resolution = 'Acknowledged'", Database.AppConnection);

        Assert.Equal(1, await Database.ScalarAsync<long>("select count(*) from journey_exception where resolved_by = 'x'"));
    }
}
