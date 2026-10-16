using System.Net;
using System.Text.Json;
using SmartRail.Infrastructure.Seeding;
using SmartRail.IntegrationTests.Database;
using Xunit;

namespace SmartRail.IntegrationTests.Journeys;

/// <summary>
/// Spec 09: GET /api/v1/stations/{code}/board, public. Train 8058 (today, Tuesday 6 Oct 2026): Veyangoda dep 08:20; Gampaha arr 08:30 dep 08:31;
/// Ragama 08:42/08:43; Maradana 08:50/08:51; Colombo Fort arr 08:55. Train 8061 (timetable only today): Veyangoda 07:50, Gampaha 08:00/08:01, Colombo Fort 08:30.
/// Every stop of both uses platform 1; platform 2 is empty.
/// </summary>
public sealed class StationBoardTests(PostgresFixture postgres) : JourneyApiTestBase(postgres)
{
    private void AdvanceTo(DateTimeOffset at)
    {
        if (at > Clock.GetUtcNow())
        {
            Clock.SetUtcNow(at);
        }
    }

    private async Task Do(int stopIndex, DateTimeOffset at, string type)
    {
        AdvanceTo(at);
        Assert.Equal(HttpStatusCode.Created, (await Report(Stops[stopIndex], at, eventType: type)).StatusCode);
    }

    private async Task<JsonElement> BoardAsync(string path)
    {
        var response = await Get(path);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await ReadAsync(response);
    }

    private static JsonElement First(JsonElement board) => board.GetProperty("trains")[0];

    // ---- AC-E09-01 (1): what is due, with platform, times, status and three-language destination ----

    [Fact]
    public async Task The_board_lists_8058_with_platform_times_status_and_the_destination_in_three_languages_without_a_token()
    {
        var response = await Get("/api/v1/stations/GPH/board"); // no token: a display has no user

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("public, max-age=5", response.Headers.CacheControl?.ToString());
        var board = await ReadAsync(response);
        Assert.Equal("Gampaha", board.GetProperty("station").GetProperty("name").GetProperty("en").GetString());
        var trains = board.GetProperty("trains").EnumerateArray().ToList(); // 8061 is already past at 08:28
        Assert.Equal(["8058", "8075"], trains.Select(t => t.GetProperty("trainNumber").GetString()).ToList()); // 8075 is the night train, later
        var train = trains[0];
        Assert.Equal("1", train.GetProperty("platform").GetString());
        Assert.Equal("Colombo Fort", train.GetProperty("destination").GetProperty("name").GetProperty("en").GetString());
        Assert.Equal("කොළඹ කොටුව", train.GetProperty("destination").GetProperty("name").GetProperty("si").GetString());
        Assert.Equal("கொழும்பு கோட்டை", train.GetProperty("destination").GetProperty("name").GetProperty("ta").GetString());
        Assert.Equal(Local(8, 30), Time(train.GetProperty("arrival"), "scheduled"));
        Assert.Equal(Local(8, 31), Time(train.GetProperty("departure"), "scheduled"));
        Assert.Equal("Due", train.GetProperty("state").GetString());
        Assert.Equal("Scheduled", train.GetProperty("statusKey").GetString());
        Assert.True(train.GetProperty("live").GetBoolean());
        Assert.True(board.GetProperty("live").GetBoolean());
    }

    [Fact]
    public async Task Codes_are_case_insensitive()
    {
        var board = await BoardAsync("/api/v1/stations/gph/board?platform=1");

        Assert.Equal("GPH", board.GetProperty("station").GetProperty("code").GetString());
        Assert.Equal(2, board.GetProperty("trains").GetArrayLength()); // 8058 and the night train
    }

    // ---- AC-E09-01 (2), AC-E09-02 (1): a delay moves the estimate ----

