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
/// Spec 14 S4: amend a weekly service from a date as a new version. The clock is Tuesday 6 Oct 2026 08:28. A weekly 8072 from Wednesday 7 Oct
/// (Mon to Fri) has journeys on 7, 8, 9, 12 and 13 October; it leaves Veyangoda 17:25.
/// </summary>
public sealed class TimetableAmendTests(PostgresFixture postgres) : JourneyApiTestBase(postgres)
{
    private string ControllerToken => Issuer.Create(roles: ["controller"]);

    private static object[] StopsLeaving(string vygDeparture, string? gphPlatform = "2") =>
    [
        new { stationCode = "VYG", arrival = (string?)null, departure = vygDeparture, platform = "1" },
        new { stationCode = "GPH", arrival = "17:35", departure = "17:36", platform = gphPlatform },
        new { stationCode = "CF", arrival = "17:58", departure = (string?)null, platform = "1" },
    ];

    private static Dictionary<string, object?> Body(
        string number = "8072", string effectiveFrom = "2026-10-12", string vygDeparture = "17:30", string[]? days = null, object[]? stops = null, bool? acknowledge = null) => new()
    {
        ["kind"] = "Weekly",
        ["trainNumber"] = number,
        ["lineCode"] = "MAIN",
        ["category"] = "Regional",
        ["serviceType"] = "Express",
        ["days"] = days ?? ["Mon", "Tue", "Wed", "Thu", "Fri"],
        ["effectiveFrom"] = effectiveFrom,
        ["stops"] = stops ?? StopsLeaving(vygDeparture),
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

    private async Task<Guid> CreateWeekly()
    {
        var body = Body(effectiveFrom: "2026-10-07", vygDeparture: "17:25");
        var response = await Post("/api/v1/timetable/services", body);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await ReadAsync(response)).GetProperty("serviceIds")[0].GetGuid();
    }

    private Task<HttpResponseMessage> Amend(Guid serviceId, Dictionary<string, object?> body, string? token = null) =>
        Post($"/api/v1/timetable/services/{serviceId}/amend", body, token);

    private Task<string?> Scalar(string sql) => Database.ScalarAsync<string>(sql);

    private async Task<string?> StatusOn(string number, string date) => await Scalar(
        $"select coalesce((select j.status from journey j join train_service s on s.id = j.service_id where s.train_number = '{number}' and j.operating_date = date '{date}'), 'none')");

    private async Task<DateTimeOffset> DepartureOn(string date)
    {
        var trains = (await ReadAsync(await Get($"/api/v1/trains?from=VYG&to=CF&date={date}"))).GetProperty("trains").EnumerateArray();
        var train = trains.Single(t => t.GetProperty("trainNumber").GetString() == "8072");
        return DateTimeOffset.Parse(train.GetProperty("atFrom").GetProperty("departure").GetProperty("scheduled").GetString()!);
    }

    // ---- the version ----

    [Fact]
    public async Task An_amendment_is_a_new_version_the_old_one_is_kept_and_journeys_before_the_date_keep_their_times()
    {
        var first = await CreateWeekly();

        var response = await Amend(first, Body());

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await ReadAsync(response);
        Assert.Equal(2, created.GetProperty("version").GetInt32());
        Assert.Equal(2, created.GetProperty("journeysUpdated").GetInt32()); // 12 and 13 October
        var second = created.GetProperty("serviceId").GetGuid();
        Assert.NotEqual(first, second);

        Assert.Equal(2, await Database.ScalarAsync<long>("select count(*) from train_service where train_number = '8072'"));
        Assert.Equal("2026-10-11", await Scalar($"select effective_to::text from train_service where id = '{first}'"));
        Assert.Equal("2026-10-12", await Scalar($"select effective_from::text from train_service where id = '{second}'"));
        Assert.Equal(first.ToString(), await Scalar($"select superseded_service_id::text from train_service where id = '{second}'"));
        // One journey per date still, whatever the version.
        Assert.Equal(0, await Database.ScalarAsync<long>(
            "select count(*) from (select service_id, operating_date from journey group by 1, 2 having count(*) > 1) d"));
        Assert.Equal(5, await Database.ScalarAsync<long>(
            "select count(*) from journey j join train_service s on s.id = j.service_id where s.train_number = '8072'"));
        Assert.Equal(3, await Database.ScalarAsync<long>($"select count(*) from journey where service_id = '{first}'"));
        Assert.Equal(2, await Database.ScalarAsync<long>($"select count(*) from journey where service_id = '{second}'"));
        // Fresh Pending stop states on the re-pointed journeys.
        Assert.Equal(6, await Database.ScalarAsync<long>(
            $"select count(*) from journey_stop_state s join journey j on j.id = s.journey_id where j.service_id = '{second}' and s.confirmation = 'Pending'"));
        Assert.Equal(0, await Database.ScalarAsync<long>(
            $"select count(*) from journey_stop_state s join journey j on j.id = s.journey_id where j.service_id = '{second}' and s.service_stop_id in (select id from service_stop where service_id = '{first}')"));
    }

