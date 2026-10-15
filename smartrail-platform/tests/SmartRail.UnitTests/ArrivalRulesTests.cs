using SmartRail.Domain;
using SmartRail.Domain.Journeys;
using Xunit;

namespace SmartRail.UnitTests;

/// <summary>Spec 06, slice A1: the pure rules for arrival reports and Completed. No database.</summary>
public class ArrivalRulesTests
{
    private static readonly DateOnly Tuesday = new(2026, 10, 6);
    private static readonly Guid Veyangoda = Guid.NewGuid();
    private static readonly Guid Gampaha = Guid.NewGuid();
    private static readonly Guid Ragama = Guid.NewGuid();
    private static readonly Guid Maradana = Guid.NewGuid();
    private static readonly Guid ColomboFort = Guid.NewGuid();

    private static IReadOnlyList<StopSchedule> Train8058() => ScheduleInstants.For(Tuesday,
    [
        new StopTimes(Veyangoda, 1, null, new(8, 20)),
        new StopTimes(Gampaha, 2, new(8, 30), new(8, 31)),
        new StopTimes(Ragama, 3, new(8, 42), new(8, 43)),
        new StopTimes(Maradana, 4, new(8, 50), new(8, 51)),
        new StopTimes(ColomboFort, 5, new(8, 55), null),
    ]);

    private static DateTimeOffset At(int hour, int minute) => OperatingTime.ToInstant(Tuesday, new TimeOnly(hour, minute));

    private static ReportedStopEvent Departed(Guid stop, DateTimeOffset at) => new(stop, ReportKind.Departed, at);

    private static ReportedStopEvent Arrived(Guid stop, DateTimeOffset at) => new(stop, ReportKind.Arrived, at);

    // ---- order of reports (R3) ----

    [Fact]
    public void An_arrival_between_the_previous_departure_and_the_stops_own_departure_is_in_order()
    {
        var existing = new[] { Departed(Veyangoda, At(8, 28)), Departed(Gampaha, At(8, 41)) };

        Assert.Equal(ReportOrderResult.Ok, JourneyProgress.CheckOrder(Train8058(), existing, Arrived(Gampaha, At(8, 40))));
    }

    [Fact]
    public void An_arrival_after_the_stops_own_departure_is_out_of_order()
    {
        var existing = new[] { Departed(Gampaha, At(8, 41)) };

        Assert.Equal(ReportOrderResult.NotStrictlyIncreasing, JourneyProgress.CheckOrder(Train8058(), existing, Arrived(Gampaha, At(8, 45))));
    }

    [Fact]
    public void An_arrival_before_the_previous_stops_departure_is_out_of_order()
    {
        var existing = new[] { Departed(Veyangoda, At(8, 28)) };

        Assert.Equal(ReportOrderResult.NotStrictlyIncreasing, JourneyProgress.CheckOrder(Train8058(), existing, Arrived(Gampaha, At(8, 27))));
    }

    [Fact]
    public void An_arrival_and_a_departure_at_the_same_moment_are_out_of_order()
    {
        var existing = new[] { Arrived(Gampaha, At(8, 40)) };

        Assert.Equal(ReportOrderResult.NotStrictlyIncreasing, JourneyProgress.CheckOrder(Train8058(), existing, Departed(Gampaha, At(8, 40))));
    }

    [Fact]
    public void The_same_stop_and_kind_with_another_time_is_a_conflict_but_another_kind_is_not()
    {
        var existing = new[] { Arrived(Gampaha, At(8, 40)) };

        Assert.Equal(ReportOrderResult.ConflictingTimeForStop, JourneyProgress.CheckOrder(Train8058(), existing, Arrived(Gampaha, At(8, 42))));
        Assert.Equal(ReportOrderResult.Ok, JourneyProgress.CheckOrder(Train8058(), existing, Departed(Gampaha, At(8, 42))));
    }

    [Fact]
    public void Skipping_stops_is_allowed_when_the_times_still_increase()
    {
        var existing = new[] { Departed(Veyangoda, At(8, 28)) };

        Assert.Equal(ReportOrderResult.Ok, JourneyProgress.CheckOrder(Train8058(), existing, Arrived(ColomboFort, At(9, 5))));
    }

