using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace SmartRail.IntegrationTests.Authentication;

/// <summary>AC-E01-03 (1, 3), EC-26 and the expired / wrong-audience / wrong-issuer / wrong-signature cases (spec sections 8 and 9).</summary>
public sealed class TokenValidationTests : IDisposable
{
    private readonly TestTokenIssuer _issuer = new();
    private readonly AuthApiFactory _api;

    public TokenValidationTests() => _api = new AuthApiFactory(_issuer);

    public void Dispose()
    {
        _api.Dispose();
        _issuer.Dispose();
    }

    private async Task<HttpResponseMessage> Get(string path, string? token = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        if (token is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return await _api.CreateClient().SendAsync(request);
    }

    [Fact]
    public async Task No_token_gets_401_with_no_detail()
    {
        var response = await Get("/test/operator");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("detail", body, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("Bearer", response.Headers.WwwAuthenticate.ToString());
    }

    [Fact]
    public async Task A_signed_in_user_passes_the_default_policy_and_nothing_else_is_needed()
    {
        Assert.Equal(HttpStatusCode.NoContent, (await Get("/test/any-signed-in-user", _issuer.Operator())).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Get("/test/any-signed-in-user")).StatusCode);
    }

    [Theory]
    [InlineData("operator", "/test/operator", HttpStatusCode.NoContent)]
    [InlineData("operator", "/test/controller", HttpStatusCode.Forbidden)]
    [InlineData("operator", "/test/caterer", HttpStatusCode.Forbidden)]
    [InlineData("controller", "/test/controller", HttpStatusCode.NoContent)]
    [InlineData("controller", "/test/operator", HttpStatusCode.Forbidden)]
    [InlineData("caterer", "/test/caterer", HttpStatusCode.NoContent)]
    [InlineData("caterer", "/test/operator", HttpStatusCode.Forbidden)]
    [InlineData("caterer", "/test/controller", HttpStatusCode.Forbidden)]
    public async Task Each_role_reaches_only_its_own_endpoints(string role, string path, HttpStatusCode expected)
    {
        var token = role switch
        {
            "operator" => _issuer.Operator(),
            "controller" => _issuer.Controller(),
            _ => _issuer.Caterer(),
        };

        var response = await Get(path, token);

        Assert.Equal(expected, response.StatusCode);
        if (expected == HttpStatusCode.Forbidden)
        {
            Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        }
    }

    [Fact]
    public async Task A_valid_token_with_no_roles_is_signed_in_but_forbidden_on_role_endpoints()
    {
        var token = _issuer.Create(roles: []);

        Assert.Equal(HttpStatusCode.NoContent, (await Get("/test/any-signed-in-user", token)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Get("/test/operator", token)).StatusCode);
    }

    [Fact]
    public async Task An_expired_token_gets_401()
    {
        // Issued 2 hours ago, valid for 15 minutes: expired long before the 30 second clock skew.
        var token = _issuer.Create(issuedAt: DateTime.UtcNow.AddHours(-2), lifetime: TimeSpan.FromMinutes(15));

        Assert.Equal(HttpStatusCode.Unauthorized, (await Get("/test/operator", token)).StatusCode);
    }

    [Fact]
    public async Task A_token_expired_within_the_clock_skew_is_still_accepted_and_one_beyond_it_is_not()
    {
        var justExpired = _issuer.Create(issuedAt: DateTime.UtcNow.AddSeconds(-15 * 60 - 10), lifetime: TimeSpan.FromMinutes(15));
        var longExpired = _issuer.Create(issuedAt: DateTime.UtcNow.AddSeconds(-15 * 60 - 120), lifetime: TimeSpan.FromMinutes(15));

        Assert.Equal(HttpStatusCode.NoContent, (await Get("/test/operator", justExpired)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Get("/test/operator", longExpired)).StatusCode);
    }

    [Fact]
    public async Task A_token_for_another_audience_gets_401()
    {
        var token = _issuer.Create(audience: "some-other-api");

        Assert.Equal(HttpStatusCode.Unauthorized, (await Get("/test/operator", token)).StatusCode);
    }

    [Fact]
    public async Task A_token_from_another_issuer_gets_401()
    {
        var token = _issuer.Create(issuer: "https://other-realm.example/realms/other");

        Assert.Equal(HttpStatusCode.Unauthorized, (await Get("/test/operator", token)).StatusCode);
    }

    [Fact]
    public async Task A_token_signed_with_an_unknown_key_gets_401()
    {
        using var rogue = RSA.Create(2048);
        var token = _issuer.Create(signWith: new RsaSecurityKey(rogue) { KeyId = "test-key" });

        Assert.Equal(HttpStatusCode.Unauthorized, (await Get("/test/operator", token)).StatusCode);
    }

    [Fact]
    public async Task An_unsigned_token_gets_401()
    {
        var header = Base64UrlEncoder.Encode("""{"alg":"none","typ":"JWT"}""");
        var payload = Base64UrlEncoder.Encode(
            $$"""{"iss":"{{TestTokenIssuer.Issuer}}","aud":"{{TestTokenIssuer.Audience}}","sub":"x","roles":["operator"],"exp":{{DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds()}}}""");

        Assert.Equal(HttpStatusCode.Unauthorized, (await Get("/test/operator", $"{header}.{payload}.")).StatusCode);
    }

    [Fact]
    public async Task Garbage_in_the_authorization_header_gets_401()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await Get("/test/operator", "not-a-token")).StatusCode);
    }

    [Theory]
    [InlineData("/api/v1/status")]
    [InlineData("/health/live")]
    [InlineData("/health/ready")]
    public async Task Public_endpoints_work_with_and_without_a_token(string path)
    {
        Assert.Equal(HttpStatusCode.OK, (await Get(path)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Get(path, _issuer.Operator())).StatusCode);
    }

    [Fact]
    public async Task A_bad_token_on_a_public_endpoint_does_not_block_it()
    {
        Assert.Equal(HttpStatusCode.OK, (await Get("/api/v1/status", "not-a-token")).StatusCode);
    }

    [Fact]
    public async Task The_client_helper_reads_a_problem_body()
    {
        var response = await Get("/test/controller", _issuer.Operator());

        var problem = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        Assert.NotNull(problem);
        Assert.Equal("Forbidden", problem["title"].ToString());
        Assert.Equal("403", problem["status"].ToString());
    }
}
