using SmartRail.Application.Features.Events.ReportEvent;
using SmartRail.Application.Features.Journeys;
using SmartRail.Domain;
using SmartRail.Domain.Journeys;
using Xunit;

namespace SmartRail.UnitTests;

/// <summary>Spec 06, slice A2: the delay report. The rules that need no database, and the shape of the request.</summary>
public class DelayRulesTests
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

    private static ReportedDelay Total(int hour, int minute, int minutes) => new(At(hour, minute), minutes, false);

    private static ReportedDelay Unknown(int hour, int minute) => new(At(hour, minute), null, true);

    private static readonly IReadOnlyCollection<ReportedStopEvent> NoStops = [];

    private static readonly IReadOnlyCollection<ReportedDelay> NoDelays = [];

    // ---- the delay in force (R4) ----

    [Fact]
    public void With_no_report_at_all_the_delay_is_zero_and_known()
    {
        Assert.Equal(new DelayState(0, true), JourneyProgress.DelayInForce(Train8058(), NoStops, NoDelays));
    }

    [Fact]
    public void A_total_after_a_measured_delay_replaces_it_and_is_not_added_to_it()
    {
        // +8 measured at 08:28, then "total 10" at 08:35: the delay is 10, not 18 (D-07, AC-E04-03 4).
        var state = JourneyProgress.DelayInForce(Train8058(), [Departed(Veyangoda, At(8, 28))], [Total(8, 35, 10)]);

        Assert.Equal(new DelayState(10, true), state);
    }

    [Fact]
    public void A_later_measurement_overrides_an_earlier_estimate()
    {
        // The operator said 20; the train then really left Gampaha 5 minutes late. The measurement wins.
        var state = JourneyProgress.DelayInForce(Train8058(), [Departed(Gampaha, At(8, 36))], [Total(8, 33, 20)]);

        Assert.Equal(new DelayState(5, true), state);
    }

    [Fact]
    public void Unknown_keeps_the_number_but_marks_it_unknown()
    {
        var state = JourneyProgress.DelayInForce(Train8058(), [Departed(Veyangoda, At(8, 28))], [Unknown(8, 35)]);

        Assert.Equal(new DelayState(8, false), state);
    }

    [Fact]
    public void The_next_measured_or_numeric_report_makes_the_delay_known_again()
    {
        var afterMeasure = JourneyProgress.DelayInForce(Train8058(), [Departed(Veyangoda, At(8, 28)), Departed(Gampaha, At(8, 41))], [Unknown(8, 35)]);
        var afterTotal = JourneyProgress.DelayInForce(Train8058(), [Departed(Veyangoda, At(8, 28))], [Unknown(8, 35), Total(8, 40, 15)]);

        Assert.Equal(new DelayState(10, true), afterMeasure);
        Assert.Equal(new DelayState(15, true), afterTotal);
    }

    [Fact]
    public void An_early_arrival_measures_zero_never_a_negative_delay()
    {
        var state = JourneyProgress.DelayInForce(Train8058(), [Arrived(Gampaha, At(8, 28))], NoDelays);

        Assert.Equal(new DelayState(0, true), state);
    }

    [Fact]
    public void The_before_filter_counts_only_reports_made_earlier_than_the_given_moment()
    {
        var stops = new[] { Departed(Veyangoda, At(8, 28)) };
        var delays = new[] { Total(8, 35, 10) };

        Assert.Equal(new DelayState(8, true), JourneyProgress.DelayInForce(Train8058(), stops, delays, before: At(8, 35)));
        Assert.Equal(new DelayState(10, true), JourneyProgress.DelayInForce(Train8058(), stops, delays, before: At(8, 36)));
        Assert.Equal(new DelayState(0, true), JourneyProgress.DelayInForce(Train8058(), stops, delays, before: At(8, 28)));
    }

    [Fact]
    public void Half_a_minute_late_rounds_up_to_a_whole_minute()
    {
        var state = JourneyProgress.DelayInForce(Train8058(), [new(Veyangoda, ReportKind.Departed, At(8, 20).AddSeconds(30))], NoDelays);

        Assert.Equal(1, state.Minutes);
    }

    // ---- the rebuild with delay reports (R4, R5, R7, R8) ----

    [Fact]
    public void A_total_of_ten_after_eight_gives_Gampaha_at_0840_and_every_later_stop_moves_with_it()
    {
        var state = JourneyProgress.Compute(Train8058(), [Departed(Veyangoda, At(8, 28))], 2, [Total(8, 35, 10)]);

        Assert.Equal(10, state.DelayMinutes);
        Assert.True(state.DelayKnown);
        Assert.Equal(JourneyStatusKey.Delayed, state.StatusKey);
        Assert.Equal(At(8, 40), state.Stops.Single(s => s.ServiceStopId == Gampaha).EstimatedArrival);
        Assert.Equal(At(8, 52), state.Stops.Single(s => s.ServiceStopId == Ragama).EstimatedArrival);
        Assert.Equal(At(9, 0), state.Stops.Single(s => s.ServiceStopId == Maradana).EstimatedArrival);
        Assert.Equal(At(9, 5), state.Stops.Single(s => s.ServiceStopId == ColomboFort).EstimatedArrival);
    }

    [Fact]
    public void A_delay_reported_before_any_stop_report_moves_every_stop_and_marks_the_journey_delayed()
    {
        var state = JourneyProgress.Compute(Train8058(), NoStops, 2, [Total(8, 15, 12)]);

        Assert.Equal(12, state.DelayMinutes);
        Assert.Equal(JourneyStatusKey.Delayed, state.StatusKey);
        Assert.Equal(StatusGroup.Warning, state.StatusGroup);
        Assert.All(state.Stops, s => Assert.Equal(StopConfirmation.Pending, s.Confirmation));
        Assert.Equal(At(8, 32), state.Stops.Single(s => s.ServiceStopId == Veyangoda).EstimatedDeparture);
        Assert.Null(state.LastReportedStopId);
    }

    [Fact]
    public void An_unknown_delay_removes_every_estimate_and_is_still_a_delay()
    {
        var state = JourneyProgress.Compute(Train8058(), [Departed(Veyangoda, At(8, 28))], 2, [Unknown(8, 35)]);

        Assert.False(state.DelayKnown);
        Assert.Equal(JourneyStatusKey.Delayed, state.StatusKey);
        Assert.Equal(StatusGroup.Warning, state.StatusGroup);
        Assert.All(state.Stops.Where(s => s.Confirmation == StopConfirmation.Pending), s =>
        {
            Assert.Null(s.EstimatedArrival);
            Assert.Null(s.EstimatedDeparture);
        });
    }

    [Fact]
    public void A_measured_report_after_unknown_brings_the_estimates_back()
    {
        var state = JourneyProgress.Compute(Train8058(), [Departed(Veyangoda, At(8, 28)), Departed(Gampaha, At(8, 41))], 2, [Unknown(8, 35)]);

        Assert.True(state.DelayKnown);
        Assert.Equal(At(8, 52), state.Stops.Single(s => s.ServiceStopId == Ragama).EstimatedArrival);
    }

    [Fact]
    public void A_small_total_inside_the_tolerance_is_on_time()
    {
        var state = JourneyProgress.Compute(Train8058(), NoStops, 2, [Total(8, 15, 2)]);

        Assert.Equal(JourneyStatusKey.OnTime, state.StatusKey);
    }

    [Fact]
    public void The_order_delay_reports_arrive_in_never_changes_the_result()
    {
        var stops = new[] { Departed(Veyangoda, At(8, 28)), Departed(Gampaha, At(8, 41)) };
        var delays = new[] { Total(8, 35, 15), Unknown(8, 38) };

        var inOrder = JourneyProgress.Compute(Train8058(), stops, 2, delays);
        var shuffled = JourneyProgress.Compute(Train8058(), [stops[1], stops[0]], 2, [delays[1], delays[0]]);

        Assert.Equal(inOrder.DelayMinutes, shuffled.DelayMinutes);
        Assert.Equal(inOrder.DelayKnown, shuffled.DelayKnown);
        Assert.Equal(inOrder.Stops, shuffled.Stops);
    }

    // ---- the shape of the request (section 4) ----

    private static ReportEventRequest Request(
        string type, Guid? stop = null, string? reason = null, int? total = null, bool? unknown = null) =>
        new(Guid.NewGuid(), type, stop, DateTimeOffset.UtcNow, reason, total, unknown);

    [Theory]
    [InlineData("Departed")]
    [InlineData("Arrived")]
    public void A_stop_report_needs_a_stop_and_nothing_about_delays(string type)
    {
        Assert.True(EventRules.TryParseKind(type, out var kind));

        Assert.True(EventRules.IsValidShape(Request(type, Gampaha), kind, out var delay));
        Assert.Null(delay);
        Assert.False(EventRules.IsValidShape(Request(type), kind, out _), "no stop");
        Assert.False(EventRules.IsValidShape(Request(type, Guid.Empty), kind, out _), "an empty stop");
        Assert.False(EventRules.IsValidShape(Request(type, Gampaha, reason: "SignalIssue"), kind, out _), "a reason does not belong");
        Assert.False(EventRules.IsValidShape(Request(type, Gampaha, total: 10), kind, out _), "a total does not belong");
        Assert.False(EventRules.IsValidShape(Request(type, Gampaha, unknown: true), kind, out _), "unknown does not belong");
    }

    [Theory]
    [InlineData("SignalIssue")]
    [InlineData("TechnicalFault")]
    [InlineData("Obstruction")]
    [InlineData("Weather")]
    [InlineData("Unknown")]
    public void A_delay_report_accepts_each_reason_with_a_total(string reason)
    {
        Assert.True(EventRules.IsValidShape(Request("DelayReported", reason: reason, total: 10), ReportKind.DelayReported, out var delay));
        Assert.Equal(new DelayPayload(reason, 10, false), delay);
    }

    [Fact]
    public void A_delay_report_may_say_the_duration_is_unknown()
    {
        Assert.True(EventRules.IsValidShape(Request("DelayReported", reason: "Weather", unknown: true), ReportKind.DelayReported, out var delay));
        Assert.Equal(new DelayPayload("Weather", null, true), delay);
    }

    [Theory]
    [InlineData(null, 10, null)]              // no reason
    [InlineData("signalissue", 10, null)]     // the codes are exact
    [InlineData("Other", 10, null)]           // free text is not built
    [InlineData("SignalIssue", null, null)]   // neither a total nor unknown
    [InlineData("SignalIssue", 10, true)]     // both
    [InlineData("SignalIssue", 0, null)]      // below 1
    [InlineData("SignalIssue", -5, null)]
    [InlineData("SignalIssue", 241, null)]    // above 240
    [InlineData("SignalIssue", null, false)]  // "unknown: false" is not "unknown"
    public void A_delay_report_with_a_bad_shape_is_refused(string? reason, int? total, bool? unknown)
    {
        Assert.False(EventRules.IsValidShape(Request("DelayReported", reason: reason, total: total, unknown: unknown), ReportKind.DelayReported, out _));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(240)]
    public void The_total_bounds_are_inclusive(int total)
    {
        Assert.True(EventRules.IsValidShape(Request("DelayReported", reason: "SignalIssue", total: total), ReportKind.DelayReported, out _));
    }

    [Fact]
    public void A_delay_report_must_not_carry_a_stop()
    {
        Assert.False(EventRules.IsValidShape(Request("DelayReported", Gampaha, "SignalIssue", 10), ReportKind.DelayReported, out _));
    }

    [Theory]
    [InlineData("departed")]
    [InlineData("Delayed")]
    [InlineData("")]
    [InlineData(null)]
    public void An_unknown_event_type_is_not_parsed(string? type)
    {
        Assert.False(EventRules.TryParseKind(type, out _));
    }

    // ---- the stored payload ----

    [Fact]
    public void The_payload_round_trips_for_a_total_and_for_unknown()
    {
        var total = new DelayPayload("SignalIssue", 10, false);
        var unknown = new DelayPayload("TechnicalFault", null, true);

        Assert.Equal("""{"reason":"SignalIssue","totalDelayMinutes":10}""", EventRules.ToJson(total));
        Assert.Equal("""{"reason":"TechnicalFault","delayUnknown":true}""", EventRules.ToJson(unknown));
        Assert.Equal(total, EventRules.FromJson(EventRules.ToJson(total)));
        Assert.Equal(unknown, EventRules.FromJson(EventRules.ToJson(unknown)));
        Assert.Null(EventRules.FromJson(null));
    }
}
