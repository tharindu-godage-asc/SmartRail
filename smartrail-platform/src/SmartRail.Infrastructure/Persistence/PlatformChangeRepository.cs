using Microsoft.EntityFrameworkCore;
using SmartRail.Application.Features.PlatformChanges;
using SmartRail.Domain.Journeys;

namespace SmartRail.Infrastructure.Persistence;

public sealed class PlatformChangeRepository(SmartrailDbContext db) : IPlatformChangeRepository
{
    public async Task<(PlatformChangeContext? Context, bool StopFound)> GetContextAsync(Guid journeyId, Guid serviceStopId, CancellationToken cancellationToken)
    {
        var journey = await db.Journeys.AsNoTracking().SingleOrDefaultAsync(j => j.Id == journeyId, cancellationToken);
        if (journey is null)
        {
            return (null, false);
        }

        var service = await db.TrainServices.AsNoTracking().SingleAsync(s => s.Id == journey.ServiceId, cancellationToken);
        var stops = await db.ServiceStops.AsNoTracking().Where(s => s.ServiceId == journey.ServiceId).OrderBy(s => s.Sequence).ToListAsync(cancellationToken);
        var stop = stops.FirstOrDefault(s => s.Id == serviceStopId);
        if (stop is null)
        {
            return (new PlatformChangeContext(journey.Status, journey.Version, service.TrainNumber, service.Direction.ToString(), "", "", default, false, null, null, new Dictionary<string, Guid>(), []), false);
        }

        var station = await db.Stations.AsNoTracking().SingleAsync(s => s.Id == stop.StationId, cancellationToken);
        var platforms = await db.Platforms.AsNoTracking().Where(p => p.StationId == station.Id).ToListAsync(cancellationToken);
        string? CodeOf(Guid? id) => id is { } value ? platforms.FirstOrDefault(p => p.Id == value)?.Code : null;

        var states = await db.JourneyStopStates.AsNoTracking().Where(s => s.JourneyId == journeyId).ToListAsync(cancellationToken);
        var state = states.FirstOrDefault(s => s.ServiceStopId == serviceStopId);

        // The train has left this stop when it has a departure here, or any later stop has an arrival or a departure.
        var laterIds = stops.Where(s => s.Sequence > stop.Sequence).Select(s => s.Id).ToHashSet();
        var departed = state?.ActualDeparture is not null
            || states.Any(s => laterIds.Contains(s.ServiceStopId) && (s.ActualArrival is not null || s.ActualDeparture is not null));

        var at = TimeAtStation(journey.OperatingDate, stops, stop.Id, state);

        // Other live journeys that stop here in the days around this one, and the platform each uses now.
        var around = await (
            from j in db.Journeys.AsNoTracking()
            where j.Id != journeyId && j.Status != JourneyStatus.Completed && j.Status != JourneyStatus.Cancelled
                  && j.OperatingDate >= journey.OperatingDate.AddDays(-1) && j.OperatingDate <= journey.OperatingDate.AddDays(1)
            join s in db.TrainServices.AsNoTracking() on j.ServiceId equals s.Id
            select new { Journey = j, Service = s }).ToListAsync(cancellationToken);
        var serviceIds = around.Select(a => a.Service.Id).Distinct().ToList();
        var otherStops = await db.ServiceStops.AsNoTracking().Where(s => serviceIds.Contains(s.ServiceId)).ToListAsync(cancellationToken);
        var otherJourneyIds = around.Select(a => a.Journey.Id).ToList();
        var otherStates = await db.JourneyStopStates.AsNoTracking().Where(s => otherJourneyIds.Contains(s.JourneyId)).ToListAsync(cancellationToken);

        var others = new List<OtherTrainAtStation>();
        foreach (var a in around)
        {
            var theirStops = otherStops.Where(s => s.ServiceId == a.Service.Id).OrderBy(s => s.Sequence).ToList();
            var here = theirStops.FirstOrDefault(s => s.StationId == station.Id);
            if (here is null)
            {
                continue;
            }

            var theirState = otherStates.FirstOrDefault(s => s.JourneyId == a.Journey.Id && s.ServiceStopId == here.Id);
            var platform = CodeOf(theirState?.PlatformId) ?? CodeOf(here.PlannedPlatformId);
            others.Add(new OtherTrainAtStation(a.Service.TrainNumber, a.Service.Direction.ToString(), TimeAtStation(a.Journey.OperatingDate, theirStops, here.Id, theirState), platform));
        }

        return (new PlatformChangeContext(
            journey.Status, journey.Version, service.TrainNumber, service.Direction.ToString(), station.Code, station.Name, at, departed,
            CodeOf(stop.PlannedPlatformId), CodeOf(state?.PlatformId), platforms.ToDictionary(p => p.Code, p => p.Id), others), true);
    }

    /// <summary>When the train is at the station: the estimate if there is one, otherwise the schedule (arrival, else departure).</summary>
    private static DateTimeOffset TimeAtStation(DateOnly operatingDate, IReadOnlyList<Domain.Reference.ServiceStop> stops, Guid stopId, JourneyStopState? state)
    {
        var schedule = ScheduleInstants.For(operatingDate, stops.Select(s => new StopTimes(s.Id, s.Sequence, s.ScheduledArrival, s.ScheduledDeparture)));
        var planned = schedule.First(s => s.ServiceStopId == stopId);
        return state?.EstimatedArrival ?? state?.EstimatedDeparture ?? planned.Arrival ?? planned.Departure ?? DateTimeOffset.MinValue;
    }

    public async Task<PlatformSaveOutcome> SaveAsync(PlatformChangeSave save, CancellationToken cancellationToken)
    {
        db.ChangeTracker.Clear();
        var journey = await db.Journeys.SingleAsync(j => j.Id == save.JourneyId, cancellationToken);
        if (journey.Version != save.ExpectedVersion)
        {
            return PlatformSaveOutcome.Conflict;
        }

        // The version moves, so a client holding the older status takes the new one (ADR-09).
        journey.Version = save.ExpectedVersion + 1;

        var state = await db.JourneyStopStates.SingleOrDefaultAsync(s => s.JourneyId == save.JourneyId && s.ServiceStopId == save.ServiceStopId, cancellationToken);
        if (state is null)
        {
            state = new JourneyStopState { JourneyId = save.JourneyId, ServiceStopId = save.ServiceStopId };
            db.JourneyStopStates.Add(state);
        }

        state.PlatformId = save.PlatformId;

        db.TrainEvents.Add(new TrainEvent
        {
            Id = Guid.CreateVersion7(save.Now),
            JourneyId = save.JourneyId,
            ClientEventId = Guid.NewGuid(),
            EventType = EventType.PlatformChanged,
            ServiceStopId = save.ServiceStopId,
            OccurredAt = save.Now,
            ReceivedAt = save.Now,
            Source = EventSource.Controller,
            CreatedBy = save.Actor,
            Payload = ChangePlatformHandler.PayloadOf(save.StationCode, save.PlatformCode, save.PreviousPlatform),
            JourneyVersionAfter = journey.Version,
        });

        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return PlatformSaveOutcome.Saved;
        }
        catch (DbUpdateConcurrencyException)
        {
            db.ChangeTracker.Clear();
            return PlatformSaveOutcome.Conflict;
        }
    }
}
