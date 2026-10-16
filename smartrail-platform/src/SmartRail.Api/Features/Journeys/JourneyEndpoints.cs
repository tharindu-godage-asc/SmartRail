using SmartRail.Api.Authentication;
using SmartRail.Application.Features.Events.ReportEvent;
using SmartRail.Application.Features.Journeys;

namespace SmartRail.Api.Features.Journeys;

public sealed record ReportEventResponse(Guid EventId, int JourneyVersion, string Status);

/// <summary>Removes line breaks from a value that came from a request or a token before it goes into a log line (log forging).</summary>
internal static class LogSafe
{
    public static string Clean(string? value) =>
        value is null ? "-" : value.Replace("\r", string.Empty, StringComparison.Ordinal).Replace("\n", string.Empty, StringComparison.Ordinal);
}

public static class JourneyFeatures
{
    private const string LoggerCategory = "SmartRail.Journeys";

    /// <summary>Handlers and the Events:* and Journeys:* settings. Registered only when a database is configured (R14).</summary>
    public static IServiceCollection AddJourneyFeatures(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton(configuration.GetSection(EventLimits.Section).Get<EventLimits>() ?? new EventLimits());
        services.AddSingleton(configuration.GetSection(JourneySettings.Section).Get<JourneySettings>() ?? new JourneySettings());
        services.AddScoped<ReportEventHandler>();
        services.AddScoped<GetJourneyStatusHandler>();
        services.AddScoped<GetTrainStatusHandler>();
        services.AddScoped<GetOperatorJourneyHandler>();
        return services;
    }

    public static IEndpointRouteBuilder MapJourneyFeatures(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/operator/journey", GetOperatorJourney)
           .WithName("GetOperatorJourney")
           .RequireAuthorization(Policies.Operator);

        app.MapPost("/api/v1/journeys/{journeyId:guid}/events", ReportEvent)
           .WithName("ReportEvent")
           .RequireAuthorization(Policies.Operator);

        // Public: passengers have no account (FR-05). Listed on the allow-list in AuthConfigurationTests.
        app.MapGet("/api/v1/journeys/{journeyId:guid}/status", GetJourneyStatus)
           .WithName("GetJourneyStatus")
           .AllowAnonymous();

        app.MapGet("/api/v1/trains/{trainNumber}/status", GetTrainStatus)
           .WithName("GetTrainStatus")
           .AllowAnonymous();

        return app;
    }

    private static async Task<IResult> GetOperatorJourney(HttpContext http, GetOperatorJourneyHandler handler, CancellationToken cancellationToken)
    {
        var subject = http.User.FindFirst("sub")?.Value;
        if (string.IsNullOrEmpty(subject))
        {
            return Problem(StatusCodes.Status403Forbidden, "Forbidden", "no_subject");
        }

        var journey = await handler.HandleAsync(subject, cancellationToken);
        return journey is null
            ? Problem(StatusCodes.Status404NotFound, "No assigned journey", "no_assignment_now")
            : TypedResults.Ok(journey);
    }

    private static async Task<IResult> ReportEvent(
        Guid journeyId,
        ReportEventRequest body,
        HttpContext http,
        ReportEventHandler handler,
        ILoggerFactory loggers,
        CancellationToken cancellationToken)
    {
        var subject = http.User.FindFirst("sub")?.Value;
        if (string.IsNullOrEmpty(subject))
        {
            return Problem(StatusCodes.Status403Forbidden, "Forbidden", "no_subject");
        }

        var result = await handler.HandleAsync(journeyId, subject, body, cancellationToken);

        if (result.Outcome == ReportOutcome.Rejected)
        {
            var (status, title, code) = Describe(result.Reason!.Value);
            // R12: refused reports are logged (never the token) and were counted by the handler.
            loggers.CreateLogger(LoggerCategory).LogWarning(
                "Report refused: reason={Reason} journey={JourneyId} clientEventId={ClientEventId} subject={Subject}",
                code, journeyId, body.ClientEventId, LogSafe.Clean(subject));
            return Problem(status, title, code);
        }

        var response = new ReportEventResponse(result.EventId!.Value, result.JourneyVersion!.Value, "Delivered");
        return result.Outcome == ReportOutcome.Recorded
            ? TypedResults.Json(response, statusCode: StatusCodes.Status201Created)
            : TypedResults.Ok(response);
    }

    private static async Task<IResult> GetJourneyStatus(Guid journeyId, GetJourneyStatusHandler handler, CancellationToken cancellationToken)
    {
        var snapshot = await handler.HandleAsync(journeyId, cancellationToken);
        return snapshot is null
            ? Problem(StatusCodes.Status404NotFound, "Journey not found", "journey_not_found")
            : TypedResults.Ok(snapshot);
    }

    private static async Task<IResult> GetTrainStatus(string trainNumber, GetTrainStatusHandler handler, CancellationToken cancellationToken)
    {
        var snapshot = await handler.HandleAsync(trainNumber, cancellationToken);
        return snapshot is null
            ? Problem(StatusCodes.Status404NotFound, "No journey for this train today", "no_journey_for_train")
            : TypedResults.Ok(snapshot);
    }

    /// <summary>RFC 9457 ProblemDetails with a short title and a stable reason code, never more detail.</summary>
    private static IResult Problem(int status, string title, string reasonCode) =>
        TypedResults.Problem(title: title, statusCode: status, extensions: new Dictionary<string, object?> { ["reason"] = reasonCode });

    private static (int Status, string Title, string Code) Describe(RejectReason reason) => reason switch
    {
        RejectReason.InvalidRequest => (StatusCodes.Status422UnprocessableEntity, "Invalid report", "invalid_request"),
        RejectReason.JourneyNotFound => (StatusCodes.Status404NotFound, "Journey not found", "journey_not_found"),
        RejectReason.NotAssigned => (StatusCodes.Status403Forbidden, "Not assigned to this journey", "not_assigned"),
        RejectReason.TimeOutOfRange => (StatusCodes.Status422UnprocessableEntity, "Reported time is outside the allowed range", "time_out_of_range"),
        RejectReason.StopNotInService => (StatusCodes.Status422UnprocessableEntity, "Stop is not part of this journey", "stop_not_in_service"),
        RejectReason.FinalStop => (StatusCodes.Status422UnprocessableEntity, "No departure from the final stop", "final_stop"),
        RejectReason.NotArrivalStation => (StatusCodes.Status422UnprocessableEntity, "No arrival report at this stop", "not_arrival_station"),
        RejectReason.JourneyNotReportable => (StatusCodes.Status409Conflict, "Journey cannot take reports now", "journey_not_reportable"),
        RejectReason.ClientEventIdReusedWithDifferentContent => (StatusCodes.Status409Conflict, "Client event ID already used", "client_event_id_reused"),
        RejectReason.StopAlreadyHasDifferentTime => (StatusCodes.Status409Conflict, "Stop already has a different time for this report", "stop_has_different_time"),
        RejectReason.DelayNotGreater => (StatusCodes.Status409Conflict, "The delay is not greater than the one running", "delay_not_greater"),
        RejectReason.OrderConflict => (StatusCodes.Status409Conflict, "Report is impossible in the stop order", "order_conflict"),
        RejectReason.VersionConflict => (StatusCodes.Status409Conflict, "Journey changed, try again", "version_conflict"),
        _ => (StatusCodes.Status500InternalServerError, "Error", "error"),
    };
}