    [Fact]
    public async Task The_passenger_search_shows_the_version_in_force_for_each_date()
    {
        await Amend(await CreateWeekly(), Body());

        Assert.Equal(Local(17, 25, date: new DateOnly(2026, 10, 8)), await DepartureOn("2026-10-08")); // version 1
        Assert.Equal(Local(17, 30, date: new DateOnly(2026, 10, 12)), await DepartureOn("2026-10-12")); // version 2
    }

    [Fact]
    public async Task The_versions_list_has_each_version_with_who_when_and_a_readable_summary()
    {
        var first = await CreateWeekly();
        var second = (await ReadAsync(await Amend(first, Body()))).GetProperty("serviceId").GetGuid();

        var detail = await ReadAsync(await Get($"/api/v1/timetable/services/{second}", ControllerToken));

        var versions = detail.GetProperty("versions").EnumerateArray().ToList();
        Assert.Equal([1, 2], versions.Select(v => v.GetProperty("version").GetInt32()).ToList());
        Assert.Equal("2026-10-11", versions[0].GetProperty("effectiveTo").GetString());
        Assert.Equal("Veyangoda 17:30 (was 17:25)", versions[1].GetProperty("summary").GetString());
        Assert.False(string.IsNullOrEmpty(versions[1].GetProperty("createdBy").GetString()));
        Assert.Equal(JsonValueKind.Null, versions[0].GetProperty("summary").ValueKind);
        // The list shows one row per train number: the newest version.
        var rows = (await ReadAsync(await Get("/api/v1/timetable/services?tab=weekly", ControllerToken))).EnumerateArray()
            .Where(r => r.GetProperty("trainNumber").GetString() == "8072").ToList();
        var row = Assert.Single(rows);
        Assert.Equal(2, row.GetProperty("version").GetInt32());
        Assert.True(row.GetProperty("amended").GetBoolean());
    }

    [Fact]
    public async Task Every_amendment_has_one_audit_row_with_the_actor_and_a_failed_one_has_none()
    {
        var first = await CreateWeekly();
        await Amend(first, Body(effectiveFrom: "2026-10-06")); // refused: not tomorrow

        Assert.Equal(0, await Database.ScalarAsync<long>("select count(*) from service_change_log where action = 'Amended'"));

        await Amend(first, Body());
        Assert.Equal(1, await Database.ScalarAsync<long>("select count(*) from service_change_log where action = 'Amended' and length(actor) > 0"));
    }

    // ---- days removed and added ----

    [Fact]
    public async Task A_day_the_new_version_drops_is_cancelled_and_a_day_it_adds_gets_a_journey()
    {
        var first = await CreateWeekly();

        // From Thursday 8 Oct: Mon to Thu and Saturday. Friday 9 Oct goes; Saturday 10 Oct is new.
        var response = await Amend(first, Body(effectiveFrom: "2026-10-08", days: ["Mon", "Tue", "Wed", "Thu", "Sat"]));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await ReadAsync(response);
        Assert.Equal(1, created.GetProperty("journeysCancelled").GetInt32());
        Assert.Equal(1, created.GetProperty("journeysGenerated").GetInt32());
        Assert.Equal("Cancelled", await StatusOn("8072", "2026-10-09"));
        Assert.Equal("Unassigned", await StatusOn("8072", "2026-10-10"));
        Assert.Equal("Unassigned", await StatusOn("8072", "2026-10-08"));
        Assert.Equal("Unassigned", await StatusOn("8072", "2026-10-07")); // before the date: untouched
    }

    // ---- refusals ----

    [Fact]
    public async Task A_journey_with_events_on_or_after_the_date_refuses_the_amendment_and_nothing_changes()
    {
        await using (var scope = Api.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<JourneyGenerationHandler>().RunAsync(CancellationToken.None);
        }

        await Database.ScalarAsync<long>(
            "with u as (update journey set status = 'Running' where service_id = (select id from train_service where train_number = '8058') and operating_date = date '2026-10-07' returning 1) select count(*) from u");
        var serviceId = await Database.ScalarAsync<Guid>("select id from train_service where train_number = '8058'");
        var stops = new object[]
        {
            new { stationCode = "VYG", arrival = (string?)null, departure = "08:25", platform = "1" },
            new { stationCode = "CF", arrival = "09:00", departure = (string?)null, platform = "1" },
        };

        var response = await Amend(serviceId, Body(number: "8058", effectiveFrom: "2026-10-07", stops: stops));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var problem = await ReadAsync(response);
        Assert.Equal("journey_has_events", problem.GetProperty("reason").GetString());
        Assert.Equal("2026-10-07", problem.GetProperty("date").GetString());
        Assert.Equal(1, await Database.ScalarAsync<long>("select count(*) from train_service where train_number = '8058'"));
        Assert.Equal(0, await Database.ScalarAsync<long>("select count(*) from service_change_log where action = 'Amended'"));
    }

