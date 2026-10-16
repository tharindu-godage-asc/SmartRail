using System.Net;
using Xunit;

namespace SmartRail.IntegrationTests.Authentication;

/// <summary>Spec 08, section 4: CORS is opt-in, exact-origin, GET and POST only, no credentials.</summary>
public sealed class CorsTests : IDisposable
{
    private const string Web = "http://localhost:5173";
    private readonly TestTokenIssuer _issuer = new();

    public void Dispose() => _issuer.Dispose();

    private AuthApiFactory Api(string? origins) =>
        new(_issuer, settings: origins is null ? null : new Dictionary<string, string> { ["Cors:AllowedOrigins"] = origins });

    private static HttpRequestMessage Preflight(string origin, string method = "POST", string headers = "authorization,content-type")
    {
        var request = new HttpRequestMessage(HttpMethod.Options, "/api/v1/status");
        request.Headers.Add("Origin", origin);
        request.Headers.Add("Access-Control-Request-Method", method);
        request.Headers.Add("Access-Control-Request-Headers", headers);
        return request;
    }

    private static string? Header(HttpResponseMessage response, string name) =>
        response.Headers.TryGetValues(name, out var values) ? string.Join(",", values) : null;

    [Fact]
    public async Task The_allowed_origin_passes_the_preflight_and_may_send_the_token()
    {
        await using var api = Api(Web);
        using var client = api.CreateClient();

        var response = await client.SendAsync(Preflight(Web));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(Web, Header(response, "Access-Control-Allow-Origin"));
        Assert.Contains("POST", Header(response, "Access-Control-Allow-Methods"));
        Assert.Contains("Authorization", Header(response, "Access-Control-Allow-Headers"), StringComparison.OrdinalIgnoreCase);
        Assert.Null(Header(response, "Access-Control-Allow-Credentials")); // bearer tokens, not cookies
    }

    [Fact]
    public async Task A_real_request_from_the_allowed_origin_gets_the_header()
    {
        await using var api = Api(Web);
        using var client = api.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/status");
        request.Headers.Add("Origin", Web);

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(Web, Header(response, "Access-Control-Allow-Origin"));
    }

    [Fact]
    public async Task Another_origin_gets_no_cors_headers()
    {
        await using var api = Api(Web);
        using var client = api.CreateClient();

        var response = await client.SendAsync(Preflight("http://evil.example"));

        Assert.Null(Header(response, "Access-Control-Allow-Origin"));
    }

    [Theory]
    [InlineData("PUT")]
    [InlineData("DELETE")]
    public async Task Only_GET_and_POST_are_allowed(string method)
    {
        await using var api = Api(Web);
        using var client = api.CreateClient();

        var response = await client.SendAsync(Preflight(Web, method));

        // A browser refuses a method that the preflight answer does not list.
        var allowed = Header(response, "Access-Control-Allow-Methods") ?? "";
        Assert.DoesNotContain(method, allowed);
        Assert.Contains("GET", allowed);
    }

    [Fact]
    public async Task With_nothing_set_no_cors_headers_are_sent()
    {
        await using var api = Api(null);
        using var client = api.CreateClient();

        var response = await client.SendAsync(Preflight(Web));

        Assert.Null(Header(response, "Access-Control-Allow-Origin"));
    }

    [Theory]
    [InlineData("*")]
    [InlineData("http://localhost:5173/")]
    [InlineData("http://localhost:5173/app")]
    [InlineData("not a url")]
    public void A_wildcard_or_a_malformed_origin_stops_startup(string origin)
    {
        using var api = Api(origin);

        var error = Assert.ThrowsAny<Exception>(() => api.CreateClient());

        Assert.Contains("Cors:AllowedOrigins", error.ToString());
    }

    [Fact]
    public async Task Several_origins_can_be_given_comma_separated()
    {
        await using var api = Api($"{Web}, https://ops.example.org");
        using var client = api.CreateClient();

        var response = await client.SendAsync(Preflight("https://ops.example.org"));

        Assert.Equal("https://ops.example.org", Header(response, "Access-Control-Allow-Origin"));
    }
}
