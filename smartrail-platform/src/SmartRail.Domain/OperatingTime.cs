namespace SmartRail.Domain;

/// <summary>
/// The system's one operating time zone (assumption A-TZ): stop times are local times of day in Asia/Colombo,
/// stored instants are UTC. Asia/Colombo has no daylight saving, so a local time maps to exactly one instant.
/// </summary>
public static class OperatingTime
{
    public const string ZoneId = "Asia/Colombo";

    public static TimeZoneInfo Zone { get; } = TimeZoneInfo.FindSystemTimeZoneById(ZoneId);

    /// <summary>The operating date "today" according to the given clock.</summary>
    public static DateOnly Today(TimeProvider clock) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(clock.GetUtcNow(), Zone).DateTime);

    /// <summary>The UTC instant of a local time of day on an operating date.</summary>
    public static DateTimeOffset ToInstant(DateOnly date, TimeOnly time)
    {
        var local = date.ToDateTime(time);
        return new DateTimeOffset(local, Zone.GetUtcOffset(local)).ToUniversalTime();
    }

    /// <summary>00:00 local time on the date, as a UTC instant.</summary>
    public static DateTimeOffset StartOfDay(DateOnly date) => ToInstant(date, TimeOnly.MinValue);

    /// <summary>00:00 local time on the following date (the exclusive end of the operating day).</summary>
    public static DateTimeOffset StartOfNextDay(DateOnly date) => StartOfDay(date.AddDays(1));
}