    [Fact]
    public async Task An_extra_train_cannot_be_amended()
    {
        var created = await Post("/api/v1/timetable/services", new
        {
            kind = "Extra", trainNumber = "8099", lineCode = "MAIN", category = "Regional", serviceType = "Slow",
            dates = new[] { "2026-10-08" }, stops = StopsLeaving("17:25"),
        });
        var serviceId = (await ReadAsync(created)).GetProperty("serviceIds")[0].GetGuid();

        var response = await Amend(serviceId, Body(number: "8099"));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("amend_extra_not_supported", (await ReadAsync(response)).GetProperty("reason").GetString());
    }

    [Theory]
    [InlineData("2026-10-06", "date_in_past")] // today
    [InlineData("2026-10-05", "date_in_past")] // the past
    [InlineData("2026-10-07", "effective_date_not_after_current")] // the day the current version starts
    public async Task The_effective_date_is_tomorrow_or_later_and_after_the_current_versions_start(string date, string code)
    {
        var serviceId = await CreateWeekly();

        var response = await Amend(serviceId, Body(effectiveFrom: date));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal(code, (await ReadAsync(response)).GetProperty("reason").GetString());
        Assert.Equal(1, await Database.ScalarAsync<long>("select count(*) from train_service where train_number = '8072'"));
    }

    [Fact]
    public async Task The_train_number_cannot_change_and_an_unknown_service_is_404()
    {
        var serviceId = await CreateWeekly();

        var renamed = await Amend(serviceId, Body(number: "8073"));
        var missing = await Amend(Guid.NewGuid(), Body());

        Assert.Equal(HttpStatusCode.UnprocessableEntity, renamed.StatusCode);
        Assert.Equal("identity_changed", (await ReadAsync(renamed)).GetProperty("reason").GetString());
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    [Fact]
    public async Task The_same_validation_as_create_applies_and_nothing_is_saved_on_an_error()
    {
        var serviceId = await CreateWeekly();
        var backwards = new object[]
        {
            new { stationCode = "VYG", arrival = (string?)null, departure = "17:25", platform = "1" },
            new { stationCode = "GPH", arrival = "17:20", departure = "17:21", platform = "2" },
            new { stationCode = "CF", arrival = "17:58", departure = (string?)null, platform = "1" },
        };

        var response = await Amend(serviceId, Body(stops: backwards));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var problem = await ReadAsync(response);
        Assert.Equal("invalid_service", problem.GetProperty("reason").GetString());
        Assert.Equal("times_not_increasing", problem.GetProperty("errors")[0].GetProperty("code").GetString());
        Assert.Equal(1, await Database.ScalarAsync<long>("select count(*) from train_service where train_number = '8072'"));
    }

    [Fact]
    public async Task A_platform_clash_needs_the_controller_to_confirm_before_the_amendment_is_saved()
    {
        var serviceId = await CreateWeekly();
        // The seeded 8058 uses Gampaha platform 1 at 08:30; this moves 8072 to platform 1 at 08:33 from Monday 12 October.
        var clash = new object[]
        {
            new { stationCode = "VYG", arrival = (string?)null, departure = "08:25", platform = "1" },
            new { stationCode = "GPH", arrival = "08:33", departure = "08:34", platform = "1" },
            new { stationCode = "CF", arrival = "09:00", departure = (string?)null, platform = "1" },
        };

        var refused = await Amend(serviceId, Body(stops: clash));
        Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode);
        Assert.Equal("warnings_not_acknowledged", (await ReadAsync(refused)).GetProperty("reason").GetString());
        Assert.Equal(1, await Database.ScalarAsync<long>("select count(*) from train_service where train_number = '8072'"));

        var accepted = await Amend(serviceId, Body(stops: clash, acknowledge: true));
        Assert.Equal(HttpStatusCode.Created, accepted.StatusCode);
        Assert.Equal("platform_close", (await ReadAsync(accepted)).GetProperty("warnings")[0].GetProperty("code").GetString());
    }

    [Fact]
    public async Task Two_amendments_at_once_take_turns_and_one_builds_on_the_other()
    {
        var serviceId = await CreateWeekly();

        var results = await Task.WhenAll(
            Amend(serviceId, Body(effectiveFrom: "2026-10-12", vygDeparture: "17:30")),
            Amend(serviceId, Body(effectiveFrom: "2026-10-12", vygDeparture: "17:40")));

        var codes = results.Select(r => r.StatusCode).OrderBy(c => c).ToList();
        Assert.Equal([HttpStatusCode.Created, HttpStatusCode.UnprocessableEntity], codes);
        Assert.Equal(2, await Database.ScalarAsync<long>("select count(*) from train_service where train_number = '8072'"));
    }

    [Fact]
    public async Task Operator_and_caterer_tokens_are_403_and_no_token_is_401()
    {
        var serviceId = await CreateWeekly();

        Assert.Equal(HttpStatusCode.Unauthorized, (await Post($"/api/v1/timetable/services/{serviceId}/amend", Body(), noToken: true)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Amend(serviceId, Body(), OperatorToken)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Amend(serviceId, Body(), Issuer.Create(roles: ["caterer"]))).StatusCode);
    }
}
