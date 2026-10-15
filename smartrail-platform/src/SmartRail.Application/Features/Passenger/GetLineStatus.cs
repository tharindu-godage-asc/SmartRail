using SmartRail.Application.Features.Journeys;
using SmartRail.Domain;
using SmartRail.Domain.Journeys;

namespace SmartRail.Application.Features.Passenger;

/// <summary>
/// <c>LastEventAt</c> is how fresh the data is: the latest time the server received a report from a journey in progress.
/// <c>ServerTime</c> is only when the answer was computed and must not be shown as "updated".
/// </summary>
public sealed record LineStatusResponse(
    string LineCode,
    LocalizedName LineName,
    string StatusGroup,
    string StatusKey,
    int JourneysInProgress,
    int DelayedJourneys,
    DateTimeOffset? LastEventAt,
    DateTimeOffset ServerTime);

/// <summary>GET /api/v1/lines/{code}/status: normal unless a journey in progress is delayed (R9). No wording: the client words it.</summary>
public sealed class GetLineStatusHandler(IPassengerQueries queries, TimeProvider clock, JourneySettings journeys)
{
    public async Task<LineStatusResponse?> HandleAsync(string lineCode, CancellationToken cancellationToken)
    {
        var line = await queries.GetLineAsync(lineCode.Trim(), cancellationToken);
        if (line is null)
        {
            return null;
        }

        var now = clock.GetUtcNow();
        var today = OperatingTime.Today(clock);
        var grace = TimeSpan.FromMinutes(journeys.VisibleAfterEndMinutes);

        var services = await queries.GetServicesOfLineAsync(line.Code, cancellationToken);
        var rows = services.Count == 0
            ? []
            : await queries.GetJourneysAsync(services.Select(s => s.ServiceId).ToList(), today.AddDays(-2), today, cancellationToken);

        var inProgress = 0;
        var delayed = 0;
        DateTimeOffset? lastEventAt = null;

        foreach (var journey in rows)
        {
            var service = services.Single(s => s.ServiceId == journey.ServiceId);
            if (journey.Status == JourneyStatus.Completed || service.Stops.Count == 0)
            {
                continue;
            }

            var schedule = ScheduleInstants.For(journey.OperatingDate, service.Stops.Select(s => new StopTimes(s.ServiceStopId, s.Sequence, s.ScheduledArrival, s.ScheduledDeparture)));
            var first = schedule[0];
            var last = schedule[^1];
            var start = first.Departure ?? first.Arrival;
            var end = (last.Arrival ?? last.Departure) + TimeSpan.FromMinutes(journey.DelayMinutes);

            // In progress: started, and not past its expected end plus the grace period (the same rule as R15 of slice 1).
            if (start is not { } startAt || end is not { } endAt || now < startAt || now > endAt + grace)
            {
                continue;
            }

            inProgress++;
            var anyReport = journey.StopStates.Any(s => s.Confirmation == StopConfirmation.Reported);
            if (JourneyProgress.StatusFor(anyReport, journey.DelayMinutes, journeys.OnTimeToleranceMinutes).Group == StatusGroup.Warning)
            {
                delayed++;
            }

            if (journey.LastEventAt is { } at && (lastEventAt is null || at > lastEventAt))
            {
                lastEventAt = at;
            }
        }

        var key = delayed == 0 ? "Normal" : "SomeDelayed";
        var group = delayed == 0 ? nameof(StatusGroup.Normal) : nameof(StatusGroup.Warning);
        return new LineStatusResponse(
            line.Code, LocalizedName.Of(line.NameEn, line.NameSi, line.NameTa), group, key, inProgress, delayed, lastEventAt, now);
    }
}
