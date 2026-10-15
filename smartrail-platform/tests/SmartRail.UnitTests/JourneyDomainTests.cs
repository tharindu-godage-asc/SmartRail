using SmartRail.Domain;
using SmartRail.Domain.Journeys;
using Xunit;

namespace SmartRail.UnitTests;

/// <summary>R4 to R6, R10, R15 and R16 of _specs/03-operator-report-live-status.md: pure rules, no database.</summary>
public class JourneyDomainTests
{
    private static readonly DateOnly Tuesday = new(2026, 10, 6);
    private static readonly Guid Veyangoda = Guid.NewGuid();
    private static readonly Guid Gampaha = Guid.NewGuid();
    private static readonly Guid Ragama = Guid.NewGuid();
    private static readonly Guid Maradana = Guid.NewGuid();
    private static readonly Guid ColomboFort = Guid.NewGuid();

    /// <summary>Train 8058 exactly as seeded (01-database-foundation section 7).</summary>
    private static IReadOnlyList<StopSchedule> Train8058() => ScheduleInstants.For(Tuesday,
    [
        new StopTimes(Veyangoda, 1, null, new(8, 20)),
        new StopTimes(Gampaha, 2, new(8, 30), new(8, 31)),
        new StopTimes(Ragama, 3, new(8, 42), new(8, 43)),
        new StopTimes(Maradana, 4, new(8, 50), new(8, 51)),
        new StopTimes(ColomboFort, 5, new(8, 55), null),
    ]);

    private static DateTimeOffset Local(int hour, int minute, int second = 0, DateOnly? date = null) =>
        OperatingTime.ToInstant(date ?? Tuesday, new TimeOnly(hour, minute, second));

    private static ReportedStopEvent Departed(Guid stop, DateTimeOffset at) => new(stop, ReportKind.Departed, at);

    private static ReportedStopEvent Arrived(Guid stop, DateTimeOffset at) => new(stop, ReportKind.Arrived, at);

    // ---- R16 scheduled instants ----

    [Fact]
    public void A_day_train_keeps_its_operating_date()
    {
        var schedule = Train8058();

        Assert.Equal(Local(8, 20), schedule[0].Departure);
        Assert.Equal(Local(8, 55), schedule[4].Arrival);
        Assert.Equal(new DateTimeOffset(2026, 10, 6, 2, 50, 0, TimeSpan.Zero), schedule[0].Departure);
    }

    [Fact]
    public void A_night_train_moves_stops_after_midnight_to_the_next_day()
    {
        var thursday = new DateOnly(2026, 10, 8);
        var friday = thursday.AddDays(1);
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        var c = Guid.NewGuid();

        var schedule = ScheduleInstants.For(thursday,
        [
            new StopTimes(a, 1, null, new(23, 50)),
            new StopTimes(b, 2, new(0, 20), new(0, 21)),
            new StopTimes(c, 3, new(8, 0), null),
        ]);

        Assert.Equal(Local(23, 50, date: thursday), schedule[0].Departure);
        Assert.Equal(Local(0, 20, date: friday), schedule[1].Arrival);
        Assert.Equal(Local(0, 21, date: friday), schedule[1].Departure);
        Assert.Equal(Local(8, 0, date: friday), schedule[2].Arrival);
    }

    [Fact]
    public void A_24_hour_journey_ends_exactly_one_day_after_it_starts()
    {
        var start = Guid.NewGuid();
        var middle = Guid.NewGuid();
        var end = Guid.NewGuid();

        var schedule = ScheduleInstants.For(Tuesday,
        [
            new StopTimes(start, 1, null, new(23, 50)),
            new StopTimes(middle, 2, new(6, 0), new(6, 5)),
            new StopTimes(end, 3, new(23, 50), null),
        ]);

        Assert.Equal(TimeSpan.FromHours(24), schedule[2].Arrival - schedule[0].Departure);
    }

