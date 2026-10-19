using System.Diagnostics.Metrics;
using SmartRail.Domain.Journeys;

namespace SmartRail.Application.Features.Assignments;

/// <summary>Assignment:* settings (spec 17, NFR-41). The defaults are the values in the spec.</summary>
public sealed class AssignmentSettings
{
    public const string Section = "Assignment";

    /// <summary>An assignment starts this long before the first departure unless the controller says otherwise.</summary>
    public int LeadMinutes { get; set; } = 30;

    /// <summary>A start up to this long before now is accepted (clock differences). More would authorise old reports.</summary>
    public int PastToleranceMinutes { get; set; } = 5;
}

/// <summary>An operator account as the identity provider knows it (FR-06). SmartRail keeps only the subject, never the name.</summary>
public sealed record DirectoryOperator(string UserId, string Name, bool Enabled = true, bool IsOperator = true);

/// <summary>Every external call goes through a port (architecture rules). Until the Keycloak adapter exists, a configured list stands in.</summary>
public interface IOperatorDirectory
{
    Task<IReadOnlyList<DirectoryOperator>> ListAsync(CancellationToken cancellationToken);

    Task<DirectoryOperator?> FindAsync(string userId, CancellationToken cancellationToken);
}

public sealed record BusyOn(Guid JourneyId, string TrainNumber, DateTimeOffset? Until);

public sealed record AssignmentView(
    Guid AssignmentId,
    string OperatorUserId,
    string OperatorName,
    DateTimeOffset ValidFrom,
    DateTimeOffset? ValidTo,
    string AssignedBy,
    string? EndedBy,
    string? EndReason,
    string? EndNote);

public sealed record JourneyListItem(
    Guid JourneyId,
    string TrainNumber,
    string From,
    string To,
    DateOnly OperatingDate,
    DateTimeOffset FirstDeparture,
    DateTimeOffset LastArrival,
    string Status,
    string Source,
    CurrentOperator? Operator);

public sealed record CurrentOperator(string UserId, string Name, DateTimeOffset ValidFrom, DateTimeOffset? ValidTo);

public sealed record OperatorChoice(string UserId, string Name, bool Enabled, bool Available, BusyOn? BusyOn);

public sealed record AssignCommand(
    Guid JourneyId,
    string OperatorUserId,
    DateTimeOffset? ValidFrom,
    DateTimeOffset? ValidTo,
    bool AcknowledgeOverlap,
    string Actor,
    DateTimeOffset Now,
    TimeSpan Lead,
    TimeSpan PastTolerance);

public enum AssignOutcomeKind
{
    Done, JourneyNotFound, NotAssignable, ValidToNotAfterFrom, ValidFromInPast, OverlapsLaterAssignment, OperatorBusy,
}

public sealed record AssignOutcome(AssignOutcomeKind Kind, AssignmentView? Assignment, AssignmentView? Ended, string? JourneyStatus, BusyOn? Busy);

public enum EndOutcomeKind { Done, NotFound, AlreadyEnded, NotStarted }

public sealed record EndOutcome(EndOutcomeKind Kind, AssignmentView? Assignment, string? JourneyStatus);

public interface IAssignmentRepository
{
    /// <summary>Journeys of an operating date with the operator covering now; "unassigned" keeps those with no assignment now or later.</summary>
    Task<IReadOnlyList<JourneyListItem>> ListJourneysAsync(DateOnly date, bool unassignedOnly, DateTimeOffset now, CancellationToken cancellationToken);

    /// <summary>Which of the operators are on another journey in the range (the journey asked about is left out), by subject.</summary>
    Task<IReadOnlyDictionary<string, BusyOn>> BusyOperatorsAsync(Guid? journeyId, DateTimeOffset from, DateTimeOffset? to, CancellationToken cancellationToken);

    /// <summary>The journey's first departure and last arrival as instants, or null for an unknown journey.</summary>
    Task<(DateTimeOffset FirstDeparture, DateTimeOffset LastArrival)?> GetJourneyTimesAsync(Guid journeyId, CancellationToken cancellationToken);

    /// <summary>All assignments of a journey, oldest first; null for an unknown journey. Names are filled in by the handler.</summary>
    Task<IReadOnlyList<AssignmentView>?> GetAssignmentsAsync(Guid journeyId, CancellationToken cancellationToken);

