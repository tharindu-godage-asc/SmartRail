using System.Diagnostics.Metrics;

namespace SmartRail.Application.Features.OperatorHistory;

/// <summary>One past journey of the signed-in operator (spec 12 section 4.1).</summary>
public sealed record HistoryJourney(
    Guid JourneyId,
    string TrainNumber,
    string FromName,
    string ToName,
    DateOnly OperatingDate,
    string Status,
    DateTimeOffset AssignedFrom,
    DateTimeOffset? AssignedUntil,
    int MyReportCount,
    int? ArrivalDelayMinutes);

public sealed record HistoryResponse(DateTimeOffset ServerTime, IReadOnlyList<HistoryJourney> Journeys, DateTimeOffset? Next);

public sealed record RecordStop(
    Guid StopId,
    string StationName,
    int Sequence,
    DateTimeOffset? ScheduledArrival,
    DateTimeOffset? ScheduledDeparture,
    DateTimeOffset? ActualArrival,
    DateTimeOffset? ActualDeparture,
    string Confirmation);

/// <summary>A report this operator made. The reason is his own input, shown back to him; it is not a published reason (D-10).</summary>
public sealed record RecordReport(
    Guid EventId,
    string Type,
    string? StationName,
    DateTimeOffset OccurredAt,
    DateTimeOffset ReceivedAt,
    string? DelayReason,
    int? TotalDelayMinutes);

public sealed record JourneyRecordResponse(
    DateTimeOffset ServerTime,
    Guid JourneyId,
    string TrainNumber,
    DateOnly OperatingDate,
    string Status,
    IReadOnlyList<RecordStop> Stops,
    IReadOnlyList<RecordReport> MyReports);

public interface IOperatorHistoryQueries
{
    /// <summary>
    /// The operator's past journeys, newest assignment first: Completed, or his assignment has ended. A journey still running under his
    /// current assignment is left out (the Journey tab shows it). <paramref name="before"/> pages by the start of the assignment.
    /// </summary>
    Task<HistoryResponse> GetHistoryAsync(string subject, int limit, DateTimeOffset? before, DateTimeOffset now, CancellationToken cancellationToken);

    /// <summary>The record of one journey, only when an assignment of this operator exists for it; null otherwise (the same for "missing" and "not yours").</summary>
    Task<JourneyRecordResponse?> GetRecordAsync(string subject, Guid journeyId, DateTimeOffset now, CancellationToken cancellationToken);
}

public enum HistoryError { None, InvalidLimit, InvalidBefore }

public sealed record HistoryResult(HistoryError Error, HistoryResponse? Response);

public static class HistoryMetrics
{
    public static readonly Meter Meter = new("SmartRail.OperatorHistory");
    public static readonly Counter<long> Reads = Meter.CreateCounter<long>("smartrail.operator.history.reads", description: "Operator history reads by kind and outcome");

    public static void Count(string kind, string outcome) =>
        Reads.Add(1, new KeyValuePair<string, object?>("kind", kind), new KeyValuePair<string, object?>("outcome", outcome));
}

public sealed class GetOperatorHistoryHandler(IOperatorHistoryQueries queries, TimeProvider clock)
{
    public const int DefaultLimit = 20;
    public const int MaxLimit = 50;

    public async Task<HistoryResult> HandleAsync(string subject, string? limit, string? before, CancellationToken cancellationToken)
    {
        var take = DefaultLimit;
        if (limit is not null && (!int.TryParse(limit, out take) || take is < 1 or > MaxLimit))
        {
            return new HistoryResult(HistoryError.InvalidLimit, null);
        }

        DateTimeOffset? cursor = null;
        if (before is not null)
        {
            if (!DateTimeOffset.TryParse(before, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AssumeUniversal, out var parsed))
            {
                return new HistoryResult(HistoryError.InvalidBefore, null);
            }

            cursor = parsed.ToUniversalTime();
        }

        var response = await queries.GetHistoryAsync(subject, take, cursor, clock.GetUtcNow(), cancellationToken);
        HistoryMetrics.Count("list", "ok");
        return new HistoryResult(HistoryError.None, response);
    }
}

public sealed class GetJourneyRecordHandler(IOperatorHistoryQueries queries, TimeProvider clock)
{
    public async Task<JourneyRecordResponse?> HandleAsync(string subject, Guid journeyId, CancellationToken cancellationToken)
    {
        var record = await queries.GetRecordAsync(subject, journeyId, clock.GetUtcNow(), cancellationToken);
        HistoryMetrics.Count("record", record is null ? "not_found" : "ok");
        return record;
    }
}
