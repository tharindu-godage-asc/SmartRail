using System.Diagnostics.Metrics;
using SmartRail.Domain;
using SmartRail.Domain.Scheduling;

namespace SmartRail.Application.Features.Timetable;

/// <summary>Scheduling:* settings (spec 13). The generator is off unless asked for, so the cluster and the tests keep the data they have.</summary>
public sealed class SchedulingSettings
{
    public const string Section = "Scheduling";

    /// <summary>Whether the hosted generator runs (at start-up and then every <see cref="GenerateEveryMinutes"/>). A create always generates its own journeys.</summary>
    public bool GenerateJourneys { get; set; }

    public int HorizonDays { get; set; } = 7;
    public int GenerateEveryMinutes { get; set; } = 60;
    public int PlatformWarningMinutes { get; set; } = 10;
}

public sealed record OptionsStation(string Code, int Sequence, IReadOnlyDictionary<string, string> Names, IReadOnlyList<string> Platforms);

public sealed record LineOptions(string LineCode, IReadOnlyList<OptionsStation> Stations);

public sealed record ServiceRow(
    Guid ServiceId,
    string TrainNumber,
    string Category,
    string ServiceType,
    string FromName,
    string ToName,
    IReadOnlyList<string>? RunsOn,
    DateOnly? Date,
    string FirstDeparture,
    int StopCount,
    string StopsLabel,
    int Version,
    bool Amended,
    DateOnly EffectiveFrom,
    IReadOnlyList<DateRange> CancelledRanges);

public sealed record DateRange(DateOnly From, DateOnly? To);

/// <summary>One cancellation as the controller sees it (the reason is for controllers only, spec 14 Q8).</summary>
public sealed record CancellationView(
    Guid CancellationId, Guid ServiceId, string TrainNumber, string FromName, string ToName, DateOnly From, DateOnly? To, string Reason, string CancelledBy, DateTimeOffset CancelledAt);

public sealed record ServiceStopView(
    int Sequence, string StationCode, IReadOnlyDictionary<string, string> Names, string? Arrival, string? Departure, string? Platform, bool IsFirst, bool IsLast);

public sealed record ServiceVersionView(
    int Version, Guid ServiceId, DateOnly EffectiveFrom, DateOnly? EffectiveTo, DateTimeOffset? CreatedAt, string? CreatedBy, string? Summary = null);

public sealed record ServiceDetail(
    Guid ServiceId,
    string TrainNumber,
    string Kind,
    string Category,
    string ServiceType,
    string Direction,
    IReadOnlyList<string>? RunsOn,
    DateOnly EffectiveFrom,
    DateOnly? EffectiveTo,
    int Version,
    IReadOnlyList<ServiceStopView> Stops,
    IReadOnlyList<ServiceVersionView> Versions,
    IReadOnlyList<CancellationView> Cancellations);

/// <summary>One service to insert: a weekly service, or one date of an extra train.</summary>
public sealed record NewService(
    Guid Id,
    string TrainNumber,
    string LineCode,
    string Category,
    string ServiceType,
    string Direction,
    ServiceKind Kind,
    string RunsOn,
    DateOnly EffectiveFrom,
    DateOnly? EffectiveTo,
    IReadOnlyList<StopDraft> Stops);

public sealed record CreateOutcome(bool Created, string? ClashTrainNumber, Guid? ClashServiceId);

/// <summary>The newest version of a service, which an amendment builds on.</summary>
public sealed record LatestVersion(Guid ServiceId, string TrainNumber, string LineCode, ServiceKind Kind, int Version, DateOnly EffectiveFrom, DateOnly? EffectiveTo);

public enum AmendResultKind { Done, NotFound, NotAfterCurrent, HasEvents }

public sealed record AmendOutcome(
    AmendResultKind Kind, Guid? NewServiceId, int Version, int Repointed, int CancelledByAmendment, DateOnly? EventDate, DateOnly? CurrentFrom);