    [Fact]
    public async Task After_Veyangoda_departs_8_minutes_late_the_arrival_estimate_moves_beside_the_schedule()
    {
        await Do(0, Local(8, 28), "Departed");

        var train = First(await BoardAsync("/api/v1/stations/GPH/board?platform=1"));

        Assert.Equal(Local(8, 30), Time(train.GetProperty("arrival"), "scheduled"));
        Assert.Equal(Local(8, 38), Time(train.GetProperty("arrival"), "estimated"));
        Assert.Equal(8, train.GetProperty("delayMinutes").GetInt32());
        Assert.True(train.GetProperty("delayKnown").GetBoolean());
        Assert.Equal("Delayed", train.GetProperty("statusKey").GetString());
    }

    [Fact]
    public async Task A_train_that_has_arrived_is_AtPlatform_with_its_departure_and_leaves_the_board_when_it_departs()
    {
        await Do(0, Local(8, 28), "Departed");
        await Do(1, Local(8, 38), "Arrived");

        var atPlatform = First(await BoardAsync("/api/v1/stations/GPH/board?platform=1"));
        Assert.Equal("AtPlatform", atPlatform.GetProperty("state").GetString());
        Assert.Equal(Local(8, 39), Time(atPlatform.GetProperty("departure"), "estimated"));

        await Do(1, Local(8, 40), "Departed");
        var after = await BoardAsync("/api/v1/stations/GPH/board?platform=1");
        // 8058 has gone: the display moves on to the night train.
        Assert.Equal("8075", Assert.Single(after.GetProperty("trains").EnumerateArray()).GetProperty("trainNumber").GetString());
    }

    [Fact]
    public async Task An_unknown_delay_has_no_estimate_and_says_where_the_train_was_last_seen()
    {
        await Do(0, Local(8, 28), "Departed");
        AdvanceTo(Local(8, 29));
        Assert.Equal(HttpStatusCode.Created, (await ReportDelay(Local(8, 29), "SignalIssue", total: null, unknown: true)).StatusCode);

        var train = First(await BoardAsync("/api/v1/stations/GPH/board?platform=1"));

        Assert.False(train.GetProperty("delayKnown").GetBoolean());
        Assert.Equal(Local(8, 30), Time(train.GetProperty("arrival"), "scheduled"));
        Assert.Equal(JsonValueKind.Null, train.GetProperty("arrival").GetProperty("estimated").ValueKind);
        var seen = train.GetProperty("lastSeen");
        Assert.Equal("Departed", seen.GetProperty("state").GetString());
        Assert.Equal("Veyangoda", seen.GetProperty("stationName").GetString());
        Assert.Equal(Local(8, 28), seen.GetProperty("at").GetDateTimeOffset());
    }

    [Fact]
    public async Task The_first_stop_has_a_departure_and_no_arrival_and_the_final_stop_the_reverse()
    {
        var first = First(await BoardAsync("/api/v1/stations/VYG/board"));
        Assert.Equal(JsonValueKind.Null, first.GetProperty("arrival").ValueKind);
        Assert.Equal(Local(8, 20), Time(first.GetProperty("departure"), "scheduled"));

        var final = (await BoardAsync("/api/v1/stations/CF/board")).GetProperty("trains").EnumerateArray()
            .Single(t => t.GetProperty("trainNumber").GetString() == "8058");
        Assert.Equal(JsonValueKind.Null, final.GetProperty("departure").ValueKind);
        Assert.Equal(Local(8, 55), Time(final.GetProperty("arrival"), "scheduled"));
    }

    // ---- AC-E09-04: nothing due ----

    [Fact]
    public async Task An_empty_platform_has_no_trains_and_nothing_next_because_none_is_planned_there()
    {
        var board = await BoardAsync("/api/v1/stations/GPH/board?platform=2");

        Assert.Empty(board.GetProperty("trains").EnumerateArray());
        Assert.Equal(JsonValueKind.Null, board.GetProperty("next").ValueKind);
        Assert.Equal(JsonValueKind.Null, board.GetProperty("firstTomorrow").ValueKind);
        Assert.False(board.GetProperty("live").GetBoolean());
    }