    /// <summary>
    /// Assigns in one transaction with the journey row locked (rule 3): a crew change ends the covering assignment at the new start, a later
    /// assignment refuses, another journey of the operator needs the acknowledgement. Unassigned becomes Scheduled.
    /// </summary>
    Task<AssignOutcome> AssignAsync(AssignCommand command, CancellationToken cancellationToken);

    /// <summary>Ends an assignment now, with who and why; a Scheduled journey with no events and no other operator goes back to Unassigned.</summary>
    Task<EndOutcome> EndAsync(Guid journeyId, Guid assignmentId, AssignmentEndReason reason, string? note, string actor, DateTimeOffset now, CancellationToken cancellationToken);
}

public static class AssignmentMetrics
{
    public static readonly Meter Meter = new("SmartRail.Assignments");
    public static readonly Counter<long> Assigned = Meter.CreateCounter<long>("smartrail.assignments.assigned", description: "Operator assignments made");
    public static readonly Counter<long> Ended = Meter.CreateCounter<long>("smartrail.assignments.ended", description: "Operator assignments ended, by reason");
}

public sealed class ListAssignmentJourneysHandler(IAssignmentRepository repository, IOperatorDirectory directory, TimeProvider clock)
{
    public async Task<(bool Valid, IReadOnlyList<JourneyListItem> Items)> HandleAsync(string? date, string? filter, CancellationToken cancellationToken)
    {
        var day = SmartRail.Domain.OperatingTime.Today(clock);
        if (date is not null && !DateOnly.TryParseExact(date, "yyyy-MM-dd", out day))
        {
            return (false, []);
        }

        var unassigned = filter is null || filter.Equals("unassigned", StringComparison.OrdinalIgnoreCase);
        if (!unassigned && !(filter ?? "").Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            return (false, []);
        }

        var items = await repository.ListJourneysAsync(day, unassigned, clock.GetUtcNow(), cancellationToken);
        var names = (await directory.ListAsync(cancellationToken)).ToDictionary(o => o.UserId, o => o.Name);
        return (true, items.Select(i => i.Operator is null ? i : i with { Operator = i.Operator with { Name = names.GetValueOrDefault(i.Operator.UserId, i.Operator.UserId) } }).ToList());
    }
}

public sealed class ListOperatorsHandler(IOperatorDirectory directory, IAssignmentRepository repository, TimeProvider clock)
{
    public async Task<IReadOnlyList<OperatorChoice>> HandleAsync(Guid? journeyId, DateTimeOffset? from, DateTimeOffset? to, CancellationToken cancellationToken)
    {
        var operators = (await directory.ListAsync(cancellationToken)).Where(o => o.IsOperator).ToList();
        var start = from ?? clock.GetUtcNow();
        var busy = await repository.BusyOperatorsAsync(journeyId, start, to, cancellationToken);
        return operators.Select(o => new OperatorChoice(o.UserId, o.Name, o.Enabled, !busy.ContainsKey(o.UserId), busy.GetValueOrDefault(o.UserId))).ToList();
    }
}

public sealed class ListJourneyAssignmentsHandler(IAssignmentRepository repository, IOperatorDirectory directory)
{
    public async Task<IReadOnlyList<AssignmentView>?> HandleAsync(Guid journeyId, CancellationToken cancellationToken)
    {
        var rows = await repository.GetAssignmentsAsync(journeyId, cancellationToken);
        if (rows is null)
        {
            return null;
        }

        var names = (await directory.ListAsync(cancellationToken)).ToDictionary(o => o.UserId, o => o.Name);
        return rows.Select(r => r with { OperatorName = names.GetValueOrDefault(r.OperatorUserId, r.OperatorUserId) }).ToList();
    }
}

public enum AssignError
{
    None, JourneyNotFound, OperatorNotFound, NotAnOperator, OperatorDisabled, ValidToNotAfterFrom, ValidFromInPast, JourneyNotAssignable,
    OverlapsLaterAssignment, OperatorOverlapsAnotherJourney, DirectoryUnavailable,
}

public sealed record AssignResult(AssignError Error, AssignmentView? Assignment, AssignmentView? Ended, string? JourneyStatus, BusyOn? Busy);

