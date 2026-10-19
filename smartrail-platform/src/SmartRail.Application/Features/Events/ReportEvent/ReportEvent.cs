using System.Diagnostics.Metrics;
using System.Text.Json;
using SmartRail.Application.Features.Exceptions;
using SmartRail.Application.Features.Journeys;
using SmartRail.Domain.Journeys;

namespace SmartRail.Application.Features.Events.ReportEvent;

/// <summary>
/// The request body of POST /api/v1/journeys/{journeyId}/events. Fields are nullable so a missing one is a 422, not a crash.
/// A stop report (Departed, Arrived) carries a stop; a delay report carries a reason and a total or "unknown" (spec 06, section 4).
/// </summary>
public sealed record ReportEventRequest(
    Guid ClientEventId,
    string? EventType,
    Guid? ServiceStopId,
    DateTimeOffset? OccurredAt,
    string? Reason = null,
    int? TotalDelayMinutes = null,
    bool? DelayUnknown = null);

public enum ReportOutcome { Recorded, Duplicate, Rejected }

/// <summary>Why a report was refused. The API maps each reason to a status code and a stable reason code.</summary>
public enum RejectReason
{
    InvalidRequest,
    JourneyNotFound,
    NotAssigned,
    AssignmentEnded,
    TimeOutOfRange,
    StopNotInService,
    FinalStop,
    NotArrivalStation,
    JourneyNotReportable,
    ClientEventIdReusedWithDifferentContent,
    StopAlreadyHasDifferentTime,
    OrderConflict,
    DelayNotGreater,
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

/// <summary>The small rules of FR-03, FR-23 and FR-38 as plain functions, so they can be tested at their exact edges.</summary>
public static class EventRules
{
    /// <summary>The delay reasons the operator app offers (AC-E03-04). The free-text "Other" is a Should and not built.</summary>
    public static readonly IReadOnlyList<string> DelayReasons = ["SignalIssue", "TechnicalFault", "Obstruction", "Weather", "Unknown"];

    public const int MaxTotalDelayMinutes = 240;

    /// <summary>
    /// Spec 17 rule 9: the assignment that covered the report was ended for a lost device and the report is RECEIVED at or after the end, so
    /// a stolen phone cannot send reports dated earlier than the end (FR-56). Any other end still accepts a report made during the assignment.
    /// </summary>
    public static bool IsBlockedByLostDevice(IEnumerable<AssignmentInfo> assignments, string operatorUserId, DateTimeOffset occurredAt, DateTimeOffset receivedAt) =>
        assignments.Any(a => a.OperatorUserId == operatorUserId
            && AssignmentRules.Covers(a.ValidFrom, a.ValidTo, occurredAt)
            && AssignmentRules.BlocksLateReport(a.EndReason, a.ValidTo, receivedAt));

    /// <summary>R1: valid_from &lt;= at &lt; valid_to, or valid_to null.</summary>
    public static bool IsAssignedAt(IEnumerable<AssignmentInfo> assignments, string operatorUserId, DateTimeOffset at) =>
        assignments.Any(a => a.OperatorUserId == operatorUserId && a.ValidFrom <= at && (a.ValidTo is null || at < a.ValidTo));

    /// <summary>R3: not more than MaxFutureMinutes ahead of the server clock, not older than MaxAgeHours (both inclusive).</summary>
    public static bool IsWithinTimeLimits(DateTimeOffset occurredAt, DateTimeOffset now, EventLimits limits) =>
        occurredAt <= now + TimeSpan.FromMinutes(limits.MaxFutureMinutes) && occurredAt >= now - TimeSpan.FromHours(limits.MaxAgeHours);