    [Fact]
    public void A_departure_earlier_than_its_arrival_at_the_same_stop_is_on_the_next_day()
    {
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        var c = Guid.NewGuid();

        var schedule = ScheduleInstants.For(Tuesday,
        [
            new StopTimes(a, 1, null, new(23, 30)),
            new StopTimes(b, 2, new(23, 55), new(0, 10)),
            new StopTimes(c, 3, new(1, 0), null),
        ]);

        Assert.Equal(Local(0, 10, date: Tuesday.AddDays(1)), schedule[1].Departure);
        Assert.Equal(Local(1, 0, date: Tuesday.AddDays(1)), schedule[2].Arrival);
    }

    // ---- R5 delay ----

    [Fact]
    public void Eight_minutes_late_carries_to_every_later_stop()
    {
        var state = JourneyProgress.Compute(Train8058(), [Departed(Veyangoda, Local(8, 28))], 2);

        Assert.Equal(8, state.DelayMinutes);
        var byStop = state.Stops.ToDictionary(s => s.ServiceStopId);
        Assert.Equal(Local(8, 38), byStop[Gampaha].EstimatedArrival);
        Assert.Equal(Local(8, 50), byStop[Ragama].EstimatedArrival);
        Assert.Equal(Local(8, 58), byStop[Maradana].EstimatedArrival);
        Assert.Equal(Local(9, 3), byStop[ColomboFort].EstimatedArrival);
        Assert.Equal(StopConfirmation.Reported, byStop[Veyangoda].Confirmation);
        Assert.Equal(Local(8, 28), byStop[Veyangoda].ActualDeparture);
        Assert.Equal(Gampaha, state.NextStopId);
        Assert.Equal(Veyangoda, state.LastReportedStopId);
    }

    [Theory]
    [InlineData(8, 30, 9)] // 8 minutes 30 seconds late: the half rounds away from zero
    [InlineData(7, 29, 7)] // 7 minutes 29 seconds late rounds down
    [InlineData(0, 30, 1)] // 30 seconds late rounds up to a minute
    [InlineData(0, -30, 0)] // 30 seconds early rounds to -1, then the clamp makes it 0
    [InlineData(-5, 0, 0)] // five minutes early is accepted with delay 0
    public void Delay_is_rounded_away_from_zero_and_then_never_negative(int minutes, int seconds, int expected)
    {
        var scheduled = Local(8, 20);
        var actual = scheduled + TimeSpan.FromMinutes(minutes) + TimeSpan.FromSeconds(seconds);

        Assert.Equal(expected, JourneyProgress.DelayMinutes(actual, scheduled));
    }

    // ---- R6 status ----

    [Theory]
    [InlineData(0, JourneyStatusKey.OnTime, StatusGroup.Normal)]
    [InlineData(2, JourneyStatusKey.OnTime, StatusGroup.Normal)]
    [InlineData(3, JourneyStatusKey.Delayed, StatusGroup.Warning)]
    [InlineData(8, JourneyStatusKey.Delayed, StatusGroup.Warning)]
    public void On_time_up_to_the_tolerance_and_delayed_beyond_it(int delay, JourneyStatusKey key, StatusGroup group)
    {
        Assert.Equal((key, group), JourneyProgress.StatusFor(true, delay, 2));
    }

    [Fact]
    public void A_journey_with_no_report_is_scheduled_and_normal_with_scheduled_estimates()
    {
        var state = JourneyProgress.Compute(Train8058(), [], 2);

        Assert.Equal((JourneyStatusKey.Scheduled, StatusGroup.Normal), (state.StatusKey, state.StatusGroup));
        Assert.Equal(0, state.DelayMinutes);
        Assert.All(state.Stops, stop => Assert.Equal(StopConfirmation.Pending, stop.Confirmation));
        Assert.Equal(Local(8, 30), state.Stops.Single(s => s.ServiceStopId == Gampaha).EstimatedArrival);
        Assert.Null(state.NextStopId);
        Assert.Null(state.LastReportedStopId);
    }

    [Fact]
    public void An_early_departure_gives_delay_zero_and_estimates_equal_to_the_schedule()
    {
        var state = JourneyProgress.Compute(Train8058(), [Departed(Veyangoda, Local(8, 15))], 2);

        Assert.Equal(0, state.DelayMinutes);
        Assert.Equal(JourneyStatusKey.OnTime, state.StatusKey);
        Assert.Equal(Local(8, 30), state.Stops.Single(s => s.ServiceStopId == Gampaha).EstimatedArrival);
    }

