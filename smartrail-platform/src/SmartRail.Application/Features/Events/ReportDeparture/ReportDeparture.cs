using System.Diagnostics.Metrics;
using SmartRail.Application.Features.Journeys;
using SmartRail.Domain.Journeys;

namespace SmartRail.Application.Features.Events.ReportDeparture;

/// <summary>The request body of POST /api/v1/journeys/{journeyId}/events. Fields are nullable so a missing one is a 422, not a crash.</summary>
public sealed record ReportDepartureRequest(Guid ClientEventId, string? EventType, Guid ServiceStopId, DateTimeOffset? OccurredAt);

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
    JourneyNotReportable,
    ClientEventIdReusedWithDifferentContent,
    StopAlreadyHasDifferentTime,
    OrderConflict,
    VersionConflict,
}

public sealed record ReportDepartureResult(ReportOutcome Outcome, Guid? EventId, int? JourneyVersion, RejectReason? Reason)
{
    public static ReportDepartureResult Recorded(Guid eventId, int version) => new(ReportOutcome.Recorded, eventId, version, null);

    public static ReportDepartureResult Duplicate(Guid eventId, int version) => new(ReportOutcome.Duplicate, eventId, version, null);

    public static ReportDepartureResult Reject(RejectReason reason) => new(ReportOutcome.Rejected, null, null, reason);
}

/// <summary>smartrail.events.received counts every report by outcome; the histogram measures the NFR-04 part we control.</summary>
public static class EventMetrics
{
    public const string MeterName = "SmartRail.Events";

    private static readonly Meter Meter = new(MeterName);
    private static readonly Counter<long> Received = Meter.CreateCounter<long>("smartrail.events.received", description: "Reports received, by outcome");
    private static readonly Histogram<double> ReportToVisible = Meter.CreateHistogram<double>(
        "smartrail.events.report_to_visible_seconds", unit: "s", description: "Seconds from the reported time to the commit (NFR-04); polling adds to it");

    public static void Count(string eventType, string outcome) =>
        Received.Add(1, new KeyValuePair<string, object?>("event_type", eventType), new KeyValuePair<string, object?>("outcome", outcome));

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
}

/// <summary>
/// Records one "Departed" report (spec 03-operator-report-live-status, section 4). The checks run in the order the spec gives,
/// the whole step is retried on a version conflict (R11), and the stop states are rebuilt from all events (R10).
/// </summary>
public sealed class ReportDepartureHandler(IJourneyRepository repository, TimeProvider clock, EventLimits limits, JourneySettings settings)
{
    private const int MaxAttempts = 3;
    private const string DepartedType = "Departed";

    public async Task<ReportDepartureResult> HandleAsync(
        Guid journeyId, string subject, ReportDepartureRequest request, CancellationToken cancellationToken)
    {
        var result = await RunAsync(journeyId, subject, request, cancellationToken);
        EventMetrics.Count(DepartedType, result.Outcome.ToString().ToLowerInvariant());
        return result;
    }

    private async Task<ReportDepartureResult> RunAsync(
        Guid journeyId, string subject, ReportDepartureRequest request, CancellationToken cancellationToken)
    {
        if (request.ClientEventId == Guid.Empty || request.ServiceStopId == Guid.Empty
            || request.OccurredAt is null || request.EventType != DepartedType)
        {
            return ReportDepartureResult.Reject(RejectReason.InvalidRequest);
        }

        // The database keeps microseconds. A client may send finer times (.NET sends 100 ns ticks), so trim to what is stored;
        // otherwise a retry of an accepted report would not equal the stored one and would wrongly be refused (R2).
        var utc = request.OccurredAt.Value.ToUniversalTime();
        var occurredAt = new DateTimeOffset(utc.Ticks - utc.Ticks % (TimeSpan.TicksPerMillisecond / 1000), TimeSpan.Zero);

        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            var data = await repository.GetAsync(journeyId, cancellationToken);
            if (data is null)
            {
                return ReportDepartureResult.Reject(RejectReason.JourneyNotFound);
            }

            // R1: authorised at the time the event happened, not at the time it arrived.
            if (!EventRules.IsAssignedAt(data.Assignments, subject, occurredAt))
            {
                return ReportDepartureResult.Reject(RejectReason.NotAssigned);
            }

            // R2: an exact repeat, or a different ID for the same stop and time, is answered with the original.
            var sameId = data.Events.FirstOrDefault(e => e.ClientEventId == request.ClientEventId);
            if (sameId is not null)
            {
                return sameId.ServiceStopId == request.ServiceStopId && sameId.OccurredAt == occurredAt
                    ? ReportDepartureResult.Duplicate(sameId.EventId, sameId.JourneyVersionAfter)
                    : ReportDepartureResult.Reject(RejectReason.ClientEventIdReusedWithDifferentContent);
            }

            var sameContent = data.Events.FirstOrDefault(e => e.ServiceStopId == request.ServiceStopId && e.OccurredAt == occurredAt);
            if (sameContent is not null)
            {
                return ReportDepartureResult.Duplicate(sameContent.EventId, sameContent.JourneyVersionAfter);
            }

            var now = clock.GetUtcNow();
            if (!EventRules.IsWithinTimeLimits(occurredAt, now, limits))
            {
                return ReportDepartureResult.Reject(RejectReason.TimeOutOfRange);
            }

            var stop = data.Stops.FirstOrDefault(s => s.ServiceStopId == request.ServiceStopId);
            if (stop is null)
            {
                return ReportDepartureResult.Reject(RejectReason.StopNotInService);
            }

            if (stop.IsFinalStop)
            {
                return ReportDepartureResult.Reject(RejectReason.FinalStop);
            }

            if (data.Status is JourneyStatus.Unassigned or JourneyStatus.Completed)
            {
                return ReportDepartureResult.Reject(RejectReason.JourneyNotReportable);
            }

            // R4: strictly increasing times with stop sequence.
            var schedule = data.Schedule();
            var existing = data.Events.Select(e => new ReportedDeparture(e.ServiceStopId, e.OccurredAt)).ToList();
            var candidate = new ReportedDeparture(request.ServiceStopId, occurredAt);
            switch (JourneyProgress.CheckOrder(schedule, existing, candidate))
            {
                case DepartureOrderResult.ConflictingTimeForStop:
                    return ReportDepartureResult.Reject(RejectReason.StopAlreadyHasDifferentTime);
                case DepartureOrderResult.NotStrictlyIncreasing:
                    return ReportDepartureResult.Reject(RejectReason.OrderConflict);
            }

            // R10: rebuild every stop from all accepted departures in occurrence order.
            var progress = JourneyProgress.Compute(schedule, [.. existing, candidate], settings.OnTimeToleranceMinutes);

            var change = new DepartureChange(
                data.JourneyId,
                data.Version,
                Guid.CreateVersion7(now),
                request.ClientEventId,
                request.ServiceStopId,
                occurredAt,
                now,
                subject,
                data.Status == JourneyStatus.Scheduled ? JourneyStatus.Running : data.Status, // R9
                progress.DelayMinutes,
                progress.Stops);

            if (await repository.SaveDepartureAsync(change, cancellationToken) == SaveOutcome.Saved)
            {
                EventMetrics.Visible((clock.GetUtcNow() - occurredAt).TotalSeconds);
                return ReportDepartureResult.Recorded(change.EventId, data.Version + 1);
            }

            // Someone else changed the journey (or took the client event ID) in between: load again and retry (R11).
        }

        return ReportDepartureResult.Reject(RejectReason.VersionConflict);
    }
}
