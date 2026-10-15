using System.Diagnostics.Metrics;
using SmartRail.Domain.Journeys;

namespace SmartRail.Application.Features.Exceptions;

/// <summary>smartrail.exceptions.raised and .resolved: how many items reached the controllers' queue and how many left it.</summary>
public static class ExceptionMetrics
{
    public const string MeterName = "SmartRail.Exceptions";

    private static readonly Meter Meter = new(MeterName);
    private static readonly Counter<long> RaisedCounter = Meter.CreateCounter<long>("smartrail.exceptions.raised", description: "Exceptions raised by reports, by type and severity");
    private static readonly Counter<long> ResolvedCounter = Meter.CreateCounter<long>("smartrail.exceptions.resolved", description: "Exceptions resolved by a controller, by action");

    public static void Raised(RaisedException exception) =>
        RaisedCounter.Add(1, new KeyValuePair<string, object?>("type", exception.Type.ToString()), new KeyValuePair<string, object?>("severity", exception.Severity.ToString()));

    public static void Resolved(string action) => ResolvedCounter.Add(1, new KeyValuePair<string, object?>("action", action));
}