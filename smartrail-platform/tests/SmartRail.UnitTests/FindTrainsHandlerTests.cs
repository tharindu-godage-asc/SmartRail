using Microsoft.Extensions.Time.Testing;
using SmartRail.Application.Features.Journeys;
using SmartRail.Application.Features.Passenger;
using SmartRail.Domain;
using SmartRail.Domain.Journeys;
using Xunit;
using static SmartRail.UnitTests.FakePassengerQueries;

namespace SmartRail.UnitTests;

/// <summary>Spec passenger-find-trains: R3, R4, R6, R7, R8 against an in-memory database and a clock the test sets.</summary>
public class FindTrainsHandlerTests
{
    private static readonly DateOnly Monday = new(2026, 10, 12);
    private static readonly DateOnly Saturday = new(2026, 10, 10);

    private readonly FakePassengerQueries _queries = new();
    private readonly FakeTimeProvider _clock = new();
    private readonly SearchSettings _search = new();
    private readonly JourneySettings _journeys = new();

    private static DateTimeOffset At(int hour, int minute, DateOnly? date = null, int second = 0) =>
        OperatingTime.ToInstant(date ?? Tuesday, new TimeOnly(hour, minute, second));

    private FindTrainsHandler Handler() => new(_queries, _clock, _search, _journeys);

    private async Task<FindTrainsResult> Find(string from, string to, string? date = null, int hour = 8, int minute = 10, DateOnly? today = null)
    {
        _clock.SetUtcNow(At(hour, minute, today));
        return await Handler().HandleAsync(from, to, date, CancellationToken.None);
    }

    private static string[] Numbers(FindTrainsResult result) => result.Response!.Trains.Select(t => t.TrainNumber).ToArray();

    // ---- validation (section 4, R7) ----

    [Theory]
    [InlineData(null, "CF")]
    [InlineData("VYG", null)]
    [InlineData("", "CF")]
    [InlineData("VYG", "  ")]
    public async Task Missing_stations_are_an_invalid_request(string? from, string? to) =>
        Assert.Equal(SearchError.InvalidRequest, (await Find(from!, to!)).Error);

    [Theory]
    [InlineData("VYG", "vyg")]
    [InlineData(" cf ", "CF")]
    public async Task The_same_station_twice_is_refused_ignoring_case_and_spaces(string from, string to) =>
        Assert.Equal(SearchError.SameStation, (await Find(from, to)).Error);

    [Fact]
    public async Task An_unknown_station_code_is_not_found() =>
        Assert.Equal(SearchError.StationNotFound, (await Find("VYG", "XXX")).Error);

    [Theory]
    [InlineData("2026-13-40")]
    [InlineData("06/10/2026")]
    [InlineData("2026-10-6")]
    [InlineData("")]
    [InlineData("tomorrow")]
    public async Task A_date_that_is_not_yyyy_MM_dd_is_invalid(string date) =>
        Assert.Equal(SearchError.InvalidDate, (await Find("VYG", "CF", date)).Error);

    [Theory]
    [InlineData("2026-10-05")] // yesterday
    [InlineData("2020-01-01")]
    [InlineData("2026-10-14")] // today plus 8
    public async Task A_date_before_today_or_after_today_plus_seven_is_out_of_range(string date) =>
        Assert.Equal(SearchError.DateOutOfRange, (await Find("VYG", "CF", date)).Error);

    [Theory]
    [InlineData("2026-10-06")] // today
    [InlineData("2026-10-13")] // today plus 7
    public async Task Today_and_seven_days_ahead_are_accepted(string date) =>
        Assert.Equal(SearchError.None, (await Find("VYG", "CF", date)).Error);

    [Fact]
    public async Task The_limit_is_configurable()
    {
        _search.MaxDaysAhead = 2;

        Assert.Equal(SearchError.None, (await Find("VYG", "CF", "2026-10-08")).Error);
        Assert.Equal(SearchError.DateOutOfRange, (await Find("VYG", "CF", "2026-10-09")).Error);
    }

