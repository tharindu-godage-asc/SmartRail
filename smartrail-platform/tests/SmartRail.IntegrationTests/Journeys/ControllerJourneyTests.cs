using System.Net;
using System.Text.Json;
using SmartRail.IntegrationTests.Database;
using Xunit;

namespace SmartRail.IntegrationTests.Journeys;

/// <summary>Spec 16 F1: the controller's read-only view of one journey. Train 8058 leaves Veyangoda at 08:20; Nimal is assigned all day.</summary>
public sealed class ControllerJourneyTests(PostgresFixture postgres) : JourneyApiTestBase(postgres)
{
    private string ControllerToken => Issuer.Controller();

    private async Task<JsonElement> DetailAsync()
    {
        var response = await Get($"/api/v1/controller/journeys/{JourneyId}", ControllerToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await ReadAsync(response);
    }

    [Fact]
    public async Task A_journey_with_no_reports_shows_its_stops_as_upcoming_with_the_assigned_operator()
    {
        var detail = await DetailAsync();

        Assert.Equal("8058", detail.GetProperty("trainNumber").GetString());
        Assert.Equal("Inbound", detail.GetProperty("direction").GetString());
        var stops = detail.GetProperty("stops").EnumerateArray().ToList();
        Assert.Equal(5, stops.Count);
        Assert.Equal("Veyangoda", stops[0].GetProperty("stationName").GetString());
        Assert.Equal("Next", stops[0].GetProperty("state").GetString());
        Assert.All(stops.Skip(1), s => Assert.Equal("Upcoming", s.GetProperty("state").GetString()));
        Assert.Equal("1", stops[0].GetProperty("plannedPlatform").GetString());
        Assert.Equal(JsonValueKind.Object, detail.GetProperty("operator").ValueKind);
        Assert.Empty(detail.GetProperty("events").EnumerateArray());
    }

    [Fact]
    public async Task Reports_move_the_stop_states_and_appear_newest_first_with_who_made_them_and_the_reason()
    {
        await Report(Stops[0], Local(8, 21), eventType: "Departed");
        Clock.SetUtcNow(Local(8, 36));
        await ReportDelay(Local(8, 36), "SignalIssue", 6, null, null);

        var detail = await DetailAsync();

        var stops = detail.GetProperty("stops").EnumerateArray().ToList();
        Assert.Equal("Passed", stops[0].GetProperty("state").GetString());
        Assert.Equal("Next", stops[1].GetProperty("state").GetString());
        Assert.Equal("SignalIssue", detail.GetProperty("reportedReason").GetString());
        Assert.Equal(JsonValueKind.Null, detail.TryGetProperty("verifiedReason", out var v) ? v.ValueKind : JsonValueKind.Null);
        var events = detail.GetProperty("events").EnumerateArray().ToList();
        Assert.Equal("DelayReported", events[0].GetProperty("type").GetString());
        Assert.Equal(6, events[0].GetProperty("totalDelayMinutes").GetInt32());
        Assert.Equal("Departed", events[1].GetProperty("type").GetString());
        Assert.Equal("Veyangoda", events[1].GetProperty("stationName").GetString());
        Assert.Equal("Operator", events[1].GetProperty("source").GetString());
        Assert.Equal("Departed", detail.GetProperty("position").GetProperty("state").GetString());
    }

    [Fact]
    public async Task An_arrival_shows_the_stop_as_at_the_station()
    {
        await Report(Stops[0], Local(8, 21), eventType: "Departed");
        Clock.SetUtcNow(Local(8, 40));
        await Report(Stops[1], Local(8, 40), eventType: "Arrived");

        var stops = (await DetailAsync()).GetProperty("stops").EnumerateArray().ToList();

        Assert.Equal("AtStation", stops[1].GetProperty("state").GetString());
    }

    [Fact]
    public async Task Only_a_controller_may_read_it_and_an_unknown_journey_is_404()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await Get($"/api/v1/controller/journeys/{JourneyId}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Get($"/api/v1/controller/journeys/{JourneyId}", Issuer.Operator())).StatusCode);

        var missing = await Get($"/api/v1/controller/journeys/{Guid.NewGuid()}", ControllerToken);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Equal("journey_not_found", (await ReadAsync(missing)).GetProperty("reason").GetString());
    }
}
