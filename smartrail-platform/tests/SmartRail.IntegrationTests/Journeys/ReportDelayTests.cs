using System.Net;
using System.Text.Json;
using SmartRail.IntegrationTests.Database;
using Xunit;

namespace SmartRail.IntegrationTests.Journeys;

/// <summary>
/// Spec 06, slice A2: AC-E03-04 (1, 2, 3, 5, 6), AC-E04-01 (1), AC-E04-02 (1, 2), AC-E04-03 (4) and the rules R1, R4 to R9.
/// Train 8058: Veyangoda dep 08:20; Gampaha arr 08:30 dep 08:31; Ragama 08:42/08:43; Maradana 08:50/08:51; Colombo Fort arr 08:55.
/// </summary>
public sealed class ReportDelayTests(PostgresFixture postgres) : JourneyApiTestBase(postgres)
{
    private void AdvanceTo(DateTimeOffset at)
    {
        if (at > Clock.GetUtcNow())
        {
            Clock.SetUtcNow(at);
        }
    }

    private async Task<HttpResponseMessage> StopReport(int stopIndex, DateTimeOffset at, string type)
    {
        AdvanceTo(at);
        return await Report(Stops[stopIndex], at, eventType: type);
    }

    private async Task<HttpResponseMessage> Delay(DateTimeOffset at, string reason = "SignalIssue", int? total = 10, bool? unknown = null, Guid? id = null)
    {
        AdvanceTo(at);
        return await ReportDelay(at, reason, total, unknown, id);
    }

    // ---- AC-E03-04 (1) (2), AC-E04-03 (4) ----

    [Fact]
    public async Task A_delay_with_a_reason_and_a_total_is_recorded_and_every_later_stop_moves()
    {
        await StopReport(0, Local(8, 28), "Departed"); // +8

        var response = await Delay(Local(8, 35), "SignalIssue", total: 10);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var status = await StatusAsync();
        Assert.Equal(10, status.GetProperty("delayMinutes").GetInt32());
        Assert.Equal("Delayed", status.GetProperty("statusKey").GetString());
        Assert.True(status.GetProperty("delayKnown").GetBoolean());
        Assert.Equal(Local(8, 40), Time(StopAt(status, 2), "estimatedArrival")); // AC-E03-04 (2)
        Assert.Equal(Local(8, 52), Time(StopAt(status, 3), "estimatedArrival"));
        Assert.Equal(Local(9, 0), Time(StopAt(status, 4), "estimatedArrival"));
        Assert.Equal(Local(9, 5), Time(StopAt(status, 5), "estimatedArrival"));
        Assert.Equal("Gampaha", status.GetProperty("primaryEta").GetProperty("stationName").GetString());
        Assert.Equal(Local(8, 40), Time(status.GetProperty("primaryEta"), "time"));
        Assert.Equal("Running", await Database.ScalarAsync<string>("select status from journey"));
    }

    [Fact]
    public async Task The_total_replaces_the_measured_delay_it_is_not_added_to_it()
    {
        await StopReport(0, Local(8, 28), "Departed"); // measured +8

        await Delay(Local(8, 35), total: 10);

        Assert.Equal(10, (await StatusAsync()).GetProperty("delayMinutes").GetInt32()); // not 18 (D-07, EC-37)
    }

    [Fact]
    public async Task The_event_is_stored_with_its_reason_and_total_but_no_stop()
    {
        await Delay(Local(8, 28), "TechnicalFault", total: 15);

        Assert.Equal("DelayReported", await Database.ScalarAsync<string>("select event_type from train_event"));
        Assert.Equal(0, await Database.ScalarAsync<long>("select count(*) from train_event where service_stop_id is not null"));
        Assert.Equal("TechnicalFault", await Database.ScalarAsync<string>("select payload->>'reason' from train_event"));
        Assert.Equal("15", await Database.ScalarAsync<string>("select payload->>'totalDelayMinutes' from train_event"));
    }

