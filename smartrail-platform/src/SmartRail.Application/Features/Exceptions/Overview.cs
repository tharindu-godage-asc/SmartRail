using SmartRail.Application.Features.Journeys;
using SmartRail.Domain;
using SmartRail.Domain.Journeys;

namespace SmartRail.Application.Features.Exceptions;

/// <summary>The last accepted report of a journey. <c>Source</c> is OperatorReport, the only source that exists today (AC-E05-01 3).</summary>
public sealed record OverviewLastEvent(string Type, string? StationName, DateTimeOffset OccurredAt, string Source);

public sealed record OverviewNextStop(string StationName, DateTimeOffset ExpectedAt);

/// <summary>One active journey on the controller overview. The client words the labels; the API sends codes.</summary>
public sealed record OverviewJourney(
    Guid JourneyId,
    string TrainNumber,
    string StatusGroup,
    string StatusKey,
    int DelayMinutes,
    bool DelayKnown,
    OverviewLastEvent? LastEvent,
    OverviewNextStop? NextStop,
    long? SecondsSinceUpdate,
    string Verification);

public sealed record OverviewResponse(int OpenExceptionCount, IReadOnlyList<OverviewJourney> Journeys, DateTimeOffset ServerTime);

/// <summary>
/// GET /api/v1/overview (FR-50, AC-E05-01): every journey in progress with its last event, delay, next stop, time since the
/// last update and verification state, plus the open exceptions count. It only reads; it never changes what passengers see.
/// </summary>
public sealed class GetOverviewHandler(IJourneyRepository journeys, IExceptionRepository exceptions, TimeProvider clock, JourneySettings settings)
{
    public async Task<OverviewResponse> HandleAsync(CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var today = OperatingTime.Today(clock);
        var grace = TimeSpan.FromMinutes(settings.VisibleAfterEndMinutes);

        // A journey takes at most 24 hours, so one still in progress started no earlier than two operating dates ago.
        var candidates = await journeys.GetUnfinishedAsync(today.AddDays(-2), today, cancellationToken);
        var summary = await exceptions.GetOpenSummaryAsync(cancellationToken);

        var items = new List<OverviewJourney>();
        foreach (var data in candidates)
        {
            if (data.Stops.Count == 0 || !InProgress(data, now, grace))
            {
                continue;
            }

            var snapshot = SnapshotBuilder.Build(data, now, settings.OnTimeToleranceMinutes);
            var last = data.Events.OrderBy(e => e.OccurredAt).LastOrDefault();
            var lastEvent = last is null
                ? null
                : new OverviewLastEvent(
                    last.Kind.ToString(),
                    last.ServiceStopId is { } stopId ? data.Stops.FirstOrDefault(s => s.ServiceStopId == stopId)?.StationName : null,
                    last.OccurredAt,
                    "OperatorReport");

            // Time since the server last heard from the journey, on the server clock (rule 9).
            var heard = data.Events.Max(e => e.ReceivedAt);
            long? seconds = heard is { } at ? Math.Max(0, (long)(now - at).TotalSeconds) : null;

            items.Add(new OverviewJourney(
                data.JourneyId, data.TrainNumber, snapshot.StatusGroup, snapshot.StatusKey, snapshot.DelayMinutes, snapshot.DelayKnown,
                lastEvent,
                snapshot.PrimaryEta is { } eta ? new OverviewNextStop(eta.StationName, eta.Time) : null,
                seconds,
                summary.AwaitingVerificationJourneyIds.Contains(data.JourneyId) ? "awaiting" : "none"));
        }

        return new OverviewResponse(summary.Count, items.OrderBy(i => i.TrainNumber, StringComparer.Ordinal).ToList(), now);
    }

    /// <summary>The same rule as the passenger line status: started, and not past its expected end plus the grace period.</summary>
    private static bool InProgress(JourneyData data, DateTimeOffset now, TimeSpan grace)
    {
        var schedule = data.Schedule();
        var start = schedule[0].Departure ?? schedule[0].Arrival;
        var end = (schedule[^1].Arrival ?? schedule[^1].Departure) + TimeSpan.FromMinutes(data.DelayMinutes);
        return start is { } startAt && end is { } endAt && now >= startAt && now <= endAt + grace;
    }
}
