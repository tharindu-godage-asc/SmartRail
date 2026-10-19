using Microsoft.AspNetCore.Mvc;
using SmartRail.Api.Authentication;
using SmartRail.Api.Features.Journeys;
using SmartRail.Application.Features.Assignments;

namespace SmartRail.Api.Features.Assignments;

public sealed record AssignBody(string? OperatorUserId, DateTimeOffset? ValidFrom, DateTimeOffset? ValidTo, bool? AcknowledgeOverlap);

public sealed record EndBody(string? Reason, string? Note);

/// <summary>
/// The controller's operator assignments (spec 17): journeys that need an operator, the operators, one journey's assignments, assign
/// (also a crew change) and end now. Controller role only; who acted comes from the token, never from the body. Mapped only with a database.
/// </summary>
public static class AssignmentFeatures
{
    public static IServiceCollection AddAssignmentFeatures(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton(configuration.GetSection(AssignmentSettings.Section).Get<AssignmentSettings>() ?? new AssignmentSettings());
        services.AddScoped<ListAssignmentJourneysHandler>();
        services.AddScoped<ListOperatorsHandler>();
        services.AddScoped<ListJourneyAssignmentsHandler>();
        services.AddScoped<AssignOperatorHandler>();
        services.AddScoped<EndAssignmentHandler>();
        return services;
    }

    public static IEndpointRouteBuilder MapAssignmentFeatures(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/assignments/journeys", ListJourneys).WithName("ListAssignmentJourneys").RequireAuthorization(Policies.Controller);
        app.MapGet("/api/v1/assignments/operators", ListOperators).WithName("ListAssignableOperators").RequireAuthorization(Policies.Controller);
        app.MapGet("/api/v1/journeys/{journeyId:guid}/assignments", ListAssignments).WithName("ListJourneyAssignments").RequireAuthorization(Policies.Controller);
        app.MapPost("/api/v1/journeys/{journeyId:guid}/assignments", Assign).WithName("AssignOperator").RequireAuthorization(Policies.Controller);
        app.MapPost("/api/v1/journeys/{journeyId:guid}/assignments/{assignmentId:guid}/end", End).WithName("EndAssignment").RequireAuthorization(Policies.Controller);
        return app;
    }

    private static async Task<IResult> ListJourneys(
        ListAssignmentJourneysHandler handler, [FromQuery(Name = "date")] string? date, [FromQuery(Name = "filter")] string? filter, CancellationToken cancellationToken)
    {
        var (valid, items) = await handler.HandleAsync(date, filter, cancellationToken);
        return valid ? TypedResults.Ok(items) : Problem(StatusCodes.Status422UnprocessableEntity, "date is yyyy-MM-dd and filter is unassigned or all", "invalid_request");
    }

    private static async Task<IResult> ListOperators(
        ListOperatorsHandler handler,
        [FromQuery(Name = "journeyId")] Guid? journeyId,
        [FromQuery(Name = "from")] DateTimeOffset? from,
        [FromQuery(Name = "to")] DateTimeOffset? to,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.HandleAsync(journeyId, from, to, cancellationToken));

    private static async Task<IResult> ListAssignments(Guid journeyId, ListJourneyAssignmentsHandler handler, CancellationToken cancellationToken)
    {
        var rows = await handler.HandleAsync(journeyId, cancellationToken);
        return rows is null ? Problem(StatusCodes.Status404NotFound, "Journey not found", "journey_not_found") : TypedResults.Ok(rows);
    }

