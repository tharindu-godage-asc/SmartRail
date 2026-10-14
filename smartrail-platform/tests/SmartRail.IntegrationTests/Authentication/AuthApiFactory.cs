using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using SmartRail.Api.Authentication;

namespace SmartRail.IntegrationTests.Authentication;

/// <summary>
/// The real API with token validation pointed at <see cref="TestTokenIssuer"/>. It also maps throw-away endpoints, one per
/// policy, so 401 and 403 are proved without shipping any such endpoint in production code (spec decision Q5).
/// </summary>
public sealed class AuthApiFactory(
    TestTokenIssuer issuer,
    bool whoAmI = false,
    ILoggerProvider? logs = null,
    string? databaseConnection = null,
    TimeProvider? clock = null)
    : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("Authentication:Authority", TestTokenIssuer.Issuer);
        builder.UseSetting("Authentication:Audience", TestTokenIssuer.Audience);
        builder.UseSetting("Diagnostics:WhoAmI", whoAmI ? "true" : "false");
        if (databaseConnection is not null)
        {
            // A database switches the journey endpoints on (R14); the connection is the smartrail_app role.
            builder.UseSetting("ConnectionStrings:Smartrail", databaseConnection);
        }

        builder.ConfigureServices(services =>
        {
            // Trust the test key instead of fetching keys from a Keycloak that is not running.
            services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, jwt =>
            {
                var configuration = new OpenIdConnectConfiguration { Issuer = TestTokenIssuer.Issuer };
                configuration.SigningKeys.Add(issuer.SigningKey);
                jwt.Configuration = configuration;
                jwt.MetadataAddress = string.Empty;
                // JwtBearer has already built its key manager from the metadata address by now; replace it.
                jwt.ConfigurationManager = new StaticConfigurationManager<OpenIdConnectConfiguration>(configuration);
            });

            services.AddSingleton<IStartupFilter>(new TestEndpointsFilter());

            if (clock is not null)
            {
                services.AddSingleton(clock);
            }

            if (logs is not null)
            {
                services.AddLogging(logging => logging.SetMinimumLevel(LogLevel.Trace).AddProvider(logs));
            }
        });
    }

    private sealed class TestEndpointsFilter : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            next(app);
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapGet("/test/operator", () => Results.NoContent()).RequireAuthorization(Policies.Operator);
                endpoints.MapGet("/test/controller", () => Results.NoContent()).RequireAuthorization(Policies.Controller);
                endpoints.MapGet("/test/caterer", () => Results.NoContent()).RequireAuthorization(Policies.Caterer);
                endpoints.MapGet("/test/any-signed-in-user", () => Results.NoContent());
            });
        };
    }
}
