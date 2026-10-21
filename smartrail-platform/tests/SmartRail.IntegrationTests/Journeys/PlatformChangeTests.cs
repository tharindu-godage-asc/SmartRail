using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using SmartRail.IntegrationTests.Database;
using Xunit;

namespace SmartRail.IntegrationTests.Journeys;

/// <summary>Spec 20 (spec 16 F4, FR-57): a controller sets the platform of one stop. Train 8058 plans platform 1 at every stop; platform 2 is empty.</summary>
public sealed class PlatformChangeTests(PostgresFixture postgres) : JourneyApiTestBase(postgres)
{
    private string ControllerToken => Issuer.Controller();

    private Task<HttpResponseMessage> Change(int stopIndex, string? platform, bool acknowledge = false, string? token = null, bool noToken = false)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/journeys/{JourneyId}/stops/{Stops[stopIndex]}/platform")
        {
            Content = JsonContent.Create(new { platformCode = platform, acknowledgeWarnings = acknowledge }),
        };
        if (!noToken)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token ?? ControllerToken);
        }

        return Client.SendAsync(request);
    }

    /// <summary>Another train (8061, which has no journey yet) on platform 2 at Gampaha, four minutes after 8058 is there.</summary>
    private async Task AnotherTrainOnPlatformTwoAtGampahaAsync(string direction)
    {
        await Database.ExecuteAsync($@"
            insert into journey (id, service_id, operating_date, status) select gen_random_uuid(), id, date '2026-10-06', 'Scheduled' from train_service where train_number = '8061';
            update train_service set direction = '{direction}' where train_number = '8061';
            update service_stop set
                planned_platform_id = (select p.id from platform p join station s on s.id = p.station_id where s.code = 'GPH' and p.code = '2'),
                scheduled_arrival = (select ss.scheduled_arrival from service_stop ss join train_service t on t.id = ss.service_id join station st on st.id = ss.station_id where t.train_number = '8058' and st.code = 'GPH') + interval '4 minutes',
                scheduled_departure = (select ss.scheduled_arrival from service_stop ss join train_service t on t.id = ss.service_id join station st on st.id = ss.station_id where t.train_number = '8058' and st.code = 'GPH') + interval '5 minutes'
            where service_id = (select id from train_service where train_number = '8061') and station_id = (select id from station where code = 'GPH');");
    }

    [Fact]
    public async Task A_controller_sets_platform_2_at_Gampaha_and_the_reads_and_boards_follow()
    {
        var response = await Change(1, "2");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await ReadAsync(response);
        Assert.Equal("2", body.GetProperty("platform").GetString());
        Assert.Equal("1", body.GetProperty("previousPlatform").GetString());

        // The controller's detail shows the current and the planned platform, and the event with the platform and who made it.
        var detail = await ReadAsync(await Get($"/api/v1/controller/journeys/{JourneyId}", ControllerToken));
        var gampaha = detail.GetProperty("stops")[1];
        Assert.Equal("2", gampaha.GetProperty("platform").GetString());
        Assert.Equal("1", gampaha.GetProperty("plannedPlatform").GetString());
        var changed = detail.GetProperty("events").EnumerateArray().First(e => e.GetProperty("type").GetString() == "PlatformChanged");
        Assert.Equal("Controller", changed.GetProperty("source").GetString());
        Assert.Equal("2", changed.GetProperty("platform").GetString());

        // The platform 2 board of Gampaha now lists 8058, and the platform 1 board no longer does.
        Assert.Contains("8058", await (await Get("/api/v1/stations/GPH/board?platform=2")).Content.ReadAsStringAsync());
        Assert.DoesNotContain("8058", await (await Get("/api/v1/stations/GPH/board?platform=1")).Content.ReadAsStringAsync());
        Assert.Equal("controller", await Database.ScalarAsync<string>("select lower(source) from train_event where event_type = 'PlatformChanged'"));
    }

    [Fact]
    public async Task The_journey_version_moves_so_clients_take_the_new_status()
    {
        var before = (await StatusAsync()).GetProperty("version").GetInt32();

        await Change(1, "2");

        Assert.Equal(before + 1, (await StatusAsync()).GetProperty("version").GetInt32());
    }

    [Fact]
    public async Task Null_goes_back_to_the_planned_platform()
    {
        await Change(1, "2");

        var response = await Change(1, null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var detail = await ReadAsync(await Get($"/api/v1/controller/journeys/{JourneyId}", ControllerToken));
        Assert.Equal(JsonValueKind.Null, detail.GetProperty("stops")[1].GetProperty("platform").ValueKind);
        Assert.Contains("8058", await (await Get("/api/v1/stations/GPH/board?platform=1")).Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData("9")]
    [InlineData("platform one")]
    public async Task A_platform_that_is_not_at_the_station_is_refused_and_changes_nothing(string platform)
    {
        var response = await Change(1, platform);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("platform_not_at_station", (await ReadAsync(response)).GetProperty("reason").GetString());
        Assert.Equal(0, await Database.ScalarAsync<long>("select count(*) from train_event where event_type = 'PlatformChanged'"));
    }

    [Fact]
    public async Task A_stop_the_train_has_left_cannot_be_changed()
    {
        Assert.Equal(HttpStatusCode.Created, (await Report(Stops[0], Local(8, 21), eventType: "Departed")).StatusCode);

        var response = await Change(0, "2");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("stop_already_departed", (await ReadAsync(response)).GetProperty("reason").GetString());
    }

    [Fact]
    public async Task Another_train_on_the_platform_within_ten_minutes_is_advice_that_must_be_acknowledged_and_then_it_is_allowed()
    {
        await AnotherTrainOnPlatformTwoAtGampahaAsync("Inbound");

        var advice = await Change(1, "2");

        Assert.Equal(HttpStatusCode.Conflict, advice.StatusCode);
        var body = await ReadAsync(advice);
        Assert.Equal("warnings_not_acknowledged", body.GetProperty("reason").GetString());
        var warning = body.GetProperty("warnings")[0];
        Assert.Equal("8061", warning.GetProperty("trainNumber").GetString());
        Assert.False(warning.GetProperty("oppositeDirection").GetBoolean());
        Assert.Equal(0, await Database.ScalarAsync<long>("select count(*) from train_event where event_type = 'PlatformChanged'"));

        Assert.Equal(HttpStatusCode.OK, (await Change(1, "2", acknowledge: true)).StatusCode); // advice never blocks (FR-57)
    }

    [Fact]
    public async Task Trains_in_opposite_directions_are_marked_so()
    {
        await AnotherTrainOnPlatformTwoAtGampahaAsync("Outbound");

        var body = await ReadAsync(await Change(1, "2"));

        Assert.True(body.GetProperty("warnings")[0].GetProperty("oppositeDirection").GetBoolean());
    }

    [Fact]
    public async Task Only_a_controller_may_change_a_platform_and_unknown_ids_are_404()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await Change(1, "2", noToken: true)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Change(1, "2", token: Issuer.Operator())).StatusCode);

        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/journeys/{Guid.NewGuid()}/stops/{Stops[1]}/platform") { Content = JsonContent.Create(new { platformCode = "2" }) };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ControllerToken);
        Assert.Equal(HttpStatusCode.NotFound, (await Client.SendAsync(request)).StatusCode);

        var wrongStop = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/journeys/{JourneyId}/stops/{Guid.NewGuid()}/platform") { Content = JsonContent.Create(new { platformCode = "2" }) };
        wrongStop.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ControllerToken);
        var answer = await Client.SendAsync(wrongStop);
        Assert.Equal(HttpStatusCode.NotFound, answer.StatusCode);
        Assert.Equal("stop_not_found", (await ReadAsync(answer)).GetProperty("reason").GetString());
    }

    [Fact]
    public async Task An_operator_report_after_a_platform_change_keeps_the_platform()
    {
        await Change(1, "2");

        await Report(Stops[0], Local(8, 21), eventType: "Departed");

        var detail = await ReadAsync(await Get($"/api/v1/controller/journeys/{JourneyId}", ControllerToken));
        Assert.Equal("2", detail.GetProperty("stops")[1].GetProperty("platform").GetString());
    }
}
