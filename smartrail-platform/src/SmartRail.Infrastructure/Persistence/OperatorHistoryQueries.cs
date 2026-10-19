using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SmartRail.Application.Features.OperatorHistory;
using SmartRail.Domain.Journeys;

namespace SmartRail.Infrastructure.Persistence;

/// <summary>
/// Read-only history of one operator's journeys (spec 12). Only rows of the signed-in operator are ever selected: the list starts from his
/// assignments, and the record needs one of his assignments for that journey. A fixed number of queries, whatever the page size (no N+1).
/// </summary>
public sealed class OperatorHistoryQueries(SmartrailDbContext db) : IOperatorHistoryQueries
{
    public async Task<HistoryResponse> GetHistoryAsync(string subject, int limit, DateTimeOffset? before, DateTimeOffset now, CancellationToken cancellationToken)
    {
        // Past means finished for him: the journey is Completed, or his assignment has ended. A journey still running under an open
        // assignment is the Journey tab's, not history (spec 12 rule 2).
        var query =
            from a in db.OperatorAssignments.AsNoTracking()
            where a.OperatorUserId == subject
            join j in db.Journeys.AsNoTracking() on a.JourneyId equals j.Id
            where j.Status == JourneyStatus.Completed || (a.ValidTo != null && a.ValidTo <= now)
            select new { Assignment = a, Journey = j };

        if (before is { } cursor)
        {
            query = query.Where(x => x.Assignment.ValidFrom < cursor);
        }

        var rows = await query
            .OrderByDescending(x => x.Assignment.ValidFrom)
            .Take(limit + 1)
            .ToListAsync(cancellationToken);

        var hasMore = rows.Count > limit;
        rows = rows.Take(limit).ToList();
        if (rows.Count == 0)
        {
            return new HistoryResponse(now, [], null);
        }

        var journeyIds = rows.Select(r => r.Journey.Id).Distinct().ToList();
        var serviceIds = rows.Select(r => r.Journey.ServiceId).Distinct().ToList();

        var services = await db.TrainServices.AsNoTracking().Where(s => serviceIds.Contains(s.Id)).ToDictionaryAsync(s => s.Id, s => s.TrainNumber, cancellationToken);
        var stops = await (
            from s in db.ServiceStops.AsNoTracking()
            where serviceIds.Contains(s.ServiceId)
            join st in db.Stations.AsNoTracking() on s.StationId equals st.Id
            select new { s.Id, s.ServiceId, s.Sequence, s.ScheduledArrival, s.ScheduledDeparture, StationName = st.Name })
            .ToListAsync(cancellationToken);
        var actualArrivals = await db.JourneyStopStates.AsNoTracking()
            .Where(x => journeyIds.Contains(x.JourneyId) && x.ActualArrival != null)
            .Select(x => new { x.JourneyId, x.ServiceStopId, x.ActualArrival })
            .ToListAsync(cancellationToken);
        var counts = await db.TrainEvents.AsNoTracking()
            .Where(e => journeyIds.Contains(e.JourneyId) && e.CreatedBy == subject)
            .GroupBy(e => e.JourneyId)
            .Select(g => new { JourneyId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.JourneyId, g => g.Count, cancellationToken);

        var items = new List<HistoryJourney>();
        foreach (var row in rows)
        {
            var journeyStops = stops.Where(s => s.ServiceId == row.Journey.ServiceId).OrderBy(s => s.Sequence).ToList();
            var first = journeyStops[0];
            var last = journeyStops[^1];

            int? arrivalDelay = null;
            var schedule = ScheduleInstants.For(row.Journey.OperatingDate, journeyStops.Select(s => new StopTimes(s.Id, s.Sequence, s.ScheduledArrival, s.ScheduledDeparture)))
                .FirstOrDefault(s => s.ServiceStopId == last.Id);
            var actual = actualArrivals.FirstOrDefault(a => a.JourneyId == row.Journey.Id && a.ServiceStopId == last.Id)?.ActualArrival;
            if (actual is { } at && schedule?.Arrival is { } due)
            {
                arrivalDelay = JourneyProgress.DelayMinutes(at, due);
            }

            items.Add(new HistoryJourney(
                row.Journey.Id, services[row.Journey.ServiceId], first.StationName, last.StationName, row.Journey.OperatingDate,
                row.Journey.Status.ToString(), row.Assignment.ValidFrom, row.Assignment.ValidTo,
                counts.GetValueOrDefault(row.Journey.Id), arrivalDelay));
        }

        return new HistoryResponse(now, items, hasMore ? rows[^1].Assignment.ValidFrom : null);
    }

    public async Task<JourneyRecordResponse?> GetRecordAsync(string subject, Guid journeyId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var owned = await db.OperatorAssignments.AsNoTracking().AnyAsync(a => a.JourneyId == journeyId && a.OperatorUserId == subject, cancellationToken);
        if (!owned)
        {
            return null;
        }

        var journey = await db.Journeys.AsNoTracking().SingleOrDefaultAsync(j => j.Id == journeyId, cancellationToken);
        if (journey is null)
        {
            return null;
        }

        var trainNumber = await db.TrainServices.AsNoTracking().Where(s => s.Id == journey.ServiceId).Select(s => s.TrainNumber).SingleAsync(cancellationToken);
        var stops = await (
            from s in db.ServiceStops.AsNoTracking()
            where s.ServiceId == journey.ServiceId
            join st in db.Stations.AsNoTracking() on s.StationId equals st.Id
            orderby s.Sequence
            select new { s.Id, s.Sequence, s.ScheduledArrival, s.ScheduledDeparture, StationName = st.Name })
            .ToListAsync(cancellationToken);
        var states = await db.JourneyStopStates.AsNoTracking().Where(x => x.JourneyId == journeyId).ToDictionaryAsync(x => x.ServiceStopId, cancellationToken);
        var schedule = ScheduleInstants.For(journey.OperatingDate, stops.Select(s => new StopTimes(s.Id, s.Sequence, s.ScheduledArrival, s.ScheduledDeparture)))
            .ToDictionary(s => s.ServiceStopId);

        var recordStops = stops.Select(s =>
        {
            states.TryGetValue(s.Id, out var state);
            return new RecordStop(
                s.Id, s.StationName, s.Sequence, schedule[s.Id].Arrival, schedule[s.Id].Departure,
                state?.ActualArrival, state?.ActualDeparture, (state?.Confirmation ?? StopConfirmation.Pending).ToString());
        }).ToList();

        var names = stops.ToDictionary(s => s.Id, s => s.StationName);
        var events = await db.TrainEvents.AsNoTracking()
            .Where(e => e.JourneyId == journeyId && e.CreatedBy == subject && (e.EventType == EventType.Departed || e.EventType == EventType.Arrived || e.EventType == EventType.DelayReported))
            .OrderBy(e => e.OccurredAt)
            .ToListAsync(cancellationToken);

        var reports = events.Select(e =>
        {
            string? reason = null;
            int? total = null;
            if (e.EventType == EventType.DelayReported && !string.IsNullOrWhiteSpace(e.Payload))
            {
                using var document = JsonDocument.Parse(e.Payload);
                var root = document.RootElement;
                reason = root.TryGetProperty("reason", out var r) ? r.GetString() : null;
                total = root.TryGetProperty("totalDelayMinutes", out var t) && t.ValueKind == JsonValueKind.Number ? t.GetInt32() : null;
            }

            return new RecordReport(
                e.Id, e.EventType.ToString(), e.ServiceStopId is { } id && names.TryGetValue(id, out var name) ? name : null,
                e.OccurredAt, e.ReceivedAt, reason, total);
        }).ToList();

        return new JourneyRecordResponse(now, journey.Id, trainNumber, journey.OperatingDate, journey.Status.ToString(), recordStops, reports);
    }
}
