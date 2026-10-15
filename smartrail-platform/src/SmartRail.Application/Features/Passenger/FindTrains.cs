using System.Diagnostics.Metrics;
using System.Globalization;
using SmartRail.Application.Features.Journeys;
using SmartRail.Domain;
using SmartRail.Domain.Journeys;

namespace SmartRail.Application.Features.Passenger;

public sealed record StationRef(string Code, LocalizedName Name);

public sealed record TimePair(DateTimeOffset Scheduled, DateTimeOffset? Estimated);

public sealed record AtFrom(TimePair? Arrival, TimePair? Departure);

public sealed record AtTo(TimePair? Arrival);

public sealed record TrainResult(
    string TrainNumber,
    string ServiceType,
    DateOnly OperatingDate,
    StationRef Destination,
    bool Live,
    Guid? JourneyId,
    AtFrom AtFrom,
    AtTo AtTo,
    int? DelayMinutes,
    string StatusGroup,
    string StatusKey,
    int? Version,
    DateTimeOffset? LastEventAt);

public sealed record TrainsResponse(StationRef From, StationRef To, DateOnly Date, DateTimeOffset ServerTime, IReadOnlyList<TrainResult> Trains);

public enum SearchError { None, InvalidRequest, SameStation, InvalidDate, DateOutOfRange, StationNotFound }

public sealed record FindTrainsResult(
    SearchError Error,
    TrainsResponse? Response,
    string Source,
    IReadOnlyList<string> UnsupportedCalendarTrains)
{
    public static FindTrainsResult Fail(SearchError error) => new(error, null, "none", []);
}

/// <summary>smartrail.trains.searches counts each search by outcome and by where its answers came from.</summary>
public static class SearchMetrics
{
    public const string MeterName = "SmartRail.Search";

    private static readonly Meter Meter = new(MeterName);
    private static readonly Counter<long> Searches = Meter.CreateCounter<long>("smartrail.trains.searches", description: "Train searches, by outcome and source");

    public static void Count(string outcome, string source) =>
        Searches.Add(1, new KeyValuePair<string, object?>("outcome", outcome), new KeyValuePair<string, object?>("source", source));
}

/// <summary>
/// GET /api/v1/trains: trains between two stations for a date (spec passenger-find-trains, R3, R4, R7, R8). Live journeys where
/// they exist, the timetable where they do not.
/// </summary>
public sealed class FindTrainsHandler(IPassengerQueries queries, TimeProvider clock, SearchSettings search, JourneySettings journeys)
{
    public async Task<FindTrainsResult> HandleAsync(string? from, string? to, string? dateText, CancellationToken cancellationToken)
    {
        var result = await RunAsync(from, to, dateText, cancellationToken);
        var outcome = result.Error != SearchError.None ? "invalid" : result.Response!.Trains.Count == 0 ? "empty" : "found";
        SearchMetrics.Count(outcome, result.Source);
        return result;
    }

    private async Task<FindTrainsResult> RunAsync(string? from, string? to, string? dateText, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(from) || string.IsNullOrWhiteSpace(to))
        {
            return FindTrainsResult.Fail(SearchError.InvalidRequest);
        }

        var fromCode = from.Trim();
        var toCode = to.Trim();
        if (string.Equals(fromCode, toCode, StringComparison.OrdinalIgnoreCase))
        {
            return FindTrainsResult.Fail(SearchError.SameStation);
        }

        var today = OperatingTime.Today(clock);
        var date = today;
        if (dateText is not null)
        {
            if (!DateOnly.TryParseExact(dateText, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out date))
            {
                return FindTrainsResult.Fail(SearchError.InvalidDate);
            }

            if (date < today || date > today.AddDays(search.MaxDaysAhead))
            {
                return FindTrainsResult.Fail(SearchError.DateOutOfRange);
            }
        }

        var stations = await queries.GetStationsAsync(cancellationToken);
        var fromStation = stations.FirstOrDefault(s => string.Equals(s.Code, fromCode, StringComparison.OrdinalIgnoreCase));
        var toStation = stations.FirstOrDefault(s => string.Equals(s.Code, toCode, StringComparison.OrdinalIgnoreCase));
        if (fromStation is null || toStation is null)
        {
            return FindTrainsResult.Fail(SearchError.StationNotFound);
        }

