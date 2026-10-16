using System.Diagnostics.Metrics;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using SmartRail.Application.Features.Journeys;
using SmartRail.IntegrationTests.Authentication;
using SmartRail.IntegrationTests.Database;
using Xunit;

namespace SmartRail.IntegrationTests.Journeys;

/// <summary>Spec passenger-find-trains on the real API and a real database. The clock starts at 08:10 on the seeded Tuesday.</summary>
public sealed class PassengerEndpointTests(PostgresFixture postgres) : JourneyApiTestBase(postgres)
{
    protected override DateTimeOffset StartClock => Local(8, 10);

    private async Task<HttpResponseMessage> Search(string query, string? token = null) => await Get($"/api/v1/trains{query}", token);

    private static string[] Numbers(JsonElement body) =>
        body.GetProperty("trains").EnumerateArray().Select(t => t.GetProperty("trainNumber").GetString()!).ToArray();

    private static JsonElement Train(JsonElement body, string number) =>
        body.GetProperty("trains").EnumerateArray().Single(t => t.GetProperty("trainNumber").GetString() == number);

    // ---- GET /api/v1/stations ----

    [Fact]
    public async Task The_stations_start_at_colombo_fort_and_carry_three_languages()
    {
        var response = await Get("/api/v1/stations");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await ReadAsync(response);
        var stations = body.GetProperty("stations").EnumerateArray().ToList();
        Assert.Equal(["CF", "MDA", "RGM", "GPH", "VYG"], stations.Select(s => s.GetProperty("code").GetString()));
        Assert.Equal(1, stations[0].GetProperty("lines")[0].GetProperty("sequence").GetInt32());
        Assert.Equal("MAIN", stations[0].GetProperty("lines")[0].GetProperty("code").GetString());
        Assert.Equal("Colombo Fort", stations[0].GetProperty("name").GetProperty("en").GetString());
    }

    [Fact]
    public async Task Every_seeded_station_has_a_sinhala_and_a_tamil_name_of_its_own()
    {
        // FR-14 and EC-78: English is a safety net, never a reason for a missing translation. This test is the guard.
        var stations = (await ReadAsync(await Get("/api/v1/stations"))).GetProperty("stations").EnumerateArray();

        foreach (var station in stations)
        {
            var name = station.GetProperty("name");
            var en = name.GetProperty("en").GetString();
            Assert.False(string.IsNullOrWhiteSpace(name.GetProperty("si").GetString()), $"{en} has no Sinhala name");
            Assert.False(string.IsNullOrWhiteSpace(name.GetProperty("ta").GetString()), $"{en} has no Tamil name");
            Assert.NotEqual(en, name.GetProperty("si").GetString());
            Assert.NotEqual(en, name.GetProperty("ta").GetString());
        }
    }

    [Fact]
    public async Task The_version_is_the_etag_and_revalidation_costs_nothing()
    {
        var first = await Get("/api/v1/stations");
        var version = (await ReadAsync(first)).GetProperty("version").GetString();

        Assert.Equal($"\"{version}\"", first.Headers.ETag!.Tag);
        Assert.Contains("no-cache", first.Headers.CacheControl!.ToString());

        var second = await Get("/api/v1/stations");
        Assert.Equal(version, (await ReadAsync(second)).GetProperty("version").GetString());
    }

    [Theory]
    [InlineData("\"{v}\"", HttpStatusCode.NotModified)]
    [InlineData("W/\"{v}\"", HttpStatusCode.NotModified)]
    [InlineData("\"other\", \"{v}\"", HttpStatusCode.NotModified)]
    [InlineData("\"other\", W/\"{v}\", \"third\"", HttpStatusCode.NotModified)]
    [InlineData("*", HttpStatusCode.NotModified)]
    [InlineData("\"other\"", HttpStatusCode.OK)]
    [InlineData("\"other\", W/\"another\"", HttpStatusCode.OK)]
    public async Task If_none_match_gives_304_when_any_listed_tag_matches(string header, HttpStatusCode expected)
    {
        var version = (await ReadAsync(await Get("/api/v1/stations"))).GetProperty("version").GetString()!;
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/stations");
        request.Headers.TryAddWithoutValidation("If-None-Match", header.Replace("{v}", version));

        var response = await Client.SendAsync(request);

        Assert.Equal(expected, response.StatusCode);
        Assert.Equal($"\"{version}\"", response.Headers.ETag!.Tag); // the tag is sent with a 304 as well
        if (expected == HttpStatusCode.NotModified)
        {
            Assert.Empty(await response.Content.ReadAsByteArrayAsync());
        }
    }