    /// <summary>The event types accepted here; anything else is a 422.</summary>
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
            case "DelayReported":
                kind = ReportKind.DelayReported;
                return true;
            default:
                kind = default;
                return false;
        }
    }

    /// <summary>
    /// The shape table of spec 06 section 4. A stop report needs a stop and nothing about delays. A delay report needs a reason
    /// and exactly one of a total (1 to 240) or "unknown", and no stop. Returns the payload of a delay report.
    /// </summary>
    public static bool IsValidShape(ReportEventRequest request, ReportKind kind, out DelayPayload? delay)
    {
        delay = null;
        var hasDelayFields = request.Reason is not null || request.TotalDelayMinutes is not null || request.DelayUnknown is not null;

        if (kind != ReportKind.DelayReported)
        {
            return request.ServiceStopId is { } stop && stop != Guid.Empty && !hasDelayFields;
        }

        if (request.ServiceStopId is not null || request.Reason is null || !DelayReasons.Contains(request.Reason))
        {
            return false;
        }

        var unknown = request.DelayUnknown == true;
        var hasTotal = request.TotalDelayMinutes is not null;
        if (unknown == hasTotal)
        {
            return false; // neither, or both
        }

        if (hasTotal && request.TotalDelayMinutes is < 1 or > MaxTotalDelayMinutes)
        {
            return false;
        }

        delay = new DelayPayload(request.Reason, request.TotalDelayMinutes, unknown);
        return true;
    }

    /// <summary>The payload as stored in the event (jsonb). The reason is kept now and published only after a controller verifies it (D-10).</summary>
    public static string ToJson(DelayPayload delay) =>
        JsonSerializer.Serialize(
            delay.DelayUnknown
                ? new { reason = delay.Reason, delayUnknown = true }
                : (object)new { reason = delay.Reason, totalDelayMinutes = delay.TotalDelayMinutes });

    public static DelayPayload? FromJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        return new DelayPayload(
            root.TryGetProperty("reason", out var reason) ? reason.GetString() ?? "" : "",
            root.TryGetProperty("totalDelayMinutes", out var total) && total.ValueKind == JsonValueKind.Number ? total.GetInt32() : null,
            root.TryGetProperty("delayUnknown", out var unknown) && unknown.ValueKind == JsonValueKind.True);
    }
}