        var byCode = stations.ToDictionary(s => s.Code, StringComparer.Ordinal);
        StationRef Ref(string code) => new(code, LocalizedName.Of(byCode[code].NameEn, byCode[code].NameSi, byCode[code].NameTa));

        // R3: both stops belong to the service and From comes before To.
        var services = (await queries.GetServicesCallingAtAsync(fromStation.Code, toStation.Code, cancellationToken))
            .Where(s => FromBeforeTo(s, fromStation.Code, toStation.Code))
            .ToList();

        var now = clock.GetUtcNow();
        var fromDate = date == today ? today.AddDays(-2) : date; // a 24 hour journey that started two dates ago can still run today
        var journeyRows = services.Count == 0
            ? []
            : await queries.GetJourneysAsync(services.Select(s => s.ServiceId).ToList(), fromDate, date, cancellationToken);

        var trains = new List<(TrainResult Train, DateTimeOffset SortKey)>();
        var unsupported = new List<string>();
        var grace = TimeSpan.FromMinutes(journeys.VisibleAfterEndMinutes);

        foreach (var service in services)
        {
            var stopsInOrder = service.Stops.OrderBy(s => s.Sequence).ToList();
            var fromStop = stopsInOrder.Single(s => s.StationCode == fromStation.Code);
            var toStop = stopsInOrder.Single(s => s.StationCode == toStation.Code);
            var destination = Ref(stopsInOrder[^1].StationCode);
            var serviceJourneys = journeyRows.Where(j => j.ServiceId == service.ServiceId).ToList();

            foreach (var journey in serviceJourneys)
            {
                var schedule = Schedule(service, journey.OperatingDate);
                if (date == today && !IsCandidateToday(journey, service, schedule, fromStop, now, grace))
                {
                    continue;
                }

                if (date != today && journey.OperatingDate != date)
                {
                    continue;
                }

                trains.Add(LiveResult(service, journey, schedule, fromStop, toStop, destination));
            }

            // R4.3: a service with no journey row for the date is answered from the timetable.
            if (serviceJourneys.Any(j => j.OperatingDate == date))
            {
                continue;
            }

            switch (ServiceCalendar.Runs(service.RunsOn, date))
            {
                case null:
                    unsupported.Add(service.TrainNumber); // R6
                    break;
                case true:
                    var schedule = Schedule(service, date);
                    var departure = schedule[fromStop.ServiceStopId].Departure;
                    if (date == today && departure is { } at && at < now)
                    {
                        break; // for today, only trains whose scheduled departure from From is not in the past
                    }

                    trains.Add(TimetableResult(service, date, schedule, fromStop, toStop, destination));
                    break;
            }
        }

        var ordered = trains
            .OrderBy(t => t.SortKey)
            .ThenBy(t => t.Train.TrainNumber, StringComparer.Ordinal)
            .Take(search.MaxResults)
            .Select(t => t.Train)
            .ToList();

        var live = ordered.Count(t => t.Live);
        var source = ordered.Count == 0 ? "none" : live == ordered.Count ? "live" : live == 0 ? "timetable" : "mixed";

