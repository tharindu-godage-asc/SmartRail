namespace SmartRail.Domain.Reference;

/// <summary>A station on a line. Sequence 1 is the line origin; distance is measured from it.</summary>
public sealed class LineStation
{
    public Guid LineId { get; set; }
    public Guid StationId { get; set; }
    public int Sequence { get; set; }
    public decimal DistanceKm { get; set; }
}
