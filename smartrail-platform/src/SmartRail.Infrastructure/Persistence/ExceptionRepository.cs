using Microsoft.EntityFrameworkCore;
using SmartRail.Application.Features.Exceptions;
using SmartRail.Domain.Journeys;

namespace SmartRail.Infrastructure.Persistence;

public sealed class ExceptionRepository(SmartrailDbContext db) : IExceptionRepository
{
    public async Task<IReadOnlyList<OpenExceptionRow>> ListOpenAsync(CancellationToken cancellationToken)
    {
        var rows = await (
            from e in db.JourneyExceptions.AsNoTracking()
            where e.ResolvedAt == null
            join j in db.Journeys.AsNoTracking() on e.JourneyId equals j.Id
            join s in db.TrainServices.AsNoTracking() on j.ServiceId equals s.Id
            join st in db.Stations.AsNoTracking() on e.StationId equals st.Id into stations
            from station in stations.DefaultIfEmpty()
            select new
            {
                e.Id, e.JourneyId, s.TrainNumber, StationName = station == null ? null : station.Name,
                e.Type, e.Severity, e.CreatedAt, e.RelatedEventId,
            })
            .ToListAsync(cancellationToken);

        // Critical first, then oldest first (AC-E05-02 3). Sorted here because the enum is stored as text.
        return rows
            .OrderBy(r => r.Severity == ExceptionSeverity.Critical ? 0 : 1)
            .ThenBy(r => r.CreatedAt)
            .Select(r => new OpenExceptionRow(r.Id, r.JourneyId, r.TrainNumber, r.StationName, r.Type, r.Severity, r.CreatedAt, r.RelatedEventId))
            .ToList();
    }

    public async Task<OpenSummary> GetOpenSummaryAsync(CancellationToken cancellationToken)
    {
        var open = await db.JourneyExceptions.AsNoTracking()
            .Where(e => e.ResolvedAt == null)
            .Select(e => new { e.JourneyId, e.Type })
            .ToListAsync(cancellationToken);
        var awaiting = open.Where(e => e.Type == ExceptionType.ReasonAwaitingVerification).Select(e => e.JourneyId).ToHashSet();
        return new OpenSummary(open.Count, awaiting);
    }

    public async Task<ResolveOutcome> ResolveAsync(Guid id, string resolvedBy, string action, DateTimeOffset at, CancellationToken cancellationToken)
    {
        // One UPDATE that only matches an open row: of two simultaneous resolves exactly one changes it.
        var changed = await db.JourneyExceptions
            .Where(e => e.Id == id && e.ResolvedAt == null)
            .ExecuteUpdateAsync(
                set => set
                    .SetProperty(e => e.ResolvedAt, at)
                    .SetProperty(e => e.ResolvedBy, resolvedBy)
                    .SetProperty(e => e.Resolution, action),
                cancellationToken);
        if (changed == 1)
        {
            return ResolveOutcome.Resolved;
        }

        return await db.JourneyExceptions.AnyAsync(e => e.Id == id, cancellationToken)
            ? ResolveOutcome.AlreadyResolved
            : ResolveOutcome.NotFound;
    }
}