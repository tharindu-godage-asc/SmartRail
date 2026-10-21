using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SmartRail.Application.Features.Timetable;
using SmartRail.Domain.Journeys;
using SmartRail.Domain.Reference;
using SmartRail.Domain.Scheduling;

namespace SmartRail.Infrastructure.Persistence;

/// <summary>EF Core persistence of the timetable (spec 13): reads for the controller screens, the create transaction and the journey generator.</summary>
public sealed class TimetableRepository(SmartrailDbContext db) : ITimetableRepository
{
    private const long GeneratorLock = 913_001;

    public async Task<LineOptions?> GetLineAsync(string lineCode, CancellationToken cancellationToken)
    {
        var line = await db.Lines.AsNoTracking().SingleOrDefaultAsync(l => l.Code == lineCode, cancellationToken);
        if (line is null)
        {
            return null;
        }

        var rows = await (
            from ls in db.LineStations.AsNoTracking()
            where ls.LineId == line.Id
            join s in db.Stations.AsNoTracking() on ls.StationId equals s.Id
            orderby ls.Sequence
            select new { s.Id, s.Code, s.Name, ls.Sequence })
            .ToListAsync(cancellationToken);
        var ids = rows.Select(r => r.Id).ToList();
        var translations = await db.StationTranslations.AsNoTracking().Where(t => ids.Contains(t.StationId)).ToListAsync(cancellationToken);
        var platforms = await db.Platforms.AsNoTracking().Where(p => ids.Contains(p.StationId)).ToListAsync(cancellationToken);

        var stations = rows.Select(r =>
        {
            var names = new Dictionary<string, string> { ["en"] = r.Name };
            foreach (var t in translations.Where(t => t.StationId == r.Id))
            {
                names[t.LanguageCode] = t.Name;
            }

            return new OptionsStation(r.Code, r.Sequence, names, platforms.Where(p => p.StationId == r.Id).Select(p => p.Code).Order().ToList());
        }).ToList();
        return new LineOptions(line.Code, stations);
    }

    public async Task<IReadOnlyList<ExistingService>> GetServicesAsync(string lineCode, CancellationToken cancellationToken)
    {
        var lineId = await db.Lines.AsNoTracking().Where(l => l.Code == lineCode).Select(l => (Guid?)l.Id).SingleOrDefaultAsync(cancellationToken);
        if (lineId is null)
        {
            return [];
        }

        var services = await db.TrainServices.AsNoTracking().Where(s => s.LineId == lineId).ToListAsync(cancellationToken);
        var ids = services.Select(s => s.Id).ToList();
        var stops = await (
            from p in db.ServiceStops.AsNoTracking()
            where ids.Contains(p.ServiceId)
            join s in db.Stations.AsNoTracking() on p.StationId equals s.Id
            select new { p.ServiceId, s.Code, p.PlannedPlatformId, p.ScheduledArrival, p.ScheduledDeparture })
            .ToListAsync(cancellationToken);
        var platformCodes = await db.Platforms.AsNoTracking().ToDictionaryAsync(p => p.Id, p => p.Code, cancellationToken);

        return services.Select(s => new ExistingService(
            s.Id, s.TrainNumber, s.RunsOn, s.EffectiveFrom, s.EffectiveTo,
            stops.Where(p => p.ServiceId == s.Id)
                .Select(p => new ExistingStop(p.Code, p.PlannedPlatformId is { } id ? platformCodes.GetValueOrDefault(id) : null, p.ScheduledArrival, p.ScheduledDeparture))
                .ToList())).ToList();
    }

