using SmartRail.Application.Features.Journeys;
using SmartRail.Application.Features.Passenger;
using SmartRail.Domain;
using SmartRail.Domain.Journeys;

namespace SmartRail.UnitTests;

/// <summary>An in-memory stand-in for the database with the real seed's shape: Main Line, five stations, trains 8058 and 8061.</summary>
public sealed class FakePassengerQueries : IPassengerQueries
{
    public static readonly DateOnly Tuesday = new(2026, 10, 6);

    public static readonly Guid Service8058 = new("00000004-0000-4000-8000-000000000001");
    public static readonly Guid Service8061 = new("00000004-0000-4000-8000-000000000002");
    public static readonly Guid Journey8058 = new("00000005-0000-4000-8000-000000000001");
    public static readonly Guid Journey8061 = new("00000005-0000-4000-8000-000000000002");

    public static Guid Stop(Guid service, int sequence) => new($"{service.ToString()[..8]}-0000-4000-8000-{sequence:x12}");

    private readonly List<StationRecord> _stations =
    [
        new("CF", "Colombo Fort", null, null, [new("MAIN", 1)]),
        new("MDA", "Maradana", null, null, [new("MAIN", 2)]),
        new("RGM", "Ragama", null, null, [new("MAIN", 3)]),
        new("GPH", "Gampaha", null, null, [new("MAIN", 4)]),
        new("VYG", "Veyangoda", null, null, [new("MAIN", 5)]),
    ];

    public List<ServiceRecord> Services { get; } =
    [
        // 8058 Veyangoda 08:20, Gampaha 08:30/08:31, Ragama 08:42/08:43, Maradana 08:50/08:51, Colombo Fort 08:55
        Service(Service8058, "8058", "Slow", "Weekdays",
            ("VYG", null, new TimeOnly(8, 20)), ("GPH", new(8, 30), new(8, 31)), ("RGM", new(8, 42), new(8, 43)),
            ("MDA", new(8, 50), new(8, 51)), ("CF", new(8, 55), null)),

        // 8061 express: Veyangoda 07:50, Gampaha 08:00/08:01, Colombo Fort 08:30 (skips Ragama and Maradana)
        Service(Service8061, "8061", "Express", "Weekdays",
            ("VYG", null, new TimeOnly(7, 50)), ("GPH", new(8, 0), new(8, 1)), ("CF", new(8, 30), null)),
    ];

    public List<JourneyRecord> Journeys { get; } = [];

    public string? LineNameSi { get; set; } = "ප්‍රධාන මාර්ගය";

    public static ServiceRecord Service(Guid id, string number, string type, string runsOn, params (string Station, TimeOnly? Arrival, TimeOnly? Departure)[] stops) =>
        new(id, number, "MAIN", type, runsOn,
            stops.Select((s, i) => new ServiceStopRecord(Stop(id, i + 1), i + 1, s.Station, s.Arrival, s.Departure)).ToList());

    public void RenameStation(string code, string name)
    {
        var index = _stations.FindIndex(s => s.Code == code);
        _stations[index] = _stations[index] with { NameEn = name };
    }

    /// <summary>A journey with every stop Pending, estimates equal to the schedule (what the demo seed creates).</summary>
    public JourneyRecord AddJourney(Guid id, Guid serviceId, DateOnly date, JourneyStatus status = JourneyStatus.Scheduled, int delay = 0, int version = 0, DateTimeOffset? lastEventAt = null)
    {
        var service = Services.Single(s => s.ServiceId == serviceId);
        var schedule = ScheduleInstants.For(date, service.Stops.Select(s => new StopTimes(s.ServiceStopId, s.Sequence, s.ScheduledArrival, s.ScheduledDeparture)));
        var states = schedule.Select(s => new StopStateInfo(s.ServiceStopId, StopConfirmation.Pending, s.Arrival, s.Departure, null)).ToList();
        var journey = new JourneyRecord(id, serviceId, date, status, version, delay, states, lastEventAt);
        Journeys.Add(journey);
        return journey;
    }

