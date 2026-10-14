using Microsoft.EntityFrameworkCore;
using Npgsql;
using SmartRail.Application.Features.Journeys;
using SmartRail.Domain.Journeys;

namespace SmartRail.Infrastructure.Persistence;

/// <summary>EF Core implementation of the journey slice's persistence. Runs as the smartrail_app role (no UPDATE or DELETE on train_event).</summary>
public sealed class JourneyRepository(SmartrailDbContext db) : IJourneyRepository
{
    public async Task<JourneyData?> GetAsync(Guid journeyId, CancellationToken cancellationToken)
    {
        var header = await Header().Where(h => h.Id == journeyId).SingleOrDefaultAsync(cancellationToken);
        return header is null ? null : await LoadAsync(header, includeEventsAndAssignments: true, cancellationToken);
    }

    public async Task<IReadOnlyList<JourneyData>> GetForTrainAsync(
        string trainNumber, DateOnly fromDate, DateOnly toDate, CancellationToken cancellationToken)
    {
        var headers = await Header()
            .Where(h => h.TrainNumber == trainNumber && h.OperatingDate >= fromDate && h.OperatingDate <= toDate)
            .OrderBy(h => h.OperatingDate)
            .ToListAsync(cancellationToken);

        var result = new List<JourneyData>(headers.Count);
        foreach (var header in headers)
        {
            result.Add(await LoadAsync(header, includeEventsAndAssignments: false, cancellationToken));
        }

        return result;
    }

    public async Task<JourneyData?> GetAssignedAsync(string operatorUserId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var assignedJourney = await db.OperatorAssignments.AsNoTracking()
            .Where(a => a.OperatorUserId == operatorUserId && a.ValidFrom <= now && (a.ValidTo == null || now < a.ValidTo))
            .OrderByDescending(a => a.ValidFrom) // deferred decision D1: with overlapping assignments the latest one wins
            .Select(a => (Guid?)a.JourneyId)
            .FirstOrDefaultAsync(cancellationToken);

        return assignedJourney is { } id ? await GetAsync(id, cancellationToken) : null;
    }

    public async Task<SaveOutcome> SaveDepartureAsync(DepartureChange change, CancellationToken cancellationToken)
    {
        var journey = await db.Journeys.SingleAsync(j => j.Id == change.JourneyId, cancellationToken);
        if (journey.Version != change.ExpectedVersion)
        {
            return SaveOutcome.Conflict;
        }

        // EF adds "AND version = <original>" to this UPDATE, because Version is the concurrency token (ADR-09).
        journey.Version = change.ExpectedVersion + 1;
        journey.Status = change.NewStatus;
        journey.CurrentDelayMinutes = change.NewDelayMinutes;

        db.TrainEvents.Add(new TrainEvent
        {
            Id = change.EventId,
            JourneyId = change.JourneyId,
            ClientEventId = change.ClientEventId,
            EventType = EventType.Departed,
            ServiceStopId = change.ServiceStopId,
            OccurredAt = change.OccurredAt,
            ReceivedAt = change.ReceivedAt,
            Source = EventSource.Operator,
            CreatedBy = change.CreatedBy,
            JourneyVersionAfter = journey.Version,
        });

        var rows = await db.JourneyStopStates.Where(s => s.JourneyId == change.JourneyId).ToDictionaryAsync(s => s.ServiceStopId, cancellationToken);
        foreach (var progress in change.NewStopStates)
        {
            if (!rows.TryGetValue(progress.ServiceStopId, out var row))
            {
                row = new JourneyStopState { JourneyId = change.JourneyId, ServiceStopId = progress.ServiceStopId };
                db.JourneyStopStates.Add(row);
            }

            row.Confirmation = progress.Confirmation;
            row.EstimatedArrival = progress.EstimatedArrival;
            row.EstimatedDeparture = progress.EstimatedDeparture;
            row.ActualDeparture = progress.ActualDeparture;
        }

        try
        {
            // One SaveChanges is one transaction: the event, the stop states and the journey are saved together or not at all.
            await db.SaveChangesAsync(cancellationToken);
            return SaveOutcome.Saved;
        }
        catch (DbUpdateConcurrencyException)
        {
            db.ChangeTracker.Clear();
            return SaveOutcome.Conflict;
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // The same (journey, client event ID) was inserted by a parallel request: the retry will see it as a duplicate.
            db.ChangeTracker.Clear();
            return SaveOutcome.Conflict;
        }
    }

