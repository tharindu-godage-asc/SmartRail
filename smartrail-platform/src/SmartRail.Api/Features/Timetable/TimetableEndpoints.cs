using Microsoft.AspNetCore.Mvc;
using SmartRail.Api.Authentication;
using SmartRail.Api.Features.Journeys;
using SmartRail.Application.Features.Timetable;
using SmartRail.Domain.Scheduling;

namespace SmartRail.Api.Features.Timetable;

public sealed record StopBody(string? StationCode, string? Arrival, string? Departure, string? Platform);

/// <summary>The body of validate and create (spec 13 section 4.5). Fields are nullable so a missing one is a validation answer, not a crash.</summary>
public sealed record ServiceBody(
    string? Kind,
    string? TrainNumber,
    string? LineCode,
    string? Category,
    string? ServiceType,
    string[]? Days,
    DateOnly[]? Dates,
    DateOnly? EffectiveFrom,
    StopBody[]? Stops,
    bool? AcknowledgeWarnings);

/// <summary>
/// The controller's timetable: read the weekly and extra services and one service, validate a draft, and create a weekly service or an
/// extra train (spec 13 S1 and S2). Controller role only. Mapped only when a database is configured.
/// </summary>
public static class TimetableFeatures
{
    public static IServiceCollection AddTimetableFeatures(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton(configuration.GetSection(SchedulingSettings.Section).Get<SchedulingSettings>() ?? new SchedulingSettings());
        services.AddScoped<GetOptionsHandler>();
        services.AddScoped<ListServicesHandler>();
        services.AddScoped<GetServiceHandler>();
        services.AddScoped<ValidateServiceHandler>();
        services.AddScoped<CreateServiceHandler>();
        services.AddScoped<JourneyGenerationHandler>();
        services.AddHostedService<TimetableGeneratorService>();
        return services;
    }

    public static IEndpointRouteBuilder MapTimetableFeatures(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/timetable").RequireAuthorization(Policies.Controller);
        group.MapGet("/options", GetOptions).WithName("GetTimetableOptions");
        group.MapGet("/services", ListServices).WithName("ListTimetableServices");
        group.MapGet("/services/{serviceId:guid}", GetService).WithName("GetTimetableService");
        group.MapPost("/validate", Validate).WithName("ValidateTimetableService");
        group.MapPost("/services", Create).WithName("CreateTimetableService");
        return app;
    }

    private static async Task<IResult> GetOptions(GetOptionsHandler handler, [FromQuery(Name = "line")] string? line, CancellationToken cancellationToken)
    {
        var options = await handler.HandleAsync(line ?? "MAIN", cancellationToken);
        return options is null ? Problem(StatusCodes.Status404NotFound, "Line not found", "line_not_found") : TypedResults.Ok(options);
    }

    private static async Task<IResult> ListServices(
        ListServicesHandler handler, [FromQuery(Name = "line")] string? line, [FromQuery(Name = "tab")] string? tab, CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(line, tab, cancellationToken);
        return result.Error switch
        {
            ListError.None => TypedResults.Ok(result.Rows),
            ListError.LineNotFound => Problem(StatusCodes.Status404NotFound, "Line not found", "line_not_found"),
            _ => Problem(StatusCodes.Status422UnprocessableEntity, "tab must be weekly, extra or cancelled", "invalid_tab"),
        };
    }

    private static async Task<IResult> GetService(Guid serviceId, GetServiceHandler handler, CancellationToken cancellationToken)
    {
        var detail = await handler.HandleAsync(serviceId, cancellationToken);
        return detail is null ? Problem(StatusCodes.Status404NotFound, "Service not found", "service_not_found") : TypedResults.Ok(detail);
    }

    private static async Task<IResult> Validate(ServiceBody body, ValidateServiceHandler handler, CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(ToDraft(body), cancellationToken);
        return result is null
            ? Problem(StatusCodes.Status404NotFound, "Line not found", "line_not_found")
            : TypedResults.Ok(new { valid = result.Valid, errors = result.Errors, warnings = result.Warnings });
    }

    private static async Task<IResult> Create(
        ServiceBody body, HttpContext http, CreateServiceHandler handler, ILoggerFactory loggers, CancellationToken cancellationToken)
    {
        var subject = http.User.FindFirst("sub")?.Value;
        if (string.IsNullOrEmpty(subject))
        {
            return Problem(StatusCodes.Status403Forbidden, "Forbidden", "no_subject");
        }

        var result = await handler.HandleAsync(ToDraft(body), body.AcknowledgeWarnings == true, subject, cancellationToken);
        switch (result.Error)
        {
            case CreateError.None:
                loggers.CreateLogger("SmartRail.Timetable").LogInformation(
                    "Service {TrainNumber} created by {Actor}: {Services} service rows, {Journeys} journeys.",
                    LogSafe.Clean(result.TrainNumber), LogSafe.Clean(subject), result.ServiceIds.Count, result.JourneysGenerated);
                return TypedResults.Created(
                    $"/api/v1/timetable/services/{result.ServiceIds[0]}",
                    new { serviceIds = result.ServiceIds, trainNumber = result.TrainNumber, version = 1, journeysGenerated = result.JourneysGenerated, warnings = result.Validation!.Warnings });
            case CreateError.LineNotFound:
                return Problem(StatusCodes.Status404NotFound, "Line not found", "line_not_found");
            case CreateError.Invalid:
                return TypedResults.Problem(
                    title: "The service is not valid", statusCode: StatusCodes.Status422UnprocessableEntity,
                    extensions: new Dictionary<string, object?> { ["reason"] = "invalid_service", ["errors"] = result.Validation!.Errors, ["warnings"] = result.Validation.Warnings });
            case CreateError.WarningsNotAcknowledged:
                return TypedResults.Problem(
                    title: "The service has warnings to confirm", statusCode: StatusCodes.Status409Conflict,
                    extensions: new Dictionary<string, object?> { ["reason"] = "warnings_not_acknowledged", ["warnings"] = result.Validation!.Warnings });
            default:
                return TypedResults.Problem(
                    title: $"Train {result.ClashTrainNumber} already runs on these dates", statusCode: StatusCodes.Status409Conflict,
                    extensions: new Dictionary<string, object?> { ["reason"] = "train_number_in_use", ["trainNumber"] = result.ClashTrainNumber });
        }
    }

    private static ServiceDraft ToDraft(ServiceBody body) => new(
        string.Equals(body.Kind, "Extra", StringComparison.OrdinalIgnoreCase) ? ServiceKind.Extra : ServiceKind.Weekly,
        body.TrainNumber?.Trim() ?? "",
        (body.LineCode ?? "MAIN").Trim().ToUpperInvariant(),
        body.Category ?? "",
        body.ServiceType ?? "",
        body.Days ?? [],
        body.Dates ?? [],
        body.EffectiveFrom,
        (body.Stops ?? []).Select(s => new StopDraft(s.StationCode ?? "", s.Arrival, s.Departure, s.Platform)).ToList());

    private static IResult Problem(int status, string title, string reasonCode) =>
        TypedResults.Problem(title: title, statusCode: status, extensions: new Dictionary<string, object?> { ["reason"] = reasonCode });
}
