using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using SmartRail.IntegrationTests.Database;
using Xunit;

namespace SmartRail.IntegrationTests.Journeys;

/// <summary>
/// Spec 07, slice B2: AC-E05-01 (1, 3, 4, 6). The controller overview of journeys in progress. Train 8058 runs 08:20 to 08:55
/// (Veyangoda dep 08:20, Gampaha arr 08:30). The clock starts at 08:28.
/// </summary>
public sealed class OverviewTests(PostgresFixture postgres) : JourneyApiTestBase(postgres)
{
    private string ControllerToken => Issuer.Controller();

    private void AdvanceTo(DateTimeOffset at)
    {
        if (at > Clock.GetUtcNow())
        {
            Clock.SetUtcNow(at);
        }
    }

    private async Task<JsonElement> OverviewAsync()
    {
        var response = await Get("/api/v1/overview", ControllerToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await ReadAsync(response);
    }

    private async Task Depart(int stopIndex, DateTimeOffset at)
    {
        AdvanceTo(at);
        Assert.Equal(HttpStatusCode.Created, (await Report(Stops[stopIndex], at, eventType: "Departed")).StatusCode);
    }

    private async Task Delay(DateTimeOffset at, string reason, int? total, bool? unknown = null)
    {
        AdvanceTo(at);
        Assert.Equal(HttpStatusCode.Created, (await ReportDelay(at, reason, total, unknown)).StatusCode);
    }

    // ---- AC-E05-01 1: what a controller sees ----

    [Fact]
    public async Task A_journey_with_no_report_yet_is_listed_with_no_last_event_and_nothing_awaiting()
    {
        var overview = await OverviewAsync();

        Assert.Equal(0, overview.GetProperty("openExceptionCount").GetInt32());
        var journey = Assert.Single(overview.GetProperty("journeys").EnumerateArray());
        Assert.Equal("8058", journey.GetProperty("trainNumber").GetString());
        Assert.Equal("Veyangoda", journey.GetProperty("from").GetString());
        Assert.Equal("Colombo Fort", journey.GetProperty("to").GetString());
        Assert.Equal(JsonValueKind.Null, journey.GetProperty("lastEvent").ValueKind);
        Assert.Equal(JsonValueKind.Null, journey.GetProperty("nextStop").ValueKind);
        Assert.Equal(JsonValueKind.Null, journey.GetProperty("secondsSinceUpdate").ValueKind);
        Assert.Equal("none", journey.GetProperty("verification").GetString());
    }

    [Fact]
    public async Task After_a_departure_the_row_shows_the_last_event_the_delay_the_next_stop_and_the_source()
    {
        await Depart(0, Local(8, 24)); // 4 minutes late

        var journey = (await OverviewAsync()).GetProperty("journeys")[0];

        var last = journey.GetProperty("lastEvent");
        Assert.Equal("Departed", last.GetProperty("type").GetString());
        Assert.Equal("Veyangoda", last.GetProperty("stationName").GetString());
        Assert.Equal(Local(8, 24), last.GetProperty("occurredAt").GetDateTimeOffset());
        Assert.Equal("OperatorReport", last.GetProperty("source").GetString()); // AC-E05-01 3
        Assert.Equal(4, journey.GetProperty("delayMinutes").GetInt32());
        Assert.True(journey.GetProperty("delayKnown").GetBoolean());
        Assert.Equal("Gampaha", journey.GetProperty("nextStop").GetProperty("stationName").GetString());
        Assert.Equal(Local(8, 34), journey.GetProperty("nextStop").GetProperty("expectedAt").GetDateTimeOffset());
    }

    [Fact]
    public async Task Seconds_since_update_count_from_when_the_server_received_the_last_report()
    {
        await Depart(0, Local(8, 24));
        Clock.Advance(TimeSpan.FromSeconds(90));

        var journey = (await OverviewAsync()).GetProperty("journeys")[0];

        Assert.Equal(90, journey.GetProperty("secondsSinceUpdate").GetInt64());
    }

    [Fact]
    public async Task An_unknown_delay_has_no_next_stop_time()
    {
        await Depart(0, Local(8, 24));
        await Delay(Local(8, 26), "Unknown", total: null, unknown: true);

        var journey = (await OverviewAsync()).GetProperty("journeys")[0];

        Assert.False(journey.GetProperty("delayKnown").GetBoolean());
        Assert.Equal(JsonValueKind.Null, journey.GetProperty("nextStop").ValueKind);
        Assert.Equal("DelayReported", journey.GetProperty("lastEvent").GetProperty("type").GetString());
    }

    // ---- AC-E05-01 4 and 6: open exceptions and verification ----

    [Fact]
    public async Task A_reported_reason_shows_awaiting_and_counts_until_a_controller_resolves_it()
    {
        await Delay(Local(8, 28), "TechnicalFault", total: 10);

        var before = await OverviewAsync();
        Assert.Equal(1, before.GetProperty("openExceptionCount").GetInt32());
        Assert.Equal("awaiting", before.GetProperty("journeys")[0].GetProperty("verification").GetString());

        var queue = await ReadAsync(await Get("/api/v1/exceptions", ControllerToken));
        var id = queue.GetProperty("items")[0].GetProperty("id").GetGuid();
        var resolve = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/exceptions/{id}/resolve") { Content = JsonContent.Create(new { action = "Acknowledged" }) };
        resolve.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ControllerToken);
        Assert.Equal(HttpStatusCode.NoContent, (await Client.SendAsync(resolve)).StatusCode);

        var after = await OverviewAsync();
        Assert.Equal(0, after.GetProperty("openExceptionCount").GetInt32());
        Assert.Equal("none", after.GetProperty("journeys")[0].GetProperty("verification").GetString());
    }

