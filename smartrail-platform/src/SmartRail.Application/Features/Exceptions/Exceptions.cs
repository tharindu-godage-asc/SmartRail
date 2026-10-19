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
    Guid? RelatedEventId,
    string? ReportedReason = null);

/// <summary>One closed exception, newest closure first (spec 07 section 14). The reason is not here: no read returns it (D-10).</summary>
public sealed record ResolvedExceptionRow(
    Guid Id,
    Guid JourneyId,
    string TrainNumber,
    string? StationName,
    ExceptionType Type,
    ExceptionSeverity Severity,
    DateTimeOffset CreatedAt,
    DateTimeOffset ResolvedAt,
    string? ResolvedByName,
    string Resolution);

public sealed record ResolvedPage(IReadOnlyList<ResolvedExceptionRow> Rows, int Total);

public sealed record OpenSummary(int Count, IReadOnlySet<Guid> AwaitingVerificationJourneyIds);

public enum ResolveOutcome { Resolved, NotFound, AlreadyResolved, InvalidAction }

/// <summary>Persistence for the exceptions queue. Implemented in Infrastructure with EF Core.</summary>
public interface IExceptionRepository
{
    /// <summary>Every open exception: Critical first, then oldest first (AC-E05-02 3).</summary>
    Task<IReadOnlyList<OpenExceptionRow>> ListOpenAsync(CancellationToken cancellationToken);

    /// <summary>The latest closed exceptions, newest closure first, at most <paramref name="limit"/>, and how many closed ones exist in all.</summary>
    Task<ResolvedPage> ListResolvedAsync(int limit, CancellationToken cancellationToken);

    /// <summary>How many exceptions are open, and which journeys have a reason awaiting verification (overview, AC-E05-01 4, 6).</summary>
    Task<OpenSummary> GetOpenSummaryAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Marks it resolved by this controller, once. Two controllers resolving at the same moment cannot both succeed:
    /// the second gets <see cref="ResolveOutcome.AlreadyResolved"/>.
    /// </summary>
    Task<ResolveOutcome> ResolveAsync(Guid id, string resolvedBy, string? resolvedByName, string action, DateTimeOffset at, CancellationToken cancellationToken);
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
    Guid? RelatedEventId,
    string? ReportedReason = null,
    bool CanVerify = false);

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
                r.CreatedAt, (long)Math.Max(0, (now - r.CreatedAt).TotalSeconds), r.RelatedEventId,
                r.ReportedReason, VerificationRules.IsApproved(r.ReportedReason)))
            .ToList();
        return new OpenExceptionsResponse(items, items.Count);
    }
}

public sealed record ResolvedExceptionItem(
    Guid Id,
    Guid JourneyId,
    string TrainNumber,
    string? StationName,
    string Type,
    string Severity,
    DateTimeOffset CreatedAt,
    DateTimeOffset ResolvedAt,
    string? ResolvedByName,
    string Resolution,
    long SecondsToResolve);

public sealed record ResolvedExceptionsResponse(IReadOnlyList<ResolvedExceptionItem> Items, int Total, int Limit);

/// <summary>The closed list (spec 07 section 14): the latest few, newest closure first. Read only.</summary>
public sealed class ListResolvedExceptionsHandler(IExceptionRepository repository)
{
    public const int Limit = 50;

    public async Task<ResolvedExceptionsResponse> HandleAsync(CancellationToken cancellationToken)
    {
        var page = await repository.ListResolvedAsync(Limit, cancellationToken);
        var items = page.Rows
            .Select(r => new ResolvedExceptionItem(
                r.Id, r.JourneyId, r.TrainNumber, r.StationName, r.Type.ToString(), r.Severity.ToString(),
                r.CreatedAt, r.ResolvedAt, r.ResolvedByName, r.Resolution, (long)Math.Max(0, (r.ResolvedAt - r.CreatedAt).TotalSeconds)))
            .ToList();
        return new ResolvedExceptionsResponse(items, page.Total, Limit);
    }
}

/// <summary>A controller resolves one exception with one of three fixed actions (Q3). It never changes journey state or what passengers see.</summary>
public sealed class ResolveExceptionHandler(IExceptionRepository repository, TimeProvider clock)
{
    public async Task<ResolveOutcome> HandleAsync(Guid id, string controllerSubject, string? controllerName, string? action, CancellationToken cancellationToken)
    {
        if (action is null || !ExceptionResolution.All.Contains(action))
        {
            return ResolveOutcome.InvalidAction;
        }

        // The name is display text only: trimmed, limited to the column size, and null when there is none.
        var name = string.IsNullOrWhiteSpace(controllerName) ? null : controllerName.Trim()[..Math.Min(64, controllerName.Trim().Length)];
        var outcome = await repository.ResolveAsync(id, controllerSubject, name, action, clock.GetUtcNow(), cancellationToken);
        if (outcome == ResolveOutcome.Resolved)
        {
            ExceptionMetrics.Resolved(action);
        }

        return outcome;
    }
}