    [Fact]
    public async Task A_completed_train_is_not_brought_back_from_the_timetable()
    {
        await Do(0, Local(8, 21), "Departed");
        await Do(4, Local(8, 56), "Arrived"); // the final stop: the journey is Completed

        var board = await BoardAsync("/api/v1/stations/CF/board");

        // 8058 is Completed and the timetable does not bring it back; only the night train (22:45) is still due.
        Assert.Equal(["8075"], board.GetProperty("trains").EnumerateArray().Select(t => t.GetProperty("trainNumber").GetString()).ToList());
    }

    // ---- errors and limits ----

    [Theory]
    [InlineData("/api/v1/stations/XXX/board", HttpStatusCode.NotFound, "station_not_found")]
    [InlineData("/api/v1/stations/GPH/board?platform=9", HttpStatusCode.NotFound, "platform_not_found")]
    [InlineData("/api/v1/stations/GPH/board?count=0", HttpStatusCode.UnprocessableEntity, "invalid_request")]
    [InlineData("/api/v1/stations/GPH/board?count=21", HttpStatusCode.UnprocessableEntity, "invalid_request")]
    [InlineData("/api/v1/stations/GPH/board?count=abc", HttpStatusCode.UnprocessableEntity, "invalid_request")]
    public async Task Bad_requests_get_a_stable_reason_code(string path, HttpStatusCode status, string reason)
    {
        var response = await Get(path);

        Assert.Equal(status, response.StatusCode);
        Assert.Equal(reason, (await ReadAsync(response)).GetProperty("reason").GetString());
    }

    [Fact]
    public async Task The_planned_platform_is_set_for_every_stop_of_the_demo_services_by_the_migration()
    {
        Assert.Equal(0, await Database.ScalarAsync<long>("select count(*) from service_stop where planned_platform_id is null"));
        Assert.Equal(0, await Database.ScalarAsync<long>(
            "select count(*) from service_stop s join platform p on p.id = s.planned_platform_id where p.code <> '1' or p.station_id <> s.station_id"));
    }
}

/// <summary>Seven o'clock: 8061 is still to come (timetable only), 8058 is a live journey that has not started.</summary>
public sealed class StationBoardBeforeTheTrainsTests(PostgresFixture postgres) : JourneyApiTestBase(postgres)
{
    protected override DateTimeOffset StartClock => Local(7, 0);

    [Fact]
    public async Task Trains_are_listed_in_the_order_they_are_expected_and_mix_timetable_and_live_rows()
    {
        var response = await Get("/api/v1/stations/GPH/board");
        var board = await ReadAsync(response);

        var trains = board.GetProperty("trains").EnumerateArray().ToList();
        Assert.Equal(["8061", "8058", "8075"], trains.Select(t => t.GetProperty("trainNumber").GetString()).ToList()); // 08:00, 08:30, then the night train 22:20
        Assert.False(trains[0].GetProperty("live").GetBoolean());
        Assert.True(trains[1].GetProperty("live").GetBoolean());
        Assert.True(board.GetProperty("live").GetBoolean()); // at least one is live
        Assert.Equal("Scheduled", trains[0].GetProperty("statusKey").GetString());
        Assert.Equal(JsonValueKind.Null, trains[0].GetProperty("arrival").GetProperty("estimated").ValueKind);
    }

    [Fact]
    public async Task The_count_limits_the_list()
    {
        var board = await ReadAsync(await Get("/api/v1/stations/GPH/board?count=1"));

        Assert.Equal("8061", Assert.Single(board.GetProperty("trains").EnumerateArray()).GetProperty("trainNumber").GetString());
    }