    // ---- the rebuild (R4, R5, R10) ----

    [Fact]
    public void An_arrival_sets_the_delay_from_the_scheduled_arrival_and_moves_every_later_stop()
    {
        var state = JourneyProgress.Compute(Train8058(), [Departed(Veyangoda, At(8, 28)), Arrived(Gampaha, At(8, 40))], 2);

        Assert.Equal(10, state.DelayMinutes);
        Assert.Equal(JourneyStatusKey.Delayed, state.StatusKey);
        var gampaha = state.Stops.Single(s => s.ServiceStopId == Gampaha);
        Assert.Equal(StopConfirmation.Reported, gampaha.Confirmation);
        Assert.Equal(At(8, 40), gampaha.ActualArrival);
        Assert.Null(gampaha.ActualDeparture);
        Assert.Equal(At(8, 41), gampaha.EstimatedDeparture);
        Assert.Equal(At(8, 52), state.Stops.Single(s => s.ServiceStopId == Ragama).EstimatedArrival);
        Assert.Equal(At(9, 5), state.Stops.Single(s => s.ServiceStopId == ColomboFort).EstimatedArrival);
        Assert.False(state.IsCompleted);
    }

    [Fact]
    public void An_early_arrival_gives_a_delay_of_zero_and_the_departure_stays_on_schedule()
    {
        var state = JourneyProgress.Compute(Train8058(), [Arrived(Gampaha, At(8, 28))], 2);

        Assert.Equal(0, state.DelayMinutes);
        Assert.Equal(JourneyStatusKey.OnTime, state.StatusKey);
        Assert.Equal(At(8, 31), state.Stops.Single(s => s.ServiceStopId == Gampaha).EstimatedDeparture);
    }

    [Fact]
    public void A_departure_after_the_arrival_keeps_both_times_and_takes_the_delay_from_the_departure()
    {
        var state = JourneyProgress.Compute(Train8058(), [Arrived(Gampaha, At(8, 40)), Departed(Gampaha, At(8, 43))], 2);

        var gampaha = state.Stops.Single(s => s.ServiceStopId == Gampaha);
        Assert.Equal(At(8, 40), gampaha.ActualArrival);
        Assert.Equal(At(8, 43), gampaha.ActualDeparture);
        Assert.Null(gampaha.EstimatedDeparture);
        Assert.Equal(12, state.DelayMinutes);
    }

    [Fact]
    public void The_order_the_reports_arrive_in_never_changes_the_result()
    {
        var events = new[] { Departed(Veyangoda, At(8, 28)), Arrived(Gampaha, At(8, 40)), Departed(Gampaha, At(8, 41)) };

        var inOrder = JourneyProgress.Compute(Train8058(), events, 2);
        var shuffled = JourneyProgress.Compute(Train8058(), [events[2], events[0], events[1]], 2);

        Assert.Equal(inOrder.DelayMinutes, shuffled.DelayMinutes);
        Assert.Equal(inOrder.Stops, shuffled.Stops);
    }

    [Fact]
    public void A_stop_skipped_before_an_arrival_is_inferred()
    {
        var state = JourneyProgress.Compute(Train8058(), [Arrived(ColomboFort, At(9, 5))], 2);

        Assert.All(state.Stops.Where(s => s.Sequence < 5), s => Assert.Equal(StopConfirmation.Inferred, s.Confirmation));
    }

    // ---- Completed (R10) ----

    [Fact]
    public void An_arrival_at_the_final_stop_completes_the_journey()
    {
        var state = JourneyProgress.Compute(Train8058(), [Departed(Maradana, At(8, 59)), Arrived(ColomboFort, At(9, 5))], 2);

        Assert.True(state.IsCompleted);
        Assert.Equal(JourneyStatusKey.Completed, state.StatusKey);
        Assert.Equal(StatusGroup.Normal, state.StatusGroup);
        Assert.Null(state.NextStopId);
    }

