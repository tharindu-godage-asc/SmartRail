using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace SmartRail.Api.Authentication;

public static class AuthenticationExtensions
{
    private const string LoggerCategory = "SmartRail.Auth";

    /// <summary>
    /// Secure by default: JWT bearer validation plus a fallback policy that requires an authenticated user on every endpoint
    /// that is not explicitly marked AllowAnonymous (R1). Roles come from the token's flat "roles" claim (R2).
    /// </summary>
    public static IServiceCollection AddSmartRailAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        var options = configuration.GetSection(AuthenticationOptions.Section).Get<AuthenticationOptions>() ?? new AuthenticationOptions();
        Validate(options);

        services.Configure<DiagnosticsOptions>(configuration.GetSection(DiagnosticsOptions.Section));

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(jwt => ConfigureJwtBearer(jwt, options));

        services.AddAuthorizationBuilder()
            .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build())
            .AddPolicy(Policies.Operator, policy => policy.RequireRole(Roles.Operator))
            .AddPolicy(Policies.Controller, policy => policy.RequireRole(Roles.Controller))
            .AddPolicy(Policies.Caterer, policy => policy.RequireRole(Roles.Caterer));

        return services;
    }

    /// <summary>R9: nothing configured is allowed (R7), half configured is a startup error.</summary>
    private static void Validate(AuthenticationOptions options)
    {
        var anySet = options.IsConfigured
                     || !string.IsNullOrWhiteSpace(options.Audience)
                     || !string.IsNullOrWhiteSpace(options.MetadataAddress);
        if (!anySet)
        {
            return;
        }

        if (!options.IsConfigured || string.IsNullOrWhiteSpace(options.Audience))
        {
            throw new InvalidOperationException(
                "Authentication is half configured: Authentication:Authority and Authentication:Audience must both be set " +
                "(or neither, which makes every protected endpoint answer 401).");
        }
    }

    private static void ConfigureJwtBearer(JwtBearerOptions jwt, AuthenticationOptions options)
    {
        jwt.MapInboundClaims = false; // keep the claim names exactly as they are in the token
        jwt.RequireHttpsMetadata = options.RequireHttpsMetadata;
        jwt.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ClockSkew = TimeSpan.FromSeconds(options.ClockSkewSeconds),
            NameClaimType = "sub",
            RoleClaimType = Roles.ClaimType,
        };

        if (options.IsConfigured)
        {
            // The issuer is the public address tokens were issued for; the keys may be fetched from an internal address.
            jwt.TokenValidationParameters.ValidIssuer = options.Authority;
            jwt.TokenValidationParameters.ValidAudience = options.Audience;
            jwt.MetadataAddress = options.MetadataAddress
                                  ?? $"{options.Authority!.TrimEnd('/')}/.well-known/openid-configuration";
        }
        else
        {
            // R7: no authority. An empty configuration has no signing keys, so every token is refused with 401 and nothing
            // tries to reach a server that is not there.
            jwt.Configuration = new OpenIdConnectConfiguration();
        }

        jwt.Events = new JwtBearerEvents
        {
            OnChallenge = async context =>
            {
                context.HandleResponse();
                var reason = context.AuthenticateFailure is null ? AuthMetrics.NoToken : AuthMetrics.InvalidToken;
                AuthMetrics.Reject(reason);
                Log(context.HttpContext, "Authentication refused", reason);

                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                context.Response.Headers.WWWAuthenticate = "Bearer";
                // R5: no detail about why the token was rejected.
                await WriteProblem(context.HttpContext, StatusCodes.Status401Unauthorized, "Unauthorized");
            },
            OnForbidden = async context =>
            {
                AuthMetrics.Reject(AuthMetrics.Forbidden);
                Log(context.HttpContext, "Authorisation refused", AuthMetrics.Forbidden);
                await WriteProblem(context.HttpContext, StatusCodes.Status403Forbidden, "Forbidden");
            },
        };
    }

    /// <summary>R6: subject (if present), endpoint and a reason code. Never the token, never the Authorization header.</summary>
    private static void Log(HttpContext http, string message, string reason)
    {
        var logger = http.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger(LoggerCategory);
        logger.LogWarning("{Message}: reason={Reason} subject={Subject} {Method} {Path}",
            message, reason, http.User.FindFirst("sub")?.Value ?? "-", http.Request.Method, http.Request.Path.Value);
    }

    private static Task WriteProblem(HttpContext http, int status, string title)
    {
        http.Response.StatusCode = status;
        http.Response.ContentType = "application/problem+json";
        return http.Response.WriteAsJsonAsync(
            new ProblemDetails { Status = status, Title = title },
            options: null,
            contentType: "application/problem+json");
    }
}