    public async Task<CreateOutcome> CreateAsync(IReadOnlyList<NewService> services, string actor, DateTimeOffset now, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        // One create per train number at a time, so two requests cannot both pass the overlap check (spec 13 Q6).
        var number = services[0].TrainNumber;
        await db.Database.ExecuteSqlInterpolatedAsync($"select pg_advisory_xact_lock(hashtext({number}))", cancellationToken);

        foreach (var service in services)
        {
            // A train number belongs to one service for good: any existing row with the number (any dates, any version, also an ended or
            // cancelled one) refuses the create. A change to a service is an amend, which makes a new version of the same number.
            var clash = await db.TrainServices.AsNoTracking()
                .Where(s => s.TrainNumber == service.TrainNumber)
                .Select(s => new { s.Id, s.TrainNumber })
                .FirstOrDefaultAsync(cancellationToken);
            if (clash is not null)
            {
                return new CreateOutcome(false, clash.TrainNumber, clash.Id);
            }
        }

        var lineId = await db.Lines.Where(l => l.Code == services[0].LineCode).Select(l => l.Id).SingleAsync(cancellationToken);
        var stationIds = await db.Stations.AsNoTracking().ToDictionaryAsync(s => s.Code, s => s.Id, cancellationToken);
        var platformIds = await db.Platforms.AsNoTracking().ToListAsync(cancellationToken);

        foreach (var service in services)
        {
            db.TrainServices.Add(new TrainService
            {
                Id = service.Id,
                TrainNumber = service.TrainNumber,
                LineId = lineId,
                ServiceType = Enum.Parse<ServiceType>(service.ServiceType),
                ServiceCategory = Enum.Parse<ServiceCategory>(service.Category),
                Direction = Enum.Parse<ServiceDirection>(service.Direction),
                RunsOn = service.RunsOn,
                Kind = service.Kind,
                Version = 1,
                EffectiveFrom = service.EffectiveFrom,
                EffectiveTo = service.EffectiveTo,
            });

            for (var i = 0; i < service.Stops.Count; i++)
            {
                var stop = service.Stops[i];
                var stationId = stationIds[stop.StationCode];
                db.ServiceStops.Add(new ServiceStop
                {
                    Id = Guid.CreateVersion7(now),
                    ServiceId = service.Id,
                    StationId = stationId,
                    Sequence = i + 1,
                    ScheduledArrival = stop.Arrival is null ? null : TimeOnly.ParseExact(stop.Arrival, "HH:mm"),
                    ScheduledDeparture = stop.Departure is null ? null : TimeOnly.ParseExact(stop.Departure, "HH:mm"),
                    PlannedPlatformId = stop.Platform is null
                        ? null
                        : platformIds.First(p => p.StationId == stationId && p.Code.Equals(stop.Platform, StringComparison.OrdinalIgnoreCase)).Id,
                });
            }

            db.ServiceChangeLogs.Add(new ServiceChangeLog
            {
                Id = Guid.CreateVersion7(now),
                ServiceId = service.Id,
                Action = "Created",
                Actor = actor,
                At = now,
                Details = JsonSerializer.Serialize(new
                {
                    trainNumber = service.TrainNumber,
                    kind = service.Kind.ToString(),
                    runsOn = service.RunsOn,
                    effectiveFrom = service.EffectiveFrom,
                    effectiveTo = service.EffectiveTo,
                    stops = service.Stops.Select(s => new { s.StationCode, s.Arrival, s.Departure, s.Platform }),
                }),
            });
        }

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new CreateOutcome(true, null, null);
    }

