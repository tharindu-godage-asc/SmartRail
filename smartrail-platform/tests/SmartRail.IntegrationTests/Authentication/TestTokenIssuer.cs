using System.Security.Cryptography;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace SmartRail.IntegrationTests.Authentication;

/// <summary>
/// A stand-in for Keycloak in the fast tests: it signs tokens with its own RSA key, and the test API trusts that key with
/// the same validation settings as production. CI therefore needs no Keycloak container (spec section 10.3).
/// </summary>
public sealed class TestTokenIssuer : IDisposable
{
    public const string Issuer = "https://test-issuer.example/realms/smartrail";
    public const string Audience = "smartrail-api";

    private readonly RSA _rsa = RSA.Create(2048);

    public TestTokenIssuer() => SigningKey = new RsaSecurityKey(_rsa) { KeyId = "test-key" };

    public SecurityKey SigningKey { get; }

    public string Create(
        string subject = "d0d0d0d0-0000-4000-8000-000000000001",
        string[]? roles = null,
        string audience = Audience,
        string issuer = Issuer,
        TimeSpan? lifetime = null,
        DateTime? issuedAt = null,
        SecurityKey? signWith = null,
        string? username = null)
    {
        var now = issuedAt ?? DateTime.UtcNow;
        var life = lifetime ?? TimeSpan.FromMinutes(15);
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = issuer,
            Audience = audience,
            Subject = new System.Security.Claims.ClaimsIdentity([new System.Security.Claims.Claim("sub", subject)]),
            IssuedAt = now,
            NotBefore = now,
            Expires = now + life,
            Claims = BuildClaims(roles, username),
            SigningCredentials = new SigningCredentials(signWith ?? SigningKey, SecurityAlgorithms.RsaSha256),
        };
        return new JsonWebTokenHandler().CreateToken(descriptor);
    }

    private static Dictionary<string, object> BuildClaims(string[]? roles, string? username)
    {
        var claims = new Dictionary<string, object> { ["roles"] = roles ?? ["operator"] };
        if (username is not null)
        {
            claims["preferred_username"] = username;
        }

        return claims;
    }

    public string Operator() => Create(roles: ["operator"]);
    public string Controller(string? username = "jayasinghe") => Create(subject: "d0d0d0d0-0000-4000-8000-000000000003", roles: ["controller"], username: username);
    public string Caterer() => Create(subject: "service-account-smartrail-caterer", roles: ["caterer"]);

    public void Dispose() => _rsa.Dispose();
}
