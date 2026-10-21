using Microsoft.EntityFrameworkCore;
using SmartRail.Application.Features.Assignments;
using SmartRail.Domain;
using SmartRail.Domain.Journeys;

namespace SmartRail.Infrastructure.Persistence;

/// <summary>
/// Operator assignments (spec 17). Every change runs in one transaction with the journey row locked (<c>FOR UPDATE</c>), so two controllers
/// cannot put two operators on one journey at once (rule 3). Names are never stored: the handlers fill them in from the directory.
/// </summary>
public sealed class AssignmentRepository(SmartrailDbContext db) : IAssignmentRepository
{
    private static AssignmentView ViewOf(OperatorAssignment a) => new(
        a.Id, a.OperatorUserId, a.OperatorUserId, a.ValidFrom, a.ValidTo, a.AssignedBy, a.EndedBy, a.EndReason?.ToString(), a.EndNote);

    private async Task<(DateTimeOffset FirstDeparture, DateTimeOffset LastArrival)> TimesOfAsync(Guid serviceId, DateOnly date, CancellationToken cancellationToken)
    {
        var stops = await db.ServiceStops.AsNoTracking().Where(s => s.ServiceId == serviceId).OrderBy(s => s.Sequence).ToListAsync(cancellationToken);
        var schedule = ScheduleInstants.For(date, stops.Select(s => new StopTimes(s.Id, s.Sequence, s.ScheduledArrival, s.ScheduledDeparture)));
        var first = schedule[0];
        var last = schedule[^1];
        return (first.Departure ?? first.Arrival ?? OperatingTime.StartOfDay(date), last.Arrival ?? last.Departure ?? OperatingTime.StartOfNextDay(date));
    }

    public async Task<(DateTimeOffset FirstDeparture, DateTimeOffset LastArrival)?> GetJourneyTimesAsync(Guid journeyId, CancellationToken cancellationToken)
    {
        var journey = await db.Journeys.AsNoTracking().SingleOrDefaultAsync(j => j.Id == journeyId, cancellationToken);
        return journey is null ? null : await TimesOfAsync(journey.ServiceId, journey.OperatingDate, cancellationToken);
    }

    public async Task<IReadOnlyList<JourneyListItem>> ListJourneysAsync(DateOnly date, bool unassignedOnly, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var journeys = await db.Journeys.AsNoTracking()
            // The Unassigned tab lists only journeys that can still be assigned; All journeys also lists the finished and cancelled ones, for the filters.
            .Where(j => j.OperatingDate == date && (!unassignedOnly || (j.Status != JourneyStatus.Cancelled && j.Status != JourneyStatus.Completed)))
            .ToListAsync(cancellationToken);
        if (journeys.Count == 0)
        {
            return [];
        }

        var ids = journeys.Select(j => j.Id).ToList();
        var serviceIds = journeys.Select(j => j.ServiceId).Distinct().ToList();
        var services = await db.TrainServices.AsNoTracking().Where(s => serviceIds.Contains(s.Id)).ToDictionaryAsync(s => s.Id, cancellationToken);
        var assignments = await db.OperatorAssignments.AsNoTracking().Where(a => ids.Contains(a.JourneyId)).ToListAsync(cancellationToken);
        var stops = await (
            from p in db.ServiceStops.AsNoTracking()
            where serviceIds.Contains(p.ServiceId)
            join st in db.Stations.AsNoTracking() on p.StationId equals st.Id
            select new { p.ServiceId, p.Id, p.Sequence, p.ScheduledArrival, p.ScheduledDeparture, st.Name })
            .ToListAsync(cancellationToken);

        var items = new List<JourneyListItem>();
        foreach (var journey in journeys)
        {
            var mine = assignments.Where(a => a.JourneyId == journey.Id).ToList();
            // Unassigned means no assignment now or later: a journey whose only assignment has ended is back in the list.
            if (unassignedOnly && mine.Any(a => a.ValidTo is null || a.ValidTo > now))
            {
                continue;
            }

            var own = stops.Where(s => s.ServiceId == journey.ServiceId).OrderBy(s => s.Sequence).ToList();
            var schedule = ScheduleInstants.For(date, own.Select(s => new StopTimes(s.Id, s.Sequence, s.ScheduledArrival, s.ScheduledDeparture)));
            var service = services[journey.ServiceId];
            var covering = mine.Where(a => AssignmentRules.Covers(a.ValidFrom, a.ValidTo, now)).OrderByDescending(a => a.ValidFrom).FirstOrDefault();
            items.Add(new JourneyListItem(
                journey.Id, service.TrainNumber, own[0].Name, own[^1].Name, date,
                schedule[0].Departure ?? schedule[0].Arrival ?? OperatingTime.StartOfDay(date), schedule[^1].Arrival ?? schedule[^1].Departure ?? OperatingTime.StartOfNextDay(date),
                journey.Status.ToString(), service.Kind.ToString(),
                covering is null ? null : new CurrentOperator(covering.OperatorUserId, covering.OperatorUserId, covering.ValidFrom, covering.ValidTo)));
        }

        return items.OrderBy(i => i.FirstDeparture).ThenBy(i => i.TrainNumber, StringComparer.Ordinal).ToList();
    }

