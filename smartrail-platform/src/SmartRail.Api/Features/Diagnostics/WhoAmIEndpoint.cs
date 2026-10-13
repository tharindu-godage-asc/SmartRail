using SmartRail.Api.Authentication;

namespace SmartRail.Api.Features.Diagnostics;

public sealed record WhoAmIResponse(string? Subject, IReadOnlyList<string> Roles);

public static class WhoAmIEndpoint
{
    /// <summary>
    /// Returns who the token says you are. Mapped only when Diagnostics:WhoAmI is true (Compose turns it on); otherwise the
    /// route does not exist. Any authenticated user may call it; the fallback policy already requires that.
    /// </summary>
    public static IEndpointRouteBuilder MapWhoAmI(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/whoami", (HttpContext http) => TypedResults.Ok(new WhoAmIResponse(
                http.User.FindFirst("sub")?.Value,
                http.User.FindAll(Roles.ClaimType).Select(claim => claim.Value).ToList())))
           .WithName("WhoAmI");
        return app;
    }
}
