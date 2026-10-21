using SmartRail.Api.Authentication;
using SmartRail.Api.Features.Journeys;
using SmartRail.Application.Features.PlatformChanges;

namespace SmartRail.Api.Features.PlatformChanges;

/// <summary>Body of the platform change: a platform code of the stop's station, or null to go back to the planned platform. A clash is advice that has to be acknowledged.</summary>
public sealed record ChangePlatformRequest(string? PlatformCode, bool AcknowledgeWarnings = false);

/// <summary>
/// A controller sets the platform of one stop of one journey (spec 20, FR-57). Controller role only; mapped with the other
/// database features. It records a decision and changes what displays and the apps show; it does not control any train.
/// </summary>
public static class PlatformChangeFeatures
{
    private const string LoggerCategory = "SmartRail.PlatformChanges";

    public static IServiceCollection AddPlatformChangeFeatures(this IServiceCollection services)
    {
        services.AddScoped<ChangePlatformHandler>();
        return services;
    }

    public static IEndpointRouteBuilder MapPlatformChangeFeatures(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/v1/journeys/{journeyId:guid}/stops/{serviceStopId:guid}/platform", Change)
           .WithName("ChangePlatform")
           .RequireAuthorization(Policies.Controller);
        return app;
    }

    private static async Task<IResult> Change(
        Guid journeyId, Guid serviceStopId, ChangePlatformRequest body, HttpContext http, ChangePlatformHandler handler, ILoggerFactory loggers, CancellationToken cancellationToken)
    {
        var subject = http.User.FindFirst("sub")?.Value;
        if (string.IsNullOrEmpty(subject))
        {
            return Problem(StatusCodes.Status403Forbidden, "Forbidden", "no_subject");
        }

        var result = await handler.HandleAsync(journeyId, serviceStopId, body.PlatformCode, body.AcknowledgeWarnings, subject, cancellationToken);
        switch (result.Error)
        {
            case PlatformChangeError.None:
                loggers.CreateLogger(LoggerCategory).LogInformation(
                    "Platform changed: journeyId={JourneyId} stopId={StopId} platform={Platform} controller={Controller}", journeyId, serviceStopId, LogSafe.Clean(result.Platform), LogSafe.Clean(subject));
                return TypedResults.Ok(new { journeyId, serviceStopId, platform = result.Platform, previousPlatform = result.PreviousPlatform, version = result.Version });

            case PlatformChangeError.WarningsNotAcknowledged:
                return TypedResults.Problem(
                    title: "Another train uses this platform at about the same time",
                    statusCode: StatusCodes.Status409Conflict,
                    extensions: new Dictionary<string, object?>
                    {
                        ["reason"] = "warnings_not_acknowledged",
                        ["warnings"] = result.Warnings!.Select(w => new { trainNumber = w.TrainNumber, minutesApart = w.MinutesApart, oppositeDirection = w.OppositeDirection }).ToList(),
                    });

            case PlatformChangeError.JourneyNotFound:
                return Problem(StatusCodes.Status404NotFound, "Journey not found", "journey_not_found");
            case PlatformChangeError.StopNotFound:
                return Problem(StatusCodes.Status404NotFound, "This stop is not a stop of the journey", "stop_not_found");
            case PlatformChangeError.JourneyNotChangeable:
                return Problem(StatusCodes.Status409Conflict, "A completed or cancelled journey cannot be changed", "journey_not_changeable");
            case PlatformChangeError.StopAlreadyDeparted:
                return Problem(StatusCodes.Status409Conflict, "The train has already left this stop", "stop_already_departed");
            case PlatformChangeError.PlatformNotAtStation:
                return Problem(StatusCodes.Status422UnprocessableEntity, "The platform is not a platform of this station", "platform_not_at_station");
            default:
                return Problem(StatusCodes.Status409Conflict, "The journey changed meanwhile: try again", "version_conflict");
        }
    }

    private static IResult Problem(int status, string title, string reasonCode) =>
        TypedResults.Problem(title: title, statusCode: status, extensions: new Dictionary<string, object?> { ["reason"] = reasonCode });
}
