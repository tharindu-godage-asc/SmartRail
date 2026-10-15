using SmartRail.Domain.Journeys;

namespace SmartRail.Application.Features.Journeys;

public sealed record StopSnapshot(
    Guid ServiceStopId,
    int Sequence,
    string StationCode,
    string StationName,
    DateTimeOffset? ScheduledArrival,
    DateTimeOffset? ScheduledDeparture,
    DateTimeOffset? EstimatedArrival,
    DateTimeOffset? EstimatedDeparture,
    DateTimeOffset? ActualDeparture,
    string Confirmation,
    DateTimeOffset? ActualArrival = null);

public sealed record EtaSnapshot(string StationCode, string StationName, DateTimeOffset Time);

/// <summary>
/// Where the train was last reported. <c>State</c> is "Arrived" while the train is at the station (DepartedAt is null) and
/// "Departed" after it left.
/// </summary>
public sealed record PositionSnapshot(
    string StationCode,
    string StationName,
    DateTimeOffset? DepartedAt,
    string State = "Departed",
    DateTimeOffset? ArrivedAt = null);

/// <summary>The public status of one journey (AC-E04-03 5, partly). The version lets clients ignore older snapshots (ADR-09).</summary>
public sealed record JourneyStatusSnapshot(
    Guid JourneyId,
    string TrainNumber,
    DateOnly OperatingDate,
    int Version,
    DateTimeOffset ServerTime,
    string StatusGroup,
    string StatusKey,
    int DelayMinutes,
    EtaSnapshot? PrimaryEta,
    PositionSnapshot? LastKnownPosition,
    IReadOnlyList<StopSnapshot> Stops);

public static class SnapshotBuilder
{
    public static JourneyStatusSnapshot Build(JourneyData data, DateTimeOffset now, int onTimeToleranceMinutes)
    {
        var schedule = data.Schedule().ToDictionary(s => s.ServiceStopId);
        var states = data.StopStates.ToDictionary(s => s.ServiceStopId);
        var stopsInOrder = data.Stops.OrderBy(s => s.Sequence).ToList();

        var stops = stopsInOrder.Select(stop =>
        {
            var times = schedule[stop.ServiceStopId];
            states.TryGetValue(stop.ServiceStopId, out var state);
            return new StopSnapshot(
                stop.ServiceStopId, stop.Sequence, stop.StationCode, stop.StationName,
                times.Arrival, times.Departure,
                state?.EstimatedArrival, state?.EstimatedDeparture, state?.ActualDeparture,
                (state?.Confirmation ?? StopConfirmation.Pending).ToString(),
                state?.ActualArrival);
        }).ToList();

        var lastReported = stopsInOrder.LastOrDefault(s => states.TryGetValue(s.ServiceStopId, out var st) && st.Confirmation == StopConfirmation.Reported);
        var (key, group) = data.Status == JourneyStatus.Completed
            ? (JourneyStatusKey.Completed, SmartRail.Domain.StatusGroup.Normal)
            : JourneyProgress.StatusFor(lastReported is not null, data.DelayMinutes, onTimeToleranceMinutes);

        EtaSnapshot? eta = null;
        PositionSnapshot? position = null;
        if (lastReported is not null)
        {
            var lastState = states[lastReported.ServiceStopId];
            if (lastState.ActualDeparture is { } departedAt)
            {
                position = new PositionSnapshot(lastReported.StationCode, lastReported.StationName, departedAt, "Departed", lastState.ActualArrival);
            }
            else if (lastState.ActualArrival is { } arrivedAt)
            {
                position = new PositionSnapshot(lastReported.StationCode, lastReported.StationName, null, "Arrived", arrivedAt);
            }

            var next = stopsInOrder.FirstOrDefault(s => s.Sequence > lastReported.Sequence);
            if (next is not null)
            {
                var estimated = states.GetValueOrDefault(next.ServiceStopId)?.EstimatedArrival
                                ?? schedule[next.ServiceStopId].Arrival + TimeSpan.FromMinutes(data.DelayMinutes);
                if (estimated is { } time)
                {
                    eta = new EtaSnapshot(next.StationCode, next.StationName, time);
                }
            }
        }

        return new JourneyStatusSnapshot(
            data.JourneyId, data.TrainNumber, data.OperatingDate, data.Version, now,
            group.ToString(), key.ToString(), data.DelayMinutes, eta, position, stops);
    }
}