        var response = new TrainsResponse(Ref(fromStation.Code), Ref(toStation.Code), date, now, ordered);
        return new FindTrainsResult(SearchError.None, response, source, unsupported);
    }

    private static bool FromBeforeTo(ServiceRecord service, string fromCode, string toCode)
    {
        var fromStop = service.Stops.FirstOrDefault(s => s.StationCode == fromCode);
        var toStop = service.Stops.FirstOrDefault(s => s.StationCode == toCode);
        return fromStop is not null && toStop is not null && fromStop.Sequence < toStop.Sequence;
    }

    private static Dictionary<Guid, StopSchedule> Schedule(ServiceRecord service, DateOnly operatingDate) =>
        ScheduleInstants.For(operatingDate, service.Stops.Select(s => new StopTimes(s.ServiceStopId, s.Sequence, s.ScheduledArrival, s.ScheduledDeparture)))
            .ToDictionary(s => s.ServiceStopId);

    /// <summary>R4.1: not Completed, not over (expected end plus the grace period), and not yet departed From.</summary>
    private bool IsCandidateToday(
        JourneyRecord journey, ServiceRecord service, Dictionary<Guid, StopSchedule> schedule, ServiceStopRecord fromStop, DateTimeOffset now, TimeSpan grace)
    {
        if (journey.Status == JourneyStatus.Completed)
        {
            return false;
        }

        var last = service.Stops.OrderBy(s => s.Sequence).Last();
        var lastTimes = schedule[last.ServiceStopId];
        var expectedEnd = (lastTimes.Arrival ?? lastTimes.Departure) + TimeSpan.FromMinutes(journey.DelayMinutes);
        if (expectedEnd is { } end && now > end + grace)
        {
            return false;
        }

        var marks = journey.StopStates
            .Join(service.Stops, state => state.ServiceStopId, stop => stop.ServiceStopId, (state, stop) => new StopMark(stop.Sequence, state.Confirmation));
        return !StopPassed.HasDeparted(marks, fromStop.Sequence);
    }

    private (TrainResult Train, DateTimeOffset SortKey) LiveResult(
        ServiceRecord service, JourneyRecord journey, Dictionary<Guid, StopSchedule> schedule,
        ServiceStopRecord fromStop, ServiceStopRecord toStop, StationRef destination)
    {
        var delay = TimeSpan.FromMinutes(journey.DelayMinutes);
        var states = journey.StopStates.ToDictionary(s => s.ServiceStopId);

        TimePair? Pair(DateTimeOffset? scheduled, DateTimeOffset? stored) =>
            scheduled is { } at ? new TimePair(at, stored ?? at + delay) : null;

        var fromTimes = schedule[fromStop.ServiceStopId];
        var toTimes = schedule[toStop.ServiceStopId];
        states.TryGetValue(fromStop.ServiceStopId, out var fromState);
        states.TryGetValue(toStop.ServiceStopId, out var toState);

        var anyReport = journey.StopStates.Any(s => s.Confirmation == StopConfirmation.Reported);
        var (key, group) = JourneyProgress.StatusFor(anyReport, journey.DelayMinutes, journeys.OnTimeToleranceMinutes);

        var departure = Pair(fromTimes.Departure, fromState?.EstimatedDeparture);
        var train = new TrainResult(
            service.TrainNumber, service.ServiceType, journey.OperatingDate, destination, true, journey.JourneyId,
            new AtFrom(Pair(fromTimes.Arrival, fromState?.EstimatedArrival), departure),
            new AtTo(Pair(toTimes.Arrival, toState?.EstimatedArrival)),
            journey.DelayMinutes, group.ToString(), key.ToString(), journey.Version, journey.LastEventAt);

        return (train, departure?.Estimated ?? departure?.Scheduled ?? DateTimeOffset.MaxValue);
    }

    private static (TrainResult Train, DateTimeOffset SortKey) TimetableResult(
        ServiceRecord service, DateOnly date, Dictionary<Guid, StopSchedule> schedule,
        ServiceStopRecord fromStop, ServiceStopRecord toStop, StationRef destination)
    {
        TimePair? Scheduled(DateTimeOffset? at) => at is { } value ? new TimePair(value, null) : null;

        var fromTimes = schedule[fromStop.ServiceStopId];
        var toTimes = schedule[toStop.ServiceStopId];
        var train = new TrainResult(
            service.TrainNumber, service.ServiceType, date, destination, false, null,
            new AtFrom(Scheduled(fromTimes.Arrival), Scheduled(fromTimes.Departure)),
            new AtTo(Scheduled(toTimes.Arrival)),
            null, nameof(StatusGroup.Normal), nameof(JourneyStatusKey.Scheduled), null, null);

        return (train, fromTimes.Departure ?? DateTimeOffset.MaxValue);
    }
}
