namespace SmartRail.Domain.Journeys;

/// <summary>A candidate journey for a train number: when it starts and when it is expected to end.</summary>
public sealed record JourneyWindow(Guid JourneyId, DateOnly OperatingDate, DateTimeOffset Start, DateTimeOffset ExpectedEnd);

/// <summary>
/// R15: which journey a passenger sees for a train number. First the journey in progress now (started, and not past its
/// expected end plus the grace period), so an overnight train never disappears at midnight; otherwise the journey whose
/// operating date is today; otherwise none. A finished journey from an earlier day is never shown.
/// </summary>
public static class JourneySelection
{
    public static Guid? Select(
        IReadOnlyCollection<JourneyWindow> candidates,
        DateOnly today,
        DateTimeOffset now,
        TimeSpan visibleAfterEnd)
    {
        var inProgress = candidates
            .Where(c => now >= c.Start && now <= c.ExpectedEnd + visibleAfterEnd)
            .OrderByDescending(c => c.Start)
            .FirstOrDefault();
        if (inProgress is not null)
        {
            return inProgress.JourneyId;
        }

        return candidates.FirstOrDefault(c => c.OperatingDate == today)?.JourneyId;
    }
}
