using Microsoft.AspNetCore.Mvc;
using SmartRail.Api.Authentication;
using SmartRail.Application.Features.OperatorHistory;

namespace SmartRail.Api.Features.OperatorHistory;

/// <summary>
/// The signed-in operator's previous journeys and the record of one of them (spec 12). Read-only, operator role only, and only ever the
/// operator's own journeys: the subject comes from the token, never from the request. Mapped only when a database is configured.
/// </summary>
public static class OperatorHistoryFeatures
{
    public static IServiceCollection AddOperatorHistoryFeatures(this IServiceCollection services)
    {
        services.AddScoped<GetOperatorHistoryHandler>();
        services.AddScoped<GetJourneyRecordHandler>();
        return services;
    }

    public static IEndpointRouteBuilder MapOperatorHistoryFeatures(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/operator/journeys/history", GetHistory).WithName("GetOperatorJourneyHistory").RequireAuthorization(Policies.Operator);
        app.MapGet("/api/v1/operator/journeys/{journeyId:guid}/record", GetRecord).WithName("GetOperatorJourneyRecord").RequireAuthorization(Policies.Operator);
        return app;
    }

    private static async Task<IResult> GetHistory(
        HttpContext http,
        GetOperatorHistoryHandler handler,
        [FromQuery(Name = "limit")] string? limit,
        [FromQuery(Name = "before")] string? before,
        CancellationToken cancellationToken)
    {
        var subject = http.User.FindFirst("sub")?.Value;
        if (string.IsNullOrEmpty(subject))
        {
            return Problem(StatusCodes.Status403Forbidden, "Forbidden", "no_subject");
        }

        var result = await handler.HandleAsync(subject, limit, before, cancellationToken);
        return result.Error switch
        {
            HistoryError.None => TypedResults.Ok(result.Response),
            HistoryError.InvalidLimit => Problem(StatusCodes.Status400BadRequest, "limit must be 1 to 50", "invalid_limit"),
            _ => Problem(StatusCodes.Status400BadRequest, "before must be a UTC instant", "invalid_before"),
        };
    }

    private static async Task<IResult> GetRecord(Guid journeyId, HttpContext http, GetJourneyRecordHandler handler, CancellationToken cancellationToken)
    {
        var subject = http.User.FindFirst("sub")?.Value;
        if (string.IsNullOrEmpty(subject))
        {
            return Problem(StatusCodes.Status403Forbidden, "Forbidden", "no_subject");
        }

        // The same answer for "no such journey" and "not yours", so nothing leaks (EC-27).
        var record = await handler.HandleAsync(subject, journeyId, cancellationToken);
        return record is null ? Problem(StatusCodes.Status404NotFound, "Journey not found", "journey_not_found") : TypedResults.Ok(record);
    }

    private static IResult Problem(int status, string title, string reasonCode) =>
        TypedResults.Problem(title: title, statusCode: status, extensions: new Dictionary<string, object?> { ["reason"] = reasonCode });
}