    [Fact]
    public async Task With_nothing_due_on_platform_2_before_7_there_is_still_nothing_next_for_that_platform()
    {
        var board = await ReadAsync(await Get("/api/v1/stations/GPH/board?platform=2"));

        Assert.Empty(board.GetProperty("trains").EnumerateArray());
        Assert.Equal(JsonValueKind.Null, board.GetProperty("next").ValueKind);
    }
}

/// <summary>At 08:28 8061 has left Gampaha but is still ahead at Colombo Fort.</summary>
public sealed class StationBoardLaterStopTests(PostgresFixture postgres) : JourneyApiTestBase(postgres)
{
    protected override DateTimeOffset StartClock => Local(8, 28);

    [Fact]
    public async Task A_train_that_left_an_earlier_station_is_still_listed_at_a_later_one()
    {
        // Train 8061 (timetable only) is due at Colombo Fort at 08:30, so it is listed there and nothing is "next".
        var board = await ReadAsync(await Get("/api/v1/stations/CF/board"));

        var numbers = board.GetProperty("trains").EnumerateArray().Select(t => t.GetProperty("trainNumber").GetString()).ToList();
        Assert.Equal(["8061", "8058", "8075"], numbers);
        Assert.Equal(JsonValueKind.Null, board.GetProperty("next").ValueKind);
    }
}

/// <summary>Eleven at night: the night train 8075 has left Gampaha (22:21), so nothing is due and the next day's first service is given.</summary>
public sealed class StationBoardEndOfDayTests(PostgresFixture postgres) : JourneyApiTestBase(postgres)
{
    protected override DateTimeOffset StartClock => Local(23, 0);

    [Fact]
    public async Task After_the_last_train_the_next_day_s_first_service_is_given_and_it_is_from_the_timetable()
    {
        var board = await ReadAsync(await Get("/api/v1/stations/GPH/board?platform=1"));

        Assert.Empty(board.GetProperty("trains").EnumerateArray());
        Assert.Equal(JsonValueKind.Null, board.GetProperty("next").ValueKind);
        var tomorrow = board.GetProperty("firstTomorrow");
        Assert.Equal("8061", tomorrow.GetProperty("trainNumber").GetString()); // Gampaha 08:00 on Wednesday is earlier than 8058's 08:30
        Assert.Equal(Local(8, 0, date: Tuesday.AddDays(1)), Time(tomorrow.GetProperty("arrival"), "scheduled"));
        Assert.False(tomorrow.GetProperty("live").GetBoolean());
        Assert.Equal("2026-10-07", tomorrow.GetProperty("operatingDate").GetString());
    }
}

/// <summary>Spec 09 section 16: the demo train 8070 that leaves 4 minutes after the demo seed runs, and the night train 8075. Clock: 14:00.</summary>
public sealed class DemoSoonTrainTests(PostgresFixture postgres) : JourneyApiTestBase(postgres)
{
    protected override DateTimeOffset StartClock => Local(14, 0);

    private async Task<TimeOnly> SeedSoonAsync()
    {
        await using var admin = Database.AdminContext();
        return await new DemoSeeder(admin, Clock).SeedSoonTrainAsync();
    }

    private async Task<JsonElement> BoardAsync(string path) => await ReadAsync(await Get(path));

    [Fact]
    public async Task The_demo_train_leaves_Veyangoda_four_minutes_after_the_seed_and_is_on_platform_1_from_the_timetable()
    {
        var leaves = await SeedSoonAsync();

        Assert.Equal(new TimeOnly(14, 4), leaves);
        var board = await BoardAsync("/api/v1/stations/VYG/board?platform=1");
        var train = board.GetProperty("trains").EnumerateArray().First(); // 14:04 is before the night train at 22:10
        Assert.Equal("8070", train.GetProperty("trainNumber").GetString());
        Assert.Equal(Local(14, 4), Time(train.GetProperty("departure"), "scheduled"));
        Assert.True(Time(train.GetProperty("departure"), "scheduled") - Clock.GetUtcNow() <= TimeSpan.FromMinutes(5)); // "within 5 minutes"
        Assert.False(train.GetProperty("live").GetBoolean()); // no journey: the timetable answers
        Assert.Equal("Scheduled", train.GetProperty("statusKey").GetString());
        Assert.Equal("1", train.GetProperty("platform").GetString());
        Assert.Equal("Colombo Fort", train.GetProperty("destination").GetProperty("name").GetProperty("en").GetString());
    }