    [Fact]
    public async Task The_date_default_is_todays_operating_date_in_colombo()
    {
        // 23:30 UTC on the 5th is 05:00 on the 6th in Colombo.
        _clock.SetUtcNow(new DateTimeOffset(2026, 10, 5, 23, 30, 0, TimeSpan.Zero));

        var result = await Handler().HandleAsync("VYG", "CF", null, CancellationToken.None);

        Assert.Equal(Tuesday, result.Response!.Date);
    }

    // ---- R3: which services qualify ----

    [Fact]
    public async Task The_express_that_skips_ragama_is_not_offered_for_a_search_through_ragama()
    {
        var result = await Find("RGM", "CF");

        Assert.DoesNotContain("8061", Numbers(result));
        Assert.Contains("8058", Numbers(result));
    }

    [Fact]
    public async Task Both_trains_serve_veyangoda_to_gampaha_and_veyangoda_to_colombo_fort()
    {
        Assert.Equal(["8061", "8058"], Numbers(await Find("VYG", "GPH", hour: 7, minute: 0)));
        Assert.Equal(["8061", "8058"], Numbers(await Find("VYG", "CF", hour: 7, minute: 0)));
    }

    [Fact]
    public async Task The_opposite_direction_is_an_empty_list_not_an_error()
    {
        var result = await Find("GPH", "VYG");

        Assert.Equal(SearchError.None, result.Error);
        Assert.Empty(result.Response!.Trains);
    }

    [Fact]
    public async Task Codes_are_matched_ignoring_case()
    {
        var result = await Find("vyg", "cf", hour: 7, minute: 0);

        Assert.Equal("VYG", result.Response!.From.Code);
        Assert.Equal(2, result.Response.Trains.Count);
    }

    // ---- R4.3: the timetable ----

    [Fact]
    public async Task Today_only_trains_still_to_leave_are_offered_from_the_timetable()
    {
        // 08:10: 8061 left Veyangoda at 07:50, 8058 leaves at 08:20.
        var result = await Find("VYG", "CF");

        Assert.Equal(["8058"], Numbers(result));
        var train = result.Response!.Trains.Single();
        Assert.False(train.Live);
        Assert.Null(train.JourneyId);
        Assert.Null(train.Version);
        Assert.Null(train.LastEventAt);
        Assert.Null(train.DelayMinutes);
        Assert.Equal("Scheduled", train.StatusKey);
        Assert.Equal("Normal", train.StatusGroup);
        Assert.Equal(new TimePair(At(8, 20), null), train.AtFrom.Departure);
        Assert.Equal(new TimePair(At(8, 55), null), train.AtTo.Arrival);
        Assert.Equal("CF", train.Destination.Code);
        Assert.Null(train.AtFrom.Arrival); // Veyangoda is the first stop: nothing arrives there
    }

    [Fact]
    public async Task The_scheduled_departure_at_exactly_now_is_not_in_the_past()
    {
        Assert.Equal(["8058"], Numbers(await Find("VYG", "CF", hour: 8, minute: 20)));
        Assert.Empty((await Find("VYG", "CF", hour: 8, minute: 21)).Response!.Trains);
    }

    [Fact]
    public async Task A_future_weekday_comes_from_the_timetable_and_a_saturday_is_empty()
    {
        var monday = await Find("VYG", "CF", "2026-10-12", hour: 22, minute: 0);
        var saturday = await Find("VYG", "CF", "2026-10-10", hour: 22, minute: 0);

        Assert.Equal(["8061", "8058"], Numbers(monday));
        Assert.All(monday.Response!.Trains, t => Assert.False(t.Live));
        Assert.Equal(Monday, monday.Response.Trains[0].OperatingDate);
        Assert.Empty(saturday.Response!.Trains);
    }

    [Fact]
    public async Task A_timetable_answer_for_a_later_date_places_the_times_on_that_date()
    {
        var result = await Find("VYG", "CF", "2026-10-12", hour: 22, minute: 0);

        Assert.Equal(At(8, 20, Monday), result.Response!.Trains.Single(t => t.TrainNumber == "8058").AtFrom.Departure!.Scheduled);
    }

