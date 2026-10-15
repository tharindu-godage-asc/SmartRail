using SmartRail.Domain.Journeys;

namespace SmartRail.Application.Features.Exceptions;

/// <summary>One row of the controllers' queue (AC-E05-02 4). The client words the age ("3 min") and the label; the API sends codes.</summary>
public sealed record OpenExceptionRow(
    Guid Id,
    Guid JourneyId,
    string TrainNumber,
    string? StationName,
    ExceptionType Type,
    ExceptionSeverity Severity,
    DateTimeOffset CreatedAt,
    Guid? RelatedEventId);

public enum ResolveOutcome { Resolved, NotFound, AlreadyResolved, InvalidAction }

/// <summary>Persistence for the exceptions queue. Implemented in Infrastructure with EF Core.</summary>
public interface IExceptionRepository
{
    /// <summary>Every open exception: Critical first, then oldest first (AC-E05-02 3).</summary>
    Task<IReadOnlyList<OpenExceptionRow>> ListOpenAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Marks it resolved by this controller, once. Two controllers resolving at the same moment cannot both succeed:
    /// the second gets <see cref="ResolveOutcome.AlreadyResolved"/>.
    /// </summary>
    Task<ResolveOutcome> ResolveAsync(Guid id, string resolvedBy, string action, DateTimeOffset at, CancellationToken cancellationToken);
}

public sealed record OpenExceptionItem(
    Guid Id,
    Guid JourneyId,
    string TrainNumber,
    string? StationName,
    string Type,
    string Severity,
    DateTimeOffset CreatedAt,
    long AgeSeconds,
    Guid? RelatedEventId);

public sealed record OpenExceptionsResponse(IReadOnlyList<OpenExceptionItem> Items, int OpenCount);

/// <summary>The queue (FR-51, AC-E05-02 3, 4). Ages come from the server clock, so a controller's phone cannot skew them.</summary>
public sealed class ListOpenExceptionsHandler(IExceptionRepository repository, TimeProvider clock)
{
    public async Task<OpenExceptionsResponse> HandleAsync(CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var rows = await repository.ListOpenAsync(cancellationToken);
        var items = rows
            .Select(r => new OpenExceptionItem(
                r.Id, r.JourneyId, r.TrainNumber, r.StationName, r.Type.ToString(), r.Severity.ToString(),
                r.CreatedAt, (long)Math.Max(0, (now - r.CreatedAt).TotalSeconds), r.RelatedEventId))
            .ToList();
        return new OpenExceptionsResponse(items, items.Count);
    }
}

/// <summary>A controller resolves one exception with one of three fixed actions (Q3). It never changes journey state or what passengers see.</summary>
public sealed class ResolveExceptionHandler(IExceptionRepository repository, TimeProvider clock)
{
    public async Task<ResolveOutcome> HandleAsync(Guid id, string controllerSubject, string? action, CancellationToken cancellationToken)
    {
        if (action is null || !ExceptionResolution.All.Contains(action))
        {
            return ResolveOutcome.InvalidAction;
        }

        var outcome = await repository.ResolveAsync(id, controllerSubject, action, clock.GetUtcNow(), cancellationToken);
        if (outcome == ResolveOutcome.Resolved)
        {
            ExceptionMetrics.Resolved(action);
        }

        return outcome;
    }
}