public sealed class AssignOperatorHandler(IOperatorDirectory directory, IAssignmentRepository repository, TimeProvider clock, AssignmentSettings settings)
{
    public async Task<AssignResult> HandleAsync(
        Guid journeyId, string operatorUserId, DateTimeOffset? validFrom, DateTimeOffset? validTo, bool acknowledgeOverlap, string actor, CancellationToken cancellationToken)
    {
        DirectoryOperator? found;
        try
        {
            found = await directory.FindAsync(operatorUserId, cancellationToken);
        }
        catch (HttpRequestException)
        {
            return Rejected(AssignError.DirectoryUnavailable);
        }

        // The directory is asked at assign time and never trusted from the client (rule 2).
        if (found is null) return Rejected(AssignError.OperatorNotFound);
        if (!found.IsOperator) return Rejected(AssignError.NotAnOperator);
        if (!found.Enabled) return Rejected(AssignError.OperatorDisabled);

        var outcome = await repository.AssignAsync(
            new AssignCommand(
                journeyId, operatorUserId, validFrom?.ToUniversalTime(), validTo?.ToUniversalTime(), acknowledgeOverlap, actor, clock.GetUtcNow(),
                TimeSpan.FromMinutes(settings.LeadMinutes), TimeSpan.FromMinutes(settings.PastToleranceMinutes)),
            cancellationToken);

        var error = outcome.Kind switch
        {
            AssignOutcomeKind.Done => AssignError.None,
            AssignOutcomeKind.JourneyNotFound => AssignError.JourneyNotFound,
            AssignOutcomeKind.NotAssignable => AssignError.JourneyNotAssignable,
            AssignOutcomeKind.ValidToNotAfterFrom => AssignError.ValidToNotAfterFrom,
            AssignOutcomeKind.ValidFromInPast => AssignError.ValidFromInPast,
            AssignOutcomeKind.OverlapsLaterAssignment => AssignError.OverlapsLaterAssignment,
            _ => AssignError.OperatorOverlapsAnotherJourney,
        };

        if (error == AssignError.None)
        {
            AssignmentMetrics.Assigned.Add(1);
            return new AssignResult(error, outcome.Assignment! with { OperatorName = found.Name }, outcome.Ended, outcome.JourneyStatus, null);
        }

        return new AssignResult(error, null, null, null, outcome.Busy);
    }

    private static AssignResult Rejected(AssignError error) => new(error, null, null, null, null);
}

public enum EndError { None, NotFound, AlreadyEnded, NotStarted, InvalidReason, NoteTooLong }

public sealed record EndResult(EndError Error, AssignmentView? Assignment, string? JourneyStatus);

public sealed class EndAssignmentHandler(IAssignmentRepository repository, IOperatorDirectory directory, TimeProvider clock)
{
    public const int MaxNote = 500;

    public async Task<EndResult> HandleAsync(Guid journeyId, Guid assignmentId, string? reason, string? note, string actor, CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<AssignmentEndReason>(reason, false, out var parsed) || !Enum.IsDefined(parsed))
        {
            return new EndResult(EndError.InvalidReason, null, null);
        }

        var text = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        if (text is { Length: > MaxNote })
        {
            return new EndResult(EndError.NoteTooLong, null, null);
        }

        var outcome = await repository.EndAsync(journeyId, assignmentId, parsed, text, actor, clock.GetUtcNow(), cancellationToken);
        switch (outcome.Kind)
        {
            case EndOutcomeKind.NotFound:
                return new EndResult(EndError.NotFound, null, null);
            case EndOutcomeKind.AlreadyEnded:
                return new EndResult(EndError.AlreadyEnded, null, null);
            case EndOutcomeKind.NotStarted:
                return new EndResult(EndError.NotStarted, null, null);
        }

        AssignmentMetrics.Ended.Add(1, new KeyValuePair<string, object?>("reason", parsed.ToString()));
        var names = (await directory.ListAsync(cancellationToken)).ToDictionary(o => o.UserId, o => o.Name);
        var view = outcome.Assignment! with { OperatorName = names.GetValueOrDefault(outcome.Assignment.OperatorUserId, outcome.Assignment.OperatorUserId) };
        return new EndResult(EndError.None, view, outcome.JourneyStatus);
    }
}
