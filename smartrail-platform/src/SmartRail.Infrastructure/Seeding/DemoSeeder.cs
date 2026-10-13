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

            foreach (var stop in stops)
            {
                db.JourneyStopStates.Add(new JourneyStopState
                {
                    JourneyId = journey.Id,
                    ServiceStopId = stop.Id,
                    EstimatedArrival = stop.ScheduledArrival is { } arrival ? OperatingTime.ToInstant(today, arrival) : null,
                    EstimatedDeparture = stop.ScheduledDeparture is { } departure ? OperatingTime.ToInstant(today, departure) : null,
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
}