public enum CancelResultKind { Done, Repeat, NotFound, NothingToCancel, HasEvents }

public enum RestoreResultKind { Done, NotFound, AlreadyRestored }

public sealed record RestoreOutcome(RestoreResultKind Kind, int RestoredJourneys);

public sealed record CancelOutcome(CancelResultKind Kind, Guid? CancellationId, int CancelledJourneys, DateOnly? EventDate);

public interface ITimetableRepository
{
    /// <summary>The stations of a line in line order with their names and platforms; null for an unknown line.</summary>
    Task<LineOptions?> GetLineAsync(string lineCode, CancellationToken cancellationToken);

    /// <summary>Every service of the line with its days, dates and stops, for the overlap check and the platform advice.</summary>
    Task<IReadOnlyList<ExistingService>> GetServicesAsync(string lineCode, CancellationToken cancellationToken);

    /// <summary>
    /// Inserts the services, their stops and the audit entries in one transaction, after taking an advisory lock on the train number and
    /// checking that no other service of that number overlaps in time. Clash means nothing was written.
    /// </summary>
    Task<CreateOutcome> CreateAsync(IReadOnlyList<NewService> services, string actor, DateTimeOffset now, CancellationToken cancellationToken);

    /// <summary>
    /// Creates the journeys that are missing for the dates, for the given services or for all of them: status Unassigned, one Pending stop
    /// state per stop. Idempotent and safe to run twice at once. Returns how many journeys it created.
    /// </summary>
    Task<int> GenerateJourneysAsync(DateOnly from, DateOnly to, IReadOnlyCollection<Guid>? serviceIds, CancellationToken cancellationToken);

    /// <summary>
    /// Cancels a train number on its line for a range of dates, all or none, in one transaction under the train-number lock: the journeys of the
    /// range without events become Cancelled, a cancellation row and an audit row are written. HasEvents names the first date with a report.
    /// </summary>
    Task<CancelOutcome> CancelAsync(Guid serviceId, DateOnly from, DateOnly? to, string reason, string actor, DateTimeOffset now, CancellationToken cancellationToken);

    /// <summary>The latest version of the service's train number on its line, or null when the service is unknown.</summary>
    Task<LatestVersion?> GetLatestVersionAsync(Guid serviceId, CancellationToken cancellationToken);

    /// <summary>
    /// Amends in one transaction under the train-number lock: inserts the next version of the latest one from its effective date, ends the
    /// old version the day before, re-points the not-yet-started journeys (rebuilding their stop states), cancels those of a removed day, and
    /// writes the audit row. HasEvents (a journey on or after the date has reports) changes nothing.
    /// </summary>
    Task<AmendOutcome> AmendAsync(NewService next, Guid serviceId, string actor, DateTimeOffset now, CancellationToken cancellationToken);

    /// <summary>
    /// Restores a cancelled service: the cancellation stays as history with who restored it and when, no longer applies, and the journeys it had
    /// cancelled go back to Unassigned (or Scheduled with an operator assigned). Under the train-number lock, in one transaction.
    /// </summary>
    Task<RestoreOutcome> RestoreAsync(Guid cancellationId, string actor, DateTimeOffset now, CancellationToken cancellationToken);

    /// <summary>Cancellations that cover a date from <paramref name="today"/> on and are not restored, for the Cancelled tab.</summary>
    Task<IReadOnlyList<CancellationView>> ListCancellationsAsync(string lineCode, DateOnly today, CancellationToken cancellationToken);

    Task<IReadOnlyList<ServiceRow>> ListAsync(string lineCode, string tab, DateOnly today, CancellationToken cancellationToken);

    Task<ServiceDetail?> GetAsync(Guid serviceId, CancellationToken cancellationToken);
}

public static class TimetableMetrics
{
    public static readonly Meter Meter = new("SmartRail.Timetable");
    public static readonly Counter<long> Changes = Meter.CreateCounter<long>("smartrail.timetable.changes", description: "Timetable changes by action and result");
    public static readonly Counter<long> Generated = Meter.CreateCounter<long>("smartrail.journeys.generated", description: "Journeys created by the generator");

