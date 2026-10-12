using SmartRail.Domain;

namespace SmartRail.Application.Features.Status;

/// <summary>First vertical slice placeholder: replace with the live train status query.</summary>
public sealed record ServiceStatus(string Line, StatusGroup Group, string Text, DateTimeOffset UpdatedAt);

public static class GetServiceStatus
{
    public static ServiceStatus Handle(TimeProvider clock) =>
        new("Main Line", StatusGroup.Normal, "Running normally", clock.GetUtcNow());
}
