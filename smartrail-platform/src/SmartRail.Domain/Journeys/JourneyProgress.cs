namespace SmartRail.Domain.Journeys;

/// <summary>What an accepted report says. The order matters: at one stop the arrival comes before the departure. A delay report has no stop.</summary>
public enum ReportKind { Arrived, Departed, DelayReported }

/// <summary>An accepted "arrived" or "departed" report: which stop, which kind, and when it really happened.</summary>
public sealed record ReportedStopEvent(Guid ServiceStopId, ReportKind Kind, DateTimeOffset OccurredAt);

/// <summary>
/// An accepted delay report (spec 06, A2): when it was made, and either the operator's estimate of the TOTAL delay or that the
/// duration is unknown (D-07: a total against the timetable, never extra minutes on top of the delay already running).
/// </summary>
public sealed record ReportedDelay(DateTimeOffset OccurredAt, int? TotalMinutes, bool Unknown);

/// <summary>The delay in whole minutes after some reports, and whether anyone knows how long it will last.</summary>
public readonly record struct DelayState(int Minutes, bool Known);

public enum ReportOrderResult { Ok, ConflictingTimeForStop, NotStrictlyIncreasing }

public enum JourneyStatusKey { Scheduled, OnTime, Delayed, Completed }

/// <summary>
/// One stop after the rebuild (R10). An Inferred stop has no estimate and no actual time of its own. A Reported stop has an
/// actual arrival, an actual departure, or both.
/// </summary>
public sealed record StopProgress(
    Guid ServiceStopId,
    int Sequence,
    StopConfirmation Confirmation,
    DateTimeOffset? EstimatedArrival,
    DateTimeOffset? EstimatedDeparture,
    DateTimeOffset? ActualDeparture,
    DateTimeOffset? ActualArrival = null);

public sealed record JourneyProgressState(
    IReadOnlyList<StopProgress> Stops,
    int DelayMinutes,
    JourneyStatusKey StatusKey,
    StatusGroup StatusGroup,
    Guid? LastReportedStopId,
    Guid? NextStopId,
    bool IsCompleted = false,
    bool DelayKnown = true);

/// <summary>
/// The rules that need no database: order of reports (R3), delay (R4), status (R8), and the rebuild of all stop states from
/// the accepted reports in occurrence order (R10 of slice 1), so the arrival order of reports can never change the result.
/// </summary>
public static class JourneyProgress
{
    /// <summary>
    /// Spec 06 R3: taken in stop order, and arrival before departure within a stop, the times must strictly increase. The same
    /// stop and kind may have only one time.
    /// </summary>
    public static ReportOrderResult CheckOrder(
        IReadOnlyList<StopSchedule> schedule,
        IReadOnlyCollection<ReportedStopEvent> existing,
        ReportedStopEvent candidate)
    {
        var sequenceOf = schedule.ToDictionary(s => s.ServiceStopId, s => s.Sequence);

        bool SameSlot(ReportedStopEvent e) => e.ServiceStopId == candidate.ServiceStopId && e.Kind == candidate.Kind;

        if (existing.Any(e => SameSlot(e) && e.OccurredAt != candidate.OccurredAt))
        {
            return ReportOrderResult.ConflictingTimeForStop;
        }

        var ordered = existing
            .Where(e => !SameSlot(e))
            .Append(candidate)
            .OrderBy(e => sequenceOf[e.ServiceStopId])
            .ThenBy(e => e.Kind)
            .ToList();

        for (var i = 1; i < ordered.Count; i++)
        {
            if (ordered[i].OccurredAt <= ordered[i - 1].OccurredAt)
            {
                return ReportOrderResult.NotStrictlyIncreasing;
            }
        }

        return ReportOrderResult.Ok;
    }

    /// <summary>
    /// R4: delay in whole minutes. Order: (1) actual minus scheduled, (2) round with MidpointRounding.AwayFromZero,
    /// (3) clamp to 0 so it is never negative. The on-time threshold (R8) is applied to this result afterwards.
    /// </summary>
    public static int DelayMinutes(DateTimeOffset actual, DateTimeOffset scheduled)
    {
        var rounded = (int)Math.Round((actual - scheduled).TotalMinutes, MidpointRounding.AwayFromZero);
        return Math.Max(0, rounded);
    }

    /// <summary>
    /// Spec 06 R4: fold every accepted report in occurrence order. A departure or arrival MEASURES the delay (actual minus
    /// scheduled, rounded, never negative); a numeric delay report sets the TOTAL; "unknown" keeps the number and marks it
    /// unknown until the next measured or numeric report. A later report overrides an earlier one, so a measurement beats an
    /// earlier estimate. With <paramref name="before"/>, only reports made before that moment count (used to validate a new one).
    /// </summary>
    public static DelayState DelayInForce(
        IReadOnlyList<StopSchedule> schedule,
        IReadOnlyCollection<ReportedStopEvent> events,
        IReadOnlyCollection<ReportedDelay> delays,
        DateTimeOffset? before = null)
    {
        var byStop = schedule.ToDictionary(s => s.ServiceStopId);
        var steps = events
            .Where(e => before is null || e.OccurredAt < before)
            .Select(e => (e.OccurredAt, Apply: (Func<DelayState, DelayState>)(_ =>
            {
                var stop = byStop[e.ServiceStopId];
                var scheduled = e.Kind == ReportKind.Arrived ? stop.Arrival : stop.Departure;
                return new DelayState(
                    DelayMinutes(e.OccurredAt, scheduled ?? throw new InvalidOperationException($"A {e.Kind} report was made for a stop with no scheduled time for it.")),
                    Known: true);
            })))
            .Concat(delays
                .Where(d => before is null || d.OccurredAt < before)
                .Select(d => (d.OccurredAt, Apply: (Func<DelayState, DelayState>)(current =>
                    d.Unknown ? current with { Known = false } : new DelayState(d.TotalMinutes!.Value, Known: true)))))
            .OrderBy(step => step.OccurredAt);

        var state = new DelayState(0, Known: true);
        foreach (var step in steps)
        {
            state = step.Apply(state);
        }

        return state;
    }

