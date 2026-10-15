using System.Diagnostics.Metrics;
using SmartRail.Application.Features.Journeys;
using SmartRail.Domain.Journeys;

namespace SmartRail.Application.Features.Events.ReportEvent;

/// <summary>The request body of POST /api/v1/journeys/{journeyId}/events. Fields are nullable so a missing one is a 422, not a crash.</summary>
public sealed record ReportEventRequest(Guid ClientEventId, string? EventType, Guid? ServiceStopId, DateTimeOffset? OccurredAt);

public enum ReportOutcome { Recorded, Duplicate, Rejected }

/// <summary>Why a report was refused. The API maps each reason to a status code and a stable reason code.</summary>
public enum RejectReason
{
    InvalidRequest,
    JourneyNotFound,
    NotAssigned,
    TimeOutOfRange,
    StopNotInService,
    FinalStop,
    NotArrivalStation,
    JourneyNotReportable,
    ClientEventIdReusedWithDifferentContent,
    StopAlreadyHasDifferentTime,
    OrderConflict,
    VersionConflict,
}

public sealed record ReportEventResult(ReportOutcome Outcome, Guid? EventId, int? JourneyVersion, RejectReason? Reason)
{
    public static ReportEventResult Recorded(Guid eventId, int version) => new(ReportOutcome.Recorded, eventId, version, null);

    public static ReportEventResult Duplicate(Guid eventId, int version) => new(ReportOutcome.Duplicate, eventId, version, null);

    public static ReportEventResult Reject(RejectReason reason) => new(ReportOutcome.Rejected, null, null, reason);
}

/// <summary>
/// smartrail.events.received counts every report by type and outcome; the histogram measures the NFR-04 part we control;
/// smartrail.journeys.completed counts journeys that ended with an accepted arrival at the final stop.
/// </summary>
public static class EventMetrics
{
    public const string MeterName = "SmartRail.Events";

    private static readonly Meter Meter = new(MeterName);
    private static readonly Counter<long> Received = Meter.CreateCounter<long>("smartrail.events.received", description: "Reports received, by type and outcome");
    private static readonly Counter<long> Completed = Meter.CreateCounter<long>("smartrail.journeys.completed", description: "Journeys completed by an arrival at the final stop");
    private static readonly Histogram<double> ReportToVisible = Meter.CreateHistogram<double>(
        "smartrail.events.report_to_visible_seconds", unit: "s", description: "Seconds from the reported time to the commit (NFR-04); polling adds to it");

    public static void Count(string eventType, string outcome) =>
        Received.Add(1, new KeyValuePair<string, object?>("event_type", eventType), new KeyValuePair<string, object?>("outcome", outcome));

    public static void JourneyCompleted() => Completed.Add(1);

    public static void Visible(double seconds) => ReportToVisible.Record(Math.Max(0, seconds));
}

/// <summary>The small rules of FR-03 and FR-38 as plain functions, so they can be tested at their exact edges.</summary>
public static class EventRules
{
    /// <summary>R1: valid_from &lt;= at &lt; valid_to, or valid_to null.</summary>
    public static bool IsAssignedAt(IEnumerable<AssignmentInfo> assignments, string operatorUserId, DateTimeOffset at) =>
        assignments.Any(a => a.OperatorUserId == operatorUserId && a.ValidFrom <= at && (a.ValidTo is null || at < a.ValidTo));

    /// <summary>R3: not more than MaxFutureMinutes ahead of the server clock, not older than MaxAgeHours (both inclusive).</summary>
    public static bool IsWithinTimeLimits(DateTimeOffset occurredAt, DateTimeOffset now, EventLimits limits) =>
        occurredAt <= now + TimeSpan.FromMinutes(limits.MaxFutureMinutes) && occurredAt >= now - TimeSpan.FromHours(limits.MaxAgeHours);

    /// <summary>The event types this slice accepts for a stop; anything else is a 422.</summary>
    public static bool TryParseKind(string? eventType, out ReportKind kind)
    {
        switch (eventType)
        {
            case "Departed":
                kind = ReportKind.Departed;
                return true;
            case "Arrived":
                kind = ReportKind.Arrived;
                return true;
            default:
                kind = default;
                return false;
        }
    }
}

/// <summary>
/// Records one "Departed" or "Arrived" report (specs 03 and 06, section 4). The checks run in the order the spec gives, the
/// whole step is retried on a version conflict (R11), and the stop states are rebuilt from all events (R10).
/// </summary>
public sealed class ReportEventHandler(IJourneyRepository repository, TimeProvider clock, EventLimits limits, JourneySettings settings)
{
    private const int MaxAttempts = 3;

    public async Task<ReportEventResult> HandleAsync(
        Guid journeyId, string subject, ReportEventRequest request, CancellationToken cancellationToken)
    {
        var result = await RunAsync(journeyId, subject, request, cancellationToken);
        // Only a known type becomes a metric label, so a client cannot create endless label values.
        EventMetrics.Count(EventRules.TryParseKind(request.EventType, out _) ? request.EventType! : "Unknown", result.Outcome.ToString().ToLowerInvariant());
        return result;
    }

