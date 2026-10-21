using System.Diagnostics.Metrics;
using System.Text.Json;
using SmartRail.Application.Features.Timetable;
using SmartRail.Domain.Journeys;

namespace SmartRail.Application.Features.PlatformChanges;

public enum PlatformChangeError
{
    None,
    JourneyNotFound,
    StopNotFound,
    JourneyNotChangeable,
    StopAlreadyDeparted,
    PlatformNotAtStation,
    WarningsNotAcknowledged,
    VersionConflict,
}

/// <summary>Where the journey and the stop stand, for the rules: the stop's station and platforms, when the train is there, and the other trains at the station.</summary>
public sealed record PlatformChangeContext(
    JourneyStatus Status,
    int Version,
    string TrainNumber,
    string Direction,
    string StationCode,
    string StationName,
    DateTimeOffset At,
    bool Departed,
    string? PlannedPlatform,
    string? CurrentPlatform,
    IReadOnlyDictionary<string, Guid> PlatformIds,
    IReadOnlyList<OtherTrainAtStation> OtherTrains);

/// <summary>Another live journey that stops at the same station, with the platform it uses now (current, else planned).</summary>
public sealed record OtherTrainAtStation(string TrainNumber, string Direction, DateTimeOffset At, string? Platform);

public sealed record PlatformChangeSave(
    Guid JourneyId,
    Guid ServiceStopId,
    int ExpectedVersion,
    Guid? PlatformId,
    string? PlatformCode,
    string? PreviousPlatform,
    string StationCode,
    string Actor,
    DateTimeOffset Now);

public enum PlatformSaveOutcome { Saved, Conflict }

public interface IPlatformChangeRepository
{
    /// <summary>The state needed to judge a change; null when the journey does not exist, and a context with an empty stop when the stop is not of the journey (see <see cref="StopFound"/>).</summary>
    Task<(PlatformChangeContext? Context, bool StopFound)> GetContextAsync(Guid journeyId, Guid serviceStopId, CancellationToken cancellationToken);

    /// <summary>One transaction: the stop's current platform, the journey version and a PlatformChanged event (source Controller). Conflict when the version moved.</summary>
    Task<PlatformSaveOutcome> SaveAsync(PlatformChangeSave save, CancellationToken cancellationToken);
}

public sealed record PlatformChangeWarning(string TrainNumber, int MinutesApart, bool OppositeDirection);

public sealed record PlatformChangeResult(
    PlatformChangeError Error,
    string? Platform = null,
    string? PreviousPlatform = null,
    int Version = 0,
    IReadOnlyList<PlatformChangeWarning>? Warnings = null);

public static class PlatformChangeMetrics
{
    public const string MeterName = "SmartRail.PlatformChanges";
    private static readonly Counter<long> Changes = new Meter(MeterName).CreateCounter<long>("smartrail.platform.changes", description: "Platform changes by a controller, by result");

    public static void Count(string result) => Changes.Add(1, new KeyValuePair<string, object?>("result", result));
}

/// <summary>
/// A controller sets or clears the platform of one stop of one journey (spec 20, spec 16 F4, FR-57). The platform must be one of the stop's
/// station. A train at the same platform within <see cref="SchedulingSettings.PlatformWarningMinutes"/> is advice that has to be
/// acknowledged, never a block. A stop the train has left cannot be changed. Null clears the choice: the planned platform applies again.
/// </summary>
public sealed class ChangePlatformHandler(IPlatformChangeRepository repository, SchedulingSettings settings, TimeProvider clock)
{
    private const int MaxAttempts = 3;

    public async Task<PlatformChangeResult> HandleAsync(
        Guid journeyId, Guid serviceStopId, string? platformCode, bool acknowledgeWarnings, string actor, CancellationToken cancellationToken)
    {
        var result = await RunAsync(journeyId, serviceStopId, string.IsNullOrWhiteSpace(platformCode) ? null : platformCode.Trim(), acknowledgeWarnings, actor, cancellationToken);
        PlatformChangeMetrics.Count(result.Error == PlatformChangeError.None ? "ok" : result.Error == PlatformChangeError.WarningsNotAcknowledged ? "needs_confirmation" : "rejected");
        return result;
    }

    private async Task<PlatformChangeResult> RunAsync(
        Guid journeyId, Guid serviceStopId, string? platformCode, bool acknowledgeWarnings, string actor, CancellationToken cancellationToken)
    {
        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            var (context, stopFound) = await repository.GetContextAsync(journeyId, serviceStopId, cancellationToken);
            if (context is null)
            {
                return new PlatformChangeResult(PlatformChangeError.JourneyNotFound);
            }

            if (!stopFound)
            {
                return new PlatformChangeResult(PlatformChangeError.StopNotFound);
            }

            if (context.Status is JourneyStatus.Completed or JourneyStatus.Cancelled)
            {
                return new PlatformChangeResult(PlatformChangeError.JourneyNotChangeable);
            }

            if (context.Departed)
            {
                return new PlatformChangeResult(PlatformChangeError.StopAlreadyDeparted);
            }

            Guid? platformId = null;
            string? stored = null;
            if (platformCode is not null)
            {
                var match = context.PlatformIds.FirstOrDefault(p => string.Equals(p.Key, platformCode, StringComparison.OrdinalIgnoreCase));
                if (match.Key is null)
                {
                    return new PlatformChangeResult(PlatformChangeError.PlatformNotAtStation);
                }

                platformId = match.Value;
                stored = match.Key;

                // The platform the train would use after the change is what other trains are compared against.
                var warnings = PlatformChangeRules.Clashes(
                    context.At,
                    context.Direction,
                    context.OtherTrains.Where(o => string.Equals(o.Platform, stored, StringComparison.OrdinalIgnoreCase)).Select(o => new PlatformUse(o.TrainNumber, o.Direction, o.At)),
                    settings.PlatformWarningMinutes);
                if (warnings.Count > 0 && !acknowledgeWarnings)
                {
                    return new PlatformChangeResult(
                        PlatformChangeError.WarningsNotAcknowledged,
                        Warnings: warnings.Select(w => new PlatformChangeWarning(w.TrainNumber, w.MinutesApart, w.OppositeDirection)).ToList());
                }
            }

            var previous = context.CurrentPlatform ?? context.PlannedPlatform;
            var saved = await repository.SaveAsync(
                new PlatformChangeSave(journeyId, serviceStopId, context.Version, platformId, stored, context.CurrentPlatform, context.StationCode, actor, clock.GetUtcNow()),
                cancellationToken);
            if (saved == PlatformSaveOutcome.Saved)
            {
                return new PlatformChangeResult(PlatformChangeError.None, stored ?? context.PlannedPlatform, previous, context.Version + 1);
            }

            // The journey moved meanwhile (an operator report): load again and retry.
        }

        return new PlatformChangeResult(PlatformChangeError.VersionConflict);
    }

    /// <summary>The payload of the PlatformChanged event.</summary>
    public static string PayloadOf(string stationCode, string? platform, string? previous) =>
        JsonSerializer.Serialize(new { station = stationCode, platform, previous });
}
