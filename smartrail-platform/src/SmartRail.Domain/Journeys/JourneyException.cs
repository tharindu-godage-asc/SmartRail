namespace SmartRail.Domain.Journeys;

/// <summary>
/// What needs a controller's attention (discovery section 19.4, D-44). Only the types a report can produce today are here;
/// the overdue, conflict, flag and suspension types arrive with the slices that can raise them, each with a migration that
/// widens the table's check constraint.
/// </summary>
public enum ExceptionType { ReasonAwaitingVerification, EarlyDeparture }

/// <summary>Critical is for a fault, an obstruction or the weather; everything else in the queue is High (AC-E05-02 1).</summary>
public enum ExceptionSeverity { Critical, High }

/// <summary>One item in the controllers' queue. Raised with the report that caused it, resolved by a controller (FR-43, FR-51).</summary>
public sealed class JourneyException
{
    public Guid Id { get; set; }
    public Guid JourneyId { get; set; }

    /// <summary>The report that raised it. One report raises at most one exception of a type (D-44, AC-E05-02 2).</summary>
    public Guid? RelatedEventId { get; set; }

    /// <summary>
    /// Where the train was when it was raised: the station of the departure, or the last reported stop before a delay report
    /// (a delay report has no stop of its own). Null when nothing had been reported yet.
    /// </summary>
    public Guid? StationId { get; set; }

    public ExceptionType Type { get; set; }
    public ExceptionSeverity Severity { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? ResolvedAt { get; set; }

    /// <summary>The Keycloak subject of the controller who resolved it.</summary>
    public string? ResolvedBy { get; set; }

    /// <summary>The controller's user name at the time, for display only (null for older rows or a token without one). The subject above is the audit key.</summary>
    public string? ResolvedByName { get; set; }

    /// <summary>The action the controller chose (<see cref="ExceptionResolution"/>).</summary>
    public string? Resolution { get; set; }
}

/// <summary>The actions a controller can choose when resolving (Q3): fixed, so the audit trail can be counted and tested.</summary>
public static class ExceptionResolution
{
    public const string Acknowledged = "Acknowledged";
    public const string NoActionNeeded = "NoActionNeeded";
    public const string Escalated = "Escalated";

    public static readonly IReadOnlyList<string> All = [Acknowledged, NoActionNeeded, Escalated];
}

public sealed record RaisedException(ExceptionType Type, ExceptionSeverity Severity);

/// <summary>
/// Which accepted reports raise an exception, as plain functions (spec 07 R1 to R4). One report raises at most one exception.
/// A forward skip, a late report in the right order, a retry, an early arrival and an on-time departure raise nothing
/// (AC-E05-02 5, EC-54).
/// </summary>
public static class ExceptionPolicy
{
    /// <summary>Q7: a departure is early when it happened at least this long before its schedule (the same half-minute rounding as the delay).</summary>
    public static readonly TimeSpan EarlyDepartureThreshold = TimeSpan.FromSeconds(30);

    /// <summary>
    /// A delay report always asks the controller to verify its reason (FR-52). Critical for a technical fault, an obstruction
    /// or weather; High for a signal issue or "Unknown" (D-44, AC-E05-02 1).
    /// </summary>
    public static RaisedException ForDelay(string reason) => new(
        ExceptionType.ReasonAwaitingVerification,
        reason is "TechnicalFault" or "Obstruction" or "Weather" ? ExceptionSeverity.Critical : ExceptionSeverity.High);

    /// <summary>High when the train left at least 30 seconds before its scheduled time (EC-08, D-49); the report is accepted all the same.</summary>
    public static RaisedException? ForDeparture(DateTimeOffset occurredAt, DateTimeOffset? scheduledDeparture) =>
        scheduledDeparture is { } scheduled && scheduled - occurredAt >= EarlyDepartureThreshold
            ? new RaisedException(ExceptionType.EarlyDeparture, ExceptionSeverity.High)
            : null;
}