    [Fact]
    public async Task It_calls_at_every_station_after_Veyangoda_at_the_expected_times()
    {
        await SeedSoonAsync();

        var gampaha = (await BoardAsync("/api/v1/stations/GPH/board?platform=1")).GetProperty("trains").EnumerateArray()
            .Single(t => t.GetProperty("trainNumber").GetString() == "8070");
        Assert.Equal(Local(14, 14), Time(gampaha.GetProperty("arrival"), "scheduled"));
        Assert.Equal(Local(14, 15), Time(gampaha.GetProperty("departure"), "scheduled"));
        var final = (await BoardAsync("/api/v1/stations/CF/board?platform=1")).GetProperty("trains").EnumerateArray()
            .Single(t => t.GetProperty("trainNumber").GetString() == "8070");
        Assert.Equal(Local(14, 39), Time(final.GetProperty("arrival"), "scheduled"));
        Assert.Equal(JsonValueKind.Null, final.GetProperty("departure").ValueKind);
    }

    [Fact]
    public async Task Running_it_again_later_moves_the_same_train_and_creates_no_second_one()
    {
        await SeedSoonAsync();
        Clock.SetUtcNow(Local(14, 20));

        var leaves = await SeedSoonAsync();

        Assert.Equal(new TimeOnly(14, 24), leaves);
        Assert.Equal(1, await Database.ScalarAsync<long>("select count(*) from train_service where train_number = '8070'"));
        Assert.Equal(5, await Database.ScalarAsync<long>("select count(*) from service_stop where service_id = '00000004-0000-4000-8000-0000000000a1'"));
        var train = (await BoardAsync("/api/v1/stations/VYG/board?platform=1")).GetProperty("trains").EnumerateArray().First();
        Assert.Equal(Local(14, 24), Time(train.GetProperty("departure"), "scheduled"));
    }

    [Fact]
    public async Task The_ordinary_demo_seed_does_not_create_it_only_the_extra_step_that_Compose_runs_does()
    {
        // JourneyApiTestBase already ran DemoSeeder.SeedAsync (the journey of 8058). It must not have added 8070.
        Assert.Equal(0, await Database.ScalarAsync<long>("select count(*) from train_service where train_number = '8070'"));
        var board = await BoardAsync("/api/v1/stations/VYG/board?platform=1");
        Assert.DoesNotContain(board.GetProperty("trains").EnumerateArray(), t => t.GetProperty("trainNumber").GetString() == "8070");
    }

    [Fact]
    public async Task The_night_train_is_an_upcoming_train_in_the_daytime_on_every_station_s_platform_1()
    {
        foreach (var code in new[] { "VYG", "GPH", "RGM", "MDA", "CF" })
        {
            var trains = (await BoardAsync($"/api/v1/stations/{code}/board?platform=1")).GetProperty("trains").EnumerateArray().ToList();

            var night = Assert.Single(trains, t => t.GetProperty("trainNumber").GetString() == "8075");
            Assert.False(night.GetProperty("live").GetBoolean());
            Assert.Equal("1", night.GetProperty("platform").GetString());
        }

        var gampaha = (await BoardAsync("/api/v1/stations/GPH/board?platform=1")).GetProperty("trains")[0];
        Assert.Equal(Local(22, 20), Time(gampaha.GetProperty("arrival"), "scheduled"));
        Assert.Equal(Local(22, 21), Time(gampaha.GetProperty("departure"), "scheduled"));
    }
}
