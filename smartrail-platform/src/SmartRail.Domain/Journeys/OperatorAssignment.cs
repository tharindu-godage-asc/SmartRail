namespace SmartRail.Domain.Journeys;

/// <summary>A time-bounded assignment of an operator to a journey. ValidTo is exclusive; null means until the journey ends.</summary>
public sealed class OperatorAssignment
{
    public Guid Id { get; set; }
    public Guid JourneyId { get; set; }

    /// <summary>The Keycloak subject of the operator.</summary>
    public required string OperatorUserId { get; set; }

    public DateTimeOffset ValidFrom { get; set; }
    public DateTimeOffset? ValidTo { get; set; }
    public required string AssignedBy { get; set; }
}
