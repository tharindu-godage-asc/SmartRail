using Microsoft.EntityFrameworkCore;
using SmartRail.Application.Features.ControllerJourneys;

namespace SmartRail.Infrastructure.Persistence;

public sealed class ControllerJourneyQueries(SmartrailDbContext db) : IControllerJourneyQueries
{
    public async Task<ControllerJourneyExtras?> GetExtrasAsync(Guid journeyId, int maxEvents, CancellationToken cancellationToken)
    {
        var service = await (
            from j in db.Journeys.AsNoTracking()
            where j.Id == journeyId
            join s in db.TrainServices.AsNoTracking() on j.ServiceId equals s.Id
            select new { s.Id, s.Direction, s.ServiceCategory, s.ServiceType })
            .SingleOrDefaultAsync(cancellationToken);
        if (service is null)
        {
            return null;
        }

        var planned = await (
            from stop in db.ServiceStops.AsNoTracking()
            where stop.ServiceId == service.Id && stop.PlannedPlatformId != null
            join p in db.Platforms.AsNoTracking() on stop.PlannedPlatformId equals p.Id
            select new { stop.Id, p.Code })
            .ToDictionaryAsync(x => x.Id, x => x.Code, cancellationToken);

        var events = await db.TrainEvents.AsNoTracking()
            .Where(e => e.JourneyId == journeyId)
            .OrderByDescending(e => e.ReceivedAt).ThenByDescending(e => e.OccurredAt)
            .Take(maxEvents)
            .Select(e => new RawEvent(e.Id, e.EventType, e.Source, e.CreatedBy, e.ServiceStopId, e.OccurredAt, e.ReceivedAt, e.Payload))
            .ToListAsync(cancellationToken);

        return new ControllerJourneyExtras(service.Direction.ToString(), service.ServiceCategory.ToString(), service.ServiceType.ToString(), planned, events);
    }
}
