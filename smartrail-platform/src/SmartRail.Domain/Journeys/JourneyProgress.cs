namespace SmartRail.Domain.Journeys;

/// <summary>An accepted "departed" report: which stop, and when it really happened.</summary>
public sealed record ReportedDeparture(Guid ServiceStopId, DateTimeOffset OccurredAt);

public enum DepartureOrderResult { Ok, ConflictingTimeForStop, NotStrictlyIncreasing }

public enum JourneyStatusKey { Scheduled, OnTime, Delayed }

/// <summary>One stop after the rebuild (R10). An Inferred stop has no estimate and no actual time of its own.</summary>
public sealed record StopProgress(
    Guid ServiceStopId,
    int Sequence,
    StopConfirmation Confirmation,
    DateTimeOffset? EstimatedArrival,
    DateTimeOffset? EstimatedDeparture,
    DateTimeOffset? ActualDeparture);

public sealed record JourneyProgressState(
    IReadOnlyList<StopProgress> Stops,
    int DelayMinutes,
    JourneyStatusKey StatusKey,
    StatusGroup StatusGroup,
    Guid? LastReportedStopId,
    Guid? NextStopId);

/// <summary>
/// The rules of slice 1 that need no database: order of reports (R4), delay (R5), status (R6), and the rebuild of all stop
/// states from the accepted reports in occurrence order (R10), so the arrival order of reports can never change the result.
/// </summary>
public static class JourneyProgress
{
    /// <summary>R4: departure times must strictly increase with stop sequence; one time per stop.</summary>
    public static DepartureOrderResult CheckOrder(
        IReadOnlyList<StopSchedule> schedule,
        IReadOnlyCollection<ReportedDeparture> existing,
        ReportedDeparture candidate)
    {
        var sequenceOf = schedule.ToDictionary(s => s.ServiceStopId, s => s.Sequence);

        if (existing.Any(e => e.ServiceStopId == candidate.ServiceStopId && e.OccurredAt != candidate.OccurredAt))
        {
            return DepartureOrderResult.ConflictingTimeForStop;
        }

        var ordered = existing
            .Where(e => e.ServiceStopId != candidate.ServiceStopId)
            .Append(candidate)
            .OrderBy(e => sequenceOf[e.ServiceStopId])
            .ToList();

        for (var i = 1; i < ordered.Count; i++)
        {
            if (ordered[i].OccurredAt <= ordered[i - 1].OccurredAt)
            {
                return DepartureOrderResult.NotStrictlyIncreasing;
            }
        }

        return DepartureOrderResult.Ok;
    }

    /// <summary>
    /// R5: delay in whole minutes. Order: (1) actual minus scheduled, (2) round with MidpointRounding.AwayFromZero,
    /// (3) clamp to 0 so it is never negative. The on-time threshold (R6) is applied to this result afterwards.
    /// </summary>
    public static int DelayMinutes(DateTimeOffset actualDeparture, DateTimeOffset scheduledDeparture)
    {
        var rounded = (int)Math.Round((actualDeparture - scheduledDeparture).TotalMinutes, MidpointRounding.AwayFromZero);
        return Math.Max(0, rounded);
    }

    /// <summary>R6: status group and key for a delay, given the number of reported stops.</summary>
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

    /// <summary>R10: rebuild every stop's state from the accepted reports. The reports must already satisfy R4.</summary>
    public static JourneyProgressState Compute(
        IReadOnlyList<StopSchedule> schedule,
        IReadOnlyCollection<ReportedDeparture> departures,
        int onTimeToleranceMinutes)
    {
        var ordered = schedule.OrderBy(s => s.Sequence).ToList();
        var reported = departures.ToDictionary(d => d.ServiceStopId);
        var lastReportedSequence = ordered.Where(s => reported.ContainsKey(s.ServiceStopId)).Select(s => s.Sequence).DefaultIfEmpty(0).Max();

        var delay = 0;
        Guid? lastReportedStop = null;
        if (lastReportedSequence > 0)
        {
            var last = ordered.Single(s => s.Sequence == lastReportedSequence);
            lastReportedStop = last.ServiceStopId;
            delay = DelayMinutes(reported[last.ServiceStopId].OccurredAt, last.Departure
                ?? throw new InvalidOperationException("A departure was reported for a stop with no scheduled departure."));
        }

        var shift = TimeSpan.FromMinutes(delay);
        var stops = new List<StopProgress>();
        foreach (var stop in ordered)
        {
            if (reported.TryGetValue(stop.ServiceStopId, out var departure))
            {
                stops.Add(new StopProgress(stop.ServiceStopId, stop.Sequence, StopConfirmation.Reported, null, null, departure.OccurredAt));
            }
            else if (stop.Sequence < lastReportedSequence)
            {
                stops.Add(new StopProgress(stop.ServiceStopId, stop.Sequence, StopConfirmation.Inferred, null, null, null));
            }
            else
            {
                stops.Add(new StopProgress(
                    stop.ServiceStopId, stop.Sequence, StopConfirmation.Pending,
                    stop.Arrival + shift, stop.Departure + shift, null));
            }
        }

        var (key, group) = StatusFor(lastReportedSequence > 0, delay, onTimeToleranceMinutes);
        var next = ordered.FirstOrDefault(s => s.Sequence > lastReportedSequence && lastReportedSequence > 0)?.ServiceStopId;
        return new JourneyProgressState(stops, delay, key, group, lastReportedStop, next);
    }
}
