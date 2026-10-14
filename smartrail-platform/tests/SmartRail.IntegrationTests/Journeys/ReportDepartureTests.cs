using System.Net;
using System.Text.Json;
using SmartRail.IntegrationTests.Database;
using Xunit;

namespace SmartRail.IntegrationTests.Journeys;

/// <summary>AC-E03-02 (1, 2, 4, 6, 8), AC-E04-01 (1), AC-E04-02 (1, 2) and the rules R1 to R13 of the spec.</summary>
public sealed class ReportDepartureTests(PostgresFixture postgres) : JourneyApiTestBase(postgres)
{
    [Fact]
    public async Task Happy_path_stores_the_event_and_the_snapshot_shows_the_delay()
    {
        var response = await Report(Stops[0], Local(8, 28));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode); // also proves a test token is valid while the clock is faked
        var body = await ReadAsync(response);
        Assert.Equal("Delivered", body.GetProperty("status").GetString());
        Assert.Equal(1, body.GetProperty("journeyVersion").GetInt32());
        Assert.Equal(1, await EventCountAsync());

        var status = await StatusAsync();
        Assert.Equal(1, status.GetProperty("version").GetInt32());
        Assert.Equal(8, status.GetProperty("delayMinutes").GetInt32());
        Assert.Equal("Warning", status.GetProperty("statusGroup").GetString());
        Assert.Equal("Delayed", status.GetProperty("statusKey").GetString());
        Assert.Equal("Reported", StopAt(status, 1).GetProperty("confirmation").GetString());
        Assert.Equal(Local(8, 28), Time(StopAt(status, 1), "actualDeparture"));
        Assert.Equal(Local(8, 38), Time(StopAt(status, 2), "estimatedArrival"));
        Assert.Equal(Local(8, 50), Time(StopAt(status, 3), "estimatedArrival"));
        Assert.Equal(Local(8, 58), Time(StopAt(status, 4), "estimatedArrival"));
        Assert.Equal(Local(9, 3), Time(StopAt(status, 5), "estimatedArrival"));

        var eta = status.GetProperty("primaryEta");
        Assert.Equal("Gampaha", eta.GetProperty("stationName").GetString());
        Assert.Equal(Local(8, 38), Time(eta, "time"));
        Assert.Equal("Veyangoda", status.GetProperty("lastKnownPosition").GetProperty("stationName").GetString());

