namespace SmartRail.Domain.Reference;

public enum ServiceType { Slow, Express }

public enum ServiceCategory { Regional, LongDistance }

/// <summary>Outbound is away from the first station of the line, Inbound is toward it.</summary>
public enum ServiceDirection { Outbound, Inbound }

public sealed class TrainService
{
    public Guid Id { get; set; }
    public required string TrainNumber { get; set; }
    public Guid LineId { get; set; }
    public ServiceType ServiceType { get; set; }
    public ServiceCategory ServiceCategory { get; set; } = ServiceCategory.Regional;
    public ServiceDirection Direction { get; set; }
    public required string RunsOn { get; set; }
    public int UnreservedCapacity { get; set; }
}
