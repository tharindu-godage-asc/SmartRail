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
/// Spec 13 S1 and S2: the controller's timetable reads, validate and create, and the journey generator. The clock is Tuesday 6 Oct 2026
/// 08:28; the horizon is 7 days, so a weekly service starting Wednesday 7 Oct on Mon to Fri has journeys on 7, 8, 9, 12 and 13 October.
/// </summary>
public sealed class TimetableTests(PostgresFixture postgres) : JourneyApiTestBase(postgres)
{
    private string ControllerToken => Issuer.Create(roles: ["controller"]);

    private static readonly object[] GoodStops =
    [
        new { stationCode = "VYG", arrival = (string?)null, departure = "17:25", platform = "1" },
        new { stationCode = "GPH", arrival = "17:35", departure = "17:36", platform = "2" },
        new { stationCode = "CF", arrival = "17:58", departure = (string?)null, platform = "1" },
    ];

    private static Dictionary<string, object?> WeeklyBody(string number = "8072", object[]? stops = null, bool? acknowledge = null) => new()
    {
        ["kind"] = "Weekly",
        ["trainNumber"] = number,
        ["lineCode"] = "MAIN",
        ["category"] = "Regional",
        ["serviceType"] = "Express",
        ["days"] = new[] { "Mon", "Tue", "Wed", "Thu", "Fri" },
        ["effectiveFrom"] = "2026-10-07",
        ["stops"] = stops ?? GoodStops,
        ["acknowledgeWarnings"] = acknowledge,
    };

