using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;
using SmartRail.Api.Features.Journeys;
using SmartRail.Application.Features.Passenger;
using SmartRail.Domain.Journeys;

namespace SmartRail.Api.Features.Passenger;

/// <summary>
/// The passenger reads (spec passenger-find-trains): stations, trains between two stations, line status. All public (FR-05) and
/// read-only. Registered and mapped only when a database is configured (R11), like the journey endpoints.
/// </summary>
public static class PassengerFeatures
{
    private const string LoggerCategory = "SmartRail.Passenger";

    /// <summary>R10: a short public cache absorbs repeated polls and costs at most 5 of the 30 seconds in NFR-04. It cannot invalidate (D10).</summary>
    private const string SnapshotCache = "public, max-age=5";

    public static IServiceCollection AddPassengerFeatures(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton(configuration.GetSection(SearchSettings.Section).Get<SearchSettings>() ?? new SearchSettings());
        services.AddSingleton(configuration.GetSection(BoardSettings.Section).Get<BoardSettings>() ?? new BoardSettings());
        services.AddScoped<GetStationsHandler>();
        services.AddScoped<FindTrainsHandler>();
        services.AddScoped<GetLineStatusHandler>();
        services.AddScoped<GetStationBoardHandler>();
        return services;
    }

    public static IEndpointRouteBuilder MapPassengerFeatures(this IEndpointRouteBuilder app)
    {
        // Public (FR-05). All three are on the allow-list in AuthConfigurationTests.
        app.MapGet("/api/v1/stations", GetStations).WithName("GetStations").AllowAnonymous();
        app.MapGet("/api/v1/trains", FindTrains).WithName("FindTrains").AllowAnonymous();
        app.MapGet("/api/v1/lines/{code}/status", GetLineStatus).WithName("GetLineStatus").AllowAnonymous();
        app.MapGet("/api/v1/stations/{code}/board", GetStationBoard).WithName("GetStationBoard").AllowAnonymous(); // spec 09, for the station displays
        return app;
    }

    private static async Task<IResult> GetStations(HttpContext http, GetStationsHandler handler, CancellationToken cancellationToken)
    {
        var response = await handler.HandleAsync(cancellationToken);
        var tag = $"\"{response.Version}\"";

        // R1 and R12: the phone sends the version it has; 304 when any listed tag (weak or not, or "*") matches.
        http.Response.Headers[HeaderNames.ETag] = tag;
        http.Response.Headers[HeaderNames.CacheControl] = "no-cache";
        if (EntityTagMatcher.Matches(http.Request.Headers[HeaderNames.IfNoneMatch].ToString(), tag))
        {
            return TypedResults.StatusCode(StatusCodes.Status304NotModified);
        }

        return TypedResults.Ok(response);
    }

    private static async Task<IResult> FindTrains(
        HttpContext http,
        FindTrainsHandler handler,
        ILoggerFactory loggers,
        [FromQuery(Name = "from")] string? fromCode,
        [FromQuery(Name = "to")] string? toCode,
        [FromQuery(Name = "date")] string? date,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(fromCode, toCode, date, cancellationToken);
        http.Response.Headers[HeaderNames.CacheControl] = SnapshotCache;

        if (result.Error != SearchError.None)
        {
            var (status, title, code) = Describe(result.Error);
            return Problem(status, title, code);
        }

        // R6: an unsupported calendar is never guessed, and the owner is told. Train numbers come from the database.
        if (result.UnsupportedCalendarTrains.Count > 0)
        {
            loggers.CreateLogger(LoggerCategory).LogWarning(
                "Services with an unsupported runs_on text were left out of the timetable answer: {TrainNumbers}",
                string.Join(',', result.UnsupportedCalendarTrains.Select(LogSafe.Clean)));
        }

        return TypedResults.Ok(result.Response);
    }

    /// <summary>Spec 09: what is due at a station, optionally for one platform. Public (a display has no user) and cached for 5 seconds like the other snapshots.</summary>
    private static async Task<IResult> GetStationBoard(
        string code,
        HttpContext http,
        GetStationBoardHandler handler,
        [FromQuery(Name = "platform")] string? platform,
        [FromQuery(Name = "count")] string? count,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(code, platform, count, cancellationToken);
        http.Response.Headers[HeaderNames.CacheControl] = SnapshotCache;

        return result.Error switch
        {
            BoardError.None => TypedResults.Ok(result.Response),
            BoardError.StationNotFound => Problem(StatusCodes.Status404NotFound, "Station not found", "station_not_found"),
            BoardError.PlatformNotFound => Problem(StatusCodes.Status404NotFound, "Platform not found at this station", "platform_not_found"),
            _ => Problem(StatusCodes.Status422UnprocessableEntity, "count must be a whole number from 1 to the maximum", "invalid_request"),
        };
    }

    private static async Task<IResult> GetLineStatus(string code, HttpContext http, GetLineStatusHandler handler, CancellationToken cancellationToken)
    {
        var response = await handler.HandleAsync(code, cancellationToken);
        http.Response.Headers[HeaderNames.CacheControl] = SnapshotCache;
        return response is null
            ? Problem(StatusCodes.Status404NotFound, "Line not found", "line_not_found")
            : TypedResults.Ok(response);
    }

    private static (int Status, string Title, string Code) Describe(SearchError error) => error switch
    {
        SearchError.InvalidRequest => (StatusCodes.Status422UnprocessableEntity, "From and to are required", "invalid_request"),
        SearchError.SameStation => (StatusCodes.Status422UnprocessableEntity, "From and to must be different stations", "same_station"),
        SearchError.InvalidDate => (StatusCodes.Status422UnprocessableEntity, "The date must be written yyyy-MM-dd", "invalid_date"),
        SearchError.DateOutOfRange => (StatusCodes.Status422UnprocessableEntity, "The date is outside the allowed range", "date_out_of_range"),
        SearchError.StationNotFound => (StatusCodes.Status404NotFound, "Station not found", "station_not_found"),
        _ => (StatusCodes.Status500InternalServerError, "Error", "error"),
    };

    private static IResult Problem(int status, string title, string reasonCode) =>
        TypedResults.Problem(title: title, statusCode: status, extensions: new Dictionary<string, object?> { ["reason"] = reasonCode });
}
