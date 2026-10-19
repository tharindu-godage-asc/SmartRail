using SmartRail.Domain.Journeys;
using SmartRail.Domain.Scheduling;
using Xunit;

namespace SmartRail.UnitTests;

/// <summary>Spec 13 section 6: the days a service runs and the rules a new service must meet.</summary>
public sealed class SchedulingTests
{
    // The line as seeded: Colombo Fort is sequence 1, Veyangoda 5. Platforms 1 and 2 everywhere.
    private static readonly IReadOnlyList<LineStationInfo> Line =
    [
        new("CF", "Colombo Fort", 1, ["1", "2"]),
        new("MDA", "Maradana", 2, ["1", "2"]),
        new("RGM", "Ragama", 3, ["1", "2"]),
        new("GPH", "Gampaha", 4, ["1", "2"]),
        new("VYG", "Veyangoda", 5, ["1", "2"]),
    ];

    private static readonly DateOnly Today = new(2026, 10, 19);
    private static readonly TimeOnly Now = new(9, 0);

    private static ServiceDraft Weekly(params StopDraft[] stops) => new(
        ServiceKind.Weekly, "8072", "MAIN", "Regional", "Express", ["Mon", "Tue", "Wed", "Thu", "Fri"], [], Today.AddDays(1), stops);

    private static readonly StopDraft[] GoodStops =
    [
        new("VYG", null, "17:25", "1"),
        new("GPH", "17:35", "17:36", "2"),
        new("CF", "17:58", null, "1"),
    ];

    private static ValidationResult Check(ServiceDraft draft, IReadOnlyList<ExistingService>? others = null) =>
        ServiceValidator.Validate(draft, Line, others ?? [], Today, Now, 10);

    private static string[] Codes(ValidationResult result) => result.Errors.Select(e => e.Code).ToArray();

    // ---- the days ----

    [Theory]
    [InlineData("Daily", 7)]
    [InlineData("Weekdays", 5)]
    [InlineData("Weekends", 2)]
    [InlineData("Mon,Wed,Fri", 3)]
    [InlineData("mon, wed", 2)]
    public void A_runs_on_text_is_read(string text, int days) => Assert.Equal(days, RunDays.Parse(text)!.Count);

    [Theory]
    [InlineData("Holidays")]
    [InlineData("Mon,Funday")]
    [InlineData("")]
    [InlineData(null)]
    public void An_unknown_runs_on_text_is_never_guessed(string? text) => Assert.Null(RunDays.Parse(text));

    [Fact]
    public void Days_are_written_in_one_normal_form()
    {
        Assert.Equal("Daily", RunDays.Normalise(["Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat"]));
        Assert.Equal("Weekdays", RunDays.Normalise(["Fri", "Mon", "Tue", "Wed", "Thu"]));
        Assert.Equal("Weekends", RunDays.Normalise(["Sun", "Sat"]));
        Assert.Equal("Mon,Wed,Fri", RunDays.Normalise(["Fri", "Mon", "Wed"]));
        Assert.Null(RunDays.Normalise([]));
        Assert.Null(RunDays.Normalise(["Mon", "xyz"]));
    }

    [Fact]
    public void A_service_runs_only_inside_its_dates_and_on_its_days()
    {
        var from = new DateOnly(2026, 10, 20); // a Tuesday
        Assert.False(ServiceRuns.On("Daily", from, null, from.AddDays(-1)));
        Assert.True(ServiceRuns.On("Daily", from, null, from));
        Assert.False(ServiceRuns.On("Daily", from, from, from.AddDays(1)));
        Assert.True(ServiceRuns.On("Mon,Wed", from, null, new DateOnly(2026, 10, 21)));
        Assert.False(ServiceRuns.On("Mon,Wed", from, null, new DateOnly(2026, 10, 22)));
        Assert.Null(ServiceRuns.On("Holidays", from, null, from));
    }

    [Fact]
    public void The_older_calendar_reading_still_holds_and_reads_day_lists()
    {
        Assert.True(ServiceCalendar.Runs("Weekdays", new DateOnly(2026, 10, 19)));
        Assert.False(ServiceCalendar.Runs("Weekends", new DateOnly(2026, 10, 19)));
        Assert.True(ServiceCalendar.Runs("Mon,Fri", new DateOnly(2026, 10, 23)));
        Assert.Null(ServiceCalendar.Runs("Holidays", new DateOnly(2026, 10, 19)));
    }

    // ---- the rules ----

