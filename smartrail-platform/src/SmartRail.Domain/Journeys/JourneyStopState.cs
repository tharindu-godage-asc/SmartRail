namespace SmartRail.Domain.Journeys;

public enum StopConfirmation { Pending, Reported, Inferred }

public sealed class JourneyStopState
{
    public Guid JourneyId { get; set; }
    public Guid ServiceStopId { get; set; }
    public DateTimeOffset? EstimatedArrival { get; set; }
    public DateTimeOffset? EstimatedDeparture { get; set; }
    public DateTimeOffset? ActualArrival { get; set; }
    public DateTimeOffset? ActualDeparture { get; set; }

    /// <summary>The current platform; null means the planned platform applies.</summary>
    public Guid? PlatformId { get; set; }

    public StopConfirmation Confirmation { get; set; } = StopConfirmation.Pending;
}
