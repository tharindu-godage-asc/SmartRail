namespace SmartRail.Domain.Journeys;

/// <summary>A stop's scheduled times of day as stored in the database (local Asia/Colombo time, no date).</summary>
public sealed record StopTimes(Guid ServiceStopId, int Sequence, TimeOnly? Arrival, TimeOnly? Departure);

/// <summary>A stop's scheduled arrival and departure as UTC instants, placed on the right calendar day.</summary>
public sealed record StopSchedule(Guid ServiceStopId, int Sequence, DateTimeOffset? Arrival, DateTimeOffset? Departure);

/// <summary>
/// R16: the schema stores stop times as plain local times of day. The scheduled instants are found by walking the stops in
/// sequence from the journey's operating date: each time (arrival, then departure) that is earlier than the one before it is
/// on the next calendar day. A journey that leaves at 23:50 with stops at 00:20 and 08:00 therefore runs Thursday 23:50,
/// Friday 00:20, Friday 08:00. This supports journeys of at most 24 hours; Asia/Colombo has no daylight saving.
/// </summary>
public static class ScheduleInstants
{
    public static IReadOnlyList<StopSchedule> For(DateOnly operatingDate, IEnumerable<StopTimes> stops)
    {
        var day = operatingDate;
        TimeOnly? previous = null;
        var result = new List<StopSchedule>();

        DateTimeOffset? Place(TimeOnly? time)
        {
            if (time is not { } value)
            {
                return null;
            }

            if (previous is { } before && value < before)
            {
                day = day.AddDays(1);
            }

            previous = value;
            return OperatingTime.ToInstant(day, value);
        }

        foreach (var stop in stops.OrderBy(s => s.Sequence))
        {
            var arrival = Place(stop.Arrival);
            var departure = Place(stop.Departure);
            result.Add(new StopSchedule(stop.ServiceStopId, stop.Sequence, arrival, departure));
        }

        return result;
    }
}