    public static void Change(string action, string result) =>
        Changes.Add(1, new KeyValuePair<string, object?>("action", action), new KeyValuePair<string, object?>("result", result));
}

public sealed class GetOptionsHandler(ITimetableRepository repository)
{
    public Task<LineOptions?> HandleAsync(string lineCode, CancellationToken cancellationToken) =>
        repository.GetLineAsync(string.IsNullOrWhiteSpace(lineCode) ? "MAIN" : lineCode, cancellationToken);
}

public enum ListError { None, LineNotFound, InvalidTab }

public sealed record ListResult(ListError Error, IReadOnlyList<ServiceRow> Rows, IReadOnlyList<CancellationView>? Cancelled = null);

public sealed class ListServicesHandler(ITimetableRepository repository, TimeProvider clock)
{
    public static readonly string[] Tabs = ["weekly", "extra", "cancelled"];

    public async Task<ListResult> HandleAsync(string? lineCode, string? tab, CancellationToken cancellationToken)
    {
        var line = string.IsNullOrWhiteSpace(lineCode) ? "MAIN" : lineCode.Trim().ToUpperInvariant();
        var which = (tab ?? "weekly").Trim().ToLowerInvariant();
        if (!Tabs.Contains(which))
        {
            return new ListResult(ListError.InvalidTab, []);
        }

        if (await repository.GetLineAsync(line, cancellationToken) is null)
        {
            return new ListResult(ListError.LineNotFound, []);
        }

        var today = OperatingTime.Today(clock);
        return which == "cancelled"
            ? new ListResult(ListError.None, [], await repository.ListCancellationsAsync(line, today, cancellationToken))
            : new ListResult(ListError.None, await repository.ListAsync(line, which, today, cancellationToken));
    }
}

public sealed class GetServiceHandler(ITimetableRepository repository)
{
    public Task<ServiceDetail?> HandleAsync(Guid serviceId, CancellationToken cancellationToken) => repository.GetAsync(serviceId, cancellationToken);
}

public sealed class ValidateServiceHandler(ITimetableRepository repository, TimeProvider clock, SchedulingSettings settings)
{
    /// <summary>Null when the line is unknown.</summary>
    public async Task<ValidationResult?> HandleAsync(ServiceDraft draft, CancellationToken cancellationToken)
    {
        var line = await repository.GetLineAsync(draft.LineCode ?? "MAIN", cancellationToken);
        if (line is null)
        {
            return null;
        }

        var others = await repository.GetServicesAsync(line.LineCode, cancellationToken);
        var now = TimeZoneInfo.ConvertTime(clock.GetUtcNow(), OperatingTime.Zone);
        return ServiceValidator.Validate(
            draft,
            line.Stations.Select(s => new LineStationInfo(s.Code, s.Names.GetValueOrDefault("en", s.Code), s.Sequence, s.Platforms)).ToList(),
            others,
            DateOnly.FromDateTime(now.DateTime),
            TimeOnly.FromDateTime(now.DateTime),
            settings.PlatformWarningMinutes);
    }
}

public enum CreateError { None, LineNotFound, Invalid, WarningsNotAcknowledged, TrainNumberInUse }

public sealed record CreateResult(
    CreateError Error,
    ValidationResult? Validation,
    IReadOnlyList<Guid> ServiceIds,
    string TrainNumber,
    int JourneysGenerated,
    string? ClashTrainNumber = null);