public sealed class GetJourneyStatusHandler(IJourneyRepository repository, TimeProvider clock, JourneySettings settings)
{
    public async Task<JourneyStatusSnapshot?> HandleAsync(Guid journeyId, CancellationToken cancellationToken)
    {
        var data = await repository.GetAsync(journeyId, cancellationToken);
        return data is null ? null : SnapshotBuilder.Build(data, clock.GetUtcNow(), settings.OnTimeToleranceMinutes);
    }
}

/// <summary>R15: the journey in progress for a train number, otherwise today's, otherwise none.</summary>
public sealed class GetTrainStatusHandler(IJourneyRepository repository, TimeProvider clock, JourneySettings settings)
{
    public async Task<JourneyStatusSnapshot?> HandleAsync(string trainNumber, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var today = SmartRail.Domain.OperatingTime.Today(clock);

        // A journey takes at most 24 hours, so one that is still in progress started no earlier than two operating dates ago.
        var candidates = await repository.GetForTrainAsync(trainNumber, today.AddDays(-2), today, cancellationToken);
        var windows = candidates
            .Where(c => c.Stops.Count > 0)
            .Select(c =>
            {
                var schedule = c.Schedule();
                var start = schedule[0].Departure ?? schedule[0].Arrival ?? DateTimeOffset.MinValue;
                var end = (schedule[^1].Arrival ?? schedule[^1].Departure ?? start) + TimeSpan.FromMinutes(c.DelayMinutes);
                return new JourneyWindow(c.JourneyId, c.OperatingDate, start, end, c.Status == JourneyStatus.Completed);
            })
            .ToList();

        var chosen = JourneySelection.Select(windows, today, now, TimeSpan.FromMinutes(settings.VisibleAfterEndMinutes));
        var data = candidates.FirstOrDefault(c => c.JourneyId == chosen);
        return chosen is null || data is null ? null : SnapshotBuilder.Build(data, now, settings.OnTimeToleranceMinutes);
    }
}

public sealed record OperatorStop(
    Guid ServiceStopId,
    int Sequence,
    string StationCode,
    string StationName,
    DateTimeOffset? ScheduledArrival,
    DateTimeOffset? ScheduledDeparture,
    bool IsFinalStop,
    bool IsArrivalStation = false);

public sealed record OperatorJourneyResponse(
    Guid JourneyId,
    string TrainNumber,
    DateOnly OperatingDate,
    string Status,
    int Version,
    IReadOnlyList<OperatorStop> Stops);

/// <summary>The journey the signed-in operator is assigned to right now, with the stop IDs the app needs to report.</summary>
public sealed class GetOperatorJourneyHandler(IJourneyRepository repository, TimeProvider clock)
{
    public async Task<OperatorJourneyResponse?> HandleAsync(string operatorUserId, CancellationToken cancellationToken)
    {
        var data = await repository.GetAssignedAsync(operatorUserId, clock.GetUtcNow(), cancellationToken);
        if (data is null)
        {
            return null;
        }

        var schedule = data.Schedule().ToDictionary(s => s.ServiceStopId);
        var stops = data.Stops.OrderBy(s => s.Sequence)
            .Select(s => new OperatorStop(
                s.ServiceStopId, s.Sequence, s.StationCode, s.StationName,
                schedule[s.ServiceStopId].Arrival, schedule[s.ServiceStopId].Departure, s.IsFinalStop, s.IsArrivalStation))
            .ToList();

        return new OperatorJourneyResponse(data.JourneyId, data.TrainNumber, data.OperatingDate, data.Status.ToString(), data.Version, stops);
    }
}
