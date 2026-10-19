using Microsoft.EntityFrameworkCore;
using SmartRail.Domain;
using SmartRail.Domain.Journeys;
using SmartRail.Infrastructure.Persistence;

namespace SmartRail.Infrastructure.Seeding;

/// <summary>
/// The demo layer: today's journey for train 8058 with a Pending stop state per stop (AC-E02-01) and an assignment
/// that covers the whole operating day, so manual testing never fails with a false "not assigned". Idempotent (R3).
/// Needs a database with the 0001 migration applied; runs as the admin user.
/// </summary>
public sealed class DemoSeeder(SmartrailDbContext db, TimeProvider clock)
{
    /// <summary>Returns true when it created the journey, false when today's journey already existed.</summary>
    public async Task<bool> SeedAsync(string? operatorSubject, CancellationToken cancellationToken = default)
    {
        var today = OperatingTime.Today(clock);
        var subject = string.IsNullOrWhiteSpace(operatorSubject) ? DemoIds.Nimal.ToString() : operatorSubject;

        var journey = await db.Journeys.SingleOrDefaultAsync(
            j => j.ServiceId == ReferenceData.Service8058 && j.OperatingDate == today, cancellationToken);
        var created = journey is null;

        if (journey is null)
        {
            // Created whatever the weekday (deferred decision D2).
            journey = new Journey
            {
                Id = Guid.CreateVersion7(clock.GetUtcNow()),
                ServiceId = ReferenceData.Service8058,
                OperatingDate = today,
                Status = JourneyStatus.Scheduled,
                Version = 0,
            };
            db.Journeys.Add(journey);

            var stops = await db.ServiceStops
                .Where(s => s.ServiceId == ReferenceData.Service8058)
                .OrderBy(s => s.Sequence)
                .ToListAsync(cancellationToken);

            // R16: the same rule the API uses to place stop times on calendar days (correct for overnight trains too).
            var schedule = ScheduleInstants.For(today, stops.Select(s => new StopTimes(s.Id, s.Sequence, s.ScheduledArrival, s.ScheduledDeparture)))
                .ToDictionary(s => s.ServiceStopId);

            foreach (var stop in stops)
            {
                db.JourneyStopStates.Add(new JourneyStopState
                {
                    JourneyId = journey.Id,
                    ServiceStopId = stop.Id,
                    EstimatedArrival = schedule[stop.Id].Arrival,
                    EstimatedDeparture = schedule[stop.Id].Departure,
                    Confirmation = StopConfirmation.Pending,
                });
            }
        }

        var assigned = await db.OperatorAssignments.AnyAsync(
            a => a.JourneyId == journey.Id && a.OperatorUserId == subject, cancellationToken);
        if (!assigned)
        {
            db.OperatorAssignments.Add(new OperatorAssignment
            {
                Id = Guid.CreateVersion7(clock.GetUtcNow()),
                JourneyId = journey.Id,
                OperatorUserId = subject,
                ValidFrom = OperatingTime.StartOfDay(today),
                ValidTo = OperatingTime.StartOfNextDay(today),
                AssignedBy = DemoIds.AssignedBy,
            });
        }

        // One SaveChanges is one transaction: the journey and all its stop states exist together or not at all.
        await db.SaveChangesAsync(cancellationToken);
        return created;
    }

    /// <summary>The demo train 8070 leaves its first station this many minutes after the seed runs.</summary>
    public const int SoonMinutes = 4;

    // Minutes after the first departure for each stop of 8070: Veyangoda, Gampaha, Ragama, Maradana, Colombo Fort (arrival, departure).
    private static readonly (int Sequence, int? Arrival, int? Departure, Guid Station)[] SoonStops =
    [
        (1, null, 0, ReferenceData.StationVeyangoda),
        (2, 10, 11, ReferenceData.StationGampaha),
        (3, 22, 23, ReferenceData.StationRagama),
        (4, 30, 31, ReferenceData.StationMaradana),
        (5, 35, null, ReferenceData.StationColomboFort),
    ];

