namespace SmartRail.Domain.Catering;

public sealed record FoodStop(int Sequence, bool HasCatering, TimeOnly? Arrival, TimeOnly? Departure);

/// <summary>
/// Which stops of a trip get a menu (FR-91, D-15, A-07d): the destination, and any stop between boarding and destination where the train
/// stands at least the minimum minutes, as long as the station has catering. The boarding station is never listed.
/// </summary>
public static class FoodStopRule
{
    public const string Destination = "Destination";
    public const string LongStop = "LongStop";

    public static IReadOnlyList<(int Sequence, string Reason)> Eligible(IReadOnlyList<FoodStop> stops, int fromSequence, int toSequence, int minLongStopMinutes)
    {
        var result = new List<(int, string)>();
        foreach (var stop in stops.OrderBy(s => s.Sequence))
        {
            if (!stop.HasCatering || stop.Sequence <= fromSequence || stop.Sequence > toSequence)
            {
                continue;
            }

            if (stop.Sequence == toSequence)
            {
                result.Add((stop.Sequence, Destination));
            }
            else if (stop is { Arrival: { } arrival, Departure: { } departure } && (departure - arrival).TotalMinutes >= minLongStopMinutes)
            {
                result.Add((stop.Sequence, LongStop));
            }
        }

        return result;
    }
}
