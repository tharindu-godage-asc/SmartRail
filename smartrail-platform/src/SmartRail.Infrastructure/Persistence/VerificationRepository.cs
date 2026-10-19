using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SmartRail.Application.Features.Events.ReportEvent;
using SmartRail.Application.Features.Verification;
using SmartRail.Domain.Journeys;

namespace SmartRail.Infrastructure.Persistence;

public sealed class VerificationRepository(SmartrailDbContext db) : IVerificationRepository
{
    private const int MaxAttempts = 3;

    public async Task<VerifyOutcome> VerifyAsync(VerifyCommand command, CancellationToken cancellationToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            db.ChangeTracker.Clear();
            var exception = await db.JourneyExceptions.AsNoTracking().SingleOrDefaultAsync(e => e.Id == command.ExceptionId, cancellationToken);
            if (exception is null)
            {
                return new VerifyOutcome(VerifyOutcomeKind.NotFound);
            }

            if (exception.ResolvedAt is not null)
            {
                return new VerifyOutcome(VerifyOutcomeKind.AlreadyResolved);
            }

            if (exception.Type != ExceptionType.ReasonAwaitingVerification || exception.RelatedEventId is null)
            {
                return new VerifyOutcome(VerifyOutcomeKind.NotAVerificationException);
            }

            string? reason = null;
            if (command.Action != VerifyAction.Reject)
            {
                reason = command.Action == VerifyAction.Change ? command.ReasonCode : await OriginalReasonAsync(exception.RelatedEventId.Value, cancellationToken);
                if (!VerificationRules.IsApproved(reason))
                {
                    return new VerifyOutcome(VerifyOutcomeKind.ReasonNotApproved);
                }
            }

            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            var journey = await db.Journeys.SingleAsync(j => j.Id == exception.JourneyId, cancellationToken);

            // Close the exception with one UPDATE that only matches an open row: of two controllers exactly one wins.
            var resolution = command.Action switch
            {
                VerifyAction.Reject => VerificationRules.Rejected,
                VerifyAction.Change => "ReasonChanged",
                _ => "ReasonVerified",
            };
            var closed = await db.JourneyExceptions
                .Where(e => e.Id == command.ExceptionId && e.ResolvedAt == null)
                .ExecuteUpdateAsync(
                    set => set
                        .SetProperty(e => e.ResolvedAt, command.Now)
                        .SetProperty(e => e.ResolvedBy, command.Actor)
                        .SetProperty(e => e.ResolvedByName, command.ActorName)
                        .SetProperty(e => e.Resolution, resolution),
                    cancellationToken);
            if (closed == 0)
            {
                return new VerifyOutcome(VerifyOutcomeKind.AlreadyResolved);
            }

            if (command.Action != VerifyAction.Reject)
            {
                // The event bumps the journey version so clients holding the older status take the new one (ADR-09).
                journey.Version += 1;
                db.TrainEvents.Add(new TrainEvent
                {
                    Id = Guid.CreateVersion7(command.Now),
                    JourneyId = journey.Id,
                    ClientEventId = Guid.NewGuid(),
                    EventType = EventType.ReasonVerified,
                    OccurredAt = command.Now,
                    ReceivedAt = command.Now,
                    Source = EventSource.Controller,
                    CreatedBy = command.Actor,
                    Payload = JsonSerializer.Serialize(new { reasonCode = reason, delayEventId = exception.RelatedEventId, changed = command.Action == VerifyAction.Change }),
                    JourneyVersionAfter = journey.Version,
                });
            }

            try
            {
                await db.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return new VerifyOutcome(command.Action == VerifyAction.Reject ? VerifyOutcomeKind.Rejected : VerifyOutcomeKind.Verified, journey.Id, command.Now);
            }
            catch (DbUpdateConcurrencyException) when (attempt < MaxAttempts)
            {
                // An operator report moved the journey version meanwhile: the transaction rolls back (the close too); try again.
            }
        }
    }

    private async Task<string?> OriginalReasonAsync(Guid delayEventId, CancellationToken cancellationToken)
    {
        var payload = await db.TrainEvents.AsNoTracking().Where(e => e.Id == delayEventId).Select(e => e.Payload).SingleOrDefaultAsync(cancellationToken);
        return payload is null ? null : EventRules.FromJson(payload)?.Reason;
    }
}
