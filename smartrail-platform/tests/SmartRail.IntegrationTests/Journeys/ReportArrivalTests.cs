using System.Net;
using System.Text.Json;
using SmartRail.IntegrationTests.Database;
using Xunit;

namespace SmartRail.IntegrationTests.Journeys;

/// <summary>
/// Spec 06, slice A1: AC-E03-03 (1 to 4), AC-E04-01 (1), AC-E04-02 (1, 2, 4), AC-E04-03 (2) and the rules R1 to R3, R8 to R11.
/// Train 8058: Veyangoda 08:20; Gampaha arr 08:30 dep 08:31 (designated); Ragama 08:42/08:43; Maradana 08:50/08:51; Colombo Fort arr 08:55.
/// </summary>
public sealed class ReportArrivalTests(PostgresFixture postgres) : JourneyApiTestBase(postgres)
{
    /// <summary>The fake clock only moves forward; a late report keeps the later time it was sent at.</summary>
    private void AdvanceClockTo(DateTimeOffset at)
    {
        if (at > Clock.GetUtcNow())
        {
            Clock.SetUtcNow(at);
        }
    }

    private async Task<HttpResponseMessage> ReportAt(Guid stop, DateTimeOffset at, string type, Guid? id = null, string? token = null)
    {
        AdvanceClockTo(at);
        return await Report(stop, at, clientEventId: id, eventType: type, token: token);
    }

    // ---- AC-E03-03 (1) ----

    [Fact]
    public async Task An_arrival_at_a_designated_stop_is_recorded_and_the_status_shows_the_train_there()
    {
        await ReportAt(Stops[0], Local(8, 28), "Departed");
        var response = await ReportAt(Stops[1], Local(8, 40), "Arrived");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("Arrived", await Database.ScalarAsync<string>("select event_type from train_event order by occurred_at desc limit 1"));

        var status = await StatusAsync();
        var position = status.GetProperty("lastKnownPosition");
        Assert.Equal("Gampaha", position.GetProperty("stationName").GetString());
        Assert.Equal("Arrived", position.GetProperty("state").GetString());
        Assert.Equal(Local(8, 40), Time(position, "arrivedAt"));
        Assert.Equal(JsonValueKind.Null, position.GetProperty("departedAt").ValueKind);

        var gampaha = StopAt(status, 2);
        Assert.Equal("Reported", gampaha.GetProperty("confirmation").GetString());
        Assert.Equal(Local(8, 40), Time(gampaha, "actualArrival"));
        Assert.Equal(JsonValueKind.Null, gampaha.GetProperty("actualDeparture").ValueKind);

        // +10 against the scheduled 08:30: the train is expected to leave at 08:41, and every later stop moves with it (R4, R5).
        Assert.Equal(10, status.GetProperty("delayMinutes").GetInt32());
        Assert.Equal("Delayed", status.GetProperty("statusKey").GetString());
        Assert.Equal(Local(8, 41), Time(gampaha, "estimatedDeparture"));
        Assert.Equal(Local(8, 52), Time(StopAt(status, 3), "estimatedArrival"));
        Assert.Equal(Local(9, 0), Time(StopAt(status, 4), "estimatedArrival"));
        Assert.Equal(Local(9, 5), Time(StopAt(status, 5), "estimatedArrival"));
        Assert.Equal("Ragama", status.GetProperty("primaryEta").GetProperty("stationName").GetString());
        Assert.Equal("Running", await Database.ScalarAsync<string>("select status from journey"));
    }

    [Fact]
    public async Task After_the_departure_the_position_moves_on_and_keeps_both_times()
    {
        await ReportAt(Stops[1], Local(8, 40), "Arrived");
        await ReportAt(Stops[1], Local(8, 42), "Departed");

        var position = (await StatusAsync()).GetProperty("lastKnownPosition");
        Assert.Equal("Departed", position.GetProperty("state").GetString());
        Assert.Equal(Local(8, 42), Time(position, "departedAt"));
        Assert.Equal(Local(8, 40), Time(position, "arrivedAt"));
    }

    // ---- AC-E03-03 (2) ----

    [Theory]
    [InlineData(0)] // the first stop has no arrival
    public async Task An_arrival_at_a_stop_with_no_scheduled_arrival_is_a_422_and_stores_nothing(int stopIndex)
    {
        var response = await ReportAt(Stops[stopIndex], Local(8, 28), "Arrived");

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("not_arrival_station", (await ReadAsync(response)).GetProperty("reason").GetString());
        Assert.Equal(0, await EventCountAsync());
    }

    // ---- spec 19: an arrival at every stop that has one ----

