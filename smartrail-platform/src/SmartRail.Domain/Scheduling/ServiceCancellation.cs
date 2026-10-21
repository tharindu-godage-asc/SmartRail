namespace SmartRail.Domain.Scheduling;

/// <summary>
/// A cancellation of a train number on the line for a range of dates (spec 14 Q2). The service row is the version the controller was
/// looking at, kept for the audit. Rows are never changed or deleted (D-70).
/// </summary>
public sealed class ServiceCancellation
{
    public Guid Id { get; set; }
    public Guid LineId { get; set; }
    public required string TrainNumber { get; set; }
    public Guid ServiceId { get; set; }
    public DateOnly FromDate { get; set; }

    /// <summary>Null means "from this date onward".</summary>
    public DateOnly? ToDate { get; set; }

    public required string Reason { get; set; }
    public required string CreatedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Set when a controller restores the service: the cancellation stays as history but no longer applies (nothing is deleted).</summary>
    public DateTimeOffset? RestoredAt { get; set; }

    public string? RestoredBy { get; set; }
}
