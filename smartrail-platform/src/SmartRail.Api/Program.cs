using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.OpenApi;
using Npgsql;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using SmartRail.Api.Authentication;
using SmartRail.Api.Features.Diagnostics;
using SmartRail.Api.Features.Exceptions;
using SmartRail.Api.Features.Journeys;
using SmartRail.Api.Features.Catering;
using SmartRail.Api.Features.OperatorHistory;
using SmartRail.Api.Features.Assignments;
using SmartRail.Api.Features.Timetable;
using SmartRail.Api.Features.Passenger;
using SmartRail.Application.Features.Passenger;
using SmartRail.Application.Features.Events.ReportEvent;
using SmartRail.Application.Features.Exceptions;
using SmartRail.Application.Features.Verification;
using SmartRail.Api.Features.Verification;
using SmartRail.Api.Features.ControllerJourneys;
using SmartRail.Api.Features.PlatformChanges;
using SmartRail.Api.Features.Status;
using SmartRail.Infrastructure;

var builder = WebApplication.CreateBuilder(args);
var env = builder.Configuration["DEPLOYMENT_ENVIRONMENT"] ?? builder.Environment.EnvironmentName;

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddSmartRailAuthentication(builder.Configuration);
builder.Services.AddSmartRailCors(builder.Configuration);

// The journey endpoints need a database. Without one (the cluster today) they are not registered or mapped at all (R14).
var databaseConfigured = !string.IsNullOrWhiteSpace(builder.Configuration.GetConnectionString("Smartrail"));
if (databaseConfigured)
{
    builder.Services.AddJourneyFeatures(builder.Configuration);
    builder.Services.AddPassengerFeatures(builder.Configuration);
    builder.Services.AddCateringFeatures(builder.Configuration);
    builder.Services.AddOperatorHistoryFeatures();
    builder.Services.AddTimetableFeatures(builder.Configuration);
    builder.Services.AddAssignmentFeatures(builder.Configuration);
    builder.Services.AddExceptionFeatures();
    builder.Services.AddVerificationFeatures();
    builder.Services.AddControllerJourneyFeatures();
    builder.Services.AddPlatformChangeFeatures();
}

builder.Services.AddHealthChecks();

// Errors nobody handled become an RFC 9457 problem answer (500, no stack trace) instead of an empty body.
builder.Services.AddProblemDetails();

// The API description (OpenAPI JSON) is for exploring and testing the API: on only when OpenApi:Enabled is true (Compose sets it), off everywhere else.
var openApiEnabled = builder.Configuration.GetValue<bool>("OpenApi:Enabled");
if (openApiEnabled)
{
    // A bearer scheme in the description gives Swagger UI an Authorize button: paste a token (scripts/get-token.sh nimal) and try the routes.
    builder.Services.AddOpenApi(options => options.AddDocumentTransformer((document, _, _) =>
    {
        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes = new Dictionary<string, IOpenApiSecurityScheme>
        {
            ["Bearer"] = new OpenApiSecurityScheme { Type = SecuritySchemeType.Http, Scheme = "bearer", BearerFormat = "JWT", Description = "A Keycloak access token (dev: scripts/get-token.sh nimal | jayasinghe)." },
        };
        document.Security = [new OpenApiSecurityRequirement { [new OpenApiSecuritySchemeReference("Bearer", document)] = [] }];
        return Task.CompletedTask;
    }));
}

// One instrumentation (OpenTelemetry) for metrics, traces and logs. The environment name is a resource
// attribute so every signal can be filtered by dev, test, staging or prod in Grafana.
builder.Services.AddOpenTelemetry()
    .ConfigureResource(r => r.AddService("smartrail-api").AddAttributes(new Dictionary<string, object>
        { ["deployment.environment"] = env }))
    .WithMetrics(m => m.AddAspNetCoreInstrumentation().AddHttpClientInstrumentation().AddRuntimeInstrumentation().AddMeter(AuthMetrics.MeterName).AddMeter(EventMetrics.MeterName).AddMeter(SearchMetrics.MeterName).AddMeter(BoardMetrics.MeterName).AddMeter(ExceptionMetrics.MeterName).AddMeter(SmartRail.Application.Features.PlatformChanges.PlatformChangeMetrics.MeterName).AddOtlpExporter())
    .WithTracing(t => t.AddAspNetCoreInstrumentation(o => o.Filter = context => !context.Request.Path.StartsWithSegments("/health")).AddHttpClientInstrumentation().AddNpgsql().AddOtlpExporter());
builder.Logging.AddOpenTelemetry(o => { o.IncludeScopes = true; o.AddOtlpExporter(); });

var app = builder.Build();
app.UseExceptionHandler();
app.UseSmartRailCors();
if (openApiEnabled)
{
    // Swagger UI at /swagger reads that description. It is a page of static files (not an endpoint), so it holds no data and needs no sign-in.
    app.UseSwaggerUI(o =>
    {
        o.SwaggerEndpoint("/openapi/v1.json", "SmartRail API v1");
        o.RoutePrefix = "swagger";
        o.DocumentTitle = "SmartRail API";
        o.EnablePersistAuthorization();
    });
}

app.UseAuthentication();
app.UseAuthorization();

// Secure by default: every endpoint requires a signed-in user unless it is marked AllowAnonymous (the public list).
// Liveness runs no checks (the process is up). Readiness runs only the checks tagged "ready", for example the database check,
// which exists only when a database is configured.
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false }).AllowAnonymous();
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") }).AllowAnonymous();
app.MapStatus();
if (databaseConfigured)
{
    app.MapJourneyFeatures();
    app.MapPassengerFeatures();
    app.MapCateringFeatures();
    app.MapOperatorHistoryFeatures();
    app.MapTimetableFeatures();
    app.MapAssignmentFeatures();
    app.MapExceptionFeatures();
    app.MapVerificationFeatures();
    app.MapControllerJourneyFeatures();
    app.MapPlatformChangeFeatures();
}

if (openApiEnabled)
{
    // Public on purpose: it describes the API, it holds no data. Served at /openapi/v1.json.
    app.MapOpenApi().AllowAnonymous();
}

if (app.Configuration.GetValue<bool>("Diagnostics:WhoAmI"))
{
    app.MapWhoAmI();
}

app.Run();

public partial class Program;
