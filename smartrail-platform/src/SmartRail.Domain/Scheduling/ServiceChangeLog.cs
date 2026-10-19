namespace SmartRail.Domain.Scheduling;

/// <summary>One audit entry for a change of the timetable: who, when, what (NFR-17). Written in the same transaction as the change.</summary>
public sealed class ServiceChangeLog
{
    public Guid Id { get; set; }
    public Guid ServiceId { get; set; }

    /// <summary>Created now; Amended and Cancelled arrive with S3 and S4.</summary>
    public required string Action { get; set; }

    /// <summary>The token subject of the controller.</summary>
    public required string Actor { get; set; }

    public DateTimeOffset At { get; set; }

    /// <summary>What changed, as JSON text (stored as jsonb).</summary>
    public string? Details { get; set; }
}
