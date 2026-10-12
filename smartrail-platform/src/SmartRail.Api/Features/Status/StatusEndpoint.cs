using SmartRail.Application.Features.Status;

namespace SmartRail.Api.Features.Status;

public static class StatusEndpoint
{
    public static IEndpointRouteBuilder MapStatus(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/status", (TimeProvider clock) => Results.Ok(GetServiceStatus.Handle(clock)))
           .WithName("GetServiceStatus");
        return app;
    }
}
