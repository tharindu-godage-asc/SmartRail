using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using SmartRail.Api.Features.Status;
using SmartRail.Infrastructure;

var builder = WebApplication.CreateBuilder(args);
var env = builder.Configuration["DEPLOYMENT_ENVIRONMENT"] ?? builder.Environment.EnvironmentName;

builder.Services.AddInfrastructure();
builder.Services.AddHealthChecks();

// One instrumentation (OpenTelemetry) for metrics, traces and logs. The environment name is a resource
// attribute so every signal can be filtered by dev, test, staging or prod in Grafana.
builder.Services.AddOpenTelemetry()
    .ConfigureResource(r => r.AddService("smartrail-api").AddAttributes(new Dictionary<string, object>
        { ["deployment.environment"] = env }))
    .WithMetrics(m => m.AddAspNetCoreInstrumentation().AddHttpClientInstrumentation().AddRuntimeInstrumentation().AddOtlpExporter())
    .WithTracing(t => t.AddAspNetCoreInstrumentation().AddHttpClientInstrumentation().AddOtlpExporter());
builder.Logging.AddOpenTelemetry(o => { o.IncludeScopes = true; o.AddOtlpExporter(); });

var app = builder.Build();
app.MapHealthChecks("/health/live");
app.MapHealthChecks("/health/ready");
app.MapStatus();
app.Run();

public partial class Program;