    /// <summary>
    /// Spec 09 section 16: creates or updates the demo train 8070 so that it leaves Veyangoda <see cref="SoonMinutes"/> minutes from now,
    /// platform 1, every day. It has no journey, so the station boards show it from the timetable until its time passes. Run again later
    /// it moves the same service (no second one). Returns the local time of the departure. Compose only (Seeding:DemoData).
    /// </summary>
    public async Task<TimeOnly> SeedSoonTrainAsync(CancellationToken cancellationToken = default)
    {
        var now = TimeZoneInfo.ConvertTime(clock.GetUtcNow(), OperatingTime.Zone);
        var first = new TimeOnly(now.Hour, now.Minute).AddMinutes(SoonMinutes);

        var service = await db.TrainServices.SingleOrDefaultAsync(s => s.Id == DemoIds.SoonService, cancellationToken);
        if (service is null)
        {
            service = new Domain.Reference.TrainService
            {
                Id = DemoIds.SoonService, TrainNumber = "8070", LineId = ReferenceData.LineMain, ServiceType = Domain.Reference.ServiceType.Slow,
                ServiceCategory = Domain.Reference.ServiceCategory.Regional, Direction = Domain.Reference.ServiceDirection.Inbound, RunsOn = "Daily",
            };
            db.TrainServices.Add(service);
        }

        var existing = await db.ServiceStops.Where(s => s.ServiceId == DemoIds.SoonService).ToDictionaryAsync(s => s.Sequence, cancellationToken);
        foreach (var (sequence, arrival, departure, station) in SoonStops)
        {
            if (!existing.TryGetValue(sequence, out var stop))
            {
                stop = new Domain.Reference.ServiceStop
                {
                    Id = DemoIds.SoonStop(sequence), ServiceId = DemoIds.SoonService, StationId = station, Sequence = sequence,
                    PlannedPlatformId = ReferenceData.PlatformOne(station),
                };
                db.ServiceStops.Add(stop);
            }

            stop.ScheduledArrival = arrival is { } a ? first.AddMinutes(a) : null;
            stop.ScheduledDeparture = departure is { } d ? first.AddMinutes(d) : null;
        }

        await db.SaveChangesAsync(cancellationToken);
        return first;
    }

    // Spec 10 demo data: eight more daily services (four toward Colombo, four away) with a journey each today, so the passenger app has
    // trains to search for in both directions. Minutes after the first departure for each stop (arrival, departure), in travel order.
    private static readonly (int? Arrival, int? Departure)[] DemoOffsets = [(null, 0), (10, 11), (22, 23), (30, 31), (35, null)];

    private static readonly Guid[] TowardColombo =
        [ReferenceData.StationVeyangoda, ReferenceData.StationGampaha, ReferenceData.StationRagama, ReferenceData.StationMaradana, ReferenceData.StationColomboFort];

    // Train number, inbound?, minutes from now to the first departure, delay in minutes (0 = on time).
    private static readonly (string Number, bool Inbound, int InMinutes, int Delay)[] DemoTrains =
    [
        ("8102", true, 12, 0), ("8104", true, 35, 8), ("8106", true, 65, 0), ("8108", true, 100, 15),
        ("8201", false, 8, 0), ("8203", false, 30, 0), ("8205", false, 55, 5), ("8207", false, 90, 0),
    ];

    private static Guid DemoGuid(int kind, int train, int n) => new($"0000000{kind}-0000-4000-8000-{0xb00000 + (train * 100) + n:x12}");

