using SmartRail.Domain.Journeys;

namespace SmartRail.Application.Features.Journeys;

/// <summary>A stop of the journey's service with its station, scheduled local times of day, and whether it is the last one.</summary>
public sealed record StopInfo(
    Guid ServiceStopId,
    int Sequence,
    string StationCode,
    string StationName,
    TimeOnly? ScheduledArrival,
    TimeOnly? ScheduledDeparture,
    bool IsFinalStop,
    bool ArrivalReportRequired = false,
    Guid StationId = default)
{
    /// <summary>
    /// D-08, FR-22: an arrival is reported at a stop flagged for it and at the final stop, and only where the stop has a
    /// scheduled arrival (the first stop has none).
    /// </summary>
    public bool IsArrivalStation => (ArrivalReportRequired || IsFinalStop) && ScheduledArrival is not null;
}

/// <summary>The stored state of one stop of one journey (the result of the last rebuild, R10).</summary>
public sealed record StopStateInfo(
    Guid ServiceStopId,
    StopConfirmation Confirmation,
    DateTimeOffset? EstimatedArrival,
    DateTimeOffset? EstimatedDeparture,
    DateTimeOffset? ActualDeparture,
    DateTimeOffset? ActualArrival = null);

/// <summary>What a delay report said: the reason code and either a total in minutes or "unknown" (stored as the event payload).</summary>
public sealed record DelayPayload(string Reason, int? TotalDelayMinutes, bool DelayUnknown);

/// <summary>An accepted "arrived", "departed" or "delay reported" event, as stored (append-only). A delay report has no stop.</summary>
public sealed record RecordedEvent(
    Guid EventId,
    Guid ClientEventId,
    Guid? ServiceStopId,
    DateTimeOffset OccurredAt,
    int JourneyVersionAfter,
    ReportKind Kind = ReportKind.Departed,
    DelayPayload? Delay = null);

public sealed record AssignmentInfo(string OperatorUserId, DateTimeOffset ValidFrom, DateTimeOffset? ValidTo);

/// <summary>Everything the slice-1 handlers need to know about one journey.</summary>
public sealed record JourneyData(
    Guid JourneyId,
    string TrainNumber,
    DateOnly OperatingDate,
    JourneyStatus Status,
    int Version,
    int DelayMinutes,
    IReadOnlyList<StopInfo> Stops,
    IReadOnlyList<StopStateInfo> StopStates,
    IReadOnlyList<RecordedEvent> Events,
    IReadOnlyList<AssignmentInfo> Assignments,
    bool DelayKnown = true)
{
    public IReadOnlyList<StopSchedule> Schedule() =>
        ScheduleInstants.For(OperatingDate, Stops.Select(s => new StopTimes(s.ServiceStopId, s.Sequence, s.ScheduledArrival, s.ScheduledDeparture)));
}

/// <summary>The write of one accepted report: the event, the rebuilt stop states and the journey, guarded by the version.</summary>
public sealed record ReportChange(
    Guid JourneyId,
    int ExpectedVersion,
    Guid EventId,
    Guid ClientEventId,
    ReportKind Kind,
    Guid? ServiceStopId,
    DateTimeOffset OccurredAt,
    DateTimeOffset ReceivedAt,
    string CreatedBy,
    JourneyStatus NewStatus,
    int NewDelayMinutes,
    IReadOnlyList<StopProgress> NewStopStates,
    string? Payload = null,
    RaisedAt? Raised = null);

/// <summary>An exception raised by a report, and where the train was (null when nothing had been reported yet).</summary>
public sealed record RaisedAt(RaisedException Exception, Guid? StationId);

public enum SaveOutcome { Saved, Conflict }

/// <summary>Persistence for the journey slice. Implemented in Infrastructure with EF Core.</summary>
public interface IJourneyRepository
{
    /// <summary>One journey with its stops, stored stop states, accepted departures and assignments; null when unknown.</summary>
    Task<JourneyData?> GetAsync(Guid journeyId, CancellationToken cancellationToken);

    /// <summary>The train's journeys with operating dates from <paramref name="fromDate"/> to <paramref name="toDate"/> (stops and stop states, no events).</summary>
    Task<IReadOnlyList<JourneyData>> GetForTrainAsync(string trainNumber, DateOnly fromDate, DateOnly toDate, CancellationToken cancellationToken);

    /// <summary>The journey the operator is assigned to at <paramref name="now"/> (the latest valid_from wins), or null.</summary>
    Task<JourneyData?> GetAssignedAsync(string operatorUserId, DateTimeOffset now, CancellationToken cancellationToken);

    /// <summary>Saves the change in one transaction. Conflict means the journey version moved or the client event ID was taken meanwhile.</summary>
    Task<SaveOutcome> SaveReportAsync(ReportChange change, CancellationToken cancellationToken);
}

/// <summary>Events:* settings (FR-38). The defaults are the values in the spec.</summary>
public sealed class EventLimits
{
    public const string Section = "Events";

    public int MaxFutureMinutes { get; set; } = 1;
    public int MaxAgeHours { get; set; } = 8;
}

/// <summary>Journeys:* settings (A-07: thresholds are configurable).</summary>
public sealed class JourneySettings
{
    public const string Section = "Journeys";

    public int OnTimeToleranceMinutes { get; set; } = 2;

    /// <summary>
    /// How long a journey that is not Completed stays visible after its expected end (R15). Stays 15 when the arrival slice sets
    /// Completed: a Completed journey leaves "in progress" by its status (spec 06 R11), and 0 would hide a train that runs late.
    /// </summary>
    public int VisibleAfterEndMinutes { get; set; } = 15;
}