/// <summary>
/// Records one "Departed", "Arrived" or "DelayReported" report (specs 03 and 06, section 4). The checks run in the order the
/// spec gives, the whole step is retried on a version conflict (R11), and the stop states are rebuilt from all events (R10).
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
        if (request.ClientEventId == Guid.Empty || request.OccurredAt is null
            || !EventRules.TryParseKind(request.EventType, out var kind)
            || !EventRules.IsValidShape(request, kind, out var delay))
        {
            return ReportEventResult.Reject(RejectReason.InvalidRequest);
        }

        var stopId = request.ServiceStopId; // null for a delay report

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

            if (EventRules.IsBlockedByLostDevice(data.Assignments, subject, occurredAt, clock.GetUtcNow()))
            {
                return ReportEventResult.Reject(RejectReason.AssignmentEnded);
            }

            // R2: an exact repeat, or a different ID for the same type, stop, time and content, is answered with the original.
            bool SameContent(RecordedEvent e) => e.Kind == kind && e.ServiceStopId == stopId && e.OccurredAt == occurredAt && e.Delay == delay;

            var sameId = data.Events.FirstOrDefault(e => e.ClientEventId == request.ClientEventId);
            if (sameId is not null)
            {
                return SameContent(sameId)
                    ? ReportEventResult.Duplicate(sameId.EventId, sameId.JourneyVersionAfter)
                    : ReportEventResult.Reject(RejectReason.ClientEventIdReusedWithDifferentContent);
            }

            var sameContent = data.Events.FirstOrDefault(SameContent);
            if (sameContent is not null)
            {
                return ReportEventResult.Duplicate(sameContent.EventId, sameContent.JourneyVersionAfter);
            }

            var now = clock.GetUtcNow();
            if (!EventRules.IsWithinTimeLimits(occurredAt, now, limits))
            {
                return ReportEventResult.Reject(RejectReason.TimeOutOfRange);
            }

            if (kind != ReportKind.DelayReported)
            {
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
            }

            if (data.Status is JourneyStatus.Unassigned or JourneyStatus.Completed or JourneyStatus.Cancelled)
            {
                return ReportEventResult.Reject(RejectReason.JourneyNotReportable);
            }

            var schedule = data.Schedule();
            var existingStops = data.Events
                .Where(e => e.Kind != ReportKind.DelayReported)
                .Select(e => new ReportedStopEvent(e.ServiceStopId!.Value, e.Kind, e.OccurredAt))
                .ToList();
            var existingDelays = data.Events
                .Where(e => e.Kind == ReportKind.DelayReported)
                .Select(e => new ReportedDelay(e.OccurredAt, e.Delay!.TotalDelayMinutes, e.Delay.DelayUnknown))
                .ToList();

            // Spec 06 R1: no two different reports of one journey happen at the same moment, so the rebuild is deterministic.
            if (data.Events.Any(e => e.OccurredAt == occurredAt))
            {
                return ReportEventResult.Reject(RejectReason.OrderConflict);
            }

            ReportedStopEvent? candidateStop = null;
            ReportedDelay? candidateDelay = null;
            if (kind == ReportKind.DelayReported)
            {
                // R6: a numeric total must be greater than the delay in force just before this report (EC-16).
                if (delay!.TotalDelayMinutes is { } total)
                {
                    var before = JourneyProgress.DelayInForce(schedule, existingStops, existingDelays, occurredAt);
                    // After "unknown" the last number is still the floor, but equal to it is allowed: nothing new was learned.
                    var acceptable = before.Known ? total > before.Minutes : total >= before.Minutes;
                    if (!acceptable)
                    {
                        return ReportEventResult.Reject(RejectReason.DelayNotGreater);
                    }
                }

                candidateDelay = new ReportedDelay(occurredAt, delay.TotalDelayMinutes, delay.DelayUnknown);
            }
            else
            {
                // R3: strictly increasing times in stop order, arrival before departure within a stop.
                candidateStop = new ReportedStopEvent(stopId!.Value, kind, occurredAt);
                switch (JourneyProgress.CheckOrder(schedule, existingStops, candidateStop))
                {
                    case ReportOrderResult.ConflictingTimeForStop:
                        return ReportEventResult.Reject(RejectReason.StopAlreadyHasDifferentTime);
                    case ReportOrderResult.NotStrictlyIncreasing:
                        return ReportEventResult.Reject(RejectReason.OrderConflict);
                }
            }

            // R10: rebuild every stop from all accepted reports in occurrence order.
            var allStops = candidateStop is null ? existingStops : [.. existingStops, candidateStop];
            var allDelays = candidateDelay is null ? existingDelays : [.. existingDelays, candidateDelay];
            var progress = JourneyProgress.Compute(schedule, allStops, settings.OnTimeToleranceMinutes, allDelays);

            // R9 and R10: the first stop report makes the journey Running (a delay report alone does not); an arrival at the
            // final stop completes it.
            var newStatus = progress.IsCompleted
                ? JourneyStatus.Completed
                : data.Status == JourneyStatus.Scheduled && kind != ReportKind.DelayReported ? JourneyStatus.Running : data.Status;

            // Spec 07: does this report need a controller's attention? At most one exception per report (D-44).
            var raised = WhatThisReportRaises(data, schedule, kind, stopId, delay, occurredAt, existingStops);

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
                progress.Stops,
                delay is null ? null : EventRules.ToJson(delay),
                raised);

            if (await repository.SaveReportAsync(change, cancellationToken) == SaveOutcome.Saved)
            {
                EventMetrics.Visible((clock.GetUtcNow() - occurredAt).TotalSeconds);
                if (newStatus == JourneyStatus.Completed)
                {
                    EventMetrics.JourneyCompleted();
                }

                if (raised is { } exception)
                {
                    ExceptionMetrics.Raised(exception.Exception);
                }

                return ReportEventResult.Recorded(change.EventId, data.Version + 1);
            }

            // Someone else changed the journey (or took the client event ID) in between: load again and retry (R11).
        }

        return ReportEventResult.Reject(RejectReason.VersionConflict);
    }

    /// <summary>
    /// A delay report asks for its reason to be verified (Critical or High by the reason). A departure that left at least 30
    /// seconds early is an early departure (High). Nothing else raises one (AC-E05-02 5). The station is where the train was:
    /// the departing stop, or the last reported stop before a delay report (it has no stop of its own).
    /// </summary>
    private static RaisedAt? WhatThisReportRaises(
        JourneyData data,
        IReadOnlyList<StopSchedule> schedule,
        ReportKind kind,
        Guid? stopId,
        DelayPayload? delay,
        DateTimeOffset occurredAt,
        IReadOnlyList<ReportedStopEvent> existingStops)
    {
        switch (kind)
        {
            case ReportKind.DelayReported:
                var lastStop = existingStops
                    .Where(e => e.OccurredAt < occurredAt)
                    .OrderBy(e => e.OccurredAt)
                    .Select(e => e.ServiceStopId)
                    .LastOrDefault();
                var station = lastStop == Guid.Empty ? (Guid?)null : data.Stops.First(s => s.ServiceStopId == lastStop).StationId;
                return new RaisedAt(ExceptionPolicy.ForDelay(delay!.Reason), station);

            case ReportKind.Departed:
                var scheduled = schedule.First(s => s.ServiceStopId == stopId).Departure;
                return ExceptionPolicy.ForDeparture(occurredAt, scheduled) is { } early
                    ? new RaisedAt(early, data.Stops.First(s => s.ServiceStopId == stopId).StationId)
                    : null;

            default:
                return null;
        }
    }
}