    /// <summary>Marks stops Reported (with the departure time and delayed estimates for the stops after them) like slice 1 does.</summary>
    public JourneyRecord Report(Guid journeyId, int stopSequence, DateTimeOffset departedAt, int delayMinutes, DateTimeOffset lastEventAt)
    {
        var journey = Journeys.Single(j => j.JourneyId == journeyId);
        var service = Services.Single(s => s.ServiceId == journey.ServiceId);
        var delay = TimeSpan.FromMinutes(delayMinutes);
        var states = journey.StopStates.Select(state =>
        {
            var stop = service.Stops.Single(s => s.ServiceStopId == state.ServiceStopId);
            if (stop.Sequence == stopSequence)
            {
                return state with { Confirmation = StopConfirmation.Reported, ActualDeparture = departedAt, EstimatedDeparture = departedAt };
            }

            if (stop.Sequence < stopSequence)
            {
                return state with { Confirmation = state.Confirmation == StopConfirmation.Reported ? state.Confirmation : StopConfirmation.Inferred };
            }

            return state with { EstimatedArrival = state.EstimatedArrival + delay, EstimatedDeparture = state.EstimatedDeparture + delay };
        }).ToList();

        var updated = journey with { StopStates = states, DelayMinutes = delayMinutes, Version = journey.Version + 1, LastEventAt = lastEventAt };
        Journeys[Journeys.IndexOf(journey)] = updated;
        return updated;
    }

    /// <summary>The train has only ARRIVED at the stop (spec 06): the stop is Reported with an arrival time and no departure.</summary>
    public JourneyRecord ReportArrival(Guid journeyId, int stopSequence, DateTimeOffset arrivedAt, int delayMinutes, DateTimeOffset lastEventAt)
    {
        var journey = Journeys.Single(j => j.JourneyId == journeyId);
        var service = Services.Single(s => s.ServiceId == journey.ServiceId);
        var delay = TimeSpan.FromMinutes(delayMinutes);
        var states = journey.StopStates.Select(state =>
        {
            var stop = service.Stops.Single(s => s.ServiceStopId == state.ServiceStopId);
            if (stop.Sequence == stopSequence)
            {
                return state with { Confirmation = StopConfirmation.Reported, ActualArrival = arrivedAt, EstimatedArrival = null };
            }

            return stop.Sequence < stopSequence
                ? state with { Confirmation = StopConfirmation.Inferred }
                : state with { EstimatedArrival = state.EstimatedArrival + delay, EstimatedDeparture = state.EstimatedDeparture + delay };
        }).ToList();

        var updated = journey with { StopStates = states, DelayMinutes = delayMinutes, Version = journey.Version + 1, LastEventAt = lastEventAt };
        Journeys[Journeys.IndexOf(journey)] = updated;
        return updated;
    }

    public Task<IReadOnlyList<StationRecord>> GetStationsAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<StationRecord>>(_stations.ToList());

    public Task<LineRecord?> GetLineAsync(string lineCode, CancellationToken cancellationToken) =>
        Task.FromResult(string.Equals(lineCode, "MAIN", StringComparison.OrdinalIgnoreCase) ? new LineRecord("MAIN", "Main Line", LineNameSi, null) : null);

    public Task<IReadOnlyList<ServiceRecord>> GetServicesCallingAtAsync(string firstStationCode, string secondStationCode, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<ServiceRecord>>(Services
            .Where(s => s.Stops.Any(p => p.StationCode == firstStationCode) && s.Stops.Any(p => p.StationCode == secondStationCode))
            .ToList());

    public Task<IReadOnlyList<ServiceRecord>> GetServicesOfLineAsync(string lineCode, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<ServiceRecord>>(Services.Where(s => s.LineCode == lineCode).ToList());

    public Task<IReadOnlyList<ServiceRecord>> GetServicesAtAsync(string stationCode, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<ServiceRecord>>(Services.Where(s => s.Stops.Any(p => p.StationCode == stationCode)).ToList());

    public Task<IReadOnlyList<string>> GetPlatformCodesAsync(string stationCode, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<string>>(_stations.Any(s => s.Code == stationCode) ? ["1", "2"] : []);

    public Task<IReadOnlyList<JourneyRecord>> GetJourneysAsync(
        IReadOnlyCollection<Guid> serviceIds, DateOnly fromDate, DateOnly toDate, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<JourneyRecord>>(Journeys
            .Where(j => serviceIds.Contains(j.ServiceId) && j.OperatingDate >= fromDate && j.OperatingDate <= toDate)
            .ToList());
}