    [Fact]
    public async Task The_version_changes_when_a_name_changes()
    {
        var before = (await ReadAsync(await Get("/api/v1/stations"))).GetProperty("version").GetString();

        await Database.ExecuteAsync("update station set name = 'Gampaha Town' where code = 'GPH'");
        var after = (await ReadAsync(await Get("/api/v1/stations"))).GetProperty("version").GetString();

        Assert.NotEqual(before, after);
    }

    // ---- GET /api/v1/trains ----

    [Fact]
    public async Task The_seeded_demo_journey_is_found_live_with_the_scheduled_times_as_estimates()
    {
        var response = await Search("?from=VYG&to=CF");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("public, max-age=5", response.Headers.CacheControl!.ToString());
        var body = await ReadAsync(response);
        Assert.Equal(["8058", "8075"], Numbers(body)); // 8061 left at 07:50 and has no journey today; 8075 is the daily night train (timetable only)
        var train = Train(body, "8058");
        Assert.True(train.GetProperty("live").GetBoolean());
        Assert.Equal(JourneyId, train.GetProperty("journeyId").GetGuid());
        Assert.Equal("Slow", train.GetProperty("serviceType").GetString());
        Assert.Equal("Colombo Fort", train.GetProperty("destination").GetProperty("name").GetProperty("en").GetString());
        Assert.Equal(Local(8, 20), Time(train.GetProperty("atFrom").GetProperty("departure"), "scheduled"));
        Assert.Equal(Local(8, 20), Time(train.GetProperty("atFrom").GetProperty("departure"), "estimated"));
        Assert.Equal(Local(8, 55), Time(train.GetProperty("atTo").GetProperty("arrival"), "scheduled"));
        Assert.Equal(JsonValueKind.Null, train.GetProperty("atFrom").GetProperty("arrival").ValueKind); // first stop
        Assert.Equal(0, train.GetProperty("delayMinutes").GetInt32());
        Assert.Equal("Scheduled", train.GetProperty("statusKey").GetString());
        Assert.Equal(JsonValueKind.Null, train.GetProperty("lastEventAt").ValueKind);
    }

    [Fact]
    public async Task A_departure_report_shows_up_for_the_passenger_waiting_at_gampaha()
    {
        // The slice 1 flow (operator reports Veyangoda 08:28, 8 minutes late), then Kasuni searches Gampaha to Colombo Fort.
        Clock.SetUtcNow(Local(8, 28, 20));
        Assert.Equal(HttpStatusCode.Created, (await Report(Stops[0], Local(8, 28))).StatusCode);

        var body = await ReadAsync(await Search("?from=GPH&to=CF"));

        var train = Train(body, "8058");
        var atFrom = train.GetProperty("atFrom");
        Assert.Equal(Local(8, 30), Time(atFrom.GetProperty("arrival"), "scheduled")); // AC-E06-01 (2): 08:38 beside 08:30
        Assert.Equal(Local(8, 38), Time(atFrom.GetProperty("arrival"), "estimated"));
        Assert.Equal(Local(8, 31), Time(atFrom.GetProperty("departure"), "scheduled"));
        Assert.Equal(Local(8, 39), Time(atFrom.GetProperty("departure"), "estimated"));
        Assert.Equal(Local(8, 55), Time(train.GetProperty("atTo").GetProperty("arrival"), "scheduled"));
        Assert.Equal(Local(9, 3), Time(train.GetProperty("atTo").GetProperty("arrival"), "estimated"));
        Assert.Equal(8, train.GetProperty("delayMinutes").GetInt32());
        Assert.Equal("Warning", train.GetProperty("statusGroup").GetString());
        Assert.Equal("Delayed", train.GetProperty("statusKey").GetString());
        Assert.Equal(1, train.GetProperty("version").GetInt32());

        // A search from Veyangoda no longer offers the train: it has left. Only the night train, 8075, is still to come.
        Assert.Equal(["8075"], Numbers(await ReadAsync(await Search("?from=VYG&to=CF"))));
    }