    [Fact]
    public void A_good_service_is_valid_with_no_warnings()
    {
        var result = Check(Weekly(GoodStops));

        Assert.True(result.Valid);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void Stops_out_of_line_order_are_refused_with_the_stop_named()
    {
        var result = Check(Weekly(new("VYG", null, "17:25", null), new("RGM", "17:40", "17:41", null), new("GPH", "17:50", "17:51", null), new("CF", "18:10", null, null)));

        Assert.Equal(["stops_not_in_line_order"], Codes(result));
        Assert.Equal("stops[2].stationCode", result.Errors[0].Field);
    }

    [Fact]
    public void A_time_going_backwards_is_refused()
    {
        var result = Check(Weekly(new("VYG", null, "17:25", null), new("GPH", "17:35", "17:36", null), new("RGM", "17:30", "17:31", null), new("CF", "18:00", null, null)));

        Assert.Equal(["times_not_increasing"], Codes(result));
    }

    [Fact]
    public void Arrival_must_be_before_departure_at_a_stop()
    {
        var result = Check(Weekly(new("VYG", null, "17:25", null), new("GPH", "17:40", "17:36", null), new("CF", "18:00", null, null)));

        Assert.Equal(["times_not_increasing"], Codes(result));
    }

    [Fact]
    public void A_platform_of_another_station_or_a_made_up_one_is_refused()
    {
        var result = Check(Weekly(new("VYG", null, "17:25", "9"), new("GPH", "17:35", "17:36", null), new("CF", "17:58", null, null)));

        Assert.Equal(["platform_not_at_station"], Codes(result));
    }

    [Fact]
    public void Fewer_than_two_stops_a_repeated_station_and_an_unknown_station_are_refused()
    {
        Assert.Equal(["stops_too_few"], Codes(Check(Weekly(new StopDraft("VYG", null, "17:25", null)))));
        Assert.Equal(["station_not_on_line"], Codes(Check(Weekly(new("VYG", null, "17:25", null), new("XXX", "17:35", null, null)))));
        Assert.Equal(["station_repeated"], Codes(Check(Weekly(new("VYG", null, "17:25", null), new("GPH", "17:35", "17:36", null), new("GPH", "17:40", null, null)))));
    }

    [Fact]
    public void The_first_stop_has_a_departure_only_and_the_last_an_arrival_only()
    {
        var result = Check(Weekly(new("VYG", "17:20", "17:25", null), new("CF", "17:58", "18:00", null)));

        Assert.Contains("first_stop_departure_only", Codes(result));
        Assert.Contains("last_stop_arrival_only", Codes(result));
    }

    [Fact]
    public void A_bad_train_number_no_days_and_a_past_date_are_refused()
    {
        var draft = Weekly(GoodStops) with { TrainNumber = "80 72!", Days = [], EffectiveFrom = Today.AddDays(-1) };

        var codes = Codes(Check(draft));

        Assert.Contains("train_number_invalid", codes);
        Assert.Contains("days_required", codes);
        Assert.Contains("date_in_past", codes);
    }

    [Fact]
    public void A_service_that_starts_today_must_leave_after_now()
    {
        var starting = Weekly(GoodStops) with { EffectiveFrom = Today };

        Assert.Empty(Codes(ServiceValidator.Validate(starting, Line, [], Today, new TimeOnly(9, 0), 10)));
        Assert.Equal(["starts_in_past"], Codes(ServiceValidator.Validate(starting, Line, [], Today, new TimeOnly(17, 30), 10)));
    }

    [Fact]
    public void An_extra_train_needs_dates_and_each_must_be_today_or_later()
    {
        var extra = Weekly(GoodStops) with { Kind = ServiceKind.Extra, Days = [], EffectiveFrom = null, Dates = [] };
        Assert.Equal(["dates_required"], Codes(Check(extra)));

        Assert.Equal(["date_in_past"], Codes(Check(extra with { Dates = [Today.AddDays(2), Today.AddDays(-1)] })));
        Assert.True(Check(extra with { Dates = [Today.AddDays(2), Today.AddDays(3)] }).Valid);
    }

    // ---- platform advice ----

    private static ExistingService Other(string platform, string time) => new(
        Guid.NewGuid(), "8065", "Weekdays", new DateOnly(2000, 1, 1), null,
        [new ExistingStop("GPH", platform, TimeOnly.Parse(time), TimeOnly.Parse(time).AddMinutes(1))]);

    [Fact]
    public void A_platform_used_by_another_train_within_ten_minutes_is_a_warning_not_an_error()
    {
        var result = Check(Weekly(GoodStops), [Other("2", "17:31")]);

        Assert.True(result.Valid);
        var warning = Assert.Single(result.Warnings);
        Assert.Equal("platform_close", warning.Code);
        Assert.Contains("8065", warning.Message);
    }

    [Fact]
    public void No_warning_when_the_time_is_far_the_platform_differs_or_the_days_do_not_meet()
    {
        Assert.Empty(Check(Weekly(GoodStops), [Other("2", "17:50")]).Warnings);
        Assert.Empty(Check(Weekly(GoodStops), [Other("1", "17:31")]).Warnings);
        var weekendOnly = Other("2", "17:31") with { RunsOn = "Weekends" };
        Assert.Empty(Check(Weekly(GoodStops), [weekendOnly]).Warnings);
    }
}