    public async Task<IReadOnlyDictionary<string, BusyOn>> BusyOperatorsAsync(Guid? journeyId, DateTimeOffset from, DateTimeOffset? to, CancellationToken cancellationToken)
    {
        var horizon = to ?? DateTimeOffset.MaxValue;
        var candidates = await db.OperatorAssignments.AsNoTracking()
            .Where(a => (journeyId == null || a.JourneyId != journeyId) && a.ValidFrom < horizon && (a.ValidTo == null || a.ValidTo > from))
            .ToListAsync(cancellationToken);
        if (candidates.Count == 0)
        {
            return new Dictionary<string, BusyOn>();
        }

        var journeyIds = candidates.Select(a => a.JourneyId).Distinct().ToList();
        var journeys = await db.Journeys.AsNoTracking().Where(j => journeyIds.Contains(j.Id)).ToDictionaryAsync(j => j.Id, cancellationToken);
        var services = await db.TrainServices.AsNoTracking()
            .Where(s => journeys.Values.Select(j => j.ServiceId).Contains(s.Id)).ToDictionaryAsync(s => s.Id, cancellationToken);

        var busy = new Dictionary<string, BusyOn>();
        foreach (var assignment in candidates.OrderBy(a => a.ValidFrom))
        {
            var journey = journeys[assignment.JourneyId];
            if (journey.Status is JourneyStatus.Cancelled or JourneyStatus.Completed)
            {
                continue;
            }

            // "Until the journey ends" is the journey's last arrival (spec 17 section 4).
            var until = assignment.ValidTo ?? (await TimesOfAsync(journey.ServiceId, journey.OperatingDate, cancellationToken)).LastArrival;
            if (until <= from || assignment.ValidFrom >= horizon)
            {
                continue;
            }

            busy.TryAdd(assignment.OperatorUserId, new BusyOn(journey.Id, services[journey.ServiceId].TrainNumber, until));
        }

        return busy;
    }

    public async Task<IReadOnlyList<AssignmentView>?> GetAssignmentsAsync(Guid journeyId, CancellationToken cancellationToken)
    {
        if (!await db.Journeys.AsNoTracking().AnyAsync(j => j.Id == journeyId, cancellationToken))
        {
            return null;
        }

        var rows = await db.OperatorAssignments.AsNoTracking().Where(a => a.JourneyId == journeyId).OrderBy(a => a.ValidFrom).ToListAsync(cancellationToken);
        return rows.Select(ViewOf).ToList();
    }