public sealed class CreateServiceHandler(
    ITimetableRepository repository, ValidateServiceHandler validator, TimeProvider clock, SchedulingSettings settings)
{
    public async Task<CreateResult> HandleAsync(ServiceDraft draft, bool acknowledgeWarnings, string actor, CancellationToken cancellationToken)
    {
        var validation = await validator.HandleAsync(draft, cancellationToken);
        if (validation is null)
        {
            TimetableMetrics.Change("created", "rejected");
            return new CreateResult(CreateError.LineNotFound, null, [], draft.TrainNumber, 0);
        }

        if (!validation.Valid)
        {
            TimetableMetrics.Change("created", "rejected");
            return new CreateResult(CreateError.Invalid, validation, [], draft.TrainNumber, 0);
        }

        // Warnings are advice and never block (FR-57): the controller confirms them.
        if (validation.Warnings.Count > 0 && !acknowledgeWarnings)
        {
            TimetableMetrics.Change("created", "rejected");
            return new CreateResult(CreateError.WarningsNotAcknowledged, validation, [], draft.TrainNumber, 0);
        }

        var line = (await repository.GetLineAsync(draft.LineCode, cancellationToken))!;
        var first = line.Stations.First(s => s.Code.Equals(draft.Stops[0].StationCode, StringComparison.OrdinalIgnoreCase));
        var last = line.Stations.First(s => s.Code.Equals(draft.Stops[^1].StationCode, StringComparison.OrdinalIgnoreCase));
        // Toward the first station of the line (sequence falling) is Inbound (FR-10); the controller never types it.
        var direction = first.Sequence > last.Sequence ? "Inbound" : "Outbound";
        var category = Enum.Parse<Domain.Reference.ServiceCategory>(draft.Category, true).ToString();
        var type = Enum.Parse<Domain.Reference.ServiceType>(draft.ServiceType, true).ToString();
        var number = draft.TrainNumber.ToUpperInvariant();
        var stops = draft.Stops.Select(s => s with { StationCode = s.StationCode.ToUpperInvariant() }).ToList();

        var services = draft.Kind == ServiceKind.Weekly
            ?
            [
                new NewService(
                    Guid.CreateVersion7(clock.GetUtcNow()), number, line.LineCode, category, type, direction, ServiceKind.Weekly,
                    RunDays.Normalise(draft.Days)!, draft.EffectiveFrom!.Value, null, stops),
            ]
            : draft.Dates.Distinct().Order().Select(date => new NewService(
                Guid.CreateVersion7(clock.GetUtcNow()), number, line.LineCode, category, type, direction, ServiceKind.Extra, "Daily", date, date, stops)).ToList();

        var outcome = await repository.CreateAsync(services, actor, clock.GetUtcNow(), cancellationToken);
        if (!outcome.Created)
        {
            TimetableMetrics.Change("created", "conflict");
            return new CreateResult(CreateError.TrainNumberInUse, validation, [], number, 0, outcome.ClashTrainNumber);
        }

        // The journeys of the new services exist at once (FR-11): from the first date, within the horizon.
        var today = OperatingTime.Today(clock);
        var horizonEnd = today.AddDays(settings.HorizonDays);
        var generated = 0;
        foreach (var service in services)
        {
            var from = service.EffectiveFrom > today ? service.EffectiveFrom : today;
            if (from <= horizonEnd)
            {
                generated += await repository.GenerateJourneysAsync(from, horizonEnd, [service.Id], cancellationToken);
            }
        }

        TimetableMetrics.Change("created", "ok");
        TimetableMetrics.Generated.Add(generated);
        return new CreateResult(CreateError.None, validation, services.Select(s => s.Id).ToList(), number, generated);
    }
}

/// <summary>The generator's core (FR-11, spec 13 rule 9): the journeys of today and the next days that are missing. Used by the hosted service.</summary>
public sealed class JourneyGenerationHandler(ITimetableRepository repository, TimeProvider clock, SchedulingSettings settings)
{
    public async Task<int> RunAsync(CancellationToken cancellationToken)
    {
        var today = OperatingTime.Today(clock);
        var created = await repository.GenerateJourneysAsync(today, today.AddDays(settings.HorizonDays), null, cancellationToken);
        TimetableMetrics.Generated.Add(created);
        return created;
    }
}

public enum CancelError { None, NotFound, InvalidReason, DateInPast, InvalidRange, NothingToCancel, JourneyHasEvents }