    private async Task<HttpResponseMessage> Post(string path, object body, string? token = null, bool noToken = false)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(body) };
        if (!noToken)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token ?? ControllerToken);
        }

        return await Client.SendAsync(request);
    }

    // ---- who may call ----

    [Fact]
    public async Task No_token_is_401_and_operator_and_caterer_tokens_are_403_on_every_route()
    {
        var paths = new[] { "/api/v1/timetable/options", "/api/v1/timetable/services", $"/api/v1/timetable/services/{Guid.NewGuid()}" };
        foreach (var path in paths)
        {
            Assert.Equal(HttpStatusCode.Unauthorized, (await Get(path)).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await Get(path, OperatorToken)).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await Get(path, Issuer.Create(roles: ["caterer"]))).StatusCode);
        }

        foreach (var path in new[] { "/api/v1/timetable/validate", "/api/v1/timetable/services" })
        {
            Assert.Equal(HttpStatusCode.Unauthorized, (await Post(path, WeeklyBody(), noToken: true)).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await Post(path, WeeklyBody(), OperatorToken)).StatusCode);
        }
    }

    // ---- S1: read ----

    [Fact]
    public async Task Options_list_the_stations_of_the_line_in_line_order_with_platforms()
    {
        var response = await Get("/api/v1/timetable/options?line=MAIN", ControllerToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var options = await ReadAsync(response);
        var stations = options.GetProperty("stations").EnumerateArray().ToList();
        Assert.Equal(["CF", "MDA", "RGM", "GPH", "VYG"], stations.Select(s => s.GetProperty("code").GetString()).ToList());
        Assert.Equal(["1", "2"], stations[4].GetProperty("platforms").EnumerateArray().Select(p => p.GetString()).ToList());
        Assert.Equal("වේයන්ගොඩ", stations[4].GetProperty("names").GetProperty("si").GetString());
        Assert.Equal(HttpStatusCode.NotFound, (await Get("/api/v1/timetable/options?line=NOPE", ControllerToken)).StatusCode);
    }

    [Fact]
    public async Task The_weekly_list_shows_the_seeded_services_with_their_days_and_first_departure()
    {
        var response = await Get("/api/v1/timetable/services?line=MAIN&tab=weekly", ControllerToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var rows = (await ReadAsync(response)).EnumerateArray().ToList();
        var row = rows.Single(r => r.GetProperty("trainNumber").GetString() == "8058");
        Assert.Equal("Veyangoda", row.GetProperty("fromName").GetString());
        Assert.Equal("Colombo Fort", row.GetProperty("toName").GetString());
        Assert.Equal("08:20", row.GetProperty("firstDeparture").GetString());
        Assert.Equal(["Mon", "Tue", "Wed", "Thu", "Fri"], row.GetProperty("runsOn").EnumerateArray().Select(d => d.GetString()).ToList());
        Assert.Equal("Every stop (5)", row.GetProperty("stopsLabel").GetString());
        Assert.False(row.GetProperty("amended").GetBoolean());
        Assert.Empty((await ReadAsync(await Get("/api/v1/timetable/services?tab=extra", ControllerToken))).EnumerateArray());
        Assert.Empty((await ReadAsync(await Get("/api/v1/timetable/services?tab=cancelled", ControllerToken))).EnumerateArray());
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await Get("/api/v1/timetable/services?tab=nonsense", ControllerToken)).StatusCode);
    }

    [Fact]
    public async Task A_service_detail_has_its_stops_platforms_and_versions_and_an_unknown_one_is_404()
    {
        var serviceId = await Database.ScalarAsync<Guid>("select id from train_service where train_number = '8058'");

        var response = await Get($"/api/v1/timetable/services/{serviceId}", ControllerToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var detail = await ReadAsync(response);
        Assert.Equal("Inbound", detail.GetProperty("direction").GetString());
        var stops = detail.GetProperty("stops").EnumerateArray().ToList();
        Assert.Equal(5, stops.Count);
        Assert.Equal("08:30", stops[1].GetProperty("arrival").GetString());
        Assert.Equal("1", stops[1].GetProperty("platform").GetString());
        Assert.Single(detail.GetProperty("versions").EnumerateArray());
        Assert.Equal(HttpStatusCode.NotFound, (await Get($"/api/v1/timetable/services/{Guid.NewGuid()}", ControllerToken)).StatusCode);
    }

    // ---- validate ----

    [Fact]
    public async Task Validate_always_answers_200_with_every_error_and_saves_nothing()
    {
        var bad = WeeklyBody(stops:
        [
            new { stationCode = "VYG", arrival = (string?)null, departure = "17:25", platform = (string?)null },
            new { stationCode = "RGM", arrival = "17:40", departure = "17:41", platform = (string?)null },
            new { stationCode = "GPH", arrival = "17:50", departure = "17:51", platform = (string?)null },
            new { stationCode = "CF", arrival = "18:10", departure = (string?)null, platform = (string?)null },
        ]);

        var response = await Post("/api/v1/timetable/validate", bad);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await ReadAsync(response);
        Assert.False(result.GetProperty("valid").GetBoolean());
        Assert.Equal("stops_not_in_line_order", result.GetProperty("errors")[0].GetProperty("code").GetString());
        Assert.Equal(0, await Database.ScalarAsync<long>("select count(*) from train_service where train_number = '8072'"));
    }

    // ---- S2: create ----

    [Fact]
    public async Task A_weekly_service_is_created_with_its_journeys_unassigned_and_a_pending_stop_state_per_stop()
    {
        var response = await Post("/api/v1/timetable/services", WeeklyBody());

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await ReadAsync(response);
        Assert.Equal("8072", created.GetProperty("trainNumber").GetString());
        Assert.Equal(5, created.GetProperty("journeysGenerated").GetInt32()); // 7, 8, 9, 12 and 13 October
        var serviceId = created.GetProperty("serviceIds")[0].GetGuid();

        Assert.Equal(5, await Database.ScalarAsync<long>($"select count(*) from journey where service_id = '{serviceId}' and status = 'Unassigned'"));
        Assert.Equal(15, await Database.ScalarAsync<long>(
            $"select count(*) from journey_stop_state s join journey j on j.id = s.journey_id where j.service_id = '{serviceId}' and s.confirmation = 'Pending'"));
        Assert.Equal("Weekdays", await Database.ScalarAsync<string>($"select runs_on from train_service where id = '{serviceId}'"));
        Assert.Equal("Inbound", await Database.ScalarAsync<string>($"select direction from train_service where id = '{serviceId}'"));
        Assert.Equal(1, await Database.ScalarAsync<long>($"select count(*) from service_change_log where service_id = '{serviceId}' and action = 'Created'"));
    }

    [Fact]
    public async Task The_passenger_app_finds_the_new_service_on_its_days_and_not_on_others()
    {
        await Post("/api/v1/timetable/services", WeeklyBody());

        var wednesday = await ReadAsync(await Get("/api/v1/trains?from=VYG&to=CF&date=2026-10-07"));
        var saturday = await ReadAsync(await Get("/api/v1/trains?from=VYG&to=CF&date=2026-10-10"));

        Assert.Contains("8072", wednesday.GetProperty("trains").EnumerateArray().Select(t => t.GetProperty("trainNumber").GetString()));
        Assert.DoesNotContain("8072", saturday.GetProperty("trains").EnumerateArray().Select(t => t.GetProperty("trainNumber").GetString()));
    }

    [Fact]
    public async Task A_service_with_errors_is_422_with_the_list_and_nothing_is_created()
    {
        var body = WeeklyBody(stops:
        [
            new { stationCode = "VYG", arrival = (string?)null, departure = "17:25", platform = (string?)null },
            new { stationCode = "GPH", arrival = "17:20", departure = "17:21", platform = (string?)null },
            new { stationCode = "CF", arrival = "17:58", departure = (string?)null, platform = (string?)null },
        ]);

        var response = await Post("/api/v1/timetable/services", body);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var problem = await ReadAsync(response);
        Assert.Equal("invalid_service", problem.GetProperty("reason").GetString());
        Assert.Equal("times_not_increasing", problem.GetProperty("errors")[0].GetProperty("code").GetString());
        Assert.Equal(0, await Database.ScalarAsync<long>("select count(*) from train_service where train_number = '8072'"));
        Assert.Equal(0, await Database.ScalarAsync<long>("select count(*) from service_change_log"));
    }

    [Fact]
    public async Task The_same_service_twice_is_409_train_number_in_use_and_nothing_is_created_twice()
    {
        Assert.Equal(HttpStatusCode.Created, (await Post("/api/v1/timetable/services", WeeklyBody())).StatusCode);

        var again = await Post("/api/v1/timetable/services", WeeklyBody());

        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        Assert.Equal("train_number_in_use", (await ReadAsync(again)).GetProperty("reason").GetString());
        Assert.Equal(1, await Database.ScalarAsync<long>("select count(*) from train_service where train_number = '8072'"));
    }

    [Fact]
    public async Task A_train_number_that_a_seeded_service_already_runs_is_refused()
    {
        var response = await Post("/api/v1/timetable/services", WeeklyBody(number: "8058"));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("8058", (await ReadAsync(response)).GetProperty("trainNumber").GetString());
    }

    [Fact]
    public async Task A_platform_clash_is_advice_only_it_needs_the_controller_to_confirm_and_then_it_is_saved()
    {
        // The seeded 8058 uses Gampaha platform 1 at 08:30. This one asks for the same platform at 08:33.
        object[] stops =
        [
            new { stationCode = "VYG", arrival = (string?)null, departure = "08:25", platform = (string?)null },
            new { stationCode = "GPH", arrival = "08:33", departure = "08:34", platform = "1" },
            new { stationCode = "CF", arrival = "09:00", departure = (string?)null, platform = (string?)null },
        ];

        var refused = await Post("/api/v1/timetable/services", WeeklyBody(stops: stops));
        Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode);
        var problem = await ReadAsync(refused);
        Assert.Equal("warnings_not_acknowledged", problem.GetProperty("reason").GetString());
        Assert.Equal("platform_close", problem.GetProperty("warnings")[0].GetProperty("code").GetString());
        Assert.Equal(0, await Database.ScalarAsync<long>("select count(*) from train_service where train_number = '8072'"));

        var accepted = await Post("/api/v1/timetable/services", WeeklyBody(stops: stops, acknowledge: true));
        Assert.Equal(HttpStatusCode.Created, accepted.StatusCode);
        Assert.Equal("platform_close", (await ReadAsync(accepted)).GetProperty("warnings")[0].GetProperty("code").GetString());
    }

    [Fact]
    public async Task An_extra_train_with_two_dates_makes_one_service_per_date_and_lists_under_extra()
    {
        var body = WeeklyBody();
        body["kind"] = "Extra";
        body.Remove("days");
        body.Remove("effectiveFrom");
        body["dates"] = new[] { "2026-10-08", "2026-10-09" };

        var response = await Post("/api/v1/timetable/services", body);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await ReadAsync(response);
        Assert.Equal(2, created.GetProperty("serviceIds").GetArrayLength());
        Assert.Equal(2, created.GetProperty("journeysGenerated").GetInt32());

        var extra = (await ReadAsync(await Get("/api/v1/timetable/services?tab=extra", ControllerToken))).EnumerateArray().ToList();
        Assert.Equal(2, extra.Count);
        Assert.Equal(["2026-10-08", "2026-10-09"], extra.Select(r => r.GetProperty("date").GetString()).ToList());
        var weekly = (await ReadAsync(await Get("/api/v1/timetable/services?tab=weekly", ControllerToken))).EnumerateArray();
        Assert.DoesNotContain("8072", weekly.Select(r => r.GetProperty("trainNumber").GetString()));

        // Only on those dates.
        var friday = await ReadAsync(await Get("/api/v1/trains?from=VYG&to=CF&date=2026-10-09"));
        var monday = await ReadAsync(await Get("/api/v1/trains?from=VYG&to=CF&date=2026-10-12"));
        Assert.Contains("8072", friday.GetProperty("trains").EnumerateArray().Select(t => t.GetProperty("trainNumber").GetString()));
        Assert.DoesNotContain("8072", monday.GetProperty("trains").EnumerateArray().Select(t => t.GetProperty("trainNumber").GetString()));
    }

    // ---- the generator ----

    private async Task<int> RunGeneratorAsync()
    {
        await using var scope = Api.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<JourneyGenerationHandler>().RunAsync(CancellationToken.None);
    }

    [Fact]
    public async Task The_generator_creates_the_missing_journeys_for_the_next_seven_days_and_a_second_run_creates_none()
    {
        // Seeded: 8058 Mon to Fri (today's journey exists), 8061 Mon to Fri, 8075 daily. Days: Tue 6 .. Tue 13 October (8 dates).
        var first = await RunGeneratorAsync();
        var second = await RunGeneratorAsync();

        Assert.True(first > 0);
        Assert.Equal(0, second);
        // 8058 has one journey per weekday in the window and today's seeded one is not doubled.
        Assert.Equal(6, await Database.ScalarAsync<long>(
            "select count(*) from journey j join train_service s on s.id = j.service_id where s.train_number = '8058'"));
        Assert.Equal(8, await Database.ScalarAsync<long>(
            "select count(*) from journey j join train_service s on s.id = j.service_id where s.train_number = '8075'"));
        Assert.Equal(0, await Database.ScalarAsync<long>(
            "select count(*) from journey j join train_service s on s.id = j.service_id where s.train_number = '8075' and j.operating_date > date '2026-10-13'"));
    }

    [Fact]
    public async Task Two_generators_at_once_create_no_duplicate_journeys()
    {
        await Task.WhenAll(RunGeneratorAsync(), RunGeneratorAsync());

        Assert.Equal(0, await Database.ScalarAsync<long>(
            "select count(*) from (select service_id, operating_date from journey group by 1, 2 having count(*) > 1) d"));
    }
}