    /// <summary>A class with an object initializer, not a positional record: EF can filter on it (a constructor call it cannot see through).</summary>
    private sealed class JourneyHeader
    {
        public Guid Id { get; init; }
        public Guid ServiceId { get; init; }
        public string TrainNumber { get; init; } = "";
        public DateOnly OperatingDate { get; init; }
        public JourneyStatus Status { get; init; }
        public int Version { get; init; }
        public int CurrentDelayMinutes { get; init; }
    }

    private IQueryable<JourneyHeader> Header() =>
        from journey in db.Journeys.AsNoTracking()
        join service in db.TrainServices.AsNoTracking() on journey.ServiceId equals service.Id
        select new JourneyHeader
        {
            Id = journey.Id,
            ServiceId = journey.ServiceId,
            TrainNumber = service.TrainNumber,
            OperatingDate = journey.OperatingDate,
            Status = journey.Status,
            Version = journey.Version,
            CurrentDelayMinutes = journey.CurrentDelayMinutes,
        };

    private async Task<JourneyData> LoadAsync(JourneyHeader header, bool includeEventsAndAssignments, CancellationToken cancellationToken)
    {
        var rawStops = await (
            from stop in db.ServiceStops.AsNoTracking()
            join station in db.Stations.AsNoTracking() on stop.StationId equals station.Id
            where stop.ServiceId == header.ServiceId
            orderby stop.Sequence
            select new { stop.Id, stop.Sequence, station.Code, station.Name, stop.ScheduledArrival, stop.ScheduledDeparture })
            .ToListAsync(cancellationToken);
        var lastSequence = rawStops.Count == 0 ? 0 : rawStops.Max(s => s.Sequence);
        var stops = rawStops
            .Select(s => new StopInfo(s.Id, s.Sequence, s.Code, s.Name, s.ScheduledArrival, s.ScheduledDeparture, s.Sequence == lastSequence))
            .ToList();

        var states = await db.JourneyStopStates.AsNoTracking()
            .Where(s => s.JourneyId == header.Id)
            .Select(s => new StopStateInfo(s.ServiceStopId, s.Confirmation, s.EstimatedArrival, s.EstimatedDeparture, s.ActualDeparture))
            .ToListAsync(cancellationToken);

        IReadOnlyList<RecordedEvent> events = [];
        IReadOnlyList<AssignmentInfo> assignments = [];
        if (includeEventsAndAssignments)
        {
            events = await db.TrainEvents.AsNoTracking()
                .Where(e => e.JourneyId == header.Id && e.EventType == EventType.Departed && e.ServiceStopId != null)
                .OrderBy(e => e.OccurredAt)
                .Select(e => new RecordedEvent(e.Id, e.ClientEventId, e.ServiceStopId!.Value, e.OccurredAt, e.JourneyVersionAfter))
                .ToListAsync(cancellationToken);

            assignments = await db.OperatorAssignments.AsNoTracking()
                .Where(a => a.JourneyId == header.Id)
                .Select(a => new AssignmentInfo(a.OperatorUserId, a.ValidFrom, a.ValidTo))
                .ToListAsync(cancellationToken);
        }

        return new JourneyData(
            header.Id, header.TrainNumber, header.OperatingDate, header.Status, header.Version, header.CurrentDelayMinutes,
            stops, states, events, assignments);
    }
}