    [Fact]
    public async Task The_last_event_time_is_the_time_the_server_received_the_report()
    {
        Clock.SetUtcNow(Local(8, 28, 20));
        await Report(Stops[0], Local(8, 28));
        var received = await Database.ScalarAsync<DateTime>("select max(received_at) from train_event");

        var body = await ReadAsync(await Search("?from=GPH&to=CF"));

        Assert.Equal(received, Train(body, "8058").GetProperty("lastEventAt").GetDateTimeOffset().UtcDateTime);
        Assert.Equal(Local(8, 28, 20).UtcDateTime, received);
    }

    [Fact]
    public async Task The_opposite_direction_is_an_empty_list_with_200()
    {
        var response = await Search("?from=GPH&to=VYG");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty(Numbers(await ReadAsync(response)));
    }

    [Fact]
    public async Task A_later_date_is_answered_from_the_timetable_and_marked_not_live()
    {
        Clock.SetUtcNow(Local(22, 0));
        var monday = new DateOnly(2026, 10, 12);

        var body = await ReadAsync(await Search("?from=VYG&to=CF&date=2026-10-12"));

        Assert.Equal(["8061", "8058", "8075"], Numbers(body)); // 8075 is the daily night train
        Assert.Equal("2026-10-12", body.GetProperty("date").GetString());
        foreach (var train in body.GetProperty("trains").EnumerateArray())
        {
            Assert.False(train.GetProperty("live").GetBoolean());
            Assert.Equal(JsonValueKind.Null, train.GetProperty("journeyId").ValueKind);
            Assert.Equal(JsonValueKind.Null, train.GetProperty("delayMinutes").ValueKind);
            Assert.Equal(JsonValueKind.Null, train.GetProperty("version").ValueKind);
            Assert.Equal(JsonValueKind.Null, train.GetProperty("lastEventAt").ValueKind);
            Assert.Equal("Scheduled", train.GetProperty("statusKey").GetString());
            Assert.Equal("Normal", train.GetProperty("statusGroup").GetString());
            Assert.Equal(JsonValueKind.Null, train.GetProperty("atFrom").GetProperty("departure").GetProperty("estimated").ValueKind);
            Assert.Equal("2026-10-12", train.GetProperty("operatingDate").GetString());
        }

        Assert.Equal(Local(8, 20, date: monday), Time(Train(body, "8058").GetProperty("atFrom").GetProperty("departure"), "scheduled"));
    }

    [Fact]
    public async Task A_saturday_has_only_the_daily_night_train_because_the_other_trains_run_on_weekdays()
    {
        Clock.SetUtcNow(Local(22, 0));

        Assert.Equal(["8075"], Numbers(await ReadAsync(await Search("?from=VYG&to=CF&date=2026-10-10"))));
    }

    [Theory]
    [InlineData("", "invalid_request", HttpStatusCode.UnprocessableEntity)]
    [InlineData("?from=VYG", "invalid_request", HttpStatusCode.UnprocessableEntity)]
    [InlineData("?to=CF", "invalid_request", HttpStatusCode.UnprocessableEntity)]
    [InlineData("?from=VYG&to=VYG", "same_station", HttpStatusCode.UnprocessableEntity)]
    [InlineData("?from=XXX&to=CF", "station_not_found", HttpStatusCode.NotFound)]
    [InlineData("?from=VYG&to=XXX", "station_not_found", HttpStatusCode.NotFound)]
    [InlineData("?from=VYG&to=CF&date=2026-13-40", "invalid_date", HttpStatusCode.UnprocessableEntity)]
    [InlineData("?from=VYG&to=CF&date=yesterday", "invalid_date", HttpStatusCode.UnprocessableEntity)]
    [InlineData("?from=VYG&to=CF&date=2026-10-05", "date_out_of_range", HttpStatusCode.UnprocessableEntity)]
    [InlineData("?from=VYG&to=CF&date=2026-10-14", "date_out_of_range", HttpStatusCode.UnprocessableEntity)]
    public async Task Every_error_has_the_right_status_and_a_stable_reason_code(string query, string reason, HttpStatusCode status)
    {
        var response = await Search(query);

        Assert.Equal(status, response.StatusCode);
        Assert.Equal(reason, (await ReadAsync(response)).GetProperty("reason").GetString());
    }

