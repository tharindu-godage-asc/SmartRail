using Microsoft.AspNetCore.Mvc;
using SmartRail.Api.Authentication;
using SmartRail.Api.Features.Journeys;
using SmartRail.Application.Features.Exceptions;

namespace SmartRail.Api.Features.Exceptions;

/// <summary>The body of POST /api/v1/exceptions/{id}/resolve: one of Acknowledged, NoActionNeeded, Escalated.</summary>
public sealed record ResolveExceptionRequest(string? Action);

/// <summary>
/// The controllers' exceptions queue (spec 07, FR-51): the open items and "resolve". Controller role only; a passenger or an
/// operator gets 403 and no token gets 401. Mapped only when a database is configured, like the other features.
/// </summary>
public static class ExceptionFeatures
{
    private const string LoggerCategory = "SmartRail.Exceptions";

    public static IServiceCollection AddExceptionFeatures(this IServiceCollection services)
    {
        services.AddScoped<ListOpenExceptionsHandler>();
        services.AddScoped<ResolveExceptionHandler>();
        return services;
    }

    public static IEndpointRouteBuilder MapExceptionFeatures(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/exceptions", ListOpen).WithName("ListOpenExceptions").RequireAuthorization(Policies.Controller);
        app.MapPost("/api/v1/exceptions/{id:guid}/resolve", Resolve).WithName("ResolveException").RequireAuthorization(Policies.Controller);
        return app;
    }

    private static async Task<IResult> ListOpen(
        [FromQuery(Name = "status")] string? status, ListOpenExceptionsHandler handler, CancellationToken cancellationToken)
    {
        // Only the open queue exists in this slice; the default is open.
        if (status is not null && !string.Equals(status, "open", StringComparison.Ordinal))
        {
            return Problem(StatusCodes.Status422UnprocessableEntity, "Only status=open is available", "invalid_request");
        }

        return TypedResults.Ok(await handler.HandleAsync(cancellationToken));
    }

    private static async Task<IResult> Resolve(
        Guid id,
        ResolveExceptionRequest body,
        HttpContext http,
        ResolveExceptionHandler handler,
        ILoggerFactory loggers,
        CancellationToken cancellationToken)
    {
        var subject = http.User.FindFirst("sub")?.Value;
        if (string.IsNullOrEmpty(subject))
        {
            return Problem(StatusCodes.Status403Forbidden, "Forbidden", "no_subject");
        }

        var outcome = await handler.HandleAsync(id, subject, body.Action, cancellationToken);
        if (outcome == ResolveOutcome.Resolved)
        {
            // Who resolved what is the audit trail (AC-E05-02 6, FR-43). The subject came from a token, so it is cleaned for the log.
            loggers.CreateLogger(LoggerCategory).LogInformation(
                "Exception resolved: exceptionId={ExceptionId} action={Action} controller={Controller}", id, body.Action, LogSafe.Clean(subject));
            return TypedResults.NoContent();
        }

        return outcome switch
        {
            ResolveOutcome.NotFound => Problem(StatusCodes.Status404NotFound, "Exception not found", "exception_not_found"),
            ResolveOutcome.AlreadyResolved => Problem(StatusCodes.Status409Conflict, "Exception is already resolved", "already_resolved"),
            _ => Problem(StatusCodes.Status422UnprocessableEntity, "The action must be Acknowledged, NoActionNeeded or Escalated", "invalid_request"),
        };
    }

    private static IResult Problem(int status, string title, string reasonCode) =>
        TypedResults.Problem(title: title, statusCode: status, extensions: new Dictionary<string, object?> { ["reason"] = reasonCode });
}