using System.Diagnostics.Metrics;

namespace SmartRail.Api.Authentication;

/// <summary>
/// smartrail.auth.rejected, tagged only with the reason (no per-user tags). deployment.environment is a resource attribute
/// that OpenTelemetry attaches to every metric, so it is on this one too.
/// </summary>
public static class AuthMetrics
{
    public const string MeterName = "SmartRail.Auth";

    public const string NoToken = "no_token";
    public const string InvalidToken = "invalid_token";
    public const string Forbidden = "forbidden";

    private static readonly Meter Meter = new(MeterName);
    private static readonly Counter<long> Rejected = Meter.CreateCounter<long>("smartrail.auth.rejected", description: "Requests refused by authentication or authorisation");

    public static void Reject(string reason) => Rejected.Add(1, new KeyValuePair<string, object?>("reason", reason));
}