public sealed record CancelResult(CancelError Error, Guid? CancellationId, int CancelledJourneys, DateOnly? From, DateOnly? To, DateOnly? EventDate);

/// <summary>Cancel a service for one date or from a date onward, with a reason (FR-11C, spec 14 section 4.1).</summary>
public sealed class CancelServiceHandler(ITimetableRepository repository, TimeProvider clock)
{
    public async Task<CancelResult> HandleAsync(Guid serviceId, DateOnly? from, DateOnly? to, string? reason, string actor, CancellationToken cancellationToken)
    {
        var text = reason?.Trim();
        if (string.IsNullOrEmpty(text) || text.Length > 200)
        {
            return Rejected(CancelError.InvalidReason);
        }

        var today = OperatingTime.Today(clock);
        if (from is not { } start || start < today)
        {
            return Rejected(CancelError.DateInPast);
        }

        if (to is { } end && end < start)
        {
            return Rejected(CancelError.InvalidRange);
        }

        var outcome = await repository.CancelAsync(serviceId, start, to, text, actor, clock.GetUtcNow(), cancellationToken);
        switch (outcome.Kind)
        {
            case CancelResultKind.NotFound:
                return Rejected(CancelError.NotFound);
            case CancelResultKind.NothingToCancel:
                return Rejected(CancelError.NothingToCancel);
            case CancelResultKind.HasEvents:
                TimetableMetrics.Change("cancelled", "conflict");
                return new CancelResult(CancelError.JourneyHasEvents, null, 0, start, to, outcome.EventDate);
            default:
                TimetableMetrics.Change("cancelled", "ok");
                return new CancelResult(CancelError.None, outcome.CancellationId, outcome.CancelledJourneys, start, to, null);
        }
    }

    private static CancelResult Rejected(CancelError error)
    {
        TimetableMetrics.Change("cancelled", "rejected");
        return new CancelResult(error, null, 0, null, null, null);
    }
}

public enum AmendError
{
    None, NotFound, ExtraNotSupported, ServiceEnded, IdentityChanged, DateTooEarly, NotAfterCurrent, Invalid, WarningsNotAcknowledged, JourneyHasEvents,
}

public sealed record AmendResult(
    AmendError Error,
    ValidationResult? Validation,
    Guid? NewServiceId,
    int Version,
    int JourneysUpdated,
    int JourneysCancelled,
    int JourneysGenerated,
    DateOnly? EventDate = null);

