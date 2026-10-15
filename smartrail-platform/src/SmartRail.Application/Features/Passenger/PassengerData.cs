using SmartRail.Application.Features.Journeys;
using SmartRail.Domain.Journeys;

namespace SmartRail.Application.Features.Passenger;

/// <summary>Search:* settings. The defaults are the values in the spec (R7).</summary>
public sealed class SearchSettings
{
    public const string Section = "Search";

    public int MaxDaysAhead { get; set; } = 7;
    public int MaxResults { get; set; } = 50;
}

public sealed record LineMembership(string LineCode, int Sequence);

public sealed record StationRecord(string Code, string NameEn, string? NameSi, string? NameTa, IReadOnlyList<LineMembership> Lines);

public sealed record LineRecord(string Code, string NameEn, string? NameSi, string? NameTa);

public sealed record ServiceStopRecord(Guid ServiceStopId, int Sequence, string StationCode, TimeOnly? ScheduledArrival, TimeOnly? ScheduledDeparture);

/// <summary>A timetable service with all its stops (the schedule instants need the whole walk, R16).</summary>
public sealed record ServiceRecord(
    Guid ServiceId,
    string TrainNumber,
    string LineCode,
    string ServiceType,
    string RunsOn,
    IReadOnlyList<ServiceStopRecord> Stops);

/// <summary>A journey with its stored stop states and the time its latest report was received (null when none).</summary>
public sealed record JourneyRecord(
    Guid JourneyId,
    Guid ServiceId,
    DateOnly OperatingDate,
    JourneyStatus Status,
    int Version,
    int DelayMinutes,
    IReadOnlyList<StopStateInfo> StopStates,
    DateTimeOffset? LastEventAt);

/// <summary>Read-only queries for the passenger endpoints. Implemented in Infrastructure with EF Core.</summary>
public interface IPassengerQueries
{
    /// <summary>All stations with their translations and line memberships.</summary>
    Task<IReadOnlyList<StationRecord>> GetStationsAsync(CancellationToken cancellationToken);

    Task<LineRecord?> GetLineAsync(string lineCode, CancellationToken cancellationToken);

    /// <summary>Services that have a stop at both stations (the caller checks the order of the two).</summary>
    Task<IReadOnlyList<ServiceRecord>> GetServicesCallingAtAsync(string firstStationCode, string secondStationCode, CancellationToken cancellationToken);

    Task<IReadOnlyList<ServiceRecord>> GetServicesOfLineAsync(string lineCode, CancellationToken cancellationToken);

    /// <summary>Journeys of the given services with operating dates from <paramref name="fromDate"/> to <paramref name="toDate"/>.</summary>
    Task<IReadOnlyList<JourneyRecord>> GetJourneysAsync(
        IReadOnlyCollection<Guid> serviceIds, DateOnly fromDate, DateOnly toDate, CancellationToken cancellationToken);
}

public sealed record LocalizedName(string En, string Si, string Ta)
{
    /// <summary>R2: a missing Sinhala or Tamil name falls back to English (a safety net; the seed has all three).</summary>
    public static LocalizedName Of(string en, string? si, string? ta) =>
        new(en, string.IsNullOrWhiteSpace(si) ? en : si, string.IsNullOrWhiteSpace(ta) ? en : ta);
}
