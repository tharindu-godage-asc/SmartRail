using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using SmartRail.Application.Features.Timetable;
using SmartRail.Infrastructure.Seeding;
using SmartRail.IntegrationTests.Database;
using Xunit;

namespace SmartRail.IntegrationTests.Journeys;

/// <summary>
/// Spec 17 A1: assign, crew change, end, lists and the lost-device rule. The clock is Tuesday 6 Oct 2026 08:28. Seeded: 8058 with Nimal assigned
/// for the whole day; the generator adds Unassigned journeys for 8061 (07:50 to 08:30) and the daily night train 8075. Operator 2 is the
/// second operator of the demo realm.
/// </summary>
public sealed class AssignmentTests(PostgresFixture postgres) : JourneyApiTestBase(postgres)
{
    private const string Operator2 = "d0d0d0d0-0000-4000-8000-000000000002";

    private string ControllerToken => Issuer.Create(roles: ["controller"]);

    private string Operator2Token => UnassignedOperatorToken;

    private async Task<HttpResponseMessage> Post(string path, object body, string? token = null, bool noToken = false)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(body) };
        if (!noToken)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token ?? ControllerToken);
        }

        return await Client.SendAsync(request);
    }

    private async Task GenerateJourneys()
    {
        await using var scope = Api.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<JourneyGenerationHandler>().RunAsync(CancellationToken.None);
    }

    private Task<Guid> JourneyOf(string number, string date = "2026-10-06") => Database.ScalarAsync<Guid>(
        $"select j.id from journey j join train_service s on s.id = j.service_id where s.train_number = '{number}' and j.operating_date = date '{date}'");

    private Task<HttpResponseMessage> Assign(Guid journey, string operatorId, DateTimeOffset? from = null, DateTimeOffset? to = null, bool? acknowledge = null, string? token = null) =>
        Post($"/api/v1/journeys/{journey}/assignments", new { operatorUserId = operatorId, validFrom = from, validTo = to, acknowledgeOverlap = acknowledge }, token);

    private Task<HttpResponseMessage> End(Guid journey, Guid assignment, string? reason = "Other", string? note = null, string? token = null) =>
        Post($"/api/v1/journeys/{journey}/assignments/{assignment}/end", new { reason, note }, token);

    private static async Task<JsonElement> Body(HttpResponseMessage response) => await ReadAsync(response);

    private async Task<Guid> AssignmentOfNimal() => await Database.ScalarAsync<Guid>($"select id from operator_assignment where journey_id = '{JourneyId}'");

    private Task<string?> Scalar(string sql) => Database.ScalarAsync<string>(sql);

    // ---- who may call ----

    [Fact]
    public async Task No_token_is_401_and_operator_caterer_tokens_are_403_on_every_route()
    {
        var gets = new[] { "/api/v1/assignments/journeys", "/api/v1/assignments/operators", $"/api/v1/journeys/{JourneyId}/assignments" };
        foreach (var path in gets)
        {
            Assert.Equal(HttpStatusCode.Unauthorized, (await Get(path)).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await Get(path, OperatorToken)).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await Get(path, Issuer.Create(roles: ["caterer"]))).StatusCode);
        }

        var posts = new[] { $"/api/v1/journeys/{JourneyId}/assignments", $"/api/v1/journeys/{JourneyId}/assignments/{Guid.NewGuid()}/end" };
        foreach (var path in posts)
        {
            Assert.Equal(HttpStatusCode.Unauthorized, (await Post(path, new { }, noToken: true)).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await Post(path, new { }, OperatorToken)).StatusCode);
        }
    }

    // ---- lists ----

    [Fact]
    public async Task The_unassigned_list_has_the_journeys_nobody_covers_and_all_shows_the_operator_by_name()
    {
        await GenerateJourneys();

        var unassigned = (await Body(await Get("/api/v1/assignments/journeys", ControllerToken))).EnumerateArray().ToList();
        var all = (await Body(await Get("/api/v1/assignments/journeys?filter=all", ControllerToken))).EnumerateArray().ToList();

        var numbers = unassigned.Select(j => j.GetProperty("trainNumber").GetString()).ToList();
        Assert.Contains("8061", numbers);
        Assert.Contains("8075", numbers);
        Assert.DoesNotContain("8058", numbers); // Nimal covers it
        var seeded = all.Single(j => j.GetProperty("trainNumber").GetString() == "8058");
        Assert.Equal("Nimal Perera", seeded.GetProperty("operator").GetProperty("name").GetString());
        var row = unassigned.Single(j => j.GetProperty("trainNumber").GetString() == "8061");
        Assert.Equal("Veyangoda", row.GetProperty("from").GetString());
        Assert.Equal("Colombo Fort", row.GetProperty("to").GetString());
        Assert.Equal(JsonValueKind.Null, row.GetProperty("operator").ValueKind);
        Assert.Equal("Unassigned", row.GetProperty("status").GetString());
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await Get("/api/v1/assignments/journeys?date=soon", ControllerToken)).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await Get("/api/v1/assignments/journeys?filter=some", ControllerToken)).StatusCode);
    }

    [Fact]
    public async Task The_operator_list_marks_who_is_free_and_who_is_busy_with_what()
    {
        await GenerateJourneys();
        var journey = await JourneyOf("8061");

        var free = (await Body(await Get($"/api/v1/assignments/operators?journeyId={journey}", ControllerToken))).EnumerateArray().ToList();
        // Nimal covers 8058 all day, which overlaps 8061 (07:50 to 08:30).
        var nimal = free.Single(o => o.GetProperty("userId").GetString() == NimalSubject);
        Assert.False(nimal.GetProperty("available").GetBoolean());
        Assert.Equal("8058", nimal.GetProperty("busyOn").GetProperty("trainNumber").GetString());
        Assert.True(free.Single(o => o.GetProperty("userId").GetString() == Operator2).GetProperty("available").GetBoolean());
        Assert.Equal("Second Operator", free.Single(o => o.GetProperty("userId").GetString() == Operator2).GetProperty("name").GetString());
    }

    // ---- assign ----

    [Fact]
    public async Task An_operator_without_an_assignment_cannot_report_and_after_assigning_can()
    {
        await GenerateJourneys();
        var journey = await JourneyOf("8061");
        var stop = ReferenceData.StopId(ReferenceData.Service8061, 1);
        Assert.Equal(HttpStatusCode.Forbidden, (await Report(stop, Local(8, 28), token: Operator2Token, journeyId: journey)).StatusCode); // EC-15

        var response = await Assign(journey, Operator2);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await Body(response);
        Assert.Equal("Scheduled", created.GetProperty("journeyStatus").GetString());
        Assert.Equal("Second Operator", created.GetProperty("name").GetString());
        Assert.Equal(Local(8, 28), created.GetProperty("validFrom").GetDateTimeOffset()); // the lead before departure is already past: now
        Assert.Equal(JsonValueKind.Null, created.GetProperty("validTo").ValueKind);
        Assert.Equal(HttpStatusCode.Created, (await Report(stop, Local(8, 28), token: Operator2Token, journeyId: journey)).StatusCode);
        Assert.Equal("Running", await Scalar($"select status from journey where id = '{journey}'")); // the first report starts it
    }

    [Fact]
    public async Task The_default_start_is_the_lead_time_before_the_first_departure_for_a_later_journey()
    {
        await GenerateJourneys();
        var journey = await JourneyOf("8075"); // the night train, first departure 22:10

        var created = await Body(await Assign(journey, Operator2));

        Assert.Equal(Local(21, 40), created.GetProperty("validFrom").GetDateTimeOffset()); // 30 minutes before
    }

    [Theory]
    [InlineData("nobody", "operator_not_found")]
    [InlineData("d0d0d0d0-0000-4000-8000-0000000000ff", "operator_not_found")]
    public async Task An_unknown_operator_is_refused_and_nothing_is_saved(string id, string reason)
    {
        await GenerateJourneys();
        var journey = await JourneyOf("8061");

        var response = await Assign(journey, id);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal(reason, (await Body(response)).GetProperty("reason").GetString());
        Assert.Equal(0, await Database.ScalarAsync<long>($"select count(*) from operator_assignment where journey_id = '{journey}'"));
    }

    [Fact]
    public async Task A_start_more_than_five_minutes_in_the_past_and_an_end_before_the_start_are_refused()
    {
        await GenerateJourneys();
        var journey = await JourneyOf("8061");

        var past = await Assign(journey, Operator2, from: Local(8, 20));
        var backwards = await Assign(journey, Operator2, from: Local(9, 0), to: Local(8, 59));
        var tolerated = await Assign(journey, Operator2, from: Local(8, 24)); // 4 minutes ago: clock difference

        Assert.Equal("valid_from_in_past_beyond_clock", (await Body(past)).GetProperty("reason").GetString());
        Assert.Equal("valid_to_not_after_from", (await Body(backwards)).GetProperty("reason").GetString());
        Assert.Equal(HttpStatusCode.UnprocessableEntity, past.StatusCode);
        Assert.Equal(HttpStatusCode.Created, tolerated.StatusCode);
    }

    [Fact]
    public async Task A_cancelled_or_completed_journey_cannot_be_assigned_and_an_unknown_one_is_404()
    {
        await GenerateJourneys();
        var journey = await JourneyOf("8061");
        await Scalar($"update journey set status = 'Cancelled' where id = '{journey}' returning 'x'");

        var cancelled = await Assign(journey, Operator2);
        var missing = await Assign(Guid.NewGuid(), Operator2);

        Assert.Equal(HttpStatusCode.Conflict, cancelled.StatusCode);
        Assert.Equal("journey_not_assignable", (await Body(cancelled)).GetProperty("reason").GetString());
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    [Fact]
    public async Task A_crew_change_ends_the_covering_assignment_at_the_changeover_and_there_is_never_two_at_once()
    {
        var response = await Assign(JourneyId, Operator2, from: Local(8, 28));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await Body(response);
        Assert.Equal(NimalSubject, body.GetProperty("ended").GetProperty("operatorUserId").GetString());
        Assert.Equal("CrewChange", body.GetProperty("ended").GetProperty("endReason").GetString());
        Assert.Equal(Local(8, 28), body.GetProperty("ended").GetProperty("validTo").GetDateTimeOffset());
        Assert.Equal(0, await Database.ScalarAsync<long>(
            $"select count(*) from operator_assignment a join operator_assignment b on a.journey_id = b.journey_id and a.id < b.id and a.valid_from < coalesce(b.valid_to, 'infinity') and b.valid_from < coalesce(a.valid_to, 'infinity') where a.journey_id = '{JourneyId}'"));
        Assert.True(await Database.ScalarAsync<long>($"select count(*) from operator_assignment where ended_by is not null and journey_id = '{JourneyId}'") == 1);
    }

    [Fact]
    public async Task After_a_crew_change_the_old_operator_is_refused_from_the_changeover_and_the_new_one_can_report()
    {
        await Assign(JourneyId, Operator2, from: Local(8, 28));

        var oldAfter = await Report(Stops[1], Local(8, 29), token: OperatorToken, eventType: "Departed");
        var newOne = await Report(Stops[0], Local(8, 28), token: Operator2Token);

        Assert.Equal(HttpStatusCode.Forbidden, oldAfter.StatusCode);
        Assert.Equal(HttpStatusCode.Created, newOne.StatusCode);
    }

    [Fact]
    public async Task An_earlier_report_synced_after_a_crew_change_is_accepted_when_it_happened_during_the_assignment()
    {
        await Assign(JourneyId, Operator2, from: Local(8, 28)); // Nimal's assignment ends at 08:28

        var late = await Report(Stops[0], Local(8, 20), token: OperatorToken); // made at 08:20, received at 08:28

        Assert.Equal(HttpStatusCode.Created, late.StatusCode); // EC-21, AC-E01-03 4
    }

    [Fact]
    public async Task An_assignment_that_starts_later_refuses_a_new_one_that_would_overlap_it()
    {
        await Assign(JourneyId, Operator2, from: Local(9, 0));

        var overlapping = await Assign(JourneyId, NimalSubject, from: Local(8, 50));

        Assert.Equal(HttpStatusCode.Conflict, overlapping.StatusCode);
        Assert.Equal("overlaps_later_assignment", (await Body(overlapping)).GetProperty("reason").GetString());
    }

    [Fact]
    public async Task An_operator_on_another_journey_in_that_range_cannot_be_assigned_until_that_journey_is_cancelled_or_gets_another_operator()
    {
        await GenerateJourneys();
        var first = await JourneyOf("8061"); // 07:50 to 08:30
        Assert.Equal(HttpStatusCode.Created, (await Assign(first, Operator2)).StatusCode);

        var refused = await Assign(JourneyId, Operator2, from: Local(8, 29)); // 8058 leaves 08:20: overlaps
        Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode);
        var problem = await Body(refused);
        Assert.Equal("operator_overlaps_another_journey", problem.GetProperty("reason").GetString());
        Assert.Equal("8061", problem.GetProperty("trainNumber").GetString());
        Assert.Equal(first, problem.GetProperty("journeyId").GetGuid());

        // It is a block now, not advice: acknowledging changes nothing.
        Assert.Equal(HttpStatusCode.Conflict, (await Assign(JourneyId, Operator2, from: Local(8, 29), acknowledge: true)).StatusCode);

        // The operator list says the same, for the journey's own window.
        var listed = (await Body(await Get($"/api/v1/assignments/operators?journeyId={JourneyId}", ControllerToken))).EnumerateArray()
            .First(o => o.GetProperty("userId").GetString() == Operator2);
        Assert.False(listed.GetProperty("available").GetBoolean());
        Assert.Equal("8061", listed.GetProperty("busyOn").GetProperty("trainNumber").GetString());

        // When the first journey is cancelled, the operator can be assigned here.
        await Scalar($"update journey set status = 'Cancelled' where id = '{first}' returning 'x'");
        Assert.Equal(HttpStatusCode.Created, (await Assign(JourneyId, Operator2, from: Local(8, 29))).StatusCode);
    }

    [Fact]
    public async Task An_operator_is_free_again_when_the_assignment_on_the_other_journey_is_ended()
    {
        await GenerateJourneys();
        var first = await JourneyOf("8061");
        var made = await Body(await Assign(first, Operator2));
        var assignmentId = made.GetProperty("assignmentId").GetGuid();
        Assert.Equal(HttpStatusCode.Conflict, (await Assign(JourneyId, Operator2, from: Local(8, 29))).StatusCode);

        // The controller ends Operator2's assignment on 8061 (for example a crew change): they are free for 8058.
        Assert.Equal(HttpStatusCode.OK, (await End(first, assignmentId, "CrewChange")).StatusCode);

        Assert.Equal(HttpStatusCode.Created, (await Assign(JourneyId, Operator2, from: Local(8, 29))).StatusCode);
    }

    [Fact]
    public async Task Two_controllers_assigning_at_once_never_put_two_operators_on_one_journey()
    {
        await GenerateJourneys();
        var journey = await JourneyOf("8075");

        var results = await Task.WhenAll(Assign(journey, NimalSubject, from: Local(22, 0)), Assign(journey, Operator2, from: Local(22, 0)));

        Assert.Equal([HttpStatusCode.Created, HttpStatusCode.Conflict], results.Select(r => r.StatusCode).OrderBy(c => c).ToList());
        Assert.Equal(1, await Database.ScalarAsync<long>($"select count(*) from operator_assignment where journey_id = '{journey}'"));
    }

    // ---- end ----

    [Fact]
    public async Task Ending_for_a_lost_device_refuses_later_reports_even_those_claiming_an_earlier_time_and_keeps_the_earlier_ones()
    {
        Assert.Equal(HttpStatusCode.Created, (await Report(Stops[0], Local(8, 20), token: OperatorToken)).StatusCode); // before the end
        var ended = await End(JourneyId, await AssignmentOfNimal(), "LostDevice", "Phone lost at the platform");

        Assert.Equal(HttpStatusCode.OK, ended.StatusCode);
        var late = await Report(Stops[1], Local(8, 22), token: OperatorToken); // claims 08:22, received now (08:28)
        var after = await Report(Stops[2], Local(8, 28), token: OperatorToken);

        Assert.Equal(HttpStatusCode.Forbidden, late.StatusCode);
        Assert.Equal("assignment_ended", (await Body(late)).GetProperty("reason").GetString());
        Assert.Equal(HttpStatusCode.Forbidden, after.StatusCode);
        Assert.Equal(1, await Database.ScalarAsync<long>($"select count(*) from train_event where journey_id = '{JourneyId}'")); // the earlier report stays
        Assert.Equal("LostDevice", await Scalar($"select end_reason from operator_assignment where journey_id = '{JourneyId}'"));
        Assert.Equal("Phone lost at the platform", await Scalar($"select end_note from operator_assignment where journey_id = '{JourneyId}'"));
        Assert.False(string.IsNullOrEmpty(await Scalar($"select ended_by from operator_assignment where journey_id = '{JourneyId}'")));
    }

    [Fact]
    public async Task Ending_for_another_reason_still_accepts_a_report_made_during_the_assignment_and_synced_later()
    {
        await End(JourneyId, await AssignmentOfNimal(), "Other");

        var late = await Report(Stops[0], Local(8, 20), token: OperatorToken); // received after the end, made during it

        Assert.Equal(HttpStatusCode.Created, late.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Report(Stops[1], Local(8, 29), token: OperatorToken)).StatusCode); // after the end: not assigned
    }

    [Fact]
    public async Task Ending_the_only_assignment_puts_the_journey_back_in_the_unassigned_list_and_keeps_its_stored_status()
    {
        await GenerateJourneys();
        var journey = await JourneyOf("8061");
        var assignment = (await Body(await Assign(journey, Operator2))).GetProperty("assignmentId").GetGuid();
        Assert.DoesNotContain("8061", (await Body(await Get("/api/v1/assignments/journeys", ControllerToken))).EnumerateArray().Select(j => j.GetProperty("trainNumber").GetString()));

        var ended = await End(journey, assignment, "Other");

        Assert.True(ended.IsSuccessStatusCode, await ended.Content.ReadAsStringAsync());
        Clock.Advance(TimeSpan.FromSeconds(1)); // the assignment ended in the instant it started, so its end is one microsecond later
        var list = (await Body(await Get("/api/v1/assignments/journeys", ControllerToken))).EnumerateArray().ToList();
        Assert.Contains("8061", list.Select(j => j.GetProperty("trainNumber").GetString())); // derived from the assignments
        // The stored status stays Scheduled, so a report made during the assignment and synced later is still accepted (EC-21).
        Assert.Equal("Scheduled", await Scalar($"select status from journey where id = '{journey}'"));
        var stop = ReferenceData.StopId(ReferenceData.Service8061, 1);
        Assert.Equal(HttpStatusCode.Created, (await Report(stop, Local(8, 28), token: Operator2Token, journeyId: journey)).StatusCode);
    }

    [Fact]
    public async Task A_journey_that_has_reports_stays_as_it_is_when_the_assignment_ends()
    {
        await Report(Stops[0], Local(8, 20), token: OperatorToken);

        await End(JourneyId, await AssignmentOfNimal(), "Other");

        Assert.NotEqual("Unassigned", await Scalar($"select status from journey where id = '{JourneyId}'"));
    }

    [Fact]
    public async Task Ending_twice_an_unknown_assignment_a_bad_reason_a_long_note_and_one_not_started_are_refused()
    {
        var nimal = await AssignmentOfNimal();
        Assert.Equal(HttpStatusCode.OK, (await End(JourneyId, nimal, "Other")).StatusCode);

        var twice = await End(JourneyId, nimal, "Other");
        Assert.Equal(HttpStatusCode.Conflict, twice.StatusCode);
        Assert.Equal("already_ended", (await Body(twice)).GetProperty("reason").GetString());
        Assert.Equal(HttpStatusCode.NotFound, (await End(JourneyId, Guid.NewGuid(), "Other")).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await End(JourneyId, nimal, "Because")).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await End(JourneyId, nimal, "Other", new string('x', 501))).StatusCode);

        var later = (await Body(await Assign(JourneyId, Operator2, from: Local(9, 0)))).GetProperty("assignmentId").GetGuid();
        var notStarted = await End(JourneyId, later, "Other");
        Assert.Equal(HttpStatusCode.Conflict, notStarted.StatusCode);
        Assert.Equal("not_started", (await Body(notStarted)).GetProperty("reason").GetString());
    }

    // ---- the history of one journey ----

    [Fact]
    public async Task The_assignments_of_a_journey_are_listed_oldest_first_with_names_who_assigned_and_how_they_ended()
    {
        await Assign(JourneyId, Operator2, from: Local(8, 28)); // a crew change

        var rows = (await Body(await Get($"/api/v1/journeys/{JourneyId}/assignments", ControllerToken))).EnumerateArray().ToList();

        Assert.Equal(2, rows.Count);
        Assert.Equal("Nimal Perera", rows[0].GetProperty("operatorName").GetString());
        Assert.Equal("CrewChange", rows[0].GetProperty("endReason").GetString());
        Assert.False(string.IsNullOrEmpty(rows[0].GetProperty("endedBy").GetString()));
        Assert.Equal("Second Operator", rows[1].GetProperty("operatorName").GetString());
        Assert.Equal(JsonValueKind.Null, rows[1].GetProperty("endedBy").ValueKind);
        Assert.False(string.IsNullOrEmpty(rows[1].GetProperty("assignedBy").GetString()));
        Assert.Equal(HttpStatusCode.NotFound, (await Get($"/api/v1/journeys/{Guid.NewGuid()}/assignments", ControllerToken)).StatusCode);
    }
}
