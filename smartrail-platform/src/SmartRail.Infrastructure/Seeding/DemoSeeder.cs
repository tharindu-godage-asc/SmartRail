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
}