    [Theory]
    [InlineData(2)] // Ragama
    [InlineData(3)] // Maradana
    public async Task An_arrival_at_an_in_between_stop_is_accepted(int stopIndex)
    {
        await Report(Stops[0], Local(8, 21), eventType: "Departed");
        Clock.SetUtcNow(Local(8, 45));

        var response = await ReportAt(Stops[stopIndex], Local(8, 44), "Arrived");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task A_departure_from_an_in_between_stop_does_not_need_its_arrival()
    {
        await Report(Stops[0], Local(8, 21), eventType: "Departed");
        Clock.SetUtcNow(Local(8, 45));

        var response = await ReportAt(Stops[2], Local(8, 44), "Departed");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task The_operator_journey_marks_every_stop_but_the_first_as_an_arrival_station()
    {
        var body = await ReadAsync(await Get("/api/v1/operator/journey", OperatorToken));

        var flags = body.GetProperty("stops").EnumerateArray().Select(s => s.GetProperty("isArrivalStation").GetBoolean()).ToList();
        Assert.Equal([false, true, true, true, true], flags);
    }

    // ---- AC-E03-03 (3), AC-E04-02 (4) ----

    [Fact]
    public async Task A_late_arriving_arrival_is_judged_by_when_it_happened_and_gives_the_in_order_result()
    {
        // The departure from Gampaha was sent first; the arrival at 08:40 reaches the server afterwards (offline sync).
        await ReportAt(Stops[1], Local(8, 41), "Departed");
        var late = await ReportAt(Stops[1], Local(8, 40), "Arrived");
        var afterLate = await StatusAsync();

        Assert.Equal(HttpStatusCode.Created, late.StatusCode);

        // The result is the one the same two reports give in the right order: both times are kept, the delay comes from the later one.
        var gampaha = StopAt(afterLate, 2);
        Assert.Equal(Local(8, 40), Time(gampaha, "actualArrival"));
        Assert.Equal(Local(8, 41), Time(gampaha, "actualDeparture"));
        Assert.Equal(10, afterLate.GetProperty("delayMinutes").GetInt32());
        Assert.Equal(Local(8, 52), Time(StopAt(afterLate, 3), "estimatedArrival"));
        Assert.Equal(2, await Database.ScalarAsync<long>("select count(*) from train_event"));
    }
    // ---- AC-E03-03 (4), spec 06 R10, R11 ----

    [Fact]
    public async Task An_arrival_at_the_final_stop_completes_the_journey_with_no_further_tap()
    {
        await ReportAt(Stops[3], Local(8, 59), "Departed");
        var response = await ReportAt(Stops[4], Local(9, 5), "Arrived");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("Completed", await Database.ScalarAsync<string>("select status from journey"));

        var status = await StatusAsync();
        Assert.Equal("Completed", status.GetProperty("statusKey").GetString());
        Assert.Equal("Normal", status.GetProperty("statusGroup").GetString());
        Assert.Equal(JsonValueKind.Null, status.GetProperty("primaryEta").ValueKind);
        Assert.Equal("Colombo Fort", status.GetProperty("lastKnownPosition").GetProperty("stationName").GetString());
        Assert.Equal("Arrived", status.GetProperty("lastKnownPosition").GetProperty("state").GetString());
    }

    [Fact]
    public async Task A_completed_journey_refuses_new_reports_but_still_answers_a_repeat()
    {
        var id = Guid.NewGuid();
        await ReportAt(Stops[4], Local(9, 5), "Arrived", id);

        var another = await ReportAt(Stops[1], Local(8, 40), "Arrived");
        var repeat = await ReportAt(Stops[4], Local(9, 5), "Arrived", id);

        Assert.Equal(HttpStatusCode.Conflict, another.StatusCode);
        Assert.Equal("journey_not_reportable", (await ReadAsync(another)).GetProperty("reason").GetString());
        Assert.Equal(HttpStatusCode.OK, repeat.StatusCode);
        Assert.Equal(1, await EventCountAsync());
    }

    [Fact]
    public async Task The_train_lookup_still_returns_todays_completed_journey()
    {
        await ReportAt(Stops[4], Local(9, 5), "Arrived");

        var body = await ReadAsync(await Get("/api/v1/trains/8058/status"));

        Assert.Equal("Completed", body.GetProperty("statusKey").GetString());
        Assert.Equal(JourneyId, body.GetProperty("journeyId").GetGuid());
    }

    // ---- AC-E04-03 (2) ----

    [Fact]
    public async Task An_early_arrival_gives_a_delay_of_zero_and_keeps_the_scheduled_departure()
    {
        var response = await ReportAt(Stops[1], Local(8, 28), "Arrived"); // 2 minutes before the scheduled 08:30

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var status = await StatusAsync();
        Assert.Equal(0, status.GetProperty("delayMinutes").GetInt32());
        Assert.Equal("OnTime", status.GetProperty("statusKey").GetString());
        Assert.Equal(Local(8, 31), Time(StopAt(status, 2), "estimatedDeparture"));
    }

    // ---- AC-E04-01 (1), rules R2 and R3 ----

    [Fact]
    public async Task A_repeat_of_an_arrival_is_acknowledged_once_by_id_and_by_content()
    {
        var id = Guid.NewGuid();
        var first = await ReportAt(Stops[1], Local(8, 40), "Arrived", id);
        var sameId = await ReportAt(Stops[1], Local(8, 40), "Arrived", id);
        var otherId = await ReportAt(Stops[1], Local(8, 40), "Arrived", Guid.NewGuid());

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, sameId.StatusCode);
        Assert.Equal(HttpStatusCode.OK, otherId.StatusCode);
        Assert.Equal(1, await EventCountAsync());
    }

    [Fact]
    public async Task The_same_id_with_another_type_is_refused()
    {
        var id = Guid.NewGuid();
        await ReportAt(Stops[1], Local(8, 40), "Arrived", id);

        var response = await ReportAt(Stops[1], Local(8, 40), "Departed", id);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("client_event_id_reused", (await ReadAsync(response)).GetProperty("reason").GetString());
    }

    [Fact]
    public async Task A_different_time_for_the_same_arrival_is_a_conflict()
    {
        await ReportAt(Stops[1], Local(8, 40), "Arrived");

        var response = await ReportAt(Stops[1], Local(8, 42), "Arrived");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("stop_has_different_time", (await ReadAsync(response)).GetProperty("reason").GetString());
    }

    [Fact]
    public async Task An_arrival_after_the_departure_of_the_same_stop_breaks_the_order()
    {
        await ReportAt(Stops[1], Local(8, 41), "Departed");

        var response = await ReportAt(Stops[1], Local(8, 45), "Arrived");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("order_conflict", (await ReadAsync(response)).GetProperty("reason").GetString());
    }

    [Fact]
    public async Task An_arrival_and_a_departure_at_the_same_moment_are_not_both_accepted()
    {
        await ReportAt(Stops[1], Local(8, 40), "Arrived");

        var response = await ReportAt(Stops[1], Local(8, 40), "Departed");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("order_conflict", (await ReadAsync(response)).GetProperty("reason").GetString());
        Assert.Equal(1, await EventCountAsync());
    }

    [Fact]
    public async Task An_arrival_before_the_departure_of_the_previous_stop_breaks_the_order()
    {
        await ReportAt(Stops[0], Local(8, 28), "Departed");

        Clock.SetUtcNow(Local(8, 28));
        var response = await Report(Stops[1], Local(8, 27), eventType: "Arrived");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("order_conflict", (await ReadAsync(response)).GetProperty("reason").GetString());
    }

    [Fact]
    public async Task A_skipped_stop_is_inferred_when_a_later_arrival_is_reported()
    {
        await ReportAt(Stops[4], Local(9, 5), "Arrived");

        var status = await StatusAsync();
        Assert.Equal("Inferred", StopAt(status, 2).GetProperty("confirmation").GetString());
        Assert.Equal("Inferred", StopAt(status, 4).GetProperty("confirmation").GetString());
        Assert.Equal("Reported", StopAt(status, 5).GetProperty("confirmation").GetString());
    }

    // ---- AC-E04-02 (1) (2) ----

    [Fact]
    public async Task An_arrival_two_minutes_ahead_or_nine_hours_old_is_a_422()
    {
        Clock.SetUtcNow(Local(9, 5));
        var ahead = await Report(Stops[1], Local(9, 7), eventType: "Arrived");
        var old = await Report(Stops[1], Local(0, 30), eventType: "Arrived"); // 8 h 35 min old, but inside the operating day's assignment

        Assert.Equal(HttpStatusCode.UnprocessableEntity, ahead.StatusCode);
        Assert.Equal("time_out_of_range", (await ReadAsync(ahead)).GetProperty("reason").GetString());
        Assert.Equal(HttpStatusCode.UnprocessableEntity, old.StatusCode);
        Assert.Equal(0, await EventCountAsync());
    }

    // ---- Authorisation (FR-03) ----

    [Fact]
    public async Task Arrivals_need_a_token_the_operator_role_and_an_assignment()
    {
        Clock.SetUtcNow(Local(8, 40));
        var none = await Report(Stops[1], Local(8, 40), eventType: "Arrived", noToken: true);
        var controller = await Report(Stops[1], Local(8, 40), eventType: "Arrived", token: Issuer.Controller());
        var unassigned = await Report(Stops[1], Local(8, 40), eventType: "Arrived", token: UnassignedOperatorToken);

        Assert.Equal(HttpStatusCode.Unauthorized, none.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, controller.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, unassigned.StatusCode);
        Assert.Equal("not_assigned", (await ReadAsync(unassigned)).GetProperty("reason").GetString());
        Assert.Equal(0, await EventCountAsync());
    }
}
