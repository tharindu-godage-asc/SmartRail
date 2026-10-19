using SmartRail.Application.Features.Exceptions;
using SmartRail.Domain.Journeys;

namespace SmartRail.Application.Features.Verification;

public enum VerifyAction { Verify, Change, Reject }

public enum VerifyOutcomeKind { Verified, Rejected, NotFound, AlreadyResolved, NotAVerificationException, ReasonNotApproved }

public sealed record VerifyOutcome(VerifyOutcomeKind Kind, Guid? JourneyId = null, DateTimeOffset? At = null);

public sealed record VerifyCommand(Guid ExceptionId, VerifyAction Action, string? ReasonCode, string Actor, string? ActorName, DateTimeOffset Now);

/// <summary>Persistence for verification. One call is one transaction: the event, the journey version and the closed exception are saved together.</summary>
public interface IVerificationRepository
{
    Task<VerifyOutcome> VerifyAsync(VerifyCommand command, CancellationToken cancellationToken);
}

public sealed record BulkVerifyItem(Guid Id, string Status, string? Reason);

/// <summary>
/// A controller verifies, changes or rejects a delay reason (FR-52, spec 16 F2). Reject only closes the exception; nothing is published.
/// The bulk form handles each id on its own, in the order sent (rule 4).
/// </summary>
public sealed class VerifyReasonHandler(IVerificationRepository repository, TimeProvider clock)
{
    public const int MaxBulk = 50;

    public static VerifyAction? ParseAction(string? action) => action switch
    {
        "verify" => VerifyAction.Verify,
        "change" => VerifyAction.Change,
        "reject" => VerifyAction.Reject,
        _ => null,
    };

    /// <summary>Null when the action is not one of verify, change or reject.</summary>
    public async Task<VerifyOutcome?> HandleAsync(Guid exceptionId, string? action, string? reasonCode, string actor, string? actorName, CancellationToken cancellationToken)
    {
        if (ParseAction(action) is not { } parsed)
        {
            return null;
        }

        // A change must name an approved code. Verify and Reject take the reason from the report (or none), so a stray code is ignored.
        if (parsed == VerifyAction.Change && !VerificationRules.IsApproved(reasonCode))
        {
            return new VerifyOutcome(VerifyOutcomeKind.ReasonNotApproved);
        }

        var trimmed = actorName?.Trim();
        var name = string.IsNullOrEmpty(trimmed) ? null : trimmed[..Math.Min(64, trimmed.Length)];
        var outcome = await repository.VerifyAsync(
            new VerifyCommand(exceptionId, parsed, parsed == VerifyAction.Change ? reasonCode : null, actor, name, clock.GetUtcNow()), cancellationToken);
        if (outcome.Kind is VerifyOutcomeKind.Verified or VerifyOutcomeKind.Rejected)
        {
            ExceptionMetrics.Resolved(parsed.ToString());
        }

        return outcome;
    }

    public async Task<IReadOnlyList<BulkVerifyItem>> HandleBulkAsync(IReadOnlyList<Guid> ids, string actor, string? actorName, CancellationToken cancellationToken)
    {
        var results = new List<BulkVerifyItem>(ids.Count);
        foreach (var id in ids.Distinct())
        {
            var outcome = (await HandleAsync(id, "verify", null, actor, actorName, cancellationToken))!;
            results.Add(outcome.Kind switch
            {
                VerifyOutcomeKind.Verified => new BulkVerifyItem(id, "verified", null),
                VerifyOutcomeKind.ReasonNotApproved => new BulkVerifyItem(id, "skipped", "reason_not_approved"),
                VerifyOutcomeKind.AlreadyResolved => new BulkVerifyItem(id, "skipped", "already_resolved"),
                VerifyOutcomeKind.NotAVerificationException => new BulkVerifyItem(id, "skipped", "not_a_verification_exception"),
                _ => new BulkVerifyItem(id, "skipped", "exception_not_found"),
            });
        }

        return results;
    }
}
