using Microsoft.EntityFrameworkCore;
using SmartRail.Application.Features.Catering;
using SmartRail.Infrastructure.Persistence;

namespace SmartRail.Infrastructure.Catering;

/// <summary>The stops of a train from the timetable with the names and the catering flag of each station (spec 11 rule 5). No journey is needed.</summary>
public sealed class TripStops(SmartrailDbContext db) : ITripStops
{
    public async Task<IReadOnlyList<MenuStopRecord>?> GetStopsAsync(string trainNumber, CancellationToken cancellationToken)
    {
        var service = await db.TrainServices.AsNoTracking().FirstOrDefaultAsync(s => s.TrainNumber == trainNumber, cancellationToken);
        if (service is null)
        {
            return null;
        }

        var rows = await (
            from stop in db.ServiceStops.AsNoTracking()
            where stop.ServiceId == service.Id
            join station in db.Stations.AsNoTracking() on stop.StationId equals station.Id
            orderby stop.Sequence
            select new { stop.Sequence, station.Id, station.Code, station.Name, station.HasCateringFacility, stop.ScheduledArrival, stop.ScheduledDeparture })
            .ToListAsync(cancellationToken);

        var translations = await db.StationTranslations.AsNoTracking()
            .Where(t => rows.Select(r => r.Id).Contains(t.StationId))
            .ToListAsync(cancellationToken);

        return rows.Select(r =>
        {
            var names = new Dictionary<string, string> { ["en"] = r.Name };
            foreach (var t in translations.Where(t => t.StationId == r.Id))
            {
                names[t.LanguageCode] = t.Name;
            }

            return new MenuStopRecord(r.Sequence, r.Code, names, r.HasCateringFacility, r.ScheduledArrival, r.ScheduledDeparture);
        }).ToList();
    }
}
