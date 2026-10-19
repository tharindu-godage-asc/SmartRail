namespace SmartRail.Domain.Journeys;

public enum JourneyStatus { Unassigned, Scheduled, Running, Completed, Cancelled }

/// <summary>One run of a service on a date. Owns all operational data (ADR-11).</summary>
public sealed class Journey
{
    public Guid Id { get; set; }
    public Guid ServiceId { get; set; }
    public DateOnly OperatingDate { get; set; }
    public JourneyStatus Status { get; set; } = JourneyStatus.Unassigned;

    /// <summary>Optimistic-concurrency token, incremented with every write to the journey (ADR-09).</summary>
    public int Version { get; set; }

    public int CurrentDelayMinutes { get; set; }
    public DateTimeOffset? ExpectedNextEventBy { get; set; }
    public bool IsOverdue { get; set; }
}