    [Fact]
    public async Task A_service_whose_calendar_text_is_unsupported_is_never_offered_and_is_reported()
    {
        _queries.Services[0] = _queries.Services[0] with { RunsOn = "Except holidays" };

        var result = await Find("VYG", "CF", "2026-10-12", hour: 22, minute: 0);

        Assert.Equal(["8061"], Numbers(result));
        Assert.Equal(["8058"], result.UnsupportedCalendarTrains);
    }

    [Fact]
    public async Task A_service_with_an_unsupported_calendar_still_shows_its_journey_row()
    {
        _queries.Services[0] = _queries.Services[0] with { RunsOn = "Except holidays" };
        _queries.AddJourney(Journey8058, Service8058, Tuesday);

        var result = await Find("VYG", "CF");

        Assert.Equal(["8058"], Numbers(result));
        Assert.True(result.Response!.Trains.Single().Live);
        Assert.Empty(result.UnsupportedCalendarTrains);
    }

    [Fact]
    public async Task The_timetable_handles_a_train_that_crosses_midnight()
    {
        var night = Service(new Guid("00000004-0000-4000-8000-0000000000aa"), "9900", "Slow", "Daily",
            ("VYG", null, new TimeOnly(23, 50)), ("GPH", new(0, 20), new(0, 20)), ("CF", new(8, 0), null));
        _queries.Services.Add(night);

        var result = await Find("GPH", "CF", "2026-10-07", hour: 22, minute: 0);

        var train = result.Response!.Trains.Single(t => t.TrainNumber == "9900");
        var wednesday = new DateOnly(2026, 10, 7);
        Assert.Equal(At(0, 20, wednesday.AddDays(1)), train.AtFrom.Departure!.Scheduled); // after midnight: the next calendar day
        Assert.Equal(At(8, 0, wednesday.AddDays(1)), train.AtTo.Arrival!.Scheduled);
    }

    // ---- R4.1: live journeys ----

    [Fact]
    public async Task A_live_journey_replaces_the_timetable_entry_and_carries_its_state()
    {
        _queries.AddJourney(Journey8058, Service8058, Tuesday, version: 0);

        var result = await Find("VYG", "CF");

        var train = result.Response!.Trains.Single();
        Assert.True(train.Live);
        Assert.Equal(Journey8058, train.JourneyId);
        Assert.Equal(0, train.DelayMinutes);
        Assert.Equal("Scheduled", train.StatusKey);
        Assert.Equal(new TimePair(At(8, 20), At(8, 20)), train.AtFrom.Departure);
        Assert.Equal(1, Numbers(result).Count(n => n == "8058")); // never also from the timetable
    }

    [Fact]
    public async Task After_a_departure_the_boarding_station_shows_the_arrival_estimate_beside_the_scheduled_time()
    {
        // AC-E06-01 (2): +8 minutes, so Gampaha 08:38 beside the scheduled 08:30.
        _queries.AddJourney(Journey8058, Service8058, Tuesday);
        _queries.Report(Journey8058, 1, At(8, 28), 8, lastEventAt: At(8, 28, second: 5));

        var result = await Find("GPH", "CF", hour: 8, minute: 29);

        var train = result.Response!.Trains.Single();
        Assert.Equal(new TimePair(At(8, 30), At(8, 38)), train.AtFrom.Arrival);
        Assert.Equal(new TimePair(At(8, 31), At(8, 39)), train.AtFrom.Departure);
        Assert.Equal(new TimePair(At(8, 55), At(9, 3)), train.AtTo.Arrival);
        Assert.Equal(8, train.DelayMinutes);
        Assert.Equal("Warning", train.StatusGroup);
        Assert.Equal("Delayed", train.StatusKey);
        Assert.Equal(1, train.Version);
        Assert.Equal(At(8, 28, second: 5), train.LastEventAt);
    }

