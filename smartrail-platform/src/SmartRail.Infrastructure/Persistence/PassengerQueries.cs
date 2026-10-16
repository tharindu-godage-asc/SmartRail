using Microsoft.EntityFrameworkCore;
using SmartRail.Application.Features.Journeys;
using SmartRail.Application.Features.Passenger;

namespace SmartRail.Infrastructure.Persistence;

/// <summary>Read-only EF Core queries for the passenger endpoints. Reference data is small, so it is loaded whole; nothing is tracked.</summary>
public sealed class PassengerQueries(SmartrailDbContext db) : IPassengerQueries
{
    private const string Sinhala = "si";
    private const string Tamil = "ta";

    public async Task<IReadOnlyList<StationRecord>> GetStationsAsync(CancellationToken cancellationToken)
    {
        var stations = await db.Stations.AsNoTracking().ToListAsync(cancellationToken);
        var translations = await db.StationTranslations.AsNoTracking().ToListAsync(cancellationToken);
        var lines = await db.Lines.AsNoTracking().ToDictionaryAsync(l => l.Id, l => l.Code, cancellationToken);
        var memberships = await db.LineStations.AsNoTracking().ToListAsync(cancellationToken);

        return stations.Select(s => new StationRecord(
            s.Code,
            s.Name,
            translations.FirstOrDefault(t => t.StationId == s.Id && t.LanguageCode == Sinhala)?.Name,
            translations.FirstOrDefault(t => t.StationId == s.Id && t.LanguageCode == Tamil)?.Name,
            memberships.Where(m => m.StationId == s.Id).Select(m => new LineMembership(lines[m.LineId], m.Sequence)).ToList()))
            .ToList();
    }

    public async Task<LineRecord?> GetLineAsync(string lineCode, CancellationToken cancellationToken)
    {
        var line = await db.Lines.AsNoTracking().FirstOrDefaultAsync(l => l.Code.ToUpper() == lineCode.ToUpper(), cancellationToken);
        if (line is null)
        {
            return null;
        }

        var translations = await db.LineTranslations.AsNoTracking().Where(t => t.LineId == line.Id).ToListAsync(cancellationToken);
        return new LineRecord(
            line.Code,
            line.Name,
            translations.FirstOrDefault(t => t.LanguageCode == Sinhala)?.Name,
            translations.FirstOrDefault(t => t.LanguageCode == Tamil)?.Name);
    }

    public async Task<IReadOnlyList<ServiceRecord>> GetServicesCallingAtAsync(
        string firstStationCode, string secondStationCode, CancellationToken cancellationToken)
    {
        var stations = await db.Stations.AsNoTracking().ToDictionaryAsync(s => s.Id, s => s.Code, cancellationToken);
        var firstId = stations.FirstOrDefault(s => s.Value == firstStationCode).Key;
        var secondId = stations.FirstOrDefault(s => s.Value == secondStationCode).Key;

        var stopsAtFirst = db.ServiceStops.AsNoTracking().Where(s => s.StationId == firstId).Select(s => s.ServiceId);
        var stopsAtSecond = db.ServiceStops.AsNoTracking().Where(s => s.StationId == secondId).Select(s => s.ServiceId);
        var serviceIds = await stopsAtFirst.Intersect(stopsAtSecond).ToListAsync(cancellationToken);

        return await LoadServicesAsync(db.TrainServices.AsNoTracking().Where(s => serviceIds.Contains(s.Id)), stations, cancellationToken);
    }

    public async Task<IReadOnlyList<ServiceRecord>> GetServicesOfLineAsync(string lineCode, CancellationToken cancellationToken)
    {
        var stations = await db.Stations.AsNoTracking().ToDictionaryAsync(s => s.Id, s => s.Code, cancellationToken);
        var lineId = await db.Lines.AsNoTracking().Where(l => l.Code == lineCode).Select(l => (Guid?)l.Id).FirstOrDefaultAsync(cancellationToken);
        if (lineId is null)
        {
            return [];
        }

        return await LoadServicesAsync(db.TrainServices.AsNoTracking().Where(s => s.LineId == lineId), stations, cancellationToken);
    }