/// <summary>Amend a weekly service from a date: the change is a new version, the old one is kept (FR-11B, D-70, spec 14 section 4.2).</summary>
public sealed class AmendServiceHandler(
    ITimetableRepository repository, ValidateServiceHandler validator, TimeProvider clock, SchedulingSettings settings)
{
    public async Task<AmendResult> HandleAsync(Guid serviceId, ServiceDraft draft, bool acknowledgeWarnings, string actor, CancellationToken cancellationToken)
    {
        var latest = await repository.GetLatestVersionAsync(serviceId, cancellationToken);
        if (latest is null)
        {
            return Rejected(AmendError.NotFound);
        }

        if (latest.Kind == ServiceKind.Extra)
        {
            return Rejected(AmendError.ExtraNotSupported);
        }

        var today = OperatingTime.Today(clock);
        if (latest.EffectiveTo is { } ended && ended < today)
        {
            return Rejected(AmendError.ServiceEnded);
        }

        if (!draft.TrainNumber.Equals(latest.TrainNumber, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(draft.LineCode, latest.LineCode, StringComparison.OrdinalIgnoreCase)
            || draft.Kind != ServiceKind.Weekly)
        {
            return Rejected(AmendError.IdentityChanged);
        }

        // Tomorrow at the earliest, so no running journey changes (spec 13 Q10); a missing date is for the validation below.
        if (draft.EffectiveFrom is { } from)
        {
            if (from <= today)
            {
                return Rejected(AmendError.DateTooEarly);
            }

            if (from <= latest.EffectiveFrom)
            {
                return Rejected(AmendError.NotAfterCurrent);
            }
        }

        var validation = await validator.HandleAsync(draft, cancellationToken);
        if (validation is null || !validation.Valid)
        {
            TimetableMetrics.Change("amended", "rejected");
            return new AmendResult(AmendError.Invalid, validation, null, 0, 0, 0, 0);
        }

        if (validation.Warnings.Count > 0 && !acknowledgeWarnings)
        {
            TimetableMetrics.Change("amended", "rejected");
            return new AmendResult(AmendError.WarningsNotAcknowledged, validation, null, 0, 0, 0, 0);
        }

        var line = (await repository.GetLineAsync(latest.LineCode, cancellationToken))!;
        var first = line.Stations.First(s => s.Code.Equals(draft.Stops[0].StationCode, StringComparison.OrdinalIgnoreCase));
        var last = line.Stations.First(s => s.Code.Equals(draft.Stops[^1].StationCode, StringComparison.OrdinalIgnoreCase));
        var direction = first.Sequence > last.Sequence ? "Inbound" : "Outbound";
        var category = Enum.Parse<Domain.Reference.ServiceCategory>(draft.Category, true).ToString();
        var type = Enum.Parse<Domain.Reference.ServiceType>(draft.ServiceType, true).ToString();
        var next = new NewService(
            Guid.CreateVersion7(clock.GetUtcNow()), latest.TrainNumber, latest.LineCode, category, type, direction, ServiceKind.Weekly,
            RunDays.Normalise(draft.Days)!, draft.EffectiveFrom!.Value, latest.EffectiveTo,
            draft.Stops.Select(s => s with { StationCode = s.StationCode.ToUpperInvariant() }).ToList());

        var outcome = await repository.AmendAsync(next, serviceId, actor, clock.GetUtcNow(), cancellationToken);
        switch (outcome.Kind)
        {
            case AmendResultKind.NotFound:
                return Rejected(AmendError.NotFound);
            case AmendResultKind.NotAfterCurrent:
                return Rejected(AmendError.NotAfterCurrent);
            case AmendResultKind.HasEvents:
                TimetableMetrics.Change("amended", "conflict");
                return new AmendResult(AmendError.JourneyHasEvents, validation, null, 0, 0, 0, 0, outcome.EventDate);
        }

        // Days the new version adds get their journeys now (the generator would also do it within the hour).
        var horizonEnd = today.AddDays(settings.HorizonDays);
        var generateFrom = next.EffectiveFrom > today ? next.EffectiveFrom : today;
        var generated = generateFrom <= horizonEnd ? await repository.GenerateJourneysAsync(generateFrom, horizonEnd, [next.Id], cancellationToken) : 0;

        TimetableMetrics.Change("amended", "ok");
        TimetableMetrics.Generated.Add(generated);
        return new AmendResult(AmendError.None, validation, next.Id, outcome.Version, outcome.Repointed, outcome.CancelledByAmendment, generated);
    }

    private static AmendResult Rejected(AmendError error)
    {
        TimetableMetrics.Change("amended", "rejected");
        return new AmendResult(error, null, null, 0, 0, 0, 0);
    }
}

public sealed record RestoreResult(RestoreResultKind Kind, int RestoredJourneys);

/// <summary>Restore a cancelled service (the other half of FR-11C): nothing is deleted, the cancellation is closed with the actor and the time.</summary>
public sealed class RestoreServiceHandler(ITimetableRepository repository, TimeProvider clock)
{
    public async Task<RestoreResult> HandleAsync(Guid cancellationId, string actor, CancellationToken cancellationToken)
    {
        var outcome = await repository.RestoreAsync(cancellationId, actor, clock.GetUtcNow(), cancellationToken);
        TimetableMetrics.Change("restored", outcome.Kind == RestoreResultKind.Done ? "ok" : "conflict");
        return new RestoreResult(outcome.Kind, outcome.RestoredJourneys);
    }
}