    // ---- R4 and R10 order and rebuild ----

    [Fact]
    public void A_forward_skip_marks_the_stops_in_between_inferred_with_no_estimate()
    {
        var state = JourneyProgress.Compute(Train8058(), [Departed(Ragama, Local(8, 52))], 2);

        var byStop = state.Stops.ToDictionary(s => s.ServiceStopId);
        Assert.Equal(StopConfirmation.Inferred, byStop[Veyangoda].Confirmation);
        Assert.Equal(StopConfirmation.Inferred, byStop[Gampaha].Confirmation);
        Assert.Null(byStop[Gampaha].EstimatedArrival);
        Assert.Null(byStop[Gampaha].ActualDeparture);
        Assert.Equal(StopConfirmation.Reported, byStop[Ragama].Confirmation);
        Assert.Equal(9, state.DelayMinutes); // 08:52 against 08:43
        Assert.Equal(Local(8, 59), byStop[Maradana].EstimatedArrival); // 08:50 plus 9
    }

    [Fact]
    public void The_arrival_order_of_reports_never_changes_the_result()
    {
        var inOrder = JourneyProgress.Compute(Train8058(),
            [Departed(Veyangoda, Local(8, 28)), Departed(Ragama, Local(8, 52))], 2);
        var lateDelivery = JourneyProgress.Compute(Train8058(),
            [Departed(Ragama, Local(8, 52)), Departed(Veyangoda, Local(8, 28))], 2);

        Assert.Equal(inOrder.DelayMinutes, lateDelivery.DelayMinutes);
        Assert.Equal(inOrder.Stops, lateDelivery.Stops);
        Assert.Equal(StopConfirmation.Inferred, inOrder.Stops.Single(s => s.ServiceStopId == Gampaha).Confirmation);
    }

    [Fact]
    public void The_journey_delay_comes_from_the_latest_departure_in_occurrence_order()
    {
        var state = JourneyProgress.Compute(Train8058(),
            [Departed(Veyangoda, Local(8, 28)), Departed(Gampaha, Local(8, 33))], 2);

        Assert.Equal(2, state.DelayMinutes); // 08:33 against 08:31, not the 8 minutes from Veyangoda
        Assert.Equal(JourneyStatusKey.OnTime, state.StatusKey);
    }

    [Fact]
    public void A_late_but_consistent_report_is_accepted()
    {
        var existing = new[] { Departed(Ragama, Local(8, 52)) };

        Assert.Equal(ReportOrderResult.Ok, JourneyProgress.CheckOrder(Train8058(), existing, Departed(Veyangoda, Local(8, 28))));
    }

    [Fact]
    public void The_same_stop_with_a_different_time_conflicts()
    {
        var existing = new[] { Departed(Veyangoda, Local(8, 28)) };

        Assert.Equal(ReportOrderResult.ConflictingTimeForStop,
            JourneyProgress.CheckOrder(Train8058(), existing, Departed(Veyangoda, Local(8, 29))));
    }

    [Fact]
    public void An_equal_time_at_a_different_stop_is_not_strictly_increasing()
    {
        var existing = new[] { Departed(Veyangoda, Local(8, 28)) };

        Assert.Equal(ReportOrderResult.NotStrictlyIncreasing,
            JourneyProgress.CheckOrder(Train8058(), existing, Departed(Gampaha, Local(8, 28))));
    }

    [Fact]
    public void A_later_stop_with_an_earlier_time_is_not_strictly_increasing()
    {
        var existing = new[] { Departed(Gampaha, Local(8, 35)) };

        Assert.Equal(ReportOrderResult.NotStrictlyIncreasing,
            JourneyProgress.CheckOrder(Train8058(), existing, Departed(Ragama, Local(8, 34))));
    }

