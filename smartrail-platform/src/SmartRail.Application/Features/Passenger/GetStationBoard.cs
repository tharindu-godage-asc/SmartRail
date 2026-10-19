using System.Diagnostics.Metrics;
using SmartRail.Application.Features.Journeys;
using SmartRail.Domain;
using SmartRail.Domain.Journeys;

namespace SmartRail.Application.Features.Passenger;

/// <summary>Board:* settings (spec 09). The defaults are the values in the spec.</summary>
public sealed class BoardSettings
{
    public const string Section = "Board";

    public int DefaultCount { get; set; } = 8;
    public int MaxCount { get; set; } = 20;
}

/// <summary>The last position reported for the journey (state is "Departed" or "Arrived"). Shown when the delay is unknown.</summary>
public sealed record LastSeen(string State, string StationName, DateTimeOffset At);

/// <summary>
/// One train calling at the station. <c>Arrival</c> is null at the first stop and <c>Departure</c> at the final stop. <c>State</c> is
/// "AtPlatform" while the train has arrived and not left, otherwise "Due". <c>Live</c> is false when the call comes from the timetable.
/// </summary>
public sealed record BoardCall(
    string TrainNumber,
    string ServiceType,
    DateOnly OperatingDate,
    StationRef Destination,
    string? Platform,
    bool Live,
    Guid? JourneyId,
    TimePair? Arrival,
    TimePair? Departure,
    string State,
    int? DelayMinutes,
    bool DelayKnown,
    string StatusGroup,
    string StatusKey,
    int? Version,
    DateTimeOffset? LastEventAt,
    LastSeen? LastSeen);

public sealed record BoardResponse(
    StationRef Station,
    DateTimeOffset ServerTime,
    bool Live,
    IReadOnlyList<BoardCall> Trains,
    BoardCall? Next,
    BoardCall? FirstTomorrow);

public enum BoardError { None, StationNotFound, PlatformNotFound, InvalidRequest }

public sealed record BoardResult(BoardError Error, BoardResponse? Response, string Source)
{
    public static BoardResult Fail(BoardError error) => new(error, null, "none");
}

/// <summary>smartrail.board.reads counts each station board read by outcome and by where its answers came from.</summary>
public static class BoardMetrics
{
    public const string MeterName = "SmartRail.Board";

    private static readonly Meter Meter = new(MeterName);
    private static readonly Counter<long> Reads = Meter.CreateCounter<long>("smartrail.board.reads", description: "Station board reads, by outcome and source");

    public static void Count(string outcome, string source) =>
        Reads.Add(1, new KeyValuePair<string, object?>("outcome", outcome), new KeyValuePair<string, object?>("source", source));
}

