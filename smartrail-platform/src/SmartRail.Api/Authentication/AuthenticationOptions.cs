namespace SmartRail.Api.Authentication;

/// <summary>
/// Authentication:* settings (rule R8). Nothing here is hard-coded in C#. With no Authority configured (the cluster today)
/// every protected endpoint answers 401 and the public ones keep working (rule R7); a half-configured set fails startup (R9).
/// </summary>
public sealed class AuthenticationOptions
{
    public const string Section = "Authentication";

    /// <summary>The issuer tokens must carry, for example http://localhost:8180/realms/smartrail.</summary>
    public string? Authority { get; set; }

    /// <summary>Where to fetch the discovery document and signing keys from. Defaults to Authority + /.well-known/openid-configuration.</summary>
    public string? MetadataAddress { get; set; }

    /// <summary>The audience tokens must carry (the client smartrail-api).</summary>
    public string? Audience { get; set; }

    /// <summary>False only in Compose, where Keycloak is plain HTTP.</summary>
    public bool RequireHttpsMetadata { get; set; } = true;

    public int ClockSkewSeconds { get; set; } = 30;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(Authority);
}

/// <summary>Diagnostics:* settings. The whoami endpoint exists only when the flag is on.</summary>
public sealed class DiagnosticsOptions
{
    public const string Section = "Diagnostics";

    public bool WhoAmI { get; set; }
}
