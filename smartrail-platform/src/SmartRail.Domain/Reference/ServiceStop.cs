namespace SmartRail.Domain.Reference;

public sealed class ServiceStop
{
    public Guid Id { get; set; }
    public Guid ServiceId { get; set; }
    public Guid StationId { get; set; }
    public int Sequence { get; set; }

    /// <summary>Local time of day (Asia/Colombo); null at the origin.</summary>
    public TimeOnly? ScheduledArrival { get; set; }

    /// <summary>Local time of day (Asia/Colombo); null at the destination.</summary>
    public TimeOnly? ScheduledDeparture { get; set; }

    public Guid? PlannedPlatformId { get; set; }
    public bool ArrivalReportRequired { get; set; }
    public bool CollectionEligible { get; set; }
}