    /// <summary>R8: status group and key for a delay, given whether anything was reported.</summary>
    public static (JourneyStatusKey Key, StatusGroup Group) StatusFor(bool anyReport, int delayMinutes, int toleranceMinutes)
    {
        if (!anyReport)
        {
            return (JourneyStatusKey.Scheduled, StatusGroup.Normal);
        }

        return delayMinutes <= toleranceMinutes
            ? (JourneyStatusKey.OnTime, StatusGroup.Normal)
            : (JourneyStatusKey.Delayed, StatusGroup.Warning);
    }

    /// <summary>R10: rebuild every stop's state from the accepted reports. The reports must already satisfy R3.</summary>
    public static JourneyProgressState Compute(
        IReadOnlyList<StopSchedule> schedule,
        IReadOnlyCollection<ReportedStopEvent> events,
        int onTimeToleranceMinutes,
        IReadOnlyCollection<ReportedDelay>? delays = null)
    {
        delays ??= [];
        var ordered = schedule.OrderBy(s => s.Sequence).ToList();
        var sequenceOf = ordered.ToDictionary(s => s.ServiceStopId, s => s.Sequence);
        var arrivals = events.Where(e => e.Kind == ReportKind.Arrived).ToDictionary(e => e.ServiceStopId, e => e.OccurredAt);
        var departures = events.Where(e => e.Kind == ReportKind.Departed).ToDictionary(e => e.ServiceStopId, e => e.OccurredAt);

        // The latest report is the one latest in stop order, arrival before departure. By R3 that is also the latest in time.
        var latest = events.OrderBy(e => sequenceOf[e.ServiceStopId]).ThenBy(e => e.Kind).LastOrDefault();
        var latestSequence = latest is null ? 0 : sequenceOf[latest.ServiceStopId];

        // R4: the delay in force after the last report of any kind (a measurement, a total, or "unknown").
        var inForce = DelayInForce(schedule, events, delays);
        var delay = inForce.Minutes;
        Guid? lastReportedStop = latest is null ? null : ordered.Single(s => s.Sequence == latestSequence).ServiceStopId;

        // R7: with the duration unknown there are no honest estimates, so the pending stops carry none.
        var shift = inForce.Known ? TimeSpan.FromMinutes(delay) : (TimeSpan?)null;
        var stops = new List<StopProgress>();
        foreach (var stop in ordered)
        {
            var hasArrival = arrivals.TryGetValue(stop.ServiceStopId, out var arrivedAt);
            var hasDeparture = departures.TryGetValue(stop.ServiceStopId, out var departedAt);

            if (hasArrival || hasDeparture)
            {
                // At the stop the train has reached but not left, the departure is expected at its scheduled time plus the delay (R5).
                DateTimeOffset? expectedDeparture = stop.Sequence == latestSequence && !hasDeparture && shift is not null ? stop.Departure + shift : null;
                stops.Add(new StopProgress(
                    stop.ServiceStopId, stop.Sequence, StopConfirmation.Reported, null, expectedDeparture,
                    hasDeparture ? departedAt : null, hasArrival ? arrivedAt : null));
            }
            else if (stop.Sequence < latestSequence)
            {
                stops.Add(new StopProgress(stop.ServiceStopId, stop.Sequence, StopConfirmation.Inferred, null, null, null));
            }
            else
            {
                stops.Add(new StopProgress(
                    stop.ServiceStopId, stop.Sequence, StopConfirmation.Pending,
                    shift is null ? null : stop.Arrival + shift, shift is null ? null : stop.Departure + shift, null));
            }
        }

        // R10: arrival at the final stop completes the journey with no further tap.
        var finalStopId = ordered.Count == 0 ? (Guid?)null : ordered[^1].ServiceStopId;
        var completed = finalStopId is { } finalId && arrivals.ContainsKey(finalId);

        var (key, group) = completed
            ? (JourneyStatusKey.Completed, StatusGroup.Normal)
            : !inForce.Known
                ? (JourneyStatusKey.Delayed, StatusGroup.Warning) // R7: an unknown delay is a delay, with no estimates
                : StatusFor(latest is not null || delays.Count > 0, delay, onTimeToleranceMinutes);
        var next = latest is null ? null : ordered.FirstOrDefault(s => s.Sequence > latestSequence)?.ServiceStopId;
        return new JourneyProgressState(stops, delay, key, group, lastReportedStop, next, completed, inForce.Known);
    }
}
