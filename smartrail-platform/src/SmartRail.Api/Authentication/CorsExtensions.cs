namespace SmartRail.Api.Authentication;

/// <summary>
/// Cross-origin access for the controller web app (spec 08). Off unless <c>Cors:AllowedOrigins</c> is set, so the cluster is
/// unchanged. The app sends bearer tokens, not cookies, so credentials are never allowed; only GET and POST and the two headers
/// it needs; and "*" is refused at startup.
/// </summary>
public static class CorsExtensions
{
    public const string Section = "Cors:AllowedOrigins";
    private const string PolicyName = "smartrail-web";

    public static IServiceCollection AddSmartRailCors(this IServiceCollection services, IConfiguration configuration)
    {
        var origins = AllowedOrigins(configuration);
        if (origins.Length == 0)
        {
            return services;
        }

        services.AddCors(options => options.AddPolicy(PolicyName, policy => policy
            .WithOrigins(origins)
            .WithMethods("GET", "POST")
            .WithHeaders("Authorization", "Content-Type")
            .SetPreflightMaxAge(TimeSpan.FromMinutes(10))));
        return services;
    }

    /// <summary>Before authentication, so a preflight (which carries no token) is answered, not refused with 401.</summary>
    public static IApplicationBuilder UseSmartRailCors(this WebApplication app) =>
        AllowedOrigins(app.Configuration).Length == 0 ? app : app.UseCors(PolicyName);

    private static string[] AllowedOrigins(IConfiguration configuration)
    {
        // Accepts a list (Cors__AllowedOrigins__0) or one comma-separated value.
        var section = configuration.GetSection(Section);
        var origins = (section.Get<string[]>() ?? [])
            .Append(section.Value ?? "")
            .SelectMany(value => value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        foreach (var origin in origins)
        {
            if (origin == "*" || !Uri.TryCreate(origin, UriKind.Absolute, out var uri) || uri.AbsolutePath != "/" || origin.EndsWith('/'))
            {
                throw new InvalidOperationException(
                    $"{Section} must list exact origins such as http://localhost:5173 (no '*', no path, no trailing slash); got '{origin}'.");
            }
        }

        return origins;
    }
}
