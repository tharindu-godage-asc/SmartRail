using System.Text.Json;
using SmartRail.Application.Features.Assignments;
using SmartRail.Application.Features.Events.ReportEvent;
using SmartRail.Application.Features.Journeys;
using SmartRail.Domain.Journeys;

namespace SmartRail.Application.Features.ControllerJourneys;

/// <summary>One event as stored, with who made it (spec 16 F1). The payload is read here so the controller can see the reason: no public read can (D-10).</summary>
public sealed record RawEvent(
    Guid Id,
    EventType Type,
    EventSource Source,
    string CreatedBy,
    Guid? ServiceStopId,
    DateTimeOffset OccurredAt,
    DateTimeOffset ReceivedAt,
    string? Payload);

/// <summary>What the journey page needs beyond the status snapshot: the service, planned platforms and the event history.</summary>
public sealed record ControllerJourneyExtras(
    string Direction,
    string Category,
    string ServiceType,
    IReadOnlyDictionary<Guid, string> PlannedPlatforms,
    IReadOnlyList<RawEvent> Events);

public interface IControllerJourneyQueries
{
    /// <summary>Null when the journey is unknown. Events newest first, at most <paramref name="maxEvents"/>.</summary>
    Task<ControllerJourneyExtras?> GetExtrasAsync(Guid journeyId, int maxEvents, CancellationToken cancellationToken);
}

public sealed record DetailStop(
    Guid ServiceStopId,
    int Sequence,
    string StationCode,
    string StationName,
    DateTimeOffset? ScheduledArrival,
    DateTimeOffset? ScheduledDeparture,
    DateTimeOffset? EstimatedArrival,
    DateTimeOffset? EstimatedDeparture,
    DateTimeOffset? ActualArrival,
    DateTimeOffset? ActualDeparture,
    string Confirmation,
    /// <summary>Passed (left), AtStation (arrived, not left), Next (the next stop to reach) or Upcoming.</summary>
    string State,
    string? PlannedPlatform,
    string? Platform);

public sealed record DetailEvent(
    Guid Id,
    DateTimeOffset At,
    string Type,
    string Source,
    string Actor,
    string? StationName,
    int? TotalDelayMinutes,
    bool DelayUnknown,
    string? Reason,
    string? ReasonCode,
    string? Platform = null);

public sealed record DetailOperator(string Name, DateTimeOffset ValidFrom, DateTimeOffset? ValidTo);

public sealed record DetailPosition(string StationCode, string StationName, string State, DateTimeOffset At);

public sealed record DetailVerifiedReason(string Code, DateTimeOffset VerifiedAt);

/// <summary>The controller's view of one journey (spec 16 F1, read only). Position is given as a place (at or between stops), never as a prediction.</summary>
public sealed record ControllerJourneyDetail(
    Guid JourneyId,
    string TrainNumber,
    DateOnly OperatingDate,
    string Direction,
    string Category,
    string ServiceType,
    string Status,
    string StatusKey,
    string StatusGroup,
    int DelayMinutes,
    bool DelayKnown,
    DateTimeOffset ServerTime,
    string? ReportedReason,
    DetailVerifiedReason? VerifiedReason,
    DetailPosition? Position,
    DetailOperator? Operator,
    IReadOnlyList<DetailStop> Stops,
    IReadOnlyList<DetailEvent> Events);