    [Fact]
    public async Task An_early_departure_counts_but_is_not_a_reason_awaiting_verification()
    {
        await Depart(0, Local(8, 19));

        var overview = await OverviewAsync();

        Assert.Equal(1, overview.GetProperty("openExceptionCount").GetInt32());
        Assert.Equal("none", overview.GetProperty("journeys")[0].GetProperty("verification").GetString());
    }

    // ---- which journeys are listed ----

    [Fact]
    public async Task A_completed_journey_is_not_listed()
    {
        await Depart(0, Local(8, 21));
        AdvanceTo(Local(8, 56));
        var arrived = await Report(Stops[4], Local(8, 56), eventType: "Arrived");
        Assert.Equal(HttpStatusCode.Created, arrived.StatusCode);

        var overview = await OverviewAsync();

        Assert.Empty(overview.GetProperty("journeys").EnumerateArray());
    }

    // ---- access, and reading changes nothing ----

    [Fact]
    public async Task Only_a_controller_may_read_the_overview()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await Get("/api/v1/overview")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Get("/api/v1/overview", OperatorToken)).StatusCode);
    }

    [Fact]
    public async Task Reading_the_overview_changes_nothing_a_passenger_sees()
    {
        await Delay(Local(8, 28), "Weather", total: 10);
        var before = await StatusAsync();
        var eventsBefore = await EventCountAsync();

        await OverviewAsync();
        await OverviewAsync();

        var after = await StatusAsync();
        Assert.Equal(before.GetProperty("version").GetInt32(), after.GetProperty("version").GetInt32());
        Assert.Equal(before.GetProperty("delayMinutes").GetInt32(), after.GetProperty("delayMinutes").GetInt32());
        Assert.Equal(eventsBefore, await EventCountAsync());
    }
}

/// <summary>The clock starts at 08:00, twenty minutes before train 8058 leaves.</summary>
public sealed class OverviewBeforeDepartureTests(PostgresFixture postgres) : JourneyApiTestBase(postgres)
{
    protected override DateTimeOffset StartClock => Local(8, 0);

    [Fact]
    public async Task A_journey_that_has_not_started_is_not_listed()
    {
        var response = await Get("/api/v1/overview", Issuer.Controller());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty((await ReadAsync(response)).GetProperty("journeys").EnumerateArray());
    }
}