    public async Task<AssignOutcome> AssignAsync(AssignCommand command, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await db.Database.ExecuteSqlInterpolatedAsync($"select id from journey where id = {command.JourneyId} for update", cancellationToken);

        var journey = await db.Journeys.SingleOrDefaultAsync(j => j.Id == command.JourneyId, cancellationToken);
        if (journey is null)
        {
            return Outcome(AssignOutcomeKind.JourneyNotFound);
        }

        if (journey.Status is JourneyStatus.Cancelled or JourneyStatus.Completed)
        {
            return Outcome(AssignOutcomeKind.NotAssignable);
        }

        var times = await TimesOfAsync(journey.ServiceId, journey.OperatingDate, cancellationToken);
        var validFrom = command.ValidFrom ?? AssignmentRules.DefaultFrom(times.FirstDeparture, command.Lead, command.Now);
        if (command.ValidFrom is not null && AssignmentRules.StartsTooFarInThePast(validFrom, command.Now, command.PastTolerance))
        {
            return Outcome(AssignOutcomeKind.ValidFromInPast);
        }

        if (command.ValidTo is { } given && given <= validFrom)
        {
            return Outcome(AssignOutcomeKind.ValidToNotAfterFrom);
        }

        var existing = await db.OperatorAssignments.Where(a => a.JourneyId == journey.Id).ToListAsync(cancellationToken);
        // Rule 4: an assignment that starts at or after the new start, and overlaps it, refuses. One that covers the new start is ended there.
        if (existing.Any(a => a.ValidFrom >= validFrom && AssignmentRules.Overlaps(validFrom, command.ValidTo, a.ValidFrom, a.ValidTo)))
        {
            return Outcome(AssignOutcomeKind.OverlapsLaterAssignment);
        }

        // Rule 5 (changed 21 Oct 2026): an operator who is on another journey in that range cannot be assigned here. It is no longer advice
        // that can be confirmed. The block goes away when that journey gets another operator, the assignment is ended, or it is cancelled.
        var busy = await BusyOperatorsAsync(journey.Id, validFrom, command.ValidTo ?? times.LastArrival, cancellationToken);
        if (busy.TryGetValue(command.OperatorUserId, out var other))
        {
            return new AssignOutcome(AssignOutcomeKind.OperatorBusy, null, null, null, other);
        }

        AssignmentView? ended = null;
        foreach (var covering in existing.Where(a => a.ValidFrom < validFrom && AssignmentRules.Covers(a.ValidFrom, a.ValidTo, validFrom)))
        {
            covering.ValidTo = validFrom;
            covering.EndedBy = command.Actor;
            covering.EndReason = AssignmentEndReason.CrewChange;
            ended = ViewOf(covering);
        }

        var assignment = new OperatorAssignment
        {
            Id = Guid.CreateVersion7(command.Now),
            JourneyId = journey.Id,
            OperatorUserId = command.OperatorUserId,
            ValidFrom = validFrom,
            ValidTo = command.ValidTo,
            AssignedBy = command.Actor,
        };
        db.OperatorAssignments.Add(assignment);

        // FR-12: assigning moves an Unassigned journey to Scheduled. The version moves too (ADR-09), so a report in flight sees the change.
        if (journey.Status == JourneyStatus.Unassigned)
        {
            journey.Status = JourneyStatus.Scheduled;
        }

        journey.Version += 1;
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new AssignOutcome(AssignOutcomeKind.Done, ViewOf(assignment), ended, journey.Status.ToString(), null);

        static AssignOutcome Outcome(AssignOutcomeKind kind) => new(kind, null, null, null, null);
    }

    public async Task<EndOutcome> EndAsync(
        Guid journeyId, Guid assignmentId, AssignmentEndReason reason, string? note, string actor, DateTimeOffset now, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await db.Database.ExecuteSqlInterpolatedAsync($"select id from journey where id = {journeyId} for update", cancellationToken);

        var assignment = await db.OperatorAssignments.SingleOrDefaultAsync(a => a.Id == assignmentId && a.JourneyId == journeyId, cancellationToken);
        if (assignment is null)
        {
            return new EndOutcome(EndOutcomeKind.NotFound, null, null);
        }

        if (assignment.ValidTo is { } end && end <= now)
        {
            return new EndOutcome(EndOutcomeKind.AlreadyEnded, null, null);
        }

        if (assignment.ValidFrom > now)
        {
            // The window check (valid_to > valid_from) forbids ending an assignment that has not started (Q9).
            return new EndOutcome(EndOutcomeKind.NotStarted, null, null);
        }

        // Ending an assignment in the very instant it started would break valid_to > valid_from, so it ends one microsecond later.
        assignment.ValidTo = now > assignment.ValidFrom ? now : assignment.ValidFrom.AddTicks(10);
        assignment.EndedBy = actor;
        assignment.EndReason = reason;
        assignment.EndNote = note;

        // The stored status is not changed: a Scheduled journey stays Scheduled, so a report the operator made during the assignment and syncs
        // later is still accepted (EC-21). "No operator assigned" and the Unassigned list come from the assignments themselves (spec 17 rule 10).
        var journey = await db.Journeys.SingleAsync(j => j.Id == journeyId, cancellationToken);
        journey.Version += 1;
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new EndOutcome(EndOutcomeKind.Done, ViewOf(assignment), journey.Status.ToString());
    }
}