    /// <summary>
    /// Creates or moves the eight demo services and their journeys for today so the first departure is a few minutes to a couple of hours
    /// from now, some of them delayed (the estimates move with the delay). Run again it updates the same rows. Compose only.
    /// </summary>
    public async Task SeedDemoTrainsAsync(string? operatorSubject, CancellationToken cancellationToken = default)
    {
        var today = OperatingTime.Today(clock);
        var now = TimeZoneInfo.ConvertTime(clock.GetUtcNow(), OperatingTime.Zone);
        var services = await db.TrainServices.Where(s => DemoTrains.Select(t => DemoGuid(4, int.Parse(t.Number), 0)).Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, cancellationToken);

        foreach (var (number, inbound, inMinutes, delay) in DemoTrains)
        {
            var n = int.Parse(number);
            var serviceId = DemoGuid(4, n, 0);
            var first = new TimeOnly(now.Hour, now.Minute).AddMinutes(inMinutes);
            if (first < new TimeOnly(now.Hour, now.Minute)) continue; // would wrap past midnight: skip rather than show a train in the past

            if (!services.TryGetValue(serviceId, out var service))
            {
                service = new Domain.Reference.TrainService
                {
                    Id = serviceId, TrainNumber = number, LineId = ReferenceData.LineMain, ServiceType = Domain.Reference.ServiceType.Slow,
                    ServiceCategory = Domain.Reference.ServiceCategory.Regional,
                    Direction = inbound ? Domain.Reference.ServiceDirection.Inbound : Domain.Reference.ServiceDirection.Outbound, RunsOn = "Daily",
                };
                db.TrainServices.Add(service);
            }

            var order = inbound ? TowardColombo : TowardColombo.Reverse().ToArray();
            var existing = await db.ServiceStops.Where(s => s.ServiceId == serviceId).ToDictionaryAsync(s => s.Sequence, cancellationToken);
            var stops = new List<Domain.Reference.ServiceStop>();
            for (var i = 0; i < order.Length; i++)
            {
                if (!existing.TryGetValue(i + 1, out var stop))
                {
                    stop = new Domain.Reference.ServiceStop
                    {
                        Id = DemoGuid(5, n, i + 1), ServiceId = serviceId, StationId = order[i], Sequence = i + 1,
                        PlannedPlatformId = ReferenceData.PlatformOne(order[i]),
                    };
                    db.ServiceStops.Add(stop);
                }

                stop.ScheduledArrival = DemoOffsets[i].Arrival is { } a ? first.AddMinutes(a) : null;
                stop.ScheduledDeparture = DemoOffsets[i].Departure is { } d ? first.AddMinutes(d) : null;
                stops.Add(stop);
            }

            var journeyId = DemoGuid(6, n, 0);
            var journey = await db.Journeys.SingleOrDefaultAsync(j => j.Id == journeyId, cancellationToken);
            if (journey is null)
            {
                journey = new Journey { Id = journeyId, ServiceId = serviceId, OperatingDate = today, Status = JourneyStatus.Scheduled, Version = 0 };
                db.Journeys.Add(journey);
            }

            // The demo operator is also assigned to 8102, later than to 8058 (the latest assignment wins), because 8058 has usually left by
            // the time someone tests: this way the operator app shows a train that is still ahead and the passenger app can find it.
            if (number == "8102")
            {
                var subject = string.IsNullOrWhiteSpace(operatorSubject) ? DemoIds.Nimal.ToString() : operatorSubject;
                if (!await db.OperatorAssignments.AnyAsync(a => a.JourneyId == journeyId && a.OperatorUserId == subject, cancellationToken))
                {
                    db.OperatorAssignments.Add(new OperatorAssignment
                    {
                        Id = Guid.CreateVersion7(clock.GetUtcNow()), JourneyId = journeyId, OperatorUserId = subject,
                        ValidFrom = OperatingTime.StartOfDay(today).AddMinutes(1), ValidTo = OperatingTime.StartOfNextDay(today), AssignedBy = DemoIds.AssignedBy,
                    });
                }
            }

            if (journey.OperatingDate != today || journey.Status != JourneyStatus.Scheduled) continue; // a reported journey is left alone
            journey.CurrentDelayMinutes = delay;

            var schedule = ScheduleInstants.For(today, stops.Select(s => new StopTimes(s.Id, s.Sequence, s.ScheduledArrival, s.ScheduledDeparture)))
                .ToDictionary(s => s.ServiceStopId);
            var states = await db.JourneyStopStates.Where(x => x.JourneyId == journeyId).ToDictionaryAsync(x => x.ServiceStopId, cancellationToken);
            foreach (var stop in stops)
            {
                if (!states.TryGetValue(stop.Id, out var state))
                {
                    state = new JourneyStopState { JourneyId = journeyId, ServiceStopId = stop.Id, Confirmation = StopConfirmation.Pending };
                    db.JourneyStopStates.Add(state);
                }

                state.EstimatedArrival = schedule[stop.Id].Arrival?.AddMinutes(delay);
                state.EstimatedDeparture = schedule[stop.Id].Departure?.AddMinutes(delay);
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Spec 12 Q7: yesterday's 8058, finished (6 minutes late at Colombo Fort), with three reports by the demo operator, so the operator app's
    /// "Previous journeys" is not empty in a demo. Idempotent: it exists once, whatever the number of API starts.
    /// </summary>
    public async Task SeedPreviousJourneyAsync(string? operatorSubject, CancellationToken cancellationToken = default)
    {
        var day = OperatingTime.Today(clock).AddDays(-1);
        var subject = string.IsNullOrWhiteSpace(operatorSubject) ? DemoIds.Nimal.ToString() : operatorSubject;
        var journeyId = DemoGuid(7, 8058, 0);
        // A journey for 8058 on that day may exist already (an earlier day's demo journey): then leave it, one per service and day.
        if (await db.Journeys.AnyAsync(j => j.Id == journeyId || (j.ServiceId == ReferenceData.Service8058 && j.OperatingDate == day), cancellationToken))
        {
            return;
        }

        var stops = await db.ServiceStops.Where(x => x.ServiceId == ReferenceData.Service8058).OrderBy(x => x.Sequence).ToListAsync(cancellationToken);
        var schedule = ScheduleInstants.For(day, stops.Select(x => new StopTimes(x.Id, x.Sequence, x.ScheduledArrival, x.ScheduledDeparture)))
            .ToDictionary(x => x.ServiceStopId);
        const int late = 6;
        var received = schedule[stops[^1].Id].Arrival!.Value.AddMinutes(late);

        db.Journeys.Add(new Journey
        {
            Id = journeyId, ServiceId = ReferenceData.Service8058, OperatingDate = day, Status = JourneyStatus.Completed, Version = 3, CurrentDelayMinutes = late,
        });
        foreach (var stop in stops)
        {
            var times = schedule[stop.Id];
            db.JourneyStopStates.Add(new JourneyStopState
            {
                JourneyId = journeyId, ServiceStopId = stop.Id,
                EstimatedArrival = times.Arrival?.AddMinutes(late), EstimatedDeparture = times.Departure?.AddMinutes(late),
                ActualArrival = times.Arrival?.AddMinutes(late), ActualDeparture = times.Departure?.AddMinutes(late),
                Confirmation = StopConfirmation.Reported,
            });
        }

        db.OperatorAssignments.Add(new OperatorAssignment
        {
            Id = DemoGuid(7, 8058, 1), JourneyId = journeyId, OperatorUserId = subject,
            ValidFrom = OperatingTime.StartOfDay(day), ValidTo = OperatingTime.StartOfNextDay(day), AssignedBy = DemoIds.AssignedBy,
        });

        TrainEvent Event(int n, EventType type, Guid? stopId, DateTimeOffset at, string? payload) => new()
        {
            Id = DemoGuid(7, 8058, 10 + n), JourneyId = journeyId, ClientEventId = DemoGuid(7, 8058, 20 + n), EventType = type, ServiceStopId = stopId,
            OccurredAt = at, ReceivedAt = at, Source = EventSource.Operator, CreatedBy = subject, Payload = payload, JourneyVersionAfter = n,
        };

        db.TrainEvents.Add(Event(1, EventType.Departed, stops[0].Id, schedule[stops[0].Id].Departure!.Value.AddMinutes(late), null));
        db.TrainEvents.Add(Event(2, EventType.DelayReported, null, schedule[stops[1].Id].Arrival!.Value.AddMinutes(-5), "{\"reason\":\"SignalIssue\",\"totalDelayMinutes\":6}"));
        db.TrainEvents.Add(Event(3, EventType.Arrived, stops[^1].Id, received, null));
        await db.SaveChangesAsync(cancellationToken);
    }
}
