using Microsoft.EntityFrameworkCore;
using Npgsql;
using SmartRail.Application.Features.Events.ReportEvent;
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

    public async Task<SaveOutcome> SaveReportAsync(ReportChange change, CancellationToken cancellationToken)
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
            EventType = change.Kind switch
            {
                ReportKind.Arrived => EventType.Arrived,
                ReportKind.DelayReported => EventType.DelayReported,
                _ => EventType.Departed,
            },
            ServiceStopId = change.ServiceStopId,
            OccurredAt = change.OccurredAt,
            ReceivedAt = change.ReceivedAt,
            Source = EventSource.Operator,
            CreatedBy = change.CreatedBy,
            Payload = change.Payload,
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
            row.ActualArrival = progress.ActualArrival;
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
            select new { stop.Id, stop.Sequence, station.Code, station.Name, stop.ScheduledArrival, stop.ScheduledDeparture, stop.ArrivalReportRequired })
            .ToListAsync(cancellationToken);
        var lastSequence = rawStops.Count == 0 ? 0 : rawStops.Max(s => s.Sequence);
        var stops = rawStops
            .Select(s => new StopInfo(
                s.Id, s.Sequence, s.Code, s.Name, s.ScheduledArrival, s.ScheduledDeparture, s.Sequence == lastSequence, s.ArrivalReportRequired))
            .ToList();

        var states = await db.JourneyStopStates.AsNoTracking()
            .Where(s => s.JourneyId == header.Id)
            .Select(s => new StopStateInfo(s.ServiceStopId, s.Confirmation, s.EstimatedArrival, s.EstimatedDeparture, s.ActualDeparture, s.ActualArrival))
            .ToListAsync(cancellationToken);

        // Events are always loaded: whether a delay is known is worked out from them (no extra column), and the public status needs it.
        var stored = await db.TrainEvents.AsNoTracking()
            .Where(e => e.JourneyId == header.Id
                && (e.EventType == EventType.Departed || e.EventType == EventType.Arrived || e.EventType == EventType.DelayReported))
            .OrderBy(e => e.OccurredAt)
            .Select(e => new { e.Id, e.ClientEventId, e.ServiceStopId, e.OccurredAt, e.JourneyVersionAfter, e.EventType, e.Payload })
            .ToListAsync(cancellationToken);
        IReadOnlyList<RecordedEvent> events = stored
            .Where(e => e.EventType == EventType.DelayReported || e.ServiceStopId is not null)
            .Select(e => new RecordedEvent(
                e.Id, e.ClientEventId, e.ServiceStopId, e.OccurredAt, e.JourneyVersionAfter,
                e.EventType switch
                {
                    EventType.Arrived => ReportKind.Arrived,
                    EventType.DelayReported => ReportKind.DelayReported,
                    _ => ReportKind.Departed,
                },
                e.EventType == EventType.DelayReported ? EventRules.FromJson(e.Payload) : null))
            .ToList();

        IReadOnlyList<AssignmentInfo> assignments = [];
        if (includeEventsAndAssignments)
        {
            assignments = await db.OperatorAssignments.AsNoTracking()
                .Where(a => a.JourneyId == header.Id)
                .Select(a => new AssignmentInfo(a.OperatorUserId, a.ValidFrom, a.ValidTo))
                .ToListAsync(cancellationToken);
        }

        var data = new JourneyData(
            header.Id, header.TrainNumber, header.OperatingDate, header.Status, header.Version, header.CurrentDelayMinutes,
            stops, states, events, assignments);

        // Is the length of the delay known? Only an operator report of "unknown" makes it unknown (spec 06 R7).
        var stopEvents = events.Where(e => e.Kind != ReportKind.DelayReported).Select(e => new ReportedStopEvent(e.ServiceStopId!.Value, e.Kind, e.OccurredAt)).ToList();
        var delays = events.Where(e => e.Kind == ReportKind.DelayReported).Select(e => new ReportedDelay(e.OccurredAt, e.Delay!.TotalDelayMinutes, e.Delay.DelayUnknown)).ToList();
        return stops.Count == 0 ? data : data with { DelayKnown = JourneyProgress.DelayInForce(data.Schedule(), stopEvents, delays).Known };
    }
}