    [Fact]
    public async Task The_edges_of_the_date_range_are_accepted()
    {
        Assert.Equal(HttpStatusCode.OK, (await Search("?from=VYG&to=CF&date=2026-10-06")).StatusCode); // today
        Assert.Equal(HttpStatusCode.OK, (await Search("?from=VYG&to=CF&date=2026-10-13")).StatusCode); // today plus 7
    }

    [Fact]
    public async Task Station_codes_are_matched_ignoring_case()
    {
        var body = await ReadAsync(await Search("?from=vyg&to=cf"));

        Assert.Equal("VYG", body.GetProperty("from").GetProperty("code").GetString());
        Assert.Equal("CF", body.GetProperty("to").GetProperty("code").GetString());
        Assert.Equal("වේයන්ගොඩ", body.GetProperty("from").GetProperty("name").GetProperty("si").GetString());
    }

    // ---- GET /api/v1/lines/{code}/status ----

    [Fact]
    public async Task A_delayed_train_makes_the_line_some_delayed_and_the_freshness_stays_put()
    {
        Clock.SetUtcNow(Local(8, 28, 20));
        await Report(Stops[0], Local(8, 28));

        var early = await ReadAsync(await Get("/api/v1/lines/MAIN/status"));
        Clock.SetUtcNow(Local(8, 40));
        var late = await ReadAsync(await Get("/api/v1/lines/MAIN/status"));

        Assert.Equal("SomeDelayed", early.GetProperty("statusKey").GetString());
        Assert.Equal("Warning", early.GetProperty("statusGroup").GetString());
        Assert.Equal(1, early.GetProperty("journeysInProgress").GetInt32());
        Assert.Equal(1, early.GetProperty("delayedJourneys").GetInt32());
        Assert.Equal(Local(8, 28, 20), early.GetProperty("lastEventAt").GetDateTimeOffset());
        Assert.Equal(early.GetProperty("lastEventAt").GetDateTimeOffset(), late.GetProperty("lastEventAt").GetDateTimeOffset());
        Assert.NotEqual(early.GetProperty("serverTime").GetDateTimeOffset(), late.GetProperty("serverTime").GetDateTimeOffset());
    }

    [Fact]
    public async Task An_unknown_line_is_a_404_and_the_code_is_matched_ignoring_case()
    {
        var unknown = await Get("/api/v1/lines/NOPE/status");

        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        Assert.Equal("line_not_found", (await ReadAsync(unknown)).GetProperty("reason").GetString());
        Assert.Equal(HttpStatusCode.OK, (await Get("/api/v1/lines/main/status")).StatusCode);
    }

    // ---- public, cheap, and nothing else changed ----

    [Theory]
    [InlineData("/api/v1/stations")]
    [InlineData("/api/v1/trains?from=VYG&to=CF")]
    [InlineData("/api/v1/lines/MAIN/status")]
    public async Task The_routes_work_with_no_token_and_with_a_bad_token(string path)
    {
        Assert.Equal(HttpStatusCode.OK, (await Get(path)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Get(path, "not-a-token")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Get(path, OperatorToken)).StatusCode);
    }

    [Fact]
    public async Task The_existing_service_status_endpoint_is_unchanged()
    {
        var response = await Get("/api/v1/status");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await ReadAsync(response);
        Assert.Equal("Main Line", body.GetProperty("line").GetString());
        Assert.Equal("Running normally", body.GetProperty("text").GetString());
    }