    private static async Task<IResult> Assign(
        Guid journeyId, AssignBody body, HttpContext http, AssignOperatorHandler handler, ILoggerFactory loggers, CancellationToken cancellationToken)
    {
        var subject = http.User.FindFirst("sub")?.Value;
        if (string.IsNullOrEmpty(subject))
        {
            return Problem(StatusCodes.Status403Forbidden, "Forbidden", "no_subject");
        }

        if (string.IsNullOrWhiteSpace(body.OperatorUserId))
        {
            return Problem(StatusCodes.Status422UnprocessableEntity, "operatorUserId is required", "operator_not_found");
        }

        var result = await handler.HandleAsync(journeyId, body.OperatorUserId.Trim(), body.ValidFrom, body.ValidTo, body.AcknowledgeOverlap == true, subject, cancellationToken);
        switch (result.Error)
        {
            case AssignError.None:
                loggers.CreateLogger("SmartRail.Assignments").LogInformation(
                    "Journey {JourneyId} assigned to {OperatorSubject} by {ControllerSubject}.", journeyId, LogSafe.Clean(body.OperatorUserId), LogSafe.Clean(subject));
                return TypedResults.Created(
                    $"/api/v1/journeys/{journeyId}/assignments",
                    new
                    {
                        assignmentId = result.Assignment!.AssignmentId,
                        operatorUserId = result.Assignment.OperatorUserId,
                        name = result.Assignment.OperatorName,
                        validFrom = result.Assignment.ValidFrom,
                        validTo = result.Assignment.ValidTo,
                        journeyStatus = result.JourneyStatus,
                        ended = result.Ended,
                    });
            case AssignError.JourneyNotFound:
                return Problem(StatusCodes.Status404NotFound, "Journey not found", "journey_not_found");
            case AssignError.OperatorNotFound:
                return Problem(StatusCodes.Status422UnprocessableEntity, "No such operator", "operator_not_found");
            case AssignError.NotAnOperator:
                return Problem(StatusCodes.Status422UnprocessableEntity, "This account does not have the operator role", "not_an_operator");
            case AssignError.OperatorDisabled:
                return Problem(StatusCodes.Status422UnprocessableEntity, "This operator account is disabled", "operator_disabled");
            case AssignError.ValidToNotAfterFrom:
                return Problem(StatusCodes.Status422UnprocessableEntity, "The end must be after the start", "valid_to_not_after_from");
            case AssignError.ValidFromInPast:
                return Problem(StatusCodes.Status422UnprocessableEntity, "The start is too far in the past", "valid_from_in_past_beyond_clock");
            case AssignError.JourneyNotAssignable:
                return Problem(StatusCodes.Status409Conflict, "This journey is cancelled or completed", "journey_not_assignable");
            case AssignError.OverlapsLaterAssignment:
                return Problem(StatusCodes.Status409Conflict, "Another assignment starts at or after this start", "overlaps_later_assignment");
            case AssignError.DirectoryUnavailable:
                return Problem(StatusCodes.Status503ServiceUnavailable, "The operator directory is not available", "operator_directory_unavailable");
            default:
                return TypedResults.Problem(
                    title: "This operator is on another journey in that range", statusCode: StatusCodes.Status409Conflict,
                    extensions: new Dictionary<string, object?>
                    {
                        ["reason"] = "operator_overlaps_another_journey",
                        ["journeyId"] = result.Busy?.JourneyId,
                        ["trainNumber"] = result.Busy?.TrainNumber,
                        ["until"] = result.Busy?.Until,
                    });
        }
    }

    private static async Task<IResult> End(
        Guid journeyId, Guid assignmentId, EndBody body, HttpContext http, EndAssignmentHandler handler, ILoggerFactory loggers, CancellationToken cancellationToken)
    {
        var subject = http.User.FindFirst("sub")?.Value;
        if (string.IsNullOrEmpty(subject))
        {
            return Problem(StatusCodes.Status403Forbidden, "Forbidden", "no_subject");
        }

        var result = await handler.HandleAsync(journeyId, assignmentId, body.Reason, body.Note, subject, cancellationToken);
        switch (result.Error)
        {
            case EndError.None:
                loggers.CreateLogger("SmartRail.Assignments").LogInformation(
                    "Assignment {AssignmentId} of journey {JourneyId} ended by {ControllerSubject}, reason {Reason}.",
                    assignmentId, journeyId, LogSafe.Clean(subject), LogSafe.Clean(body.Reason));
                return TypedResults.Ok(new { assignment = result.Assignment, journeyStatus = result.JourneyStatus });
            case EndError.NotFound:
                return Problem(StatusCodes.Status404NotFound, "Assignment not found", "assignment_not_found");
            case EndError.AlreadyEnded:
                return Problem(StatusCodes.Status409Conflict, "This assignment has already ended", "already_ended");
            case EndError.NotStarted:
                return Problem(StatusCodes.Status409Conflict, "This assignment has not started yet", "not_started");
            case EndError.NoteTooLong:
                return Problem(StatusCodes.Status422UnprocessableEntity, "The note is at most 500 characters", "note_too_long");
            default:
                return Problem(StatusCodes.Status422UnprocessableEntity, "The reason is LostDevice, CrewChange or Other", "invalid_reason");
        }
    }

    private static IResult Problem(int status, string title, string reasonCode) =>
        TypedResults.Problem(title: title, statusCode: status, extensions: new Dictionary<string, object?> { ["reason"] = reasonCode });
}