public sealed class GetControllerJourneyHandler(
    IJourneyRepository journeys,
    IControllerJourneyQueries queries,
    IOperatorDirectory directory,
    TimeProvider clock,
    JourneySettings settings)
{
    public const int MaxEvents = 100;

    public async Task<ControllerJourneyDetail?> HandleAsync(Guid journeyId, CancellationToken cancellationToken)
    {
        var data = await journeys.GetAsync(journeyId, cancellationToken);
        var extras = data is null ? null : await queries.GetExtrasAsync(journeyId, MaxEvents, cancellationToken);
        if (data is null || extras is null)
        {
            return null;
        }

        var now = clock.GetUtcNow();
        var snapshot = SnapshotBuilder.Build(data, now, settings.OnTimeToleranceMinutes);
        var stopName = snapshot.Stops.ToDictionary(s => s.ServiceStopId, s => s.StationName);

        // The next stop to reach is the first one that has not been left and is not the one the train is at.
        var nextId = snapshot.Stops.OrderBy(s => s.Sequence).FirstOrDefault(s => s.ActualDeparture is null && s.ActualArrival is null)?.ServiceStopId;
        var stops = snapshot.Stops.OrderBy(s => s.Sequence).Select(s => new DetailStop(
            s.ServiceStopId, s.Sequence, s.StationCode, s.StationName,
            s.ScheduledArrival, s.ScheduledDeparture, s.EstimatedArrival, s.EstimatedDeparture, s.ActualArrival, s.ActualDeparture,
            s.Confirmation,
            s.ActualDeparture is not null ? "Passed" : s.ActualArrival is not null ? "AtStation" : s.ServiceStopId == nextId ? "Next" : "Upcoming",
            extras.PlannedPlatforms.GetValueOrDefault(s.ServiceStopId),
            data.StopStates.FirstOrDefault(st => st.ServiceStopId == s.ServiceStopId)?.PlatformCode)).ToList();

        var events = new List<DetailEvent>(extras.Events.Count);
        string? reportedReason = null;
        foreach (var e in extras.Events)
        {
            var actor = await ActorAsync(e, cancellationToken);
            var delay = e.Type == EventType.DelayReported ? EventRules.FromJson(e.Payload) : null;
            if (delay is not null && reportedReason is null)
            {
                reportedReason = delay.Reason; // newest first: the latest delay report's reason
            }

            events.Add(new DetailEvent(
                e.Id, e.OccurredAt, e.Type.ToString(), e.Source.ToString(), actor,
                e.ServiceStopId is { } id ? stopName.GetValueOrDefault(id) : null,
                delay?.TotalDelayMinutes, delay?.DelayUnknown ?? false, delay?.Reason, e.Type == EventType.ReasonVerified ? ReasonCodeOf(e.Payload) : null,
                e.Type == EventType.PlatformChanged ? PlatformOf(e.Payload) : null));
        }

        var current = data.Assignments.Where(a => AssignmentRules.Covers(a.ValidFrom, a.ValidTo, now)).OrderByDescending(a => a.ValidFrom).FirstOrDefault();
        DetailOperator? op = null;
        if (current is not null)
        {
            var person = await SafeFindAsync(current.OperatorUserId, cancellationToken);
            op = new DetailOperator(person?.Name ?? "Operator", current.ValidFrom, current.ValidTo);
        }

        var position = snapshot.LastKnownPosition is { } p
            ? new DetailPosition(p.StationCode, p.StationName, p.State, (p.State == "Arrived" ? p.ArrivedAt : p.DepartedAt) ?? snapshot.ServerTime)
            : null;

        return new ControllerJourneyDetail(
            data.JourneyId, data.TrainNumber, data.OperatingDate, extras.Direction, extras.Category, extras.ServiceType,
            data.Status.ToString(), snapshot.StatusKey, snapshot.StatusGroup, data.DelayMinutes, data.DelayKnown, now,
            reportedReason, snapshot.Reason is { } r ? new DetailVerifiedReason(r.Code, r.VerifiedAt) : null, position, op, stops, events);
    }

    /// <summary>The platform a PlatformChanged event set (null = back to the planned platform).</summary>
    private static string? PlatformOf(string? payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
        {
            return null;
        }

        using var document = System.Text.Json.JsonDocument.Parse(payload);
        return document.RootElement.TryGetProperty("platform", out var platform) && platform.ValueKind == System.Text.Json.JsonValueKind.String ? platform.GetString() : null;
    }

    private async Task<string> ActorAsync(RawEvent e, CancellationToken cancellationToken)
    {
        if (e.Source == EventSource.Simulator)
        {
            return "Simulator";
        }

        var person = await SafeFindAsync(e.CreatedBy, cancellationToken);
        return person?.Name ?? (e.Source == EventSource.Operator ? "Operator" : "Controller");
    }

    private async Task<DirectoryOperator?> SafeFindAsync(string userId, CancellationToken cancellationToken)
    {
        try
        {
            return await directory.FindAsync(userId, cancellationToken);
        }
        catch (HttpRequestException)
        {
            return null; // the page still shows; names fall back to the role
        }
    }

    private static string? ReasonCodeOf(string? payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
        {
            return null;
        }

        using var document = JsonDocument.Parse(payload);
        return document.RootElement.TryGetProperty("reasonCode", out var code) ? code.GetString() : null;
    }
}
