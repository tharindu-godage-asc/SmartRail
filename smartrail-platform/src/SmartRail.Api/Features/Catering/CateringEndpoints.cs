using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;
using SmartRail.Application.Features.Catering;
using SmartRail.Infrastructure.Catering;

namespace SmartRail.Api.Features.Catering;

/// <summary>
/// The food menu read for the passenger app (spec 11). The menu itself comes from SmallBites, the caterer's own system; SmartRail only
/// reads it and never sends it anything (D-72). Mapped only when a database is configured, like the other passenger reads.
/// </summary>
public static class CateringFeatures
{
    public static IServiceCollection AddCateringFeatures(this IServiceCollection services, IConfiguration configuration)
    {
        var settings = configuration.GetSection(CateringSettings.Section).Get<CateringSettings>() ?? new CateringSettings();
        services.AddSingleton(settings);
        services.AddSingleton<IMenuSource>(sp => new SmallBitesMenuSource(
            new HttpClient { Timeout = TimeSpan.FromSeconds(3) },
            settings,
            sp.GetRequiredService<TimeProvider>(),
            sp.GetRequiredService<ILogger<SmallBitesMenuSource>>()));
        services.AddScoped<ITripStops, TripStops>();
        services.AddScoped<GetTrainMenuHandler>();
        return services;
    }

    public static IEndpointRouteBuilder MapCateringFeatures(this IEndpointRouteBuilder app)
    {
        // Public like the other passenger reads; on the allow-list in AuthConfigurationTests.
        app.MapGet("/api/v1/trains/{trainNumber}/menu", GetTrainMenu).WithName("GetTrainMenu").AllowAnonymous();
        return app;
    }

    private static async Task<IResult> GetTrainMenu(
        string trainNumber,
        HttpContext http,
        GetTrainMenuHandler handler,
        [FromQuery(Name = "from")] string? from,
        [FromQuery(Name = "to")] string? to,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(trainNumber, from, to, cancellationToken);
        http.Response.Headers[HeaderNames.CacheControl] = "public, max-age=15";
        return result.Error switch
        {
            MenuError.None => TypedResults.Ok(result.Response),
            MenuError.TrainNotFound => TypedResults.Problem(statusCode: StatusCodes.Status404NotFound, title: "Train not found", extensions: Code("train_not_found")),
            MenuError.StationNotFound => TypedResults.Problem(statusCode: StatusCodes.Status404NotFound, title: "Station not found on this train", extensions: Code("station_not_found")),
            _ => TypedResults.Problem(statusCode: StatusCodes.Status422UnprocessableEntity, title: "From must come before to", extensions: Code("invalid_stop_order")),
        };
    }

    private static Dictionary<string, object?> Code(string reason) => new() { ["reason"] = reason };
}
