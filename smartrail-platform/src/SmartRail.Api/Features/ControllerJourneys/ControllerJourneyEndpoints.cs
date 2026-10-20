using SmartRail.Api.Authentication;
using SmartRail.Application.Features.ControllerJourneys;

namespace SmartRail.Api.Features.ControllerJourneys;

/// <summary>
/// The controller's read of one journey (spec 16 F1): stops, state, events with who made them, the operator and the reason. Read only.
/// Controller role only; mapped only when a database is configured.
/// </summary>
public static class ControllerJourneyFeatures
{
    public static IServiceCollection AddControllerJourneyFeatures(this IServiceCollection services)
    {
        services.AddScoped<GetControllerJourneyHandler>();
        return services;
    }

    public static IEndpointRouteBuilder MapControllerJourneyFeatures(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/controller/journeys/{journeyId:guid}", Get)
           .WithName("GetControllerJourney")
           .RequireAuthorization(Policies.Controller);
        return app;
    }

    private static async Task<IResult> Get(Guid journeyId, GetControllerJourneyHandler handler, CancellationToken cancellationToken)
    {
        var detail = await handler.HandleAsync(journeyId, cancellationToken);
        return detail is null
            ? TypedResults.Problem(title: "Journey not found", statusCode: StatusCodes.Status404NotFound, extensions: new Dictionary<string, object?> { ["reason"] = "journey_not_found" })
            : TypedResults.Ok(detail);
    }
}
