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

    /// <summary>Weekly services run on their days from <see cref="EffectiveFrom"/>; an extra train runs on one date (spec 13).</summary>
    public Scheduling.ServiceKind Kind { get; set; } = Scheduling.ServiceKind.Weekly;

    /// <summary>Version of the service: an amendment is a new row with the next number (S4); every service so far is version 1.</summary>
    public int Version { get; set; } = 1;

    /// <summary>The first date the service runs. The seeded services have run since 2000-01-01.</summary>
    public DateOnly EffectiveFrom { get; set; } = new(2000, 1, 1);

    /// <summary>The last date it runs; null means it has no end.</summary>
    public DateOnly? EffectiveTo { get; set; }

    public Guid? SupersededServiceId { get; set; }
}