    [Fact]
    public async Task Searches_are_counted_by_outcome_and_source()
    {
        var counts = new Dictionary<string, long>();
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, l) =>
        {
            if (instrument.Name == "smartrail.trains.searches")
            {
                l.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((_, value, tags, _) =>
        {
            var outcome = tags.ToArray().Single(t => t.Key == "outcome").Value;
            var source = tags.ToArray().Single(t => t.Key == "source").Value;
            lock (counts)
            {
                counts[$"{outcome}/{source}"] = counts.GetValueOrDefault($"{outcome}/{source}") + value;
            }
        });
        listener.Start();

        await Search("?from=VYG&to=CF"); // found, mixed: the live 8058 and the timetable-only night train 8075
        await Search("?from=GPH&to=VYG"); // empty
        await Search("?from=VYG"); // invalid
        Clock.SetUtcNow(Local(22, 0));
        await Search("?from=VYG&to=CF&date=2026-10-12"); // found, timetable

        lock (counts)
        {
            Assert.Equal(1, counts["found/mixed"]);
            Assert.Equal(1, counts["empty/none"]);
            Assert.Equal(1, counts["invalid/none"]);
            Assert.Equal(1, counts["found/timetable"]);
        }
    }

    [Fact]
    public async Task The_grace_period_is_one_shared_setting_and_the_search_follows_it()
    {
        // R14: one JourneySettings instance serves slice 1's lookup and this search, so they cannot disagree.
        Assert.Single(Api.Services.GetServices<JourneySettings>());
        Assert.Equal(15, Api.Services.GetRequiredService<JourneySettings>().VisibleAfterEndMinutes);

        // Default 15: the 08:55 expected end plus 15 minutes means 09:10 is the last visible minute.
        Clock.SetUtcNow(Local(9, 10));
        Assert.Equal(["8058", "8075"], Numbers(await ReadAsync(await Search("?from=VYG&to=CF"))));
        Clock.SetUtcNow(Local(9, 11));
        Assert.Equal(["8075"], Numbers(await ReadAsync(await Search("?from=VYG&to=CF")))); // 8058 is over; the night train is still to come

        // Change the one setting to 30 and the search moves with it.
        await using var thirty = new AuthApiFactory(
            Issuer, databaseConnection: Database.AppConnection, clock: Clock,
            settings: new Dictionary<string, string> { ["Journeys:VisibleAfterEndMinutes"] = "30" });
        using var client = thirty.CreateClient();
        Assert.Equal(30, thirty.Services.GetRequiredService<JourneySettings>().VisibleAfterEndMinutes);

        Clock.SetUtcNow(Local(9, 25));
        Assert.Equal(["8058", "8075"], Numbers(JsonDocument.Parse(await client.GetStringAsync("/api/v1/trains?from=VYG&to=CF")).RootElement));
        Clock.SetUtcNow(Local(9, 26));
        Assert.Equal(["8075"], Numbers(JsonDocument.Parse(await client.GetStringAsync("/api/v1/trains?from=VYG&to=CF")).RootElement));
    }
}

/// <summary>The same endpoints at times before the 08:10 start of <see cref="PassengerEndpointTests"/> (the fake clock cannot go back). Clock: 02:00.</summary>
public sealed class PassengerEarlyMorningTests(PostgresFixture postgres) : JourneyApiTestBase(postgres)
{
    protected override DateTimeOffset StartClock => Local(2, 0);

    private async Task<HttpResponseMessage> Search(string query) => await Get($"/api/v1/trains{query}");

    private static string[] Numbers(JsonElement body) =>
        body.GetProperty("trains").EnumerateArray().Select(t => t.GetProperty("trainNumber").GetString()!).ToArray();

    [Fact]
    public async Task The_express_is_offered_between_stations_it_serves_and_not_through_the_stations_it_skips()
    {
        Clock.SetUtcNow(Local(7, 0));

        // 8075 is the daily night train: it calls at every station.
        Assert.Equal(["8061", "8058", "8075"], Numbers(await ReadAsync(await Search("?from=VYG&to=GPH"))));
        Assert.Equal(["8058", "8075"], Numbers(await ReadAsync(await Search("?from=RGM&to=CF"))));
        Assert.Equal(["8058", "8075"], Numbers(await ReadAsync(await Search("?from=VYG&to=MDA"))));
    }

    [Fact]
    public async Task The_line_is_normal_before_the_first_train_and_has_nothing_to_age()
    {
        Clock.SetUtcNow(Local(2, 0));

        var response = await Get("/api/v1/lines/MAIN/status");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("public, max-age=5", response.Headers.CacheControl!.ToString());
        var body = await ReadAsync(response);
        Assert.Equal("Normal", body.GetProperty("statusKey").GetString());
        Assert.Equal("Normal", body.GetProperty("statusGroup").GetString());
        Assert.Equal(0, body.GetProperty("journeysInProgress").GetInt32());
        Assert.Equal(JsonValueKind.Null, body.GetProperty("lastEventAt").ValueKind);
        Assert.Equal(Local(2, 0), body.GetProperty("serverTime").GetDateTimeOffset());
        Assert.Equal("Main Line", body.GetProperty("lineName").GetProperty("en").GetString());
        Assert.False(string.IsNullOrWhiteSpace(body.GetProperty("lineName").GetProperty("si").GetString()));
    }
}