    public async Task<IReadOnlyList<JourneyRecord>> GetJourneysAsync(
        IReadOnlyCollection<Guid> serviceIds, DateOnly fromDate, DateOnly toDate, CancellationToken cancellationToken)
    {
        var journeys = await db.Journeys.AsNoTracking()
            .Where(j => serviceIds.Contains(j.ServiceId) && j.OperatingDate >= fromDate && j.OperatingDate <= toDate)
            .ToListAsync(cancellationToken);
        if (journeys.Count == 0)
        {
            return [];
        }

        var ids = journeys.Select(j => j.Id).ToList();
        var states = await db.JourneyStopStates.AsNoTracking().Where(s => ids.Contains(s.JourneyId)).ToListAsync(cancellationToken);

        // The latest time the server received a report for each journey (null when it has none).
        var lastReceived = await db.TrainEvents.AsNoTracking()
            .Where(e => ids.Contains(e.JourneyId))
            .GroupBy(e => e.JourneyId)
            .Select(g => new { JourneyId = g.Key, At = g.Max(e => e.ReceivedAt) })
            .ToDictionaryAsync(x => x.JourneyId, x => x.At, cancellationToken);

        return journeys.Select(j => new JourneyRecord(
            j.Id, j.ServiceId, j.OperatingDate, j.Status, j.Version, j.CurrentDelayMinutes,
            states.Where(s => s.JourneyId == j.Id)
                .Select(s => new StopStateInfo(s.ServiceStopId, s.Confirmation, s.EstimatedArrival, s.EstimatedDeparture, s.ActualDeparture, s.ActualArrival))
                .ToList(),
            lastReceived.TryGetValue(j.Id, out var at) ? at : null))
            .ToList();
    }

    public async Task<IReadOnlyList<ServiceRecord>> GetServicesAtAsync(string stationCode, CancellationToken cancellationToken)
    {
        var stations = await db.Stations.AsNoTracking().ToDictionaryAsync(s => s.Id, s => s.Code, cancellationToken);
        var stationId = stations.FirstOrDefault(s => s.Value == stationCode).Key;
        var serviceIds = await db.ServiceStops.AsNoTracking().Where(s => s.StationId == stationId).Select(s => s.ServiceId).Distinct().ToListAsync(cancellationToken);

        return await LoadServicesAsync(db.TrainServices.AsNoTracking().Where(s => serviceIds.Contains(s.Id)), stations, cancellationToken);
    }

    public async Task<IReadOnlyList<string>> GetPlatformCodesAsync(string stationCode, CancellationToken cancellationToken) =>
        await (from platform in db.Platforms.AsNoTracking()
               join station in db.Stations.AsNoTracking() on platform.StationId equals station.Id
               where station.Code == stationCode
               orderby platform.Code
               select platform.Code).ToListAsync(cancellationToken);

    private async Task<IReadOnlyList<ServiceRecord>> LoadServicesAsync(
        IQueryable<Domain.Reference.TrainService> services, Dictionary<Guid, string> stationCodes, CancellationToken cancellationToken)
    {
        var list = await services.ToListAsync(cancellationToken);
        if (list.Count == 0)
        {
            return [];
        }

        var ids = list.Select(s => s.Id).ToList();
        var stops = await db.ServiceStops.AsNoTracking().Where(s => ids.Contains(s.ServiceId)).ToListAsync(cancellationToken);
        var lineCodes = await db.Lines.AsNoTracking().ToDictionaryAsync(l => l.Id, l => l.Code, cancellationToken);
        var platformCodes = await db.Platforms.AsNoTracking().ToDictionaryAsync(p => p.Id, p => p.Code, cancellationToken);

        return list.Select(s => new ServiceRecord(
            s.Id, s.TrainNumber, lineCodes[s.LineId], s.ServiceType.ToString(), s.RunsOn,
            stops.Where(p => p.ServiceId == s.Id).OrderBy(p => p.Sequence)
                .Select(p => new ServiceStopRecord(
                    p.Id, p.Sequence, stationCodes[p.StationId], p.ScheduledArrival, p.ScheduledDeparture,
                    p.PlannedPlatformId is { } platformId && platformCodes.TryGetValue(platformId, out var code) ? code : null))
                .ToList()))
            .ToList();
    }
}
