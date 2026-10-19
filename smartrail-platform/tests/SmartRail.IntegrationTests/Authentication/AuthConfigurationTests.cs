using System.Diagnostics.Metrics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SmartRail.Api.Features.Diagnostics;
using SmartRail.IntegrationTests.Database;
using Xunit;

namespace SmartRail.IntegrationTests.Authentication;

/// <summary>Rules R1, R6, R7, R9 and the metric (spec sections 6, 9 and 10.3).</summary>
[Collection(PostgresCollection.Name)]
public sealed class AuthConfigurationTests(PostgresFixture postgres) : IDisposable
{
    private readonly TestTokenIssuer _issuer = new();

    public void Dispose() => _issuer.Dispose();

    /// <summary>The only endpoints that may be anonymous. Adding one is a conscious decision made by editing this list.</summary>
    private static readonly string[] PublicEndpoints = ["/api/v1/status", "/health/live", "/health/ready"];

    /// <summary>The journey slice adds two public passenger reads and the passenger slice three more; all exist only when a database is configured (R14, R11).</summary>
    private static readonly string[] PublicEndpointsWithDatabase =
    [
        .. PublicEndpoints,
        "/api/v1/journeys/{journeyId:guid}/status",
        "/api/v1/trains/{trainNumber}/status",
        "/api/v1/stations",
        "/api/v1/trains",
        "/api/v1/lines/{code}/status",
        "/api/v1/stations/{code}/board",
        "/api/v1/trains/{trainNumber}/menu",
    ];

    [Fact]
    public async Task Every_endpoint_is_protected_unless_it_is_on_the_public_list()
    {
        await using var api = new AuthApiFactory(_issuer, whoAmI: true);
        using var scope = api.Services.CreateScope();

        var fallback = await scope.ServiceProvider.GetRequiredService<IAuthorizationPolicyProvider>().GetFallbackPolicyAsync();
        Assert.NotNull(fallback);
        Assert.Contains(fallback.Requirements, requirement => requirement is Microsoft.AspNetCore.Authorization.Infrastructure.DenyAnonymousAuthorizationRequirement);

        var anonymous = api.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Where(endpoint => endpoint.Metadata.GetMetadata<IAllowAnonymous>() is not null)
            .Select(endpoint => "/" + endpoint.RoutePattern.RawText!.TrimStart('/'))
            .Order()
            .ToArray();

        Assert.Equal(PublicEndpoints.Order().ToArray(), anonymous);
    }

    [Fact]
    public async Task With_a_database_exactly_five_more_endpoints_are_public_and_the_rest_stay_protected()
    {
        // The journey endpoints exist only when a database is configured, so this check must run with one (R14).
        var database = await postgres.CreateDatabaseAsync();
        await using var api = new AuthApiFactory(_issuer, whoAmI: true, databaseConnection: database.AppConnection);

        var endpoints = api.Services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>().ToList();
        var anonymous = endpoints
            .Where(endpoint => endpoint.Metadata.GetMetadata<IAllowAnonymous>() is not null)
            .Select(endpoint => "/" + endpoint.RoutePattern.RawText!.TrimStart('/'))
            .Order()
            .ToArray();

        Assert.Equal(PublicEndpointsWithDatabase.Order().ToArray(), anonymous);

        // The two staff routes are present and demand a policy, not just a signed-in user.
        foreach (var route in new[] { "/api/v1/operator/journey", "/api/v1/journeys/{journeyId:guid}/events", "/api/v1/operator/journeys/history", "/api/v1/operator/journeys/{journeyId:guid}/record" })
        {
            var endpoint = endpoints.Single(e => "/" + e.RoutePattern.RawText!.TrimStart('/') == route);
            Assert.Contains(endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>(), data => data.Policy == "Operator");
        }
    }