        Assert.Equal("Running", await Database.ScalarAsync<string>("select status from journey"));
        var created = await Database.ScalarAsync<string>("select created_by from train_event");
        Assert.Equal(NimalSubject, created);
        Assert.Equal("Operator", await Database.ScalarAsync<string>("select source from train_event"));
    }

    [Fact]
    public async Task An_exact_repeat_returns_the_original_result_and_stores_nothing_new()
    {
        var id = Guid.NewGuid();
        var first = await Report(Stops[0], Local(8, 28), clientEventId: id);
        var second = await Report(Stops[0], Local(8, 28), clientEventId: id);

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Equal((await ReadAsync(first)).GetProperty("eventId").GetGuid(), (await ReadAsync(second)).GetProperty("eventId").GetGuid());
        Assert.Equal(1, await EventCountAsync());
        Assert.Equal(1, (await StatusAsync()).GetProperty("version").GetInt32());
    }

    [Fact]
    public async Task A_repeat_with_a_time_finer_than_a_microsecond_is_still_a_duplicate()
    {
        // Found by the end-to-end run on the real stack: Postgres stores microseconds, .NET sends 100 ns ticks.
        var id = Guid.NewGuid();
        var precise = Local(8, 28).AddTicks(7); // 700 ns past the minute

        var first = await Report(Stops[0], precise, clientEventId: id);
        var second = await Report(Stops[0], precise, clientEventId: id);
        var otherId = await Report(Stops[0], precise, clientEventId: Guid.NewGuid());

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Equal(HttpStatusCode.OK, otherId.StatusCode);
        Assert.Equal(1, await EventCountAsync());
    }

    [Fact]
    public async Task A_different_client_event_id_for_the_same_stop_and_time_returns_the_original_event()
    {
        var first = await Report(Stops[0], Local(8, 28));
        var second = await Report(Stops[0], Local(8, 28), clientEventId: Guid.NewGuid());

        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Equal((await ReadAsync(first)).GetProperty("eventId").GetGuid(), (await ReadAsync(second)).GetProperty("eventId").GetGuid());
        Assert.Equal(1, await EventCountAsync());
    }

    [Fact]
    public async Task The_same_client_event_id_with_a_different_stop_or_time_is_a_409_and_stores_nothing()
    {
        var id = Guid.NewGuid();
        await Report(Stops[0], Local(8, 28), clientEventId: id);

        var differentTime = await Report(Stops[0], Local(8, 29), clientEventId: id);
        var differentStop = await Report(Stops[1], Local(8, 33), clientEventId: id);

        Assert.Equal(HttpStatusCode.Conflict, differentTime.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, differentStop.StatusCode);
        Assert.Equal("client_event_id_reused", (await ReadAsync(differentTime)).GetProperty("reason").GetString());
        Assert.Equal(1, await EventCountAsync());
    }

    [Fact]
    public async Task The_same_stop_with_a_different_time_is_a_409()
    {
        await Report(Stops[0], Local(8, 28));

        var response = await Report(Stops[0], Local(8, 29));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("stop_has_different_time", (await ReadAsync(response)).GetProperty("reason").GetString());
        Assert.Equal(1, await EventCountAsync());
    }

    [Fact]
    public async Task An_equal_time_at_a_different_stop_is_a_409()
    {
        await Report(Stops[0], Local(8, 28));

        var response = await Report(Stops[1], Local(8, 28));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("order_conflict", (await ReadAsync(response)).GetProperty("reason").GetString());
    }

    [Fact]
    public async Task A_later_stop_with_an_earlier_time_is_a_409()
    {
        Clock.SetUtcNow(Local(8, 40));
        await Report(Stops[1], Local(8, 33));

        var response = await Report(Stops[2], Local(8, 32));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(1, await EventCountAsync());
    }

    [Fact]
    public async Task A_forward_skip_marks_the_stops_in_between_inferred_and_raises_no_error()
    {
        Clock.SetUtcNow(Local(8, 53));

        var response = await Report(Stops[2], Local(8, 52)); // Ragama, with no Veyangoda or Gampaha report

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var status = await StatusAsync();
        Assert.Equal("Inferred", StopAt(status, 1).GetProperty("confirmation").GetString());
        Assert.Equal("Inferred", StopAt(status, 2).GetProperty("confirmation").GetString());
        Assert.Equal("Reported", StopAt(status, 3).GetProperty("confirmation").GetString());
        Assert.Equal(JsonValueKind.Null, StopAt(status, 2).GetProperty("estimatedArrival").ValueKind);
        Assert.Equal(9, status.GetProperty("delayMinutes").GetInt32());
    }

    [Fact]
    public async Task A_late_report_gives_the_same_state_as_reporting_in_order()
    {
        Clock.SetUtcNow(Local(8, 53));
        await Report(Stops[2], Local(8, 52)); // Ragama arrives first
        var late = await Report(Stops[0], Local(8, 28)); // Veyangoda from an offline sync

        Assert.Equal(HttpStatusCode.Created, late.StatusCode);
        var status = await StatusAsync();
        Assert.Equal("Reported", StopAt(status, 1).GetProperty("confirmation").GetString());
        Assert.Equal(Local(8, 28), Time(StopAt(status, 1), "actualDeparture"));
        Assert.Equal("Inferred", StopAt(status, 2).GetProperty("confirmation").GetString());
        Assert.Equal("Reported", StopAt(status, 3).GetProperty("confirmation").GetString());
        Assert.Equal(9, status.GetProperty("delayMinutes").GetInt32());
        Assert.Equal(2, status.GetProperty("version").GetInt32());
    }

    [Fact]
    public async Task An_early_departure_is_accepted_with_delay_zero()
    {
        var response = await Report(Stops[0], Local(8, 15));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var status = await StatusAsync();
        Assert.Equal(0, status.GetProperty("delayMinutes").GetInt32());
        Assert.Equal("OnTime", status.GetProperty("statusKey").GetString());
        Assert.Equal("Normal", status.GetProperty("statusGroup").GetString());
        Assert.Equal(Local(8, 30), Time(StopAt(status, 2), "estimatedArrival"));
    }

    [Fact]
    public async Task A_departure_two_minutes_late_is_still_on_time_and_three_is_delayed()
    {
        await Report(Stops[0], Local(8, 22));
        Assert.Equal("OnTime", (await StatusAsync()).GetProperty("statusKey").GetString());

        Clock.SetUtcNow(Local(8, 35));
        await Report(Stops[1], Local(8, 34)); // 8:31 scheduled, so 3 minutes
        Assert.Equal("Delayed", (await StatusAsync()).GetProperty("statusKey").GetString());
    }

    [Fact]
    public async Task The_first_report_moves_the_journey_from_scheduled_to_running()
    {
        Assert.Equal("Scheduled", await Database.ScalarAsync<string>("select status from journey"));

        await Report(Stops[0], Local(8, 28));

        Assert.Equal("Running", await Database.ScalarAsync<string>("select status from journey"));
    }

    // ---- who may report, and when ----

    [Fact]
    public async Task No_token_is_401_and_a_controller_token_is_403()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await Report(Stops[0], Local(8, 28), noToken: true)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Report(Stops[0], Local(8, 28), token: Issuer.Controller())).StatusCode);
        Assert.Equal(0, await EventCountAsync());
    }

    [Fact]
    public async Task An_operator_not_assigned_to_the_journey_gets_403_and_nothing_is_stored()
    {
        var response = await Report(Stops[0], Local(8, 28), token: UnassignedOperatorToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("not_assigned", (await ReadAsync(response)).GetProperty("reason").GetString());
        Assert.Equal(0, await EventCountAsync());
        Assert.Equal("Scheduled", await Database.ScalarAsync<string>("select status from journey"));
    }

    [Fact]
    public async Task The_assignment_is_judged_when_the_event_happened_not_when_it_arrived()
    {
        // Nimal is assigned only from 08:25 to 08:40.
        await Database.ExecuteAsync("delete from operator_assignment");
        await Database.ExecuteAsync(
            $"insert into operator_assignment (id, journey_id, operator_user_id, valid_from, valid_to, assigned_by) " +
            $"values (gen_random_uuid(), '{JourneyId}', '{NimalSubject}', '{Local(8, 25):O}', '{Local(8, 40):O}', 'test')");

        Clock.SetUtcNow(Local(8, 45)); // the report arrives after the assignment ended
        var happenedWhileAssigned = await Report(Stops[0], Local(8, 28));
        var happenedAfter = await Report(Stops[1], Local(8, 44));

        Assert.Equal(HttpStatusCode.Created, happenedWhileAssigned.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, happenedAfter.StatusCode);
    }

    [Fact]
    public async Task A_report_from_the_future_or_from_nine_hours_ago_is_a_422()
    {
        var future = await Report(Stops[0], Local(8, 30)); // clock is 08:28, so two minutes ahead

        Clock.SetUtcNow(Local(18, 0)); // still inside Nimal's assignment for the day, so only the age is wrong
        var old = await Report(Stops[1], Local(8, 28)); // nine and a half hours old

        Assert.Equal(HttpStatusCode.UnprocessableEntity, future.StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, old.StatusCode);
        Assert.Equal("time_out_of_range", (await ReadAsync(future)).GetProperty("reason").GetString());
        Assert.Equal(0, await EventCountAsync());
    }

    [Fact]
    public async Task A_late_retry_of_an_accepted_report_still_gets_its_original_result()
    {
        var id = Guid.NewGuid();
        await Report(Stops[0], Local(8, 28), clientEventId: id);

        Clock.SetUtcNow(Local(8, 28).AddHours(9)); // the original is now older than the 8 hour limit
        var retry = await Report(Stops[0], Local(8, 28), clientEventId: id);

        Assert.Equal(HttpStatusCode.OK, retry.StatusCode);
        Assert.Equal(1, await EventCountAsync());
    }

    [Theory]
    [InlineData("Unassigned")]
    [InlineData("Completed")]
    public async Task A_journey_that_is_unassigned_or_completed_refuses_reports_with_409(string status)
    {
        await Database.ExecuteAsync($"update journey set status = '{status}'");

        var response = await Report(Stops[0], Local(8, 28));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("journey_not_reportable", (await ReadAsync(response)).GetProperty("reason").GetString());
        Assert.Equal(0, await EventCountAsync());
    }

    // ---- request shape ----

    [Fact]
    public async Task A_departure_from_the_final_stop_is_a_422()
    {
        var response = await Report(Stops[4], Local(8, 28));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("final_stop", (await ReadAsync(response)).GetProperty("reason").GetString());
    }

    [Fact]
    public async Task A_stop_that_is_not_part_of_the_journey_is_a_422()
    {
        var response = await Report(Guid.NewGuid(), Local(8, 28));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("stop_not_in_service", (await ReadAsync(response)).GetProperty("reason").GetString());
    }

    [Theory]
    [InlineData("Arrived")]
    [InlineData("departed")]
    [InlineData(null)]
    public async Task An_event_type_other_than_departed_is_a_422(string? eventType)
    {
        var response = await Report(Stops[0], Local(8, 28), eventType: eventType);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("invalid_request", (await ReadAsync(response)).GetProperty("reason").GetString());
    }

    [Fact]
    public async Task A_missing_client_event_id_or_stop_is_a_422()
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/journeys/{JourneyId}/events")
        {
            Content = new StringContent("""{"eventType":"Departed"}""", System.Text.Encoding.UTF8, "application/json"),
        };
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", OperatorToken);

        var response = await Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task An_unknown_journey_is_a_404()
    {
        var response = await Report(Stops[0], Local(8, 28), journeyId: Guid.NewGuid());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ---- concurrency ----

    [Fact]
    public async Task Simultaneous_reports_are_recorded_once_each_and_the_version_never_skips()
    {
        Clock.SetUtcNow(Local(8, 45));

        var responses = await Task.WhenAll(
            Report(Stops[0], Local(8, 28)),
            Report(Stops[1], Local(8, 33)),
            Report(Stops[2], Local(8, 44)));

        var created = responses.Count(r => r.StatusCode == HttpStatusCode.Created);
        Assert.All(responses, r => Assert.Contains(r.StatusCode, new[] { HttpStatusCode.Created, HttpStatusCode.Conflict }));
        Assert.True(created >= 1);
        Assert.Equal(created, await EventCountAsync());
        Assert.Equal(created, await Database.ScalarAsync<int>("select version from journey"));
        Assert.Equal(created, await Database.ScalarAsync<long>("select count(distinct journey_version_after) from train_event"));
    }

    [Fact]
    public async Task Two_simultaneous_copies_of_the_same_report_store_one_event()
    {
        var id = Guid.NewGuid();

        var responses = await Task.WhenAll(
            Report(Stops[0], Local(8, 28), clientEventId: id),
            Report(Stops[0], Local(8, 28), clientEventId: id));

        Assert.Equal(1, await EventCountAsync());
        Assert.Contains(responses, r => r.StatusCode == HttpStatusCode.Created);
        Assert.All(responses, r => Assert.Contains(r.StatusCode, new[] { HttpStatusCode.Created, HttpStatusCode.OK, HttpStatusCode.Conflict }));
    }
}
