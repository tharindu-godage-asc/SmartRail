namespace SmartRail.Domain.Journeys;

/// <summary>Another train that uses the platform in question: its number, direction and when it is at the station (estimated, else scheduled).</summary>
public sealed record PlatformUse(string TrainNumber, string Direction, DateTimeOffset At);

/// <summary>Advice only (FR-57): another train is at the same platform within a few minutes. Opposite directions are worded more strongly.</summary>
public sealed record PlatformClashWarning(string TrainNumber, int MinutesApart, bool OppositeDirection);

/// <summary>
/// The platform advice of spec 20 (spec 16 F4) as a plain function. A platform change is never refused because of another train: the
/// controller is told and confirms. SmartRail records the decision; it does not control trains or detect collisions (FR-63).
/// </summary>
public static class PlatformChangeRules
{
    /// <summary>Trains within <paramref name="windowMinutes"/> (inclusive) of this train's time at the same platform, nearest first.</summary>
    public static IReadOnlyList<PlatformClashWarning> Clashes(DateTimeOffset at, string direction, IEnumerable<PlatformUse> others, int windowMinutes) =>
        others
            .Select(o => (Use: o, Minutes: (int)Math.Round(Math.Abs((o.At - at).TotalMinutes), MidpointRounding.AwayFromZero)))
            .Where(x => x.Minutes <= windowMinutes)
            .OrderBy(x => x.Minutes)
            .Select(x => new PlatformClashWarning(x.Use.TrainNumber, x.Minutes, !string.Equals(x.Use.Direction, direction, StringComparison.OrdinalIgnoreCase)))
            .ToList();
}
