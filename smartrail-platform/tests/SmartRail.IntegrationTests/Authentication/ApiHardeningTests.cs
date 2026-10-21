using System.Net;
using System.Net.Http.Headers;
using Xunit;

namespace SmartRail.IntegrationTests.Authentication;

/// <summary>An error nobody handled is a problem answer without details, health probes stay out of traces' way, and the API description is opt-in.</summary>
public sealed class ApiHardeningTests : IDisposable
{
    private readonly TestTokenIssuer _issuer = new();

    public void Dispose() => _issuer.Dispose();

    [Fact]
    public async Task An_unhandled_error_is_a_500_problem_answer_that_does_not_leak_the_exception()
    {
        await using var factory = new AuthApiFactory(_issuer);
        using var client = factory.CreateClient();

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _issuer.Operator());

        var response = await client.GetAsync("/test/boom");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("\"status\":500", body);
        Assert.DoesNotContain("secret detail", body);
        Assert.DoesNotContain("InvalidOperationException", body);
    }

    [Fact]
    public async Task The_api_description_is_served_without_sign_in_only_when_switched_on()
    {
        await using var on = new AuthApiFactory(_issuer, settings: new Dictionary<string, string> { ["OpenApi:Enabled"] = "true" });
        using var client = on.CreateClient();
        var served = await client.GetAsync("/openapi/v1.json");
        Assert.Equal(HttpStatusCode.OK, served.StatusCode);
        Assert.Contains("\"openapi\"", await served.Content.ReadAsStringAsync());

        await using var off = new AuthApiFactory(_issuer, settings: new Dictionary<string, string> { ["OpenApi:Enabled"] = "false" });
        using var other = off.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await other.GetAsync("/openapi/v1.json")).StatusCode); // no such public route: the secure-by-default policy answers
    }

    [Fact]
    public async Task Health_probes_still_answer_without_a_token()
    {
        await using var factory = new AuthApiFactory(_issuer);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = null;

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/live")).StatusCode);
    }
}
