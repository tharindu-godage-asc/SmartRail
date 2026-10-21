namespace SmartRail.Domain.Journeys;

/// <summary>
/// A journey is "overdue to start" when its first departure time has passed by more than a grace period and nothing has started it:
/// it is still Unassigned or Scheduled (the first stop report makes it Running). This is information for the controllers (nobody has
/// been assigned, or the operator has not reported the departure); SmartRail does not act on it.
/// </summary>
public static class OverdueRules
{
    public static bool IsOverdueToStart(JourneyStatus status, DateTimeOffset firstDeparture, DateTimeOffset now, int graceMinutes) =>
        status is JourneyStatus.Unassigned or JourneyStatus.Scheduled && now > firstDeparture + TimeSpan.FromMinutes(graceMinutes);

    /// <summary>Whole minutes past the departure time (rounded down, never negative).</summary>
    public static int MinutesLate(DateTimeOffset firstDeparture, DateTimeOffset now) => (int)Math.Max(0, Math.Floor((now - firstDeparture).TotalMinutes));
}