    public async Task<int> GenerateJourneysAsync(DateOnly from, DateOnly to, IReadOnlyCollection<Guid>? serviceIds, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        // Two generators at once (a create and the hourly run) take turns; with the unique index (service, date) nothing is created twice.
        await db.Database.ExecuteSqlInterpolatedAsync($"select pg_advisory_xact_lock({GeneratorLock})", cancellationToken);

        var services = await db.TrainServices.AsNoTracking()
            .Where(s => (serviceIds == null || serviceIds.Contains(s.Id)) && s.EffectiveFrom <= to && (s.EffectiveTo == null || s.EffectiveTo >= from))
            .ToListAsync(cancellationToken);
        if (services.Count == 0)
        {
            return 0;
        }

        var ids = services.Select(s => s.Id).ToList();
        var numbers = services.Select(s => s.TrainNumber).Distinct().ToList();
        var cancelled = (await db.ServiceCancellations.AsNoTracking().Where(c => c.RestoredAt == null && numbers.Contains(c.TrainNumber)).ToListAsync(cancellationToken))
            .ToLookup(c => (c.LineId, c.TrainNumber), c => new CancellationRange(c.FromDate, c.ToDate));
        var stops = await db.ServiceStops.AsNoTracking().Where(s => ids.Contains(s.ServiceId)).ToListAsync(cancellationToken);
        var existing = (await db.Journeys.AsNoTracking()
            .Where(j => ids.Contains(j.ServiceId) && j.OperatingDate >= from && j.OperatingDate <= to)
            .Select(j => new { j.ServiceId, j.OperatingDate })
            .ToListAsync(cancellationToken))
            .Select(j => (j.ServiceId, j.OperatingDate))
            .ToHashSet();

        var created = 0;
        for (var date = from; date <= to; date = date.AddDays(1))
        {
            foreach (var service in services)
            {
                // A cancelled date gets no journey, and one that is already Cancelled keeps its row (unique service and date).
                if (ServiceRuns.On(service.RunsOn, service.EffectiveFrom, service.EffectiveTo, date, cancelled[(service.LineId, service.TrainNumber)]) != true
                    || existing.Contains((service.Id, date)))
                {
                    continue;
                }

                var journeyStops = stops.Where(s => s.ServiceId == service.Id).OrderBy(s => s.Sequence).ToList();
                var schedule = ScheduleInstants.For(date, journeyStops.Select(s => new StopTimes(s.Id, s.Sequence, s.ScheduledArrival, s.ScheduledDeparture)))
                    .ToDictionary(s => s.ServiceStopId);
                var journey = new Journey
                {
                    Id = Guid.CreateVersion7(), ServiceId = service.Id, OperatingDate = date, Status = JourneyStatus.Unassigned, Version = 0,
                };
                db.Journeys.Add(journey);
                foreach (var stop in journeyStops)
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

                created++;
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return created;
    }

    public async Task<IReadOnlyList<ServiceRow>> ListAsync(string lineCode, string tab, DateOnly today, CancellationToken cancellationToken)
    {
        var lineId = await db.Lines.AsNoTracking().Where(l => l.Code == lineCode).Select(l => l.Id).SingleAsync(cancellationToken);
        var extra = tab == "extra";
        var services = await db.TrainServices.AsNoTracking()
            .Where(s => s.LineId == lineId
                && (extra ? s.Kind == ServiceKind.Extra && s.EffectiveFrom >= today : s.Kind == ServiceKind.Weekly && (s.EffectiveTo == null || s.EffectiveTo >= today)))
            .OrderBy(s => s.TrainNumber).ThenBy(s => s.EffectiveFrom)
            .ToListAsync(cancellationToken);
        if (!extra)
        {
            // One row per train number: its newest version (the one in force, or the one that starts later, spec 14 section 4.3).
            services = services.GroupBy(s => s.TrainNumber).Select(g => g.OrderByDescending(s => s.Version).First()).OrderBy(s => s.TrainNumber).ToList();

            // A service cancelled from today onward is no longer in the weekly schedule: it is under Cancelled until it is restored.
            var numbersNow = services.Select(s => s.TrainNumber).ToList();
            var cancelledOnward = (await db.ServiceCancellations.AsNoTracking()
                    .Where(c => c.RestoredAt == null && c.LineId == lineId && numbersNow.Contains(c.TrainNumber) && c.ToDate == null && c.FromDate <= today)
                    .Select(c => c.TrainNumber).ToListAsync(cancellationToken)).ToHashSet();
            services = services.Where(s => !cancelledOnward.Contains(s.TrainNumber)).ToList();
        }

        if (services.Count == 0)
        {
            return [];
        }

        var ids = services.Select(s => s.Id).ToList();
        var stops = await (
            from p in db.ServiceStops.AsNoTracking()
            where ids.Contains(p.ServiceId)
            join st in db.Stations.AsNoTracking() on p.StationId equals st.Id
            select new { p.ServiceId, p.Sequence, st.Name, p.ScheduledDeparture, p.ScheduledArrival })
            .ToListAsync(cancellationToken);
        var lineStationCount = await db.LineStations.AsNoTracking().CountAsync(l => l.LineId == lineId, cancellationToken);
        var shownNumbers = services.Select(s => s.TrainNumber).Distinct().ToList();
        var cancelledRanges = (await db.ServiceCancellations.AsNoTracking()
                .Where(c => c.RestoredAt == null && c.LineId == lineId && shownNumbers.Contains(c.TrainNumber) && (c.ToDate == null || c.ToDate >= today))
                .ToListAsync(cancellationToken))
            .ToLookup(c => c.TrainNumber, c => new DateRange(c.FromDate, c.ToDate));

        return services.Select(s =>
        {
            var own = stops.Where(p => p.ServiceId == s.Id).OrderBy(p => p.Sequence).ToList();
            var first = own[0];
            var days = RunDays.Parse(s.RunsOn);
            return new ServiceRow(
                s.Id, s.TrainNumber, s.ServiceCategory.ToString(), s.ServiceType.ToString(), first.Name, own[^1].Name,
                extra || days is null ? null : DayCodes(days), extra ? s.EffectiveFrom : null,
                (first.ScheduledDeparture ?? first.ScheduledArrival)?.ToString("HH:mm") ?? "",
                own.Count, own.Count == lineStationCount ? $"Every stop ({own.Count})" : $"Selected stops ({own.Count} of {lineStationCount})",
                s.Version, s.Version > 1, s.EffectiveFrom, cancelledRanges[s.TrainNumber].ToList());
        }).ToList();
    }

    public async Task<ServiceDetail?> GetAsync(Guid serviceId, CancellationToken cancellationToken)
    {
        var service = await db.TrainServices.AsNoTracking().SingleOrDefaultAsync(s => s.Id == serviceId, cancellationToken);
        if (service is null)
        {
            return null;
        }

        var stops = await (
            from p in db.ServiceStops.AsNoTracking()
            where p.ServiceId == serviceId
            join st in db.Stations.AsNoTracking() on p.StationId equals st.Id
            orderby p.Sequence
            select new { p.Sequence, st.Id, st.Code, st.Name, p.PlannedPlatformId, p.ScheduledArrival, p.ScheduledDeparture })
            .ToListAsync(cancellationToken);
        var stationIds = stops.Select(s => s.Id).ToList();
        var translations = await db.StationTranslations.AsNoTracking().Where(t => stationIds.Contains(t.StationId)).ToListAsync(cancellationToken);
        var platformCodes = await db.Platforms.AsNoTracking().ToDictionaryAsync(p => p.Id, p => p.Code, cancellationToken);

        var views = stops.Select(s =>
        {
            var names = new Dictionary<string, string> { ["en"] = s.Name };
            foreach (var t in translations.Where(t => t.StationId == s.Id))
            {
                names[t.LanguageCode] = t.Name;
            }

            return new ServiceStopView(
                s.Sequence, s.Code, names, s.ScheduledArrival?.ToString("HH:mm"), s.ScheduledDeparture?.ToString("HH:mm"),
                s.PlannedPlatformId is { } id ? platformCodes.GetValueOrDefault(id) : null, s.Sequence == 1, s.Sequence == stops.Count);
        }).ToList();

        var versions = await (
            from v in db.TrainServices.AsNoTracking()
            where v.TrainNumber == service.TrainNumber && v.Kind == service.Kind && v.LineId == service.LineId
            orderby v.Version, v.EffectiveFrom
            select new { v.Id, v.Version, v.EffectiveFrom, v.EffectiveTo })
            .ToListAsync(cancellationToken);
        var versionIds = versions.Select(v => v.Id).ToList();
        var logs = await db.ServiceChangeLogs.AsNoTracking()
            .Where(l => versionIds.Contains(l.ServiceId) && (l.Action == "Created" || l.Action == "Amended"))
            .ToListAsync(cancellationToken);

        var cancellations = await ListCancellationsForAsync(service.LineId, service.TrainNumber, null, cancellationToken);
        var runDays = RunDays.Parse(service.RunsOn);
        return new ServiceDetail(
            service.Id, service.TrainNumber, service.Kind.ToString(), service.ServiceCategory.ToString(), service.ServiceType.ToString(),
            service.Direction.ToString(), service.Kind == ServiceKind.Extra || runDays is null ? null : DayCodes(runDays),
            service.EffectiveFrom, service.EffectiveTo, service.Version, views,
            versions.Select(v =>
            {
                var log = logs.FirstOrDefault(l => l.ServiceId == v.Id);
                return new ServiceVersionView(v.Version, v.Id, v.EffectiveFrom, v.EffectiveTo, log?.At, log?.Actor, SummaryOf(log));
            }).ToList(),
            cancellations);
    }

    public async Task<CancelOutcome> CancelAsync(
        Guid serviceId, DateOnly from, DateOnly? to, string reason, string actor, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var service = await db.TrainServices.AsNoTracking().SingleOrDefaultAsync(s => s.Id == serviceId, cancellationToken);
        if (service is null)
        {
            return new CancelOutcome(CancelResultKind.NotFound, null, 0, null);
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        // The same lock as create: one change per train number at a time.
        await db.Database.ExecuteSqlInterpolatedAsync($"select pg_advisory_xact_lock(hashtext({service.TrainNumber}))", cancellationToken);

        var repeat = await db.ServiceCancellations.AsNoTracking()
            .Where(c => c.RestoredAt == null && c.LineId == service.LineId && c.TrainNumber == service.TrainNumber && c.FromDate == from && c.ToDate == to)
            .Select(c => (Guid?)c.Id).FirstOrDefaultAsync(cancellationToken);
        if (repeat is not null)
        {
            return new CancelOutcome(CancelResultKind.Repeat, repeat, 0, null);
        }

        var services = await db.TrainServices.AsNoTracking()
            .Where(s => s.LineId == service.LineId && s.TrainNumber == service.TrainNumber).ToListAsync(cancellationToken);
        var existingRanges = (await db.ServiceCancellations.AsNoTracking()
                .Where(c => c.RestoredAt == null && c.LineId == service.LineId && c.TrainNumber == service.TrainNumber).ToListAsync(cancellationToken))
            .Select(c => new CancellationRange(c.FromDate, c.ToDate)).ToList();

        // Dates on which no version runs (a Sunday for a weekday train) are skipped; nothing in the range at all is an error.
        var last = to ?? from.AddDays(366);
        var runsSomewhere = false;
        for (var date = from; date <= last && !runsSomewhere; date = date.AddDays(1))
        {
            runsSomewhere = services.Any(s => ServiceRuns.On(s.RunsOn, s.EffectiveFrom, s.EffectiveTo, date, existingRanges) == true);
        }

        if (!runsSomewhere)
        {
            return new CancelOutcome(CancelResultKind.NothingToCancel, null, 0, null);
        }

        var serviceIds = services.Select(s => s.Id).ToList();
        var journeys = await db.Journeys
            .Where(j => serviceIds.Contains(j.ServiceId) && j.OperatingDate >= from && (to == null || j.OperatingDate <= to))
            .ToListAsync(cancellationToken);
        var journeyIds = journeys.Select(j => j.Id).ToList();
        var withEvents = (await db.TrainEvents.AsNoTracking().Where(e => journeyIds.Contains(e.JourneyId)).Select(e => e.JourneyId).Distinct()
            .ToListAsync(cancellationToken)).ToHashSet();

        // All or none: one journey with a report, or one that is running or finished, refuses the whole range (FR-54 is the tool for those).
        var blocked = journeys.Where(j => withEvents.Contains(j.Id) || j.Status is JourneyStatus.Running or JourneyStatus.Completed)
            .OrderBy(j => j.OperatingDate).FirstOrDefault();
        if (blocked is not null)
        {
            return new CancelOutcome(CancelResultKind.HasEvents, null, 0, blocked.OperatingDate);
        }

        var cancelled = 0;
        foreach (var journey in journeys.Where(j => j.Status != JourneyStatus.Cancelled))
        {
            journey.Status = JourneyStatus.Cancelled;
            journey.Version += 1;
            cancelled++;
        }

        var cancellation = new ServiceCancellation
        {
            Id = Guid.CreateVersion7(now), LineId = service.LineId, TrainNumber = service.TrainNumber, ServiceId = serviceId,
            FromDate = from, ToDate = to, Reason = reason, CreatedBy = actor, CreatedAt = now,
        };
        db.ServiceCancellations.Add(cancellation);
        db.ServiceChangeLogs.Add(new ServiceChangeLog
        {
            Id = Guid.CreateVersion7(now), ServiceId = serviceId, Action = "Cancelled", Actor = actor, At = now,
            Details = JsonSerializer.Serialize(new { from, to, reason, journeys = cancelled }),
        });

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new CancelOutcome(CancelResultKind.Done, cancellation.Id, cancelled, null);
    }

    public async Task<RestoreOutcome> RestoreAsync(Guid cancellationId, string actor, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var cancellation = await db.ServiceCancellations.AsNoTracking().SingleOrDefaultAsync(c => c.Id == cancellationId, cancellationToken);
        if (cancellation is null)
        {
            return new RestoreOutcome(RestoreResultKind.NotFound, 0);
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        // The same lock as create and cancel: one change per train number at a time.
        await db.Database.ExecuteSqlInterpolatedAsync($"select pg_advisory_xact_lock(hashtext({cancellation.TrainNumber}))", cancellationToken);

        // Read again under the lock: another controller may have restored it a moment ago.
        var current = await db.ServiceCancellations.SingleAsync(c => c.Id == cancellationId, cancellationToken);
        if (current.RestoredAt is not null)
        {
            return new RestoreOutcome(RestoreResultKind.AlreadyRestored, 0);
        }

        current.RestoredAt = now;
        current.RestoredBy = actor;

        // The journeys of the range that this cancellation had cancelled go back, unless another active cancellation still covers their date,
        // or the service does not run that day (an amendment removed it). A journey with an operator assigned returns as Scheduled.
        var remaining = (await db.ServiceCancellations.AsNoTracking()
                .Where(c => c.RestoredAt == null && c.Id != cancellationId && c.LineId == current.LineId && c.TrainNumber == current.TrainNumber)
                .ToListAsync(cancellationToken))
            .Select(c => new CancellationRange(c.FromDate, c.ToDate)).ToList();
        var services = await db.TrainServices.AsNoTracking()
            .Where(s => s.LineId == current.LineId && s.TrainNumber == current.TrainNumber).ToDictionaryAsync(s => s.Id, cancellationToken);
        var journeys = await db.Journeys
            .Where(j => services.Keys.Contains(j.ServiceId) && j.Status == JourneyStatus.Cancelled && j.OperatingDate >= current.FromDate && (current.ToDate == null || j.OperatingDate <= current.ToDate))
            .ToListAsync(cancellationToken);
        var assigned = (await db.OperatorAssignments.AsNoTracking().Where(a => journeys.Select(j => j.Id).Contains(a.JourneyId)).Select(a => a.JourneyId).Distinct()
            .ToListAsync(cancellationToken)).ToHashSet();

        var restored = 0;
        foreach (var journey in journeys)
        {
            var service = services[journey.ServiceId];
            if (ServiceRuns.On(service.RunsOn, service.EffectiveFrom, service.EffectiveTo, journey.OperatingDate, remaining) != true)
            {
                continue;
            }

            journey.Status = assigned.Contains(journey.Id) ? JourneyStatus.Scheduled : JourneyStatus.Unassigned;
            journey.Version += 1;
            restored++;
        }

        db.ServiceChangeLogs.Add(new ServiceChangeLog
        {
            Id = Guid.CreateVersion7(now), ServiceId = current.ServiceId, Action = "Restored", Actor = actor, At = now,
            Details = JsonSerializer.Serialize(new { cancellationId, from = current.FromDate, to = current.ToDate, journeys = restored }),
        });

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new RestoreOutcome(RestoreResultKind.Done, restored);
    }

    public async Task<IReadOnlyList<CancellationView>> ListCancellationsAsync(string lineCode, DateOnly today, CancellationToken cancellationToken)
    {
        var lineId = await db.Lines.AsNoTracking().Where(l => l.Code == lineCode).Select(l => l.Id).SingleAsync(cancellationToken);
        return await ListCancellationsForAsync(lineId, null, today, cancellationToken);
    }

    private async Task<IReadOnlyList<CancellationView>> ListCancellationsForAsync(Guid lineId, string? trainNumber, DateOnly? today, CancellationToken cancellationToken)
    {
        var rows = await db.ServiceCancellations.AsNoTracking()
            .Where(c => c.RestoredAt == null && c.LineId == lineId && (trainNumber == null || c.TrainNumber == trainNumber) && (today == null || c.ToDate == null || c.ToDate >= today))
            .OrderBy(c => c.FromDate).ThenBy(c => c.CreatedAt)
            .ToListAsync(cancellationToken);
        if (rows.Count == 0)
        {
            return [];
        }

        var ids = rows.Select(r => r.ServiceId).Distinct().ToList();
        var stops = await (
            from p in db.ServiceStops.AsNoTracking()
            where ids.Contains(p.ServiceId)
            join st in db.Stations.AsNoTracking() on p.StationId equals st.Id
            select new { p.ServiceId, p.Sequence, st.Name })
            .ToListAsync(cancellationToken);
        return rows.Select(r =>
        {
            var own = stops.Where(p => p.ServiceId == r.ServiceId).OrderBy(p => p.Sequence).ToList();
            return new CancellationView(
                r.Id, r.ServiceId, r.TrainNumber, own.FirstOrDefault()?.Name ?? "", own.LastOrDefault()?.Name ?? "",
                r.FromDate, r.ToDate, r.Reason, r.CreatedBy, r.CreatedAt);
        }).ToList();
    }

    /// <summary>The readable list of changes of an amendment ("Gampaha 08:42 (was 08:38)"), from the audit row; null for a first version.</summary>
    private static string? SummaryOf(ServiceChangeLog? log)
    {
        if (log is not { Action: "Amended", Details: { } details })
        {
            return null;
        }

        using var document = JsonDocument.Parse(details);
        return document.RootElement.TryGetProperty("changes", out var changes) && changes.ValueKind == JsonValueKind.Array
            ? string.Join("; ", changes.EnumerateArray().Select(c => c.GetString()))
            : null;
    }

    public async Task<(Guid ServiceId, DateOnly Date)?> GetJourneyServiceAsync(Guid journeyId, CancellationToken cancellationToken)
    {
        var journey = await db.Journeys.AsNoTracking().SingleOrDefaultAsync(j => j.Id == journeyId, cancellationToken);
        return journey is null ? null : (journey.ServiceId, journey.OperatingDate);
    }

    public async Task<LatestVersion?> GetLatestVersionAsync(Guid serviceId, CancellationToken cancellationToken)
    {
        var service = await db.TrainServices.AsNoTracking().SingleOrDefaultAsync(s => s.Id == serviceId, cancellationToken);
        if (service is null)
        {
            return null;
        }

        var latest = await db.TrainServices.AsNoTracking()
            .Where(s => s.LineId == service.LineId && s.TrainNumber == service.TrainNumber && s.Kind == service.Kind)
            .OrderByDescending(s => s.Version).ThenByDescending(s => s.EffectiveFrom)
            .FirstAsync(cancellationToken);
        var lineCode = await db.Lines.AsNoTracking().Where(l => l.Id == latest.LineId).Select(l => l.Code).SingleAsync(cancellationToken);
        return new LatestVersion(latest.Id, latest.TrainNumber, lineCode, latest.Kind, latest.Version, latest.EffectiveFrom, latest.EffectiveTo);
    }

    public async Task<AmendOutcome> AmendAsync(NewService next, Guid serviceId, string actor, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var known = await db.TrainServices.AsNoTracking().SingleOrDefaultAsync(s => s.Id == serviceId, cancellationToken);
        if (known is null)
        {
            return Outcome(AmendResultKind.NotFound);
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await db.Database.ExecuteSqlInterpolatedAsync($"select pg_advisory_xact_lock(hashtext({next.TrainNumber}))", cancellationToken);

        // Under the lock, so two amendments at once take turns: the second one builds on the version the first one made.
        var all = await db.TrainServices
            .Where(s => s.LineId == known.LineId && s.TrainNumber == known.TrainNumber)
            .ToListAsync(cancellationToken);
        var previous = all.Where(s => s.Kind == ServiceKind.Weekly).OrderByDescending(s => s.Version).ThenByDescending(s => s.EffectiveFrom).First();
        if (next.EffectiveFrom <= previous.EffectiveFrom)
        {
            return Outcome(AmendResultKind.NotAfterCurrent, currentFrom: previous.EffectiveFrom);
        }

        var allIds = all.Select(s => s.Id).ToList();
        var journeys = await db.Journeys
            .Where(j => allIds.Contains(j.ServiceId) && j.OperatingDate >= next.EffectiveFrom)
            .ToListAsync(cancellationToken);
        var journeyIds = journeys.Select(j => j.Id).ToList();
        var withEvents = (await db.TrainEvents.AsNoTracking().Where(e => journeyIds.Contains(e.JourneyId)).Select(e => e.JourneyId).Distinct()
            .ToListAsync(cancellationToken)).ToHashSet();
        var blocked = journeys.Where(j => withEvents.Contains(j.Id) || j.Status is JourneyStatus.Running or JourneyStatus.Completed)
            .OrderBy(j => j.OperatingDate).FirstOrDefault();
        if (blocked is not null)
        {
            return Outcome(AmendResultKind.HasEvents, eventDate: blocked.OperatingDate);
        }

        var stationIds = await db.Stations.AsNoTracking().ToDictionaryAsync(s => s.Code, s => s.Id, cancellationToken);
        var stationNames = await db.Stations.AsNoTracking().ToDictionaryAsync(s => s.Code, s => s.Name, cancellationToken);
        var platforms = await db.Platforms.AsNoTracking().ToListAsync(cancellationToken);
        var platformCodes = platforms.ToDictionary(p => p.Id, p => p.Code);

        var oldStops = await (
            from p in db.ServiceStops.AsNoTracking()
            where p.ServiceId == previous.Id
            join st in db.Stations.AsNoTracking() on p.StationId equals st.Id
            orderby p.Sequence
            select new { st.Code, p.PlannedPlatformId, p.ScheduledArrival, p.ScheduledDeparture })
            .ToListAsync(cancellationToken);
        var before = oldStops.Select(o => new ExistingStop(o.Code, o.PlannedPlatformId is { } id ? platformCodes.GetValueOrDefault(id) : null, o.ScheduledArrival, o.ScheduledDeparture)).ToList();
        var after = next.Stops.Select(x => new ExistingStop(
            x.StationCode, x.Platform, x.Arrival is null ? null : TimeOnly.ParseExact(x.Arrival, "HH:mm"), x.Departure is null ? null : TimeOnly.ParseExact(x.Departure, "HH:mm"))).ToList();
        var changes = AmendmentDiff.Describe(before, after, stationNames, previous.RunsOn, next.RunsOn);

        var version = previous.Version + 1;
        var newRow = new TrainService
        {
            Id = next.Id,
            TrainNumber = next.TrainNumber,
            LineId = previous.LineId,
            ServiceType = Enum.Parse<ServiceType>(next.ServiceType),
            ServiceCategory = Enum.Parse<ServiceCategory>(next.Category),
            Direction = Enum.Parse<ServiceDirection>(next.Direction),
            RunsOn = next.RunsOn,
            Kind = ServiceKind.Weekly,
            Version = version,
            EffectiveFrom = next.EffectiveFrom,
            EffectiveTo = previous.EffectiveTo,
            SupersededServiceId = previous.Id,
        };
        db.TrainServices.Add(newRow);
        previous.EffectiveTo = next.EffectiveFrom.AddDays(-1);

        var newStops = new List<ServiceStop>();
        for (var i = 0; i < next.Stops.Count; i++)
        {
            var stop = next.Stops[i];
            var stationId = stationIds[stop.StationCode];
            var row = new ServiceStop
            {
                Id = Guid.CreateVersion7(now),
                ServiceId = next.Id,
                StationId = stationId,
                Sequence = i + 1,
                ScheduledArrival = after[i].Arrival,
                ScheduledDeparture = after[i].Departure,
                PlannedPlatformId = stop.Platform is null
                    ? null
                    : platforms.First(p => p.StationId == stationId && p.Code.Equals(stop.Platform, StringComparison.OrdinalIgnoreCase)).Id,
            };
            newStops.Add(row);
            db.ServiceStops.Add(row);
        }

        // Re-point the journeys that have not started: same id, same assignment, same date; new service and fresh Pending stop states. A day the
        // new version no longer runs on is cancelled ("Removed by amendment"). A journey that is already Cancelled is left alone.
        var cancellations = (await db.ServiceCancellations.AsNoTracking()
                .Where(c => c.RestoredAt == null && c.LineId == previous.LineId && c.TrainNumber == previous.TrainNumber).ToListAsync(cancellationToken))
            .Select(c => new CancellationRange(c.FromDate, c.ToDate)).ToList();
        var schedules = new Dictionary<DateOnly, IReadOnlyDictionary<Guid, StopSchedule>>();
        var repointed = 0;
        var removed = new List<DateOnly>();
        foreach (var journey in journeys.Where(j => j.Status != JourneyStatus.Cancelled).OrderBy(j => j.OperatingDate))
        {
            if (ServiceRuns.On(next.RunsOn, next.EffectiveFrom, next.EffectiveTo, journey.OperatingDate, cancellations) != true)
            {
                journey.Status = JourneyStatus.Cancelled;
                journey.Version += 1;
                removed.Add(journey.OperatingDate);
                continue;
            }

            var date = journey.OperatingDate;
            if (!schedules.TryGetValue(date, out var schedule))
            {
                schedule = ScheduleInstants.For(date, newStops.Select(x => new StopTimes(x.Id, x.Sequence, x.ScheduledArrival, x.ScheduledDeparture))).ToDictionary(x => x.ServiceStopId);
                schedules[date] = schedule;
            }

            db.JourneyStopStates.RemoveRange(await db.JourneyStopStates.Where(x => x.JourneyId == journey.Id).ToListAsync(cancellationToken));
            journey.ServiceId = next.Id;
            journey.Version += 1;
            journey.CurrentDelayMinutes = 0;
            foreach (var stop in newStops)
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

            repointed++;
        }

        db.ServiceChangeLogs.Add(new ServiceChangeLog
        {
            Id = Guid.CreateVersion7(now),
            ServiceId = next.Id,
            Action = "Amended",
            Actor = actor,
            At = now,
            Details = JsonSerializer.Serialize(new
            {
                fromVersion = previous.Version,
                toVersion = version,
                effectiveFrom = next.EffectiveFrom,
                changes,
                removedDates = removed,
                journeysUpdated = repointed,
            }),
        });

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new AmendOutcome(AmendResultKind.Done, next.Id, version, repointed, removed.Count, null, null);

        static AmendOutcome Outcome(AmendResultKind kind, DateOnly? eventDate = null, DateOnly? currentFrom = null) =>
            new(kind, null, 0, 0, 0, eventDate, currentFrom);
    }

    private static IReadOnlyList<string> DayCodes(IReadOnlySet<DayOfWeek> days)
    {
        string[] codes = ["Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun"];
        DayOfWeek[] week = [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday];
        return week.Where(days.Contains).Select(d => codes[Array.IndexOf(week, d)]).ToList();
    }
}
