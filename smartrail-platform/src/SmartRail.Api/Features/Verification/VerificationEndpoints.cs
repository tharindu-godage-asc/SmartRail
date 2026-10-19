using SmartRail.Api.Authentication;
using SmartRail.Api.Features.Journeys;
using SmartRail.Application.Features.Verification;

namespace SmartRail.Api.Features.Verification;

/// <summary>Body of POST /api/v1/exceptions/{id}/verify: action is verify, change or reject; reasonCode is needed for change.</summary>
public sealed record VerifyRequest(string? Action, string? ReasonCode);

public sealed record BulkVerifyRequest(List<Guid>? Ids);

/// <summary>
/// Verify a delay reason (spec 16 F2, FR-52). Controller role only. Mapped with the other exception routes, so only with a database.
/// </summary>
public static class VerificationFeatures
{
    private const string LoggerCategory = "SmartRail.Verification";

    public static IServiceCollection AddVerificationFeatures(this IServiceCollection services)
    {
        services.AddScoped<VerifyReasonHandler>();
        return services;
    }

    public static IEndpointRouteBuilder MapVerificationFeatures(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/v1/exceptions/{id:guid}/verify", Verify).WithName("VerifyReason").RequireAuthorization(Policies.Controller);
        app.MapPost("/api/v1/exceptions/verify", VerifyMany).WithName("VerifyReasons").RequireAuthorization(Policies.Controller);
        return app;
    }

    private static async Task<IResult> Verify(
        Guid id, VerifyRequest body, HttpContext http, VerifyReasonHandler handler, ILoggerFactory loggers, CancellationToken cancellationToken)
    {
        var subject = http.User.FindFirst("sub")?.Value;
        if (string.IsNullOrEmpty(subject))
        {
            return Problem(StatusCodes.Status403Forbidden, "Forbidden", "no_subject");
        }

        var outcome = await handler.HandleAsync(id, body.Action, body.ReasonCode, subject, http.User.FindFirst("preferred_username")?.Value, cancellationToken);
        if (outcome is null)
        {
            return Problem(StatusCodes.Status422UnprocessableEntity, "action must be verify, change or reject", "invalid_request");
        }

        if (outcome.Kind is VerifyOutcomeKind.Verified or VerifyOutcomeKind.Rejected)
        {
            loggers.CreateLogger(LoggerCategory).LogInformation(
                "Reason {Outcome}: exceptionId={ExceptionId} journeyId={JourneyId} controller={Controller}", outcome.Kind, id, outcome.JourneyId, LogSafe.Clean(subject));
            return TypedResults.Ok(new { journeyId = outcome.JourneyId, verifiedAt = outcome.Kind == VerifyOutcomeKind.Verified ? outcome.At : null });
        }

        return outcome.Kind switch
        {
            VerifyOutcomeKind.NotFound => Problem(StatusCodes.Status404NotFound, "Exception not found", "exception_not_found"),
            VerifyOutcomeKind.AlreadyResolved => Problem(StatusCodes.Status409Conflict, "Exception is already resolved", "already_resolved"),
            VerifyOutcomeKind.NotAVerificationException => Problem(StatusCodes.Status422UnprocessableEntity, "This exception has no reason to verify", "not_a_verification_exception"),
            _ => Problem(StatusCodes.Status422UnprocessableEntity, "Only an approved reason can be published", "reason_not_approved"),
        };
    }

    private static async Task<IResult> VerifyMany(
        BulkVerifyRequest body, HttpContext http, VerifyReasonHandler handler, ILoggerFactory loggers, CancellationToken cancellationToken)
    {
        var subject = http.User.FindFirst("sub")?.Value;
        if (string.IsNullOrEmpty(subject))
        {
            return Problem(StatusCodes.Status403Forbidden, "Forbidden", "no_subject");
        }

        if (body.Ids is not { Count: > 0 } ids || ids.Count > VerifyReasonHandler.MaxBulk)
        {
            return Problem(StatusCodes.Status422UnprocessableEntity, $"ids must hold 1 to {VerifyReasonHandler.MaxBulk} items", "invalid_request");
        }

        var results = await handler.HandleBulkAsync(ids, subject, http.User.FindFirst("preferred_username")?.Value, cancellationToken);
        loggers.CreateLogger(LoggerCategory).LogInformation(
            "Reasons verified in bulk: verified={Verified} skipped={Skipped} controller={Controller}",
            results.Count(r => r.Status == "verified"), results.Count(r => r.Status != "verified"), LogSafe.Clean(subject));
        return TypedResults.Ok(new { results });
    }

    private static IResult Problem(int status, string title, string reasonCode) =>
        TypedResults.Problem(title: title, statusCode: status, extensions: new Dictionary<string, object?> { ["reason"] = reasonCode });
}
