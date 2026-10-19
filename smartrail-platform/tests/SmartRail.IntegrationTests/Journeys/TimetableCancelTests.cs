using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using SmartRail.Application.Features.Timetable;
using SmartRail.IntegrationTests.Database;
using Xunit;

namespace SmartRail.IntegrationTests.Journeys;

/// <summary>
/// Spec 14 S3: cancel a service for one date or from a date onward. The clock is Tuesday 6 Oct 2026 08:28. A weekly 8072 from Wednesday 7 Oct
/// (Mon to Fri) has journeys on 7, 8, 9, 12 and 13 October; the seeded 8058 (Mon to Fri) has today's journey.
/// </summary>
public sealed class TimetableCancelTests(PostgresFixture postgres) : JourneyApiTestBase(postgres)
{
    private string ControllerToken => Issuer.Create(roles: ["controller"]);

    private static readonly object[] GoodStops =
    [
        new { stationCode = "VYG", arrival = (string?)null, departure = "17:25", platform = "1" },
        new { stationCode = "GPH", arrival = "17:35", departure = "17:36", platform = "2" },
        new { stationCode = "CF", arrival = "17:58", departure = (string?)null, platform = "1" },
    ];

    private async Task<HttpResponseMessage> Post(string path, object body, string? token = null, bool noToken = false)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(body) };
        if (!noToken)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token ?? ControllerToken);
        }

        return await Client.SendAsync(request);
    }

    private async Task<Guid> CreateWeekly(string number = "8072")
    {
        var response = await Post("/api/v1/timetable/services", new
        {
            kind = "Weekly", trainNumber = number, lineCode = "MAIN", category = "Regional", serviceType = "Express",
            days = new[] { "Mon", "Tue", "Wed", "Thu", "Fri" }, effectiveFrom = "2026-10-07", stops = GoodStops,
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await ReadAsync(response)).GetProperty("serviceIds")[0].GetGuid();
    }

    private Task<HttpResponseMessage> Cancel(Guid serviceId, string? from, string? to, string? reason = "Rolling stock not available", string? token = null) =>
        Post($"/api/v1/timetable/services/{serviceId}/cancel", new { from, to, reason }, token);

    private async Task<int> RunGeneratorAsync()
    {
        await using var scope = Api.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<JourneyGenerationHandler>().RunAsync(CancellationToken.None);
    }

    private Task<string?> StatusOn(string number, string date) => Database.ScalarAsync<string>(
        $"select coalesce((select j.status from journey j join train_service s on s.id = j.service_id where s.train_number = '{number}' and j.operating_date = date '{date}'), 'none')");

    private async Task<List<string?>> TrainsOn(string date) =>
        (await ReadAsync(await Get($"/api/v1/trains?from=VYG&to=CF&date={date}"))).GetProperty("trains").EnumerateArray()
            .Select(t => t.GetProperty("trainNumber").GetString()).ToList();

    // ---- one date ----

    [Fact]
    public async Task Cancelling_one_date_cancels_that_journey_only_and_the_passenger_search_no_longer_lists_it()
    {
        var serviceId = await CreateWeekly();

        var response = await Cancel(serviceId, "2026-10-09", "2026-10-09");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(1, (await ReadAsync(response)).GetProperty("cancelledJourneys").GetInt32());
        Assert.Equal("Cancelled", await StatusOn("8072", "2026-10-09"));
        Assert.Equal("Unassigned", await StatusOn("8072", "2026-10-08"));
        Assert.Equal("Unassigned", await StatusOn("8072", "2026-10-12"));
        Assert.DoesNotContain("8072", await TrainsOn("2026-10-09"));
        Assert.Contains("8072", await TrainsOn("2026-10-08"));
        Assert.Equal(1, await Database.ScalarAsync<long>("select count(*) from service_change_log where action = 'Cancelled'"));
        Assert.Equal(1, await Database.ScalarAsync<long>("select count(*) from train_service where train_number = '8072'")); // never deleted
    }

    [Fact]
    public async Task The_cancelled_tab_and_the_detail_show_the_cancellation_with_reason_and_who()
    {
        var serviceId = await CreateWeekly();
        await Cancel(serviceId, "2026-10-09", "2026-10-09", "  Rolling stock not available ");

        var tab = (await ReadAsync(await Get("/api/v1/timetable/services?tab=cancelled", ControllerToken))).EnumerateArray().ToList();
        var detail = await ReadAsync(await Get($"/api/v1/timetable/services/{serviceId}", ControllerToken));
        var weekly = (await ReadAsync(await Get("/api/v1/timetable/services?tab=weekly", ControllerToken))).EnumerateArray()
            .Single(r => r.GetProperty("trainNumber").GetString() == "8072");

        var row = Assert.Single(tab);
        Assert.Equal("8072", row.GetProperty("trainNumber").GetString());
        Assert.Equal("Veyangoda", row.GetProperty("fromName").GetString());
        Assert.Equal("Rolling stock not available", row.GetProperty("reason").GetString());
        Assert.False(string.IsNullOrEmpty(row.GetProperty("cancelledBy").GetString()));
        Assert.Equal("2026-10-09", row.GetProperty("from").GetString());
        Assert.Single(detail.GetProperty("cancellations").EnumerateArray());
        Assert.Single(weekly.GetProperty("cancelledRanges").EnumerateArray());
    }

    [Fact]
    public async Task A_reason_with_markup_is_kept_as_text()
    {
        var serviceId = await CreateWeekly();

        await Cancel(serviceId, "2026-10-09", "2026-10-09", "<script>alert(1)</script>");

        var row = (await ReadAsync(await Get("/api/v1/timetable/services?tab=cancelled", ControllerToken)))[0];
        Assert.Equal("<script>alert(1)</script>", row.GetProperty("reason").GetString());
    }

    // ---- from a date onward ----

    [Fact]
    public async Task Cancelling_from_a_date_onward_cancels_the_existing_journeys_and_the_generator_creates_none_later()
    {
        await RunGeneratorAsync(); // 8075 runs daily: journeys 6 .. 13 October
        var serviceId = await Database.ScalarAsync<Guid>("select id from train_service where train_number = '8075'");

        var response = await Cancel(serviceId, "2026-10-10", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(4, (await ReadAsync(response)).GetProperty("cancelledJourneys").GetInt32()); // 10, 11, 12, 13
        Assert.Equal("Cancelled", await StatusOn("8075", "2026-10-13"));
        Assert.NotEqual("Cancelled", await StatusOn("8075", "2026-10-09"));

        Clock.SetUtcNow(Local(8, 0, date: new DateOnly(2026, 10, 15)));
        await RunGeneratorAsync();
        Assert.Equal("none", await StatusOn("8075", "2026-10-15"));
        Assert.Equal("none", await StatusOn("8075", "2026-10-20"));
        Assert.Equal(0, await Database.ScalarAsync<long>(
            "select count(*) from journey j join train_service s on s.id = j.service_id where s.train_number = '8075' and j.operating_date > date '2026-10-13'"));
    }

    // ---- repeat, skip, errors ----

    [Fact]
    public async Task Repeating_the_same_cancel_changes_nothing_and_writes_no_second_row()
    {
        var serviceId = await CreateWeekly();
        await Cancel(serviceId, "2026-10-09", "2026-10-09");

        var again = await Cancel(serviceId, "2026-10-09", "2026-10-09");

        Assert.Equal(HttpStatusCode.OK, again.StatusCode);
        Assert.Equal(0, (await ReadAsync(again)).GetProperty("cancelledJourneys").GetInt32());
        Assert.Equal(1, await Database.ScalarAsync<long>("select count(*) from service_cancellation"));
        Assert.Equal(1, await Database.ScalarAsync<long>("select count(*) from service_change_log where action = 'Cancelled'"));
    }

    [Fact]
    public async Task A_range_with_no_date_the_train_runs_on_is_422_nothing_to_cancel()
    {
        var serviceId = await Database.ScalarAsync<Guid>("select id from train_service where train_number = '8058'"); // Mon to Fri

        var response = await Cancel(serviceId, "2026-10-10", "2026-10-11"); // Saturday and Sunday

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("nothing_to_cancel", (await ReadAsync(response)).GetProperty("reason").GetString());
    }

    [Theory]
    [InlineData("2026-10-09", "2026-10-09", "", "invalid_reason")]
    [InlineData("2026-10-09", "2026-10-09", null, "invalid_reason")]
    [InlineData("2026-10-05", "2026-10-09", "x", "date_in_past")]
    [InlineData(null, "2026-10-09", "x", "date_in_past")]
    [InlineData("2026-10-09", "2026-10-08", "x", "invalid_range")]
    public async Task A_bad_reason_date_or_range_is_422_and_nothing_changes(string? from, string? to, string? reason, string code)
    {
        var serviceId = await CreateWeekly();

        var response = await Cancel(serviceId, from, to, reason);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal(code, (await ReadAsync(response)).GetProperty("reason").GetString());
        Assert.Equal(0, await Database.ScalarAsync<long>("select count(*) from service_cancellation"));
    }

    [Fact]
    public async Task A_reason_of_201_characters_is_refused_and_200_is_accepted()
    {
        var serviceId = await CreateWeekly();

        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await Cancel(serviceId, "2026-10-09", "2026-10-09", new string('a', 201))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Cancel(serviceId, "2026-10-09", "2026-10-09", new string('a', 200))).StatusCode);
    }

    [Fact]
    public async Task An_unknown_service_is_404_and_the_matrix_of_tokens_holds()
    {
        var serviceId = await CreateWeekly();

        Assert.Equal(HttpStatusCode.NotFound, (await Cancel(Guid.NewGuid(), "2026-10-09", "2026-10-09")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Post($"/api/v1/timetable/services/{serviceId}/cancel", new { }, noToken: true)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Cancel(serviceId, "2026-10-09", "2026-10-09", token: OperatorToken)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Cancel(serviceId, "2026-10-09", "2026-10-09", token: Issuer.Create(roles: ["caterer"]))).StatusCode);
    }

    // ---- events block it ----

    [Fact]
    public async Task A_range_with_a_reported_journey_is_refused_whole_and_nothing_changes()
    {
        await RunGeneratorAsync();
        Assert.Equal(HttpStatusCode.Created, (await Report(Stops[0], Local(8, 28))).StatusCode);
        var serviceId = await Database.ScalarAsync<Guid>("select id from train_service where train_number = '8058'");

        var response = await Cancel(serviceId, "2026-10-06", "2026-10-09");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var problem = await ReadAsync(response);
        Assert.Equal("journey_has_events", problem.GetProperty("reason").GetString());
        Assert.Equal("2026-10-06", problem.GetProperty("date").GetString());
        Assert.Equal(0, await Database.ScalarAsync<long>("select count(*) from journey where status = 'Cancelled'"));
        Assert.Equal(0, await Database.ScalarAsync<long>("select count(*) from service_cancellation"));
    }

    // ---- what a cancelled journey means elsewhere ----

    [Fact]
    public async Task A_cancelled_journey_takes_no_operator_reports_is_not_the_operators_journey_and_is_not_found_by_passengers()
    {
        var serviceId = await Database.ScalarAsync<Guid>("select id from train_service where train_number = '8058'");
        Assert.Equal(HttpStatusCode.OK, (await Cancel(serviceId, "2026-10-06", "2026-10-06")).StatusCode);

        var report = await Report(Stops[0], Local(8, 28));
        var mine = await Get("/api/v1/operator/journey", OperatorToken);
        var byId = await Get($"/api/v1/journeys/{JourneyId}/status");
        var byNumber = await Get("/api/v1/trains/8058/status");
        var board = await ReadAsync(await Get("/api/v1/stations/GPH/board"));

        Assert.Equal(HttpStatusCode.Conflict, report.StatusCode);
        Assert.Equal("journey_not_reportable", (await ReadAsync(report)).GetProperty("reason").GetString());
        Assert.Equal(HttpStatusCode.NotFound, mine.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, byId.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, byNumber.StatusCode);
        Assert.DoesNotContain("8058", board.GetProperty("trains").EnumerateArray().Select(t => t.GetProperty("trainNumber").GetString()));
    }

    [Fact]
    public async Task An_extra_train_can_be_cancelled_like_a_weekly_one()
    {
        var response = await Post("/api/v1/timetable/services", new
        {
            kind = "Extra", trainNumber = "8099", lineCode = "MAIN", category = "Regional", serviceType = "Slow",
            dates = new[] { "2026-10-08" }, stops = GoodStops,
        });
        var serviceId = (await ReadAsync(response)).GetProperty("serviceIds")[0].GetGuid();

        var cancelled = await Cancel(serviceId, "2026-10-08", "2026-10-08");

        Assert.Equal(HttpStatusCode.OK, cancelled.StatusCode);
        Assert.Equal("Cancelled", await StatusOn("8099", "2026-10-08"));
        Assert.DoesNotContain("8099", await TrainsOn("2026-10-08"));
    }
}