    [Fact]
    public void A_departure_from_the_last_stop_but_one_does_not_complete_it()
    {
        var state = JourneyProgress.Compute(Train8058(), [Departed(Maradana, At(8, 59))], 2);

        Assert.False(state.IsCompleted);
        Assert.Equal(ColomboFort, state.NextStopId);
    }

    // ---- the train lookup (spec 06 R11) ----

    private static JourneyWindow Window(Guid id, DateOnly day, DateTimeOffset start, DateTimeOffset end, bool completed = false) =>
        new(id, day, start, end, completed);

    [Fact]
    public void A_completed_journey_is_not_in_progress_so_it_cannot_hide_the_next_days_journey()
    {
        var thursday = new DateOnly(2026, 10, 8);
        var friday = thursday.AddDays(1);
        var nightTrain = Guid.NewGuid();
        var fridaysTrain = Guid.NewGuid();
        var now = OperatingTime.ToInstant(friday, new TimeOnly(8, 10));

        var chosen = JourneySelection.Select(
        [
            Window(nightTrain, thursday, OperatingTime.ToInstant(thursday, new TimeOnly(23, 50)), OperatingTime.ToInstant(friday, new TimeOnly(8, 0)), completed: true),
            Window(fridaysTrain, friday, OperatingTime.ToInstant(friday, new TimeOnly(8, 5)), OperatingTime.ToInstant(friday, new TimeOnly(9, 0))),
        ], friday, now, TimeSpan.FromMinutes(15));

        Assert.Equal(fridaysTrain, chosen);
    }

    [Fact]
    public void A_finished_night_train_from_yesterday_is_not_shown_when_nothing_else_runs_today()
    {
        var thursday = new DateOnly(2026, 10, 8);
        var friday = thursday.AddDays(1);
        var nightTrain = Guid.NewGuid();
        var now = OperatingTime.ToInstant(friday, new TimeOnly(8, 10));

        var chosen = JourneySelection.Select(
        [
            Window(nightTrain, thursday, OperatingTime.ToInstant(thursday, new TimeOnly(23, 50)), OperatingTime.ToInstant(friday, new TimeOnly(8, 0)), completed: true),
        ], friday, now, TimeSpan.FromMinutes(15));

        Assert.Null(chosen);
    }

    [Fact]
    public void A_train_running_later_than_its_last_estimate_is_still_returned_while_it_is_not_completed()
    {
        var running = Guid.NewGuid();
        var now = OperatingTime.ToInstant(Tuesday, new TimeOnly(9, 5));

        var chosen = JourneySelection.Select(
            [Window(running, Tuesday, At(8, 20), At(8, 55))], Tuesday, now, TimeSpan.FromMinutes(15));

        Assert.Equal(running, chosen);
    }

    [Fact]
    public void A_completed_journey_of_today_is_still_returned_by_the_today_step()
    {
        var done = Guid.NewGuid();

        var chosen = JourneySelection.Select(
            [Window(done, Tuesday, At(8, 20), At(8, 55), completed: true)], Tuesday, At(9, 6), TimeSpan.FromMinutes(15));

        Assert.Equal(done, chosen);
    }

    // ---- what a passenger search treats as "already left" ----

    [Fact]
    public void A_train_that_has_only_arrived_at_a_stop_has_not_left_it()
    {
        var marks = new[] { new StopMark(1, StopConfirmation.Reported), new StopMark(2, StopConfirmation.Reported, Left: false) };

        Assert.True(StopPassed.HasDeparted(marks, 1));
        Assert.False(StopPassed.HasDeparted(marks, 2));
    }

    [Fact]
    public void A_train_at_a_later_stop_has_left_every_earlier_one_even_if_only_its_arrival_was_reported()
    {
        var marks = new[] { new StopMark(3, StopConfirmation.Reported, Left: false) };

        Assert.True(StopPassed.HasDeparted(marks, 2));
        Assert.False(StopPassed.HasDeparted(marks, 3));
    }

    [Fact]
    public void A_skipped_stop_counts_as_left()
    {
        Assert.True(StopPassed.HasDeparted([new StopMark(2, StopConfirmation.Inferred, Left: false)], 2));
    }
}