    [Fact]
    public void An_earlier_stop_reported_later_than_a_later_stop_is_not_strictly_increasing()
    {
        var existing = new[] { Departed(Ragama, Local(8, 44)) };

        Assert.Equal(ReportOrderResult.NotStrictlyIncreasing,
            JourneyProgress.CheckOrder(Train8058(), existing, Departed(Veyangoda, Local(8, 50))));
    }

    // ---- R15 which journey a passenger sees ----

    private static JourneyWindow Window(Guid id, DateOnly date, DateTimeOffset start, DateTimeOffset end) => new(id, date, start, end);

    private static readonly TimeSpan Grace = TimeSpan.FromMinutes(15);

    [Fact]
    public void An_overnight_train_is_still_selected_after_midnight_and_at_08_00()
    {
        var thursday = new DateOnly(2026, 10, 8);
        var friday = thursday.AddDays(1);
        var thursdaysTrain = Guid.NewGuid();
        var window = Window(thursdaysTrain, thursday, Local(23, 0, date: thursday), Local(8, 30, date: friday));

        foreach (var (hour, minute) in new[] { (0, 10), (3, 0), (8, 0) })
        {
            var now = Local(hour, minute, date: friday);
            Assert.Equal(thursdaysTrain, JourneySelection.Select([window], friday, now, Grace));
        }
    }

    [Fact]
    public void The_grace_period_after_the_expected_end_is_inclusive_and_one_minute_later_it_is_over()
    {
        var friday = new DateOnly(2026, 10, 9);
        var id = Guid.NewGuid();
        var thursday = friday.AddDays(-1);
        var end = Local(8, 30, date: friday);
        var window = Window(id, thursday, Local(23, 0, date: thursday), end);

        Assert.Equal(id, JourneySelection.Select([window], friday, end + Grace, Grace));
        Assert.Null(JourneySelection.Select([window], friday, end + Grace + TimeSpan.FromMinutes(1), Grace));
    }

    [Fact]
    public void When_the_overnight_train_is_over_the_same_trains_journey_for_today_takes_over()
    {
        var thursday = new DateOnly(2026, 10, 8);
        var friday = thursday.AddDays(1);
        var thursdays = Window(Guid.NewGuid(), thursday, Local(23, 0, date: thursday), Local(8, 30, date: friday));
        var fridays = Window(Guid.NewGuid(), friday, Local(23, 0, date: friday), Local(8, 30, date: friday.AddDays(1)));

        var now = Local(9, 0, date: friday);

        Assert.Equal(fridays.JourneyId, JourneySelection.Select([thursdays, fridays], friday, now, Grace));
    }

    [Fact]
    public void Two_journeys_in_progress_choose_the_one_that_started_later()
    {
        var friday = new DateOnly(2026, 10, 9);
        var earlier = Window(Guid.NewGuid(), friday.AddDays(-1), Local(23, 0, date: friday.AddDays(-1)), Local(23, 30, date: friday));
        var later = Window(Guid.NewGuid(), friday, Local(22, 0, date: friday), Local(23, 59, date: friday));

        Assert.Equal(later.JourneyId, JourneySelection.Select([earlier, later], friday, Local(23, 15, date: friday), Grace));
    }

    [Fact]
    public void A_day_train_that_finished_is_still_todays_journey_until_the_date_changes()
    {
        var id = Guid.NewGuid();
        var window = Window(id, Tuesday, Local(8, 20), Local(8, 55));

        Assert.Equal(id, JourneySelection.Select([window], Tuesday, Local(15, 0), Grace));
        Assert.Equal(id, JourneySelection.Select([window], Tuesday, Local(8, 0), Grace)); // not started: upcoming, for planning
    }

    [Fact]
    public void After_midnight_a_finished_day_train_with_no_journey_today_is_not_shown()
    {
        var window = Window(Guid.NewGuid(), Tuesday, Local(8, 20), Local(8, 55));
        var wednesday = Tuesday.AddDays(1);

        Assert.Null(JourneySelection.Select([window], wednesday, Local(0, 1, date: wednesday), Grace));
    }

    [Fact]
    public void No_candidates_means_no_journey()
    {
        Assert.Null(JourneySelection.Select([], Tuesday, Local(9, 0), Grace));
    }
}