/// <summary>
/// GET /api/v1/stations/{code}/board (spec 09): the trains that have not yet left the station, optionally for one platform, in the
/// order they are expected. Live journeys where they exist, the timetable where they do not (same rules as the train search).
/// When nothing is due it also says what comes next today, or the first service on the next day a service runs.
/// </summary>
public sealed class GetStationBoardHandler(
    IPassengerQueries queries,
    IJourneyRepository journeys,
    TimeProvider clock,
    BoardSettings board,
    SearchSettings search,
    JourneySettings journeySettings)
{
    public async Task<BoardResult> HandleAsync(string? stationCode, string? platform, string? countText, CancellationToken cancellationToken)
    {
        var result = await RunAsync(stationCode, platform, countText, cancellationToken);
        var outcome = result.Error != BoardError.None ? "invalid" : result.Response!.Trains.Count == 0 ? "empty" : "found";
        BoardMetrics.Count(outcome, result.Source);
        return result;
    }

    private async Task<BoardResult> RunAsync(string? stationCode, string? platformText, string? countText, CancellationToken cancellationToken)
    {
        var count = board.DefaultCount;
        if (countText is not null && (!int.TryParse(countText, out count) || count < 1 || count > board.MaxCount))
        {
            return BoardResult.Fail(BoardError.InvalidRequest);
        }

        if (string.IsNullOrWhiteSpace(stationCode))
        {
            return BoardResult.Fail(BoardError.StationNotFound);
        }

        var stations = await queries.GetStationsAsync(cancellationToken);
        var station = stations.FirstOrDefault(s => string.Equals(s.Code, stationCode.Trim(), StringComparison.OrdinalIgnoreCase));
        if (station is null)
        {
            return BoardResult.Fail(BoardError.StationNotFound);
        }

        var platform = string.IsNullOrWhiteSpace(platformText) ? null : platformText.Trim();
        if (platform is not null)
        {
            var codes = await queries.GetPlatformCodesAsync(station.Code, cancellationToken);
            if (!codes.Contains(platform, StringComparer.OrdinalIgnoreCase))
            {
                return BoardResult.Fail(BoardError.PlatformNotFound);
            }
        }

        var byCode = stations.ToDictionary(s => s.Code, StringComparer.Ordinal);
        StationRef Ref(string code) => new(code, LocalizedName.Of(byCode[code].NameEn, byCode[code].NameSi, byCode[code].NameTa));

        var now = clock.GetUtcNow();
        var today = OperatingTime.Today(clock);
        var grace = TimeSpan.FromMinutes(journeySettings.VisibleAfterEndMinutes);

        var services = await queries.GetServicesAtAsync(station.Code, cancellationToken);
        var existing = services.Count == 0
            ? []
            : await queries.GetJourneysAsync(services.Select(s => s.ServiceId).ToList(), today.AddDays(-2), today, cancellationToken);
        // Every unfinished journey, with its events (so the delay length is known) and its stop states. A journey that ran today and
        // is Completed is not in this list, but it IS in `existing`, so the timetable does not bring its train back.
        var live = await journeys.GetUnfinishedAsync(today.AddDays(-2), today, cancellationToken);

        var calls = new List<(BoardCall Call, DateTimeOffset SortKey)>();
        foreach (var service in services)
        {
            var stops = service.Stops.OrderBy(s => s.Sequence).ToList();
            var stopAt = stops.First(s => s.StationCode == station.Code);
            var destination = Ref(stops[^1].StationCode);

            foreach (var data in live.Where(j => j.TrainNumber == service.TrainNumber))
            {
                if (IsOnBoard(data, stopAt, now, grace))
                {
                    calls.Add(LiveCall(service, data, stopAt, destination, now));
                }
            }

            // A service with no journey row for today is answered from the timetable, while its time at this station is not in the past.
            var hasRowToday = existing.Any(j => j.ServiceId == service.ServiceId && j.OperatingDate == today);
            if (!hasRowToday && service.RunsOnDate(today) == true)
            {
                var timetable = TimetableCall(service, today, stopAt, destination);
                if (CallTime(timetable) is { } at && at >= now)
                {
                    calls.Add((timetable, at));
                }
            }
        }

        var trains = calls
            .Where(c => platform is null || string.Equals(c.Call.Platform, platform, StringComparison.OrdinalIgnoreCase))
            .OrderBy(c => c.SortKey)
            .ThenBy(c => c.Call.TrainNumber, StringComparer.Ordinal)
            .Take(count)
            .Select(c => c.Call)
            .ToList();

        BoardCall? next = null;
        BoardCall? firstTomorrow = null;
        if (trains.Count == 0)
        {
            (next, firstTomorrow) = FindNext(services, existing, station.Code, platform, today, now, Ref);
        }

        var anyLive = trains.Any(t => t.Live);
        var source = trains.Count == 0 ? "none" : anyLive ? (trains.All(t => t.Live) ? "live" : "mixed") : "timetable";
        return new BoardResult(BoardError.None, new BoardResponse(Ref(station.Code), now, anyLive, trains, next, firstTomorrow), source);
    }

    /// <summary>Not Completed, not over (expected end plus the grace period), and the train has not left this stop (same rule as the search, R4.1).</summary>
    private static bool IsOnBoard(JourneyData data, ServiceStopRecord stopAt, DateTimeOffset now, TimeSpan grace)
    {
        if (data.Status is JourneyStatus.Completed or JourneyStatus.Cancelled || data.Stops.Count == 0)
        {
            return false;
        }

        var schedule = data.Schedule();
        var last = schedule[^1];
        var expectedEnd = (last.Arrival ?? last.Departure) + TimeSpan.FromMinutes(data.DelayMinutes);
        if (expectedEnd is { } end && now > end + grace)
        {
            return false;
        }

        var marks = data.StopStates.Join(
            data.Stops, state => state.ServiceStopId, stop => stop.ServiceStopId,
            (state, stop) => new StopMark(stop.Sequence, state.Confirmation, state.ActualDeparture is not null));
        return !StopPassed.HasDeparted(marks, stopAt.Sequence);
    }

    private (BoardCall Call, DateTimeOffset SortKey) LiveCall(
        ServiceRecord service, JourneyData data, ServiceStopRecord stopAt, StationRef destination, DateTimeOffset now)
    {
        var snapshot = SnapshotBuilder.Build(data, now, journeySettings.OnTimeToleranceMinutes);
        var stop = snapshot.Stops.Single(s => s.ServiceStopId == stopAt.ServiceStopId);
        var delay = TimeSpan.FromMinutes(data.DelayMinutes);

        // With the delay length unknown there is no honest estimate (spec 06 R7): the scheduled time stays, the estimate is empty.
        TimePair? Pair(DateTimeOffset? scheduled, DateTimeOffset? stored) =>
            scheduled is { } at ? new TimePair(at, data.DelayKnown ? stored ?? at + delay : null) : null;

        var arrival = Pair(stop.ScheduledArrival, stop.EstimatedArrival);
        var departure = Pair(stop.ScheduledDeparture, stop.EstimatedDeparture);
        var atPlatform = stop.ActualArrival is not null && stop.ActualDeparture is null;
        var platform = data.StopStates.FirstOrDefault(s => s.ServiceStopId == stopAt.ServiceStopId)?.PlatformCode ?? stopAt.PlannedPlatform;
        LastSeen? seen = snapshot.LastKnownPosition is { } position && (position.DepartedAt ?? position.ArrivedAt) is { } at
            ? new LastSeen(position.State, position.StationName, at)
            : null;

        var call = new BoardCall(
            service.TrainNumber, service.ServiceType, data.OperatingDate, destination, platform, true, data.JourneyId,
            arrival, departure, atPlatform ? "AtPlatform" : "Due", data.DelayMinutes, data.DelayKnown,
            snapshot.StatusGroup, snapshot.StatusKey, data.Version, data.Events.Max(e => e.ReceivedAt), seen);
        return (call, CallTime(call) ?? DateTimeOffset.MaxValue);
    }

    private static BoardCall TimetableCall(ServiceRecord service, DateOnly date, ServiceStopRecord stopAt, StationRef destination)
    {
        var schedule = ScheduleInstants.For(date, service.Stops.Select(s => new StopTimes(s.ServiceStopId, s.Sequence, s.ScheduledArrival, s.ScheduledDeparture)))
            .Single(s => s.ServiceStopId == stopAt.ServiceStopId);
        TimePair? Scheduled(DateTimeOffset? at) => at is { } value ? new TimePair(value, null) : null;

        return new BoardCall(
            service.TrainNumber, service.ServiceType, date, destination, stopAt.PlannedPlatform, false, null,
            Scheduled(schedule.Arrival), Scheduled(schedule.Departure), "Due", null, true,
            nameof(StatusGroup.Normal), nameof(JourneyStatusKey.Scheduled), null, null, null);
    }

    /// <summary>The time the train is expected at the station: arrival (estimated, else scheduled), otherwise departure.</summary>
    private static DateTimeOffset? CallTime(BoardCall call) =>
        call.Arrival is { } arrival ? arrival.Estimated ?? arrival.Scheduled
        : call.Departure is { } departure ? departure.Estimated ?? departure.Scheduled
        : null;

    /// <summary>
    /// D-45: nothing is due. <c>next</c> is the next scheduled call later today; when there is none, <c>firstTomorrow</c> is the first call
    /// of the next day (within the search horizon) on which a service runs. Both come from the timetable and are never live.
    /// </summary>
    private (BoardCall? Next, BoardCall? FirstTomorrow) FindNext(
        IReadOnlyList<ServiceRecord> services, IReadOnlyList<JourneyRecord> existing, string stationCode, string? platform,
        DateOnly today, DateTimeOffset now, Func<string, StationRef> reference)
    {
        BoardCall? Earliest(DateOnly date, bool skipServicesWithAJourneyToday)
        {
            BoardCall? best = null;
            DateTimeOffset bestAt = DateTimeOffset.MaxValue;
            foreach (var service in services)
            {
                var stops = service.Stops.OrderBy(s => s.Sequence).ToList();
                var stopAt = stops.First(s => s.StationCode == stationCode);
                if (platform is not null && !string.Equals(stopAt.PlannedPlatform, platform, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (service.RunsOnDate(date) != true)
                {
                    continue;
                }

                // A train that already has today's journey is either listed (so the board is not empty) or has left: never "next".
                if (skipServicesWithAJourneyToday && existing.Any(j => j.ServiceId == service.ServiceId && j.OperatingDate == today))
                {
                    continue;
                }

                var call = TimetableCall(service, date, stopAt, reference(stops[^1].StationCode));
                if (CallTime(call) is { } at && (date != today || at >= now) && at < bestAt)
                {
                    best = call;
                    bestAt = at;
                }
            }

            return best;
        }

        if (Earliest(today, skipServicesWithAJourneyToday: true) is { } later)
        {
            return (later, null);
        }

        for (var day = 1; day <= search.MaxDaysAhead; day++)
        {
            if (Earliest(today.AddDays(day), skipServicesWithAJourneyToday: false) is { } first)
            {
                return (null, first);
            }
        }

        return (null, null);
    }
}