    [Fact]
    public async Task A_train_that_already_left_the_boarding_station_is_not_offered_but_one_that_has_not_is()
    {
        _queries.AddJourney(Journey8058, Service8058, Tuesday);
        _queries.Report(Journey8058, 1, At(8, 28), 8, At(8, 28));

        Assert.Empty((await Find("VYG", "CF", hour: 8, minute: 29)).Response!.Trains);
        Assert.Equal(["8058"], Numbers(await Find("GPH", "CF", hour: 8, minute: 29)));
    }

    [Fact]
    public async Task A_train_that_has_only_arrived_at_the_boarding_station_is_still_offered()
    {
        // Spec 06: an arrival report is not a departure. The train is standing at Gampaha, so a passenger can still board it there.
        _queries.AddJourney(Journey8058, Service8058, Tuesday);
        _queries.ReportArrival(Journey8058, 2, At(8, 40), 10, At(8, 40));

        Assert.Equal(["8058"], Numbers(await Find("GPH", "CF", hour: 8, minute: 41)));
        Assert.Empty((await Find("VYG", "CF", hour: 8, minute: 41)).Response!.Trains); // it left Veyangoda
    }

    [Fact]
    public async Task A_skipped_boarding_stop_counts_as_left()
    {
        _queries.AddJourney(Journey8058, Service8058, Tuesday);
        _queries.Report(Journey8058, 3, At(8, 52), 9, At(8, 52)); // Ragama reported: Veyangoda and Gampaha become Inferred

        Assert.Empty((await Find("GPH", "CF", hour: 8, minute: 53)).Response!.Trains);
        Assert.Equal(["8058"], Numbers(await Find("MDA", "CF", hour: 8, minute: 53)));
    }

    [Theory]
    [InlineData(JourneyStatus.Unassigned, true)]
    [InlineData(JourneyStatus.Scheduled, true)]
    [InlineData(JourneyStatus.Running, true)]
    [InlineData(JourneyStatus.Completed, false)]
    public async Task Only_a_completed_journey_is_left_out(JourneyStatus status, bool expected)
    {
        _queries.AddJourney(Journey8058, Service8058, Tuesday, status);

        Assert.Equal(expected, (await Find("VYG", "CF")).Response!.Trains.Any(t => t.TrainNumber == "8058"));
    }

    [Fact]
    public async Task A_journey_that_is_over_is_left_out_after_the_grace_period_and_not_before()
    {
        _queries.AddJourney(Journey8058, Service8058, Tuesday);

        // Expected end 08:55, grace 15 minutes: gone after 09:10. (A Scheduled journey would still be searchable from VYG until it leaves.)
        Assert.NotEmpty((await Find("VYG", "CF", hour: 9, minute: 10)).Response!.Trains);
        Assert.Empty((await Find("VYG", "CF", hour: 9, minute: 11)).Response!.Trains);
    }

    [Fact]
    public async Task The_delay_moves_the_end_of_a_journey_later()
    {
        _queries.AddJourney(Journey8058, Service8058, Tuesday, delay: 20);

        Assert.NotEmpty((await Find("VYG", "CF", hour: 9, minute: 30)).Response!.Trains); // 08:55 + 20 + 15 = 09:30
        Assert.Empty((await Find("VYG", "CF", hour: 9, minute: 31)).Response!.Trains);
    }

    [Fact]
    public async Task A_night_train_that_started_yesterday_is_still_offered_to_a_stop_it_has_not_reached()
    {
        var night = Service(new Guid("00000004-0000-4000-8000-0000000000aa"), "9900", "Slow", "Daily",
            ("VYG", null, new TimeOnly(23, 50)), ("GPH", new(0, 20), new(0, 20)), ("CF", new(8, 0), null));
        _queries.Services.Add(night);
        _queries.AddJourney(new Guid("00000005-0000-4000-8000-0000000000aa"), night.ServiceId, Tuesday, JourneyStatus.Running);

        // Wednesday 00:10 in Colombo: the Tuesday journey is running and has not reached Gampaha (00:20).
        var wednesday = Tuesday.AddDays(1);
        var result = await Find("GPH", "CF", hour: 0, minute: 10, today: wednesday);

        // Two correct answers for the same service: yesterday's run still in progress, and the next run (operating date Wednesday) from the timetable.
        Assert.Equal(2, result.Response!.Trains.Count(t => t.TrainNumber == "9900"));
        Assert.Single(result.Response.Trains, t => t.TrainNumber == "9900" && !t.Live && t.OperatingDate == wednesday);
        var train = result.Response.Trains.Single(t => t.TrainNumber == "9900" && t.Live);
        Assert.True(train.Live);
        Assert.Equal(Tuesday, train.OperatingDate);
        Assert.Equal(At(0, 20, wednesday), train.AtFrom.Departure!.Scheduled);
    }