    private async Task<ReportEventResult> RunAsync(
        Guid journeyId, string subject, ReportEventRequest request, CancellationToken cancellationToken)
    {
        if (request.ClientEventId == Guid.Empty || request.ServiceStopId is null || request.ServiceStopId == Guid.Empty
            || request.OccurredAt is null || !EventRules.TryParseKind(request.EventType, out var kind))
        {
            return ReportEventResult.Reject(RejectReason.InvalidRequest);
        }

        var stopId = request.ServiceStopId.Value;

        // The database keeps microseconds. A client may send finer times (.NET sends 100 ns ticks), so trim to what is stored;
        // otherwise a retry of an accepted report would not equal the stored one and would wrongly be refused (R2).
        var utc = request.OccurredAt.Value.ToUniversalTime();
        var occurredAt = new DateTimeOffset(utc.Ticks - utc.Ticks % (TimeSpan.TicksPerMillisecond / 1000), TimeSpan.Zero);

        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            var data = await repository.GetAsync(journeyId, cancellationToken);
            if (data is null)
            {
                return ReportEventResult.Reject(RejectReason.JourneyNotFound);
            }

            // R1: authorised at the time the event happened, not at the time it arrived.
            if (!EventRules.IsAssignedAt(data.Assignments, subject, occurredAt))
            {
                return ReportEventResult.Reject(RejectReason.NotAssigned);
            }

            // R2: an exact repeat, or a different ID for the same type, stop and time, is answered with the original.
            var sameId = data.Events.FirstOrDefault(e => e.ClientEventId == request.ClientEventId);
            if (sameId is not null)
            {
                return sameId.Kind == kind && sameId.ServiceStopId == stopId && sameId.OccurredAt == occurredAt
                    ? ReportEventResult.Duplicate(sameId.EventId, sameId.JourneyVersionAfter)
                    : ReportEventResult.Reject(RejectReason.ClientEventIdReusedWithDifferentContent);
            }

            var sameContent = data.Events.FirstOrDefault(e => e.Kind == kind && e.ServiceStopId == stopId && e.OccurredAt == occurredAt);
            if (sameContent is not null)
            {
                return ReportEventResult.Duplicate(sameContent.EventId, sameContent.JourneyVersionAfter);
            }

            var now = clock.GetUtcNow();
            if (!EventRules.IsWithinTimeLimits(occurredAt, now, limits))
            {
                return ReportEventResult.Reject(RejectReason.TimeOutOfRange);
            }

            var stop = data.Stops.FirstOrDefault(s => s.ServiceStopId == stopId);
            if (stop is null)
            {
                return ReportEventResult.Reject(RejectReason.StopNotInService);
            }

            if (kind == ReportKind.Departed && stop.IsFinalStop)
            {
                return ReportEventResult.Reject(RejectReason.FinalStop);
            }

            // D-08: an arrival only at a designated stop or the final stop (never the first stop, which has no arrival).
            if (kind == ReportKind.Arrived && !stop.IsArrivalStation)
            {
                return ReportEventResult.Reject(RejectReason.NotArrivalStation);
            }

            if (data.Status is JourneyStatus.Unassigned or JourneyStatus.Completed)
            {
                return ReportEventResult.Reject(RejectReason.JourneyNotReportable);
            }

            // R3: strictly increasing times in stop order, arrival before departure within a stop.
            var schedule = data.Schedule();
            var existing = data.Events.Select(e => new ReportedStopEvent(e.ServiceStopId, e.Kind, e.OccurredAt)).ToList();
            var candidate = new ReportedStopEvent(stopId, kind, occurredAt);
            switch (JourneyProgress.CheckOrder(schedule, existing, candidate))
            {
                case ReportOrderResult.ConflictingTimeForStop:
                    return ReportEventResult.Reject(RejectReason.StopAlreadyHasDifferentTime);
                case ReportOrderResult.NotStrictlyIncreasing:
                    return ReportEventResult.Reject(RejectReason.OrderConflict);
            }

            // R10: rebuild every stop from all accepted reports in occurrence order.
            var progress = JourneyProgress.Compute(schedule, [.. existing, candidate], settings.OnTimeToleranceMinutes);

            // R9 and R10: the first stop report makes the journey Running; an arrival at the final stop completes it.
            var newStatus = progress.IsCompleted
                ? JourneyStatus.Completed
                : data.Status == JourneyStatus.Scheduled ? JourneyStatus.Running : data.Status;

            var change = new ReportChange(
                data.JourneyId,
                data.Version,
                Guid.CreateVersion7(now),
                request.ClientEventId,
                kind,
                stopId,
                occurredAt,
                now,
                subject,
                newStatus,
                progress.DelayMinutes,
                progress.Stops);

            if (await repository.SaveReportAsync(change, cancellationToken) == SaveOutcome.Saved)
            {
                EventMetrics.Visible((clock.GetUtcNow() - occurredAt).TotalSeconds);
                if (newStatus == JourneyStatus.Completed)
                {
                    EventMetrics.JourneyCompleted();
                }

                return ReportEventResult.Recorded(change.EventId, data.Version + 1);
            }

            // Someone else changed the journey (or took the client event ID) in between: load again and retry (R11).
        }

        return ReportEventResult.Reject(RejectReason.VersionConflict);
    }
}