    [Fact]
    public async Task Without_a_database_the_journey_endpoints_do_not_exist_and_readiness_is_unchanged()
    {
        await using var api = new AuthApiFactory(_issuer);
        var client = api.CreateClient();

        var routes = api.Services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>()
            .Select(e => e.RoutePattern.RawText).ToList();
        Assert.DoesNotContain(routes, route => route!.Contains("/journeys/", StringComparison.Ordinal) || route.Contains("/trains", StringComparison.Ordinal) || route.Contains("/operator/", StringComparison.Ordinal) || route.Contains("/stations", StringComparison.Ordinal) || route.Contains("/lines/", StringComparison.Ordinal));

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/ready")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/status")).StatusCode);

        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/operator/journey");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _issuer.Operator());
        Assert.Equal(HttpStatusCode.NotFound, (await client.SendAsync(request)).StatusCode);
    }

    [Fact]
    public async Task Whoami_does_not_exist_unless_the_flag_is_on()
    {
        await using var off = new AuthApiFactory(_issuer, whoAmI: false);
        var withToken = new HttpRequestMessage(HttpMethod.Get, "/api/v1/whoami");
        withToken.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _issuer.Operator());

        // Not mapped at all: a valid token gets 404, not 200.
        Assert.Equal(HttpStatusCode.NotFound, (await off.CreateClient().SendAsync(withToken)).StatusCode);

        // The fallback policy also covers unknown routes, so without a token the answer is 401: a stranger cannot tell
        // which routes exist.
        Assert.Equal(HttpStatusCode.Unauthorized, (await off.CreateClient().GetAsync("/api/v1/whoami")).StatusCode);
    }

    [Fact]
    public async Task Whoami_returns_the_subject_and_roles_when_the_flag_is_on()
    {
        await using var on = new AuthApiFactory(_issuer, whoAmI: true);
        var client = on.CreateClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/whoami")).StatusCode);

        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/whoami");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _issuer.Operator());
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<WhoAmIResponse>();
        Assert.Equal("d0d0d0d0-0000-4000-8000-000000000001", body!.Subject);
        Assert.Equal(["operator"], body.Roles);
    }

    [Fact]
    public async Task With_no_authority_configured_the_public_endpoints_work_exactly_as_before()
    {
        // R7: the cluster today. No Authentication settings at all, and the app stays up and Ready.
        await using var api = new WebApplicationFactory<Program>();
        var client = api.CreateClient();

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/status")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/ready")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/live")).StatusCode);
    }

    [Fact]
    public async Task With_no_authority_a_protected_endpoint_refuses_every_token()
    {
        await using var api = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.UseSetting("Diagnostics:WhoAmI", "true"));
        var client = api.CreateClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/whoami")).StatusCode);

        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/whoami");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _issuer.Operator());
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.SendAsync(request)).StatusCode);
    }

    [Theory]
    [InlineData("Authentication:Authority", "https://issuer.example/realms/smartrail")]
    [InlineData("Authentication:Audience", "smartrail-api")]
    [InlineData("Authentication:MetadataAddress", "http://keycloak:8080/realms/smartrail/.well-known/openid-configuration")]
    public void A_half_configured_authority_fails_startup_with_a_clear_message(string key, string value)
    {
        // R9: only one of the required pair set (or only the metadata address) is a mistake, not "no authentication".
        using var api = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.UseSetting(key, value));

        var error = Assert.ThrowsAny<Exception>(() => api.CreateClient());

        var message = (error as InvalidOperationException ?? error.InnerException as InvalidOperationException)?.Message
                      ?? error.ToString();
        Assert.Contains("half configured", message);
    }

    [Fact]
    public async Task Rejections_are_counted_with_the_three_reasons()
    {
        var counts = new Dictionary<string, long>();
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, l) =>
        {
            if (instrument.Name == "smartrail.auth.rejected")
            {
                l.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((_, value, tags, _) =>
        {
            foreach (var tag in tags)
            {
                if (tag.Key == "reason")
                {
                    lock (counts)
                    {
                        counts[(string)tag.Value!] = counts.GetValueOrDefault((string)tag.Value!) + value;
                    }
                }
            }
        });
        listener.Start();

        await using var api = new AuthApiFactory(_issuer);
        var client = api.CreateClient();

        await client.GetAsync("/test/operator"); // no token
        var bad = new HttpRequestMessage(HttpMethod.Get, "/test/operator");
        bad.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _issuer.Create(audience: "x"));
        await client.SendAsync(bad); // invalid token
        var wrongRole = new HttpRequestMessage(HttpMethod.Get, "/test/controller");
        wrongRole.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _issuer.Operator());
        await client.SendAsync(wrongRole); // forbidden

        lock (counts)
        {
            Assert.Equal(1, counts["no_token"]);
            Assert.Equal(1, counts["invalid_token"]);
            Assert.Equal(1, counts["forbidden"]);
        }
    }

    [Fact]
    public async Task No_log_line_contains_a_token_or_an_authorization_header()
    {
        var logs = new CapturingLoggerProvider();
        await using var api = new AuthApiFactory(_issuer, logs: logs);
        var client = api.CreateClient();
        var tokens = new[]
        {
            _issuer.Operator(), // accepted
            _issuer.Create(audience: "x"), // wrong audience
            _issuer.Create(issuedAt: DateTime.UtcNow.AddHours(-2)), // expired
            _issuer.Create(issuer: "https://other.example"), // wrong issuer
        };

        foreach (var token in tokens)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, "/test/controller");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            await client.SendAsync(request);
        }

        var everything = string.Join('\n', logs.Messages);
        Assert.NotEmpty(logs.Messages);
        foreach (var token in tokens)
        {
            Assert.DoesNotContain(token, everything);
            Assert.DoesNotContain(token.Split('.')[1], everything); // the payload part either
        }

        Assert.DoesNotContain("Authorization:", everything, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Bearer eyJ", everything);
    }

    private sealed class CapturingLoggerProvider : ILoggerProvider
    {
        public List<string> Messages { get; } = [];

        public ILogger CreateLogger(string categoryName) => new Capture(this);

        public void Dispose()
        {
        }

        private sealed class Capture(CapturingLoggerProvider owner) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                lock (owner.Messages)
                {
                    owner.Messages.Add(formatter(state, exception));
                    if (exception is not null)
                    {
                        owner.Messages.Add(exception.ToString());
                    }
                }
            }
        }
    }
}
