namespace SmartRail.Domain.Journeys;

/// <summary>Why an assignment was ended before it ran out by itself (FR-56, spec 17).</summary>
public enum AssignmentEndReason { LostDevice, CrewChange, Other }

/// <summary>
/// The rules of spec 17 section 6 as pure functions: when an assignment covers an instant, whether two overlap, where an assignment starts
/// by default, and when a report is refused because the assignment was ended for a lost device. No I/O.
/// </summary>
public static class AssignmentRules
{
    /// <summary>ValidFrom is inclusive and ValidTo is exclusive; a null ValidTo is "until the journey ends" (open).</summary>
    public static bool Covers(DateTimeOffset validFrom, DateTimeOffset? validTo, DateTimeOffset at) => validFrom <= at && (validTo is null || at < validTo);

    /// <summary>Two ranges overlap when each starts before the other ends. An open end never ends.</summary>
    public static bool Overlaps(DateTimeOffset aFrom, DateTimeOffset? aTo, DateTimeOffset bFrom, DateTimeOffset? bTo) =>
        (bTo is null || aFrom < bTo) && (aTo is null || bFrom < aTo);

    /// <summary>
    /// Where an assignment starts when the controller does not say: the lead time before the first departure, but never earlier than now
    /// (spec 17 Q3). The lead lets the operator open the journey before the train leaves.
    /// </summary>
    public static DateTimeOffset DefaultFrom(DateTimeOffset firstDeparture, TimeSpan lead, DateTimeOffset now)
    {
        var start = firstDeparture - lead;
        return start < now ? now : start;
    }

    /// <summary>
    /// A start more than the tolerance before now would authorise old reports after the fact (EC-22), so it is refused. A little past is
    /// allowed for clock differences.
    /// </summary>
    public static bool StartsTooFarInThePast(DateTimeOffset validFrom, DateTimeOffset now, TimeSpan tolerance) => validFrom < now - tolerance;

    /// <summary>
    /// A report from an operator whose assignment was ended for a lost device is refused when it is RECEIVED at or after the end, whatever
    /// time it claims (FR-56, AC-E05-07 1). Any other end keeps accepting a report made during the assignment and synced later (EC-21).
    /// </summary>
    public static bool BlocksLateReport(AssignmentEndReason? endReason, DateTimeOffset? validTo, DateTimeOffset receivedAt) =>
        endReason == AssignmentEndReason.LostDevice && validTo is { } end && receivedAt >= end;
}