    [Fact]
    public async Task A_delay_before_any_departure_is_accepted_the_journey_stays_Scheduled_and_the_status_is_Delayed()
    {
        var response = await Delay(Local(8, 28), total: 12);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("Scheduled", await Database.ScalarAsync<string>("select status from journey"));
        var status = await StatusAsync();
        Assert.Equal("Delayed", status.GetProperty("statusKey").GetString());
        Assert.Equal(12, status.GetProperty("delayMinutes").GetInt32());
        Assert.Equal(Local(8, 42), Time(StopAt(status, 2), "estimatedArrival"));
    }

    // ---- AC-E03-04 (3): the reason is never published ----

    [Fact]
    public async Task No_public_read_returns_the_reason()
    {
        await StopReport(0, Local(8, 28), "Departed");
        await Delay(Local(8, 35), "TechnicalFault", total: 10);

        foreach (var path in new[]
        {
            $"/api/v1/journeys/{JourneyId}/status",
            "/api/v1/trains/8058/status",
            "/api/v1/lines/MAIN/status",
            "/api/v1/trains?from=GPH&to=CF",
            "/api/v1/stations",
        })
        {
            var body = await (await Get(path)).Content.ReadAsStringAsync();
            Assert.DoesNotContain("TechnicalFault", body);
            Assert.DoesNotContain("technicalfault", body, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("\"reason\"", body);
        }

        var operatorJourney = await (await Get("/api/v1/operator/journey", OperatorToken)).Content.ReadAsStringAsync();
        Assert.DoesNotContain("TechnicalFault", operatorJourney);
    }

    [Fact]
    public async Task The_response_to_the_report_itself_does_not_echo_the_reason()
    {
        var response = await Delay(Local(8, 28), "Obstruction", total: 10);

        Assert.DoesNotContain("Obstruction", await response.Content.ReadAsStringAsync());
    }

    // ---- AC-E03-04 (5): Unknown ----

    [Fact]
    public async Task An_unknown_delay_gives_delayKnown_false_a_Delayed_status_and_no_estimates()
    {
        await StopReport(0, Local(8, 28), "Departed"); // +8

        var response = await Delay(Local(8, 35), "Weather", total: null, unknown: true);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var status = await StatusAsync();
        Assert.False(status.GetProperty("delayKnown").GetBoolean());
        Assert.Equal("Delayed", status.GetProperty("statusKey").GetString());
        Assert.Equal("Warning", status.GetProperty("statusGroup").GetString());
        Assert.Equal(JsonValueKind.Null, status.GetProperty("primaryEta").ValueKind);
        Assert.Equal(8, status.GetProperty("delayMinutes").GetInt32()); // the number is kept
        foreach (var sequence in new[] { 2, 3, 4, 5 })
        {
            Assert.Equal(JsonValueKind.Null, StopAt(status, sequence).GetProperty("estimatedArrival").ValueKind);
        }

        // The train's position is still shown: only the ETA is withheld.
        Assert.Equal("Veyangoda", status.GetProperty("lastKnownPosition").GetProperty("stationName").GetString());
    }

    [Fact]
    public async Task A_later_measured_report_makes_the_delay_known_again()
    {
        await StopReport(0, Local(8, 28), "Departed");
        await Delay(Local(8, 35), total: null, unknown: true);

        await StopReport(1, Local(8, 41), "Departed"); // +10 measured

        var status = await StatusAsync();
        Assert.True(status.GetProperty("delayKnown").GetBoolean());
        Assert.Equal(10, status.GetProperty("delayMinutes").GetInt32());
        Assert.Equal(Local(8, 52), Time(StopAt(status, 3), "estimatedArrival"));
    }

    [Fact]
    public async Task A_later_numeric_total_makes_the_delay_known_again()
    {
        await StopReport(0, Local(8, 28), "Departed");
        await Delay(Local(8, 35), total: null, unknown: true);

        await Delay(Local(8, 38), total: 12);

        var status = await StatusAsync();
        Assert.True(status.GetProperty("delayKnown").GetBoolean());
        Assert.Equal(12, status.GetProperty("delayMinutes").GetInt32());
    }

    // ---- AC-E03-04 (6): only a bigger total is accepted ----

    [Theory]
    [InlineData(8)]
    [InlineData(5)]
    public async Task A_total_not_greater_than_the_running_delay_is_a_409(int total)
    {
        await StopReport(0, Local(8, 28), "Departed"); // +8

        var response = await Delay(Local(8, 35), total: total);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("delay_not_greater", (await ReadAsync(response)).GetProperty("reason").GetString());
        Assert.Equal(1, await EventCountAsync());
    }

    [Fact]
    public async Task A_total_greater_than_the_running_delay_is_accepted()
    {
        await StopReport(0, Local(8, 28), "Departed"); // +8

        Assert.Equal(HttpStatusCode.Created, (await Delay(Local(8, 35), total: 10)).StatusCode);
    }

    [Fact]
    public async Task After_unknown_a_total_equal_to_the_last_number_is_accepted_but_a_lower_one_is_not()
    {
        await StopReport(0, Local(8, 28), "Departed"); // +8
        await Delay(Local(8, 35), total: null, unknown: true);

        var lower = await Delay(Local(8, 36), total: 5);
        var equal = await Delay(Local(8, 37), total: 8);

        Assert.Equal(HttpStatusCode.Conflict, lower.StatusCode);
        Assert.Equal(HttpStatusCode.Created, equal.StatusCode);
    }

    [Fact]
    public async Task The_first_delay_of_a_journey_only_needs_to_be_above_zero()
    {
        Assert.Equal(HttpStatusCode.Created, (await Delay(Local(8, 28), total: 1)).StatusCode);
    }

    // ---- shape (section 4) ----

    [Theory]
    [InlineData(null, 10, null)]               // no reason
    [InlineData("Other", 10, null)]            // free text is not built
    [InlineData("signalissue", 10, null)]      // codes are exact
    [InlineData("SignalIssue", null, null)]    // neither a total nor unknown
    [InlineData("SignalIssue", 10, true)]      // both
    [InlineData("SignalIssue", 0, null)]
    [InlineData("SignalIssue", 241, null)]
    public async Task A_badly_shaped_delay_report_is_a_422_and_stores_nothing(string? reason, int? total, bool? unknown)
    {
        var response = await ReportDelay(Local(8, 28), reason, total, unknown);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("invalid_request", (await ReadAsync(response)).GetProperty("reason").GetString());
        Assert.Equal(0, await EventCountAsync());
    }

    [Fact]
    public async Task A_delay_report_that_names_a_stop_is_a_422()
    {
        var response = await ReportDelay(Local(8, 28), stopId: Stops[1]);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task A_departure_that_carries_delay_fields_is_a_422()
    {
        var response = await Client.SendAsync(new HttpRequestMessage(HttpMethod.Post, $"/api/v1/journeys/{JourneyId}/events")
        {
            Headers = { Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", OperatorToken) },
            Content = System.Net.Http.Json.JsonContent.Create(new
            {
                clientEventId = Guid.NewGuid(),
                eventType = "Departed",
                serviceStopId = Stops[0],
                occurredAt = Local(8, 28),
                reason = "SignalIssue",
            }),
        });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal(0, await EventCountAsync());
    }

    // ---- AC-E04-01 (1): repeats ----

    [Fact]
    public async Task A_repeat_by_id_or_by_content_is_acknowledged_once()
    {
        var id = Guid.NewGuid();
        var first = await Delay(Local(8, 28), total: 10, id: id);
        var sameId = await Delay(Local(8, 28), total: 10, id: id);
        var otherId = await Delay(Local(8, 28), total: 10, id: Guid.NewGuid());

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, sameId.StatusCode);
        Assert.Equal(HttpStatusCode.OK, otherId.StatusCode);
        Assert.Equal(1, await EventCountAsync());
    }

    [Fact]
    public async Task The_same_id_with_different_content_is_refused()
    {
        var id = Guid.NewGuid();
        await Delay(Local(8, 28), "SignalIssue", total: 10, id: id);

        var response = await Delay(Local(8, 28), "Weather", total: 10, id: id);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("client_event_id_reused", (await ReadAsync(response)).GetProperty("reason").GetString());
    }

    [Fact]
    public async Task A_different_delay_at_the_same_moment_is_an_order_conflict()
    {
        await Delay(Local(8, 28), total: 10);

        var response = await Delay(Local(8, 28), total: 15);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("order_conflict", (await ReadAsync(response)).GetProperty("reason").GetString());
        Assert.Equal(1, await EventCountAsync());
    }

    [Fact]
    public async Task A_delay_at_the_moment_of_a_departure_is_an_order_conflict_both_ways()
    {
        await StopReport(0, Local(8, 28), "Departed");
        var delay = await Delay(Local(8, 28), total: 12);

        Assert.Equal(HttpStatusCode.Conflict, delay.StatusCode);
        Assert.Equal("order_conflict", (await ReadAsync(delay)).GetProperty("reason").GetString());

        await Delay(Local(8, 40), total: 15);
        var departure = await StopReport(1, Local(8, 40), "Departed");
        Assert.Equal(HttpStatusCode.Conflict, departure.StatusCode);
        Assert.Equal("order_conflict", (await ReadAsync(departure)).GetProperty("reason").GetString());
    }

    // ---- AC-E04-02 (1) (2), AC-E04-02 (4) ----

    [Fact]
    public async Task A_delay_two_minutes_ahead_or_older_than_eight_hours_is_a_422()
    {
        Clock.SetUtcNow(Local(9, 5));
        var ahead = await ReportDelay(Local(9, 7));
        var old = await ReportDelay(Local(0, 30)); // 8 h 35 min old, inside the operating day's assignment

        Assert.Equal(HttpStatusCode.UnprocessableEntity, ahead.StatusCode);
        Assert.Equal("time_out_of_range", (await ReadAsync(ahead)).GetProperty("reason").GetString());
        Assert.Equal(HttpStatusCode.UnprocessableEntity, old.StatusCode);
        Assert.Equal(0, await EventCountAsync());
    }

    [Fact]
    public async Task A_delay_that_arrives_late_is_judged_by_when_it_happened()
    {
        await StopReport(0, Local(8, 28), "Departed"); // +8
        await StopReport(1, Local(8, 41), "Departed"); // +10, measured
        // The operator said "total 9" at 08:35, while +8 was running; it only reaches the server now.
        var late = await ReportDelay(Local(8, 35), total: 9);

        Assert.Equal(HttpStatusCode.Created, late.StatusCode); // 9 > 8, judged against what was in force at 08:35
        // The measurement at 08:41 is later, so it still wins.
        Assert.Equal(10, (await StatusAsync()).GetProperty("delayMinutes").GetInt32());
    }

    // ---- state, authorisation ----

    [Fact]
    public async Task A_completed_journey_refuses_a_delay_report()
    {
        await StopReport(4, Local(9, 5), "Arrived");

        var response = await Delay(Local(9, 5).AddSeconds(30), total: 30);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("journey_not_reportable", (await ReadAsync(response)).GetProperty("reason").GetString());
    }

    [Fact]
    public async Task Delay_reports_need_a_token_the_operator_role_and_an_assignment()
    {
        var none = await ReportDelay(Local(8, 28), noToken: true);
        var controller = await ReportDelay(Local(8, 28), token: Issuer.Controller());
        var unassigned = await ReportDelay(Local(8, 28), token: UnassignedOperatorToken);

        Assert.Equal(HttpStatusCode.Unauthorized, none.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, controller.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, unassigned.StatusCode);
        Assert.Equal(0, await EventCountAsync());
    }
}

/// <summary>A delay reported before the train has left its first stop (the clock starts at 08:15, before the 08:20 departure).</summary>
public sealed class ReportDelayBeforeDepartureTests(PostgresFixture postgres) : JourneyApiTestBase(postgres)
{
    protected override DateTimeOffset StartClock => Local(8, 15);

    [Fact]
    public async Task The_passenger_search_shows_a_delay_reported_before_the_first_departure()
    {
        var response = await ReportDelay(Local(8, 15), total: 12);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var body = await ReadAsync(await Get("/api/v1/trains?from=VYG&to=CF"));

        var train = body.GetProperty("trains").EnumerateArray().Single(t => t.GetProperty("trainNumber").GetString() == "8058");
        Assert.Equal(12, train.GetProperty("delayMinutes").GetInt32());
        Assert.Equal("Delayed", train.GetProperty("statusKey").GetString());
    }
}