    [Fact]
    public async Task A_live_journey_row_for_a_later_date_is_returned_as_live()
    {
        _queries.AddJourney(new Guid("00000005-0000-4000-8000-0000000000bb"), Service8058, Monday, JourneyStatus.Unassigned);

        var result = await Find("VYG", "CF", "2026-10-12", hour: 22, minute: 0);

        Assert.True(result.Response!.Trains.Single(t => t.TrainNumber == "8058").Live);
        Assert.False(result.Response.Trains.Single(t => t.TrainNumber == "8061").Live);
        Assert.Equal("mixed", result.Source);
    }

    // ---- R7: order and cap ----

    [Fact]
    public async Task Results_are_sorted_by_estimated_departure_then_train_number()
    {
        _queries.AddJourney(Journey8058, Service8058, Tuesday, delay: 40); // 08:20 + 40 = 09:00 estimated
        _queries.AddJourney(Journey8061, Service8061, Tuesday);
        var late = _queries.Journeys.Single(j => j.JourneyId == Journey8058);
        _queries.Journeys[_queries.Journeys.IndexOf(late)] = late with
        {
            StopStates = late.StopStates.Select(s => s with { EstimatedDeparture = s.EstimatedDeparture + TimeSpan.FromMinutes(40) }).ToList(),
        };

        var result = await Find("VYG", "CF", hour: 7, minute: 0);

        Assert.Equal(["8061", "8058"], Numbers(result)); // 07:50 then 09:00
    }

    [Fact]
    public async Task The_number_of_results_is_capped_after_sorting()
    {
        _search.MaxResults = 1;

        var result = await Find("VYG", "CF", hour: 7, minute: 0);

        Assert.Equal(["8061"], Numbers(result)); // the earliest departure survives
    }

    [Fact]
    public async Task The_source_says_where_the_answers_came_from()
    {
        Assert.Equal("timetable", (await Find("VYG", "CF", hour: 7, minute: 0)).Source);
        _queries.AddJourney(Journey8058, Service8058, Tuesday);
        _queries.AddJourney(Journey8061, Service8061, Tuesday);
        Assert.Equal("live", (await Find("VYG", "CF", hour: 7, minute: 0)).Source);
        Assert.Equal("none", (await Find("GPH", "VYG", hour: 7, minute: 0)).Source);
    }
}

/// <summary>Spec R9: the line banner, and the freshness field that is not the clock.</summary>
public class LineStatusHandlerTests
{
    private readonly FakePassengerQueries _queries = new();
    private readonly FakeTimeProvider _clock = new();
    private readonly JourneySettings _journeys = new();

    private static DateTimeOffset At(int hour, int minute, int second = 0) =>
        OperatingTime.ToInstant(Tuesday, new TimeOnly(hour, minute, second));

    private async Task<LineStatusResponse?> Status(int hour, int minute, string code = "MAIN")
    {
        _clock.SetUtcNow(At(hour, minute));
        return await new GetLineStatusHandler(_queries, _clock, _journeys).HandleAsync(code, CancellationToken.None);
    }

    [Fact]
    public async Task With_no_journey_in_progress_the_line_is_normal_with_nothing_to_age()
    {
        var status = await Status(2, 0);

        Assert.Equal("Normal", status!.StatusKey);
        Assert.Equal("Normal", status.StatusGroup);
        Assert.Equal(0, status.JourneysInProgress);
        Assert.Equal(0, status.DelayedJourneys);
        Assert.Null(status.LastEventAt);
        Assert.Equal(At(2, 0), status.ServerTime);
    }

    [Fact]
    public async Task A_journey_that_has_not_started_yet_is_not_in_progress()
    {
        _queries.AddJourney(Journey8058, Service8058, Tuesday);

        Assert.Equal(0, (await Status(8, 0))!.JourneysInProgress);
        Assert.Equal(1, (await Status(8, 20))!.JourneysInProgress);
    }

    [Fact]
    public async Task On_time_journeys_keep_the_line_normal()
    {
        _queries.AddJourney(Journey8058, Service8058, Tuesday);
        _queries.AddJourney(Journey8061, Service8061, Tuesday);

        var status = await Status(8, 25);

        Assert.Equal(2, status!.JourneysInProgress);
        Assert.Equal("Normal", status.StatusKey);
    }

    [Fact]
    public async Task One_delayed_journey_makes_the_line_some_delayed()
    {
        _queries.AddJourney(Journey8058, Service8058, Tuesday);
        _queries.AddJourney(Journey8061, Service8061, Tuesday);
        _queries.Report(Journey8058, 1, At(8, 28), 8, At(8, 28, 5));

        var status = await Status(8, 30);

        Assert.Equal("SomeDelayed", status!.StatusKey);
        Assert.Equal("Warning", status.StatusGroup);
        Assert.Equal(2, status.JourneysInProgress);
        Assert.Equal(1, status.DelayedJourneys);
    }

    [Theory]
    [InlineData(2, "Normal")]
    [InlineData(3, "SomeDelayed")]
    public async Task The_on_time_tolerance_decides_the_boundary(int delay, string expected)
    {
        _queries.AddJourney(Journey8058, Service8058, Tuesday);
        _queries.Report(Journey8058, 1, At(8, 20).AddMinutes(delay), delay, At(8, 25));

        Assert.Equal(expected, (await Status(8, 30))!.StatusKey);
    }

    [Fact]
    public async Task The_last_event_time_is_the_latest_report_and_does_not_move_with_the_clock()
    {
        _queries.AddJourney(Journey8058, Service8058, Tuesday);
        _queries.AddJourney(Journey8061, Service8061, Tuesday);
        _queries.Report(Journey8058, 1, At(8, 28), 8, At(8, 28, 5));
        _queries.Report(Journey8061, 1, At(7, 52), 2, At(7, 52, 30));

        var early = await Status(8, 30);
        var late = await Status(8, 50);

        Assert.Equal(At(8, 28, 5), early!.LastEventAt);
        Assert.Equal(early.LastEventAt, late!.LastEventAt);
        Assert.NotEqual(early.ServerTime, late.ServerTime);
    }

    [Fact]
    public async Task A_journey_with_no_report_gives_no_last_event_time()
    {
        _queries.AddJourney(Journey8058, Service8058, Tuesday);

        Assert.Null((await Status(8, 25))!.LastEventAt);
    }

    [Fact]
    public async Task A_completed_or_over_journey_is_not_counted()
    {
        _queries.AddJourney(Journey8058, Service8058, Tuesday, JourneyStatus.Completed);
        _queries.AddJourney(Journey8061, Service8061, Tuesday);

        Assert.Equal(1, (await Status(8, 25))!.JourneysInProgress);
        Assert.Equal(0, (await Status(8, 46))!.JourneysInProgress); // 8061 ends 08:30, grace until 08:45
    }

    [Fact]
    public async Task The_names_come_in_three_languages_and_an_unknown_line_is_null()
    {
        var status = await Status(8, 0);

        Assert.Equal("Main Line", status!.LineName.En);
        Assert.Equal("ප්‍රධාන මාර්ගය", status.LineName.Si);
        Assert.Equal("Main Line", status.LineName.Ta); // no Tamil name in the fake: the English fallback
        Assert.Null(await Status(8, 0, "NOPE"));
        Assert.NotNull(await Status(8, 0, "main"));
    }
}
