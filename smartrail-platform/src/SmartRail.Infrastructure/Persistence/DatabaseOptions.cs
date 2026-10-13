namespace SmartRail.Infrastructure.Persistence;

/// <summary>Database:* settings. Off by default; only Docker Compose turns migrations on (rule R5).</summary>
public sealed class DatabaseOptions
{
    public const string Section = "Database";

    /// <summary>Apply pending EF Core migrations (as the admin user) before the API starts serving.</summary>
    public bool ApplyMigrationsOnStartup { get; set; }
}

/// <summary>Seeding:* settings. The reference data always comes from the migration; this is for the demo layer only.</summary>
public sealed class SeedingOptions
{
    public const string Section = "Seeding";

    /// <summary>Create today's demo journey 8058, its stop states and the demo operator assignment.</summary>
    public bool DemoData { get; set; }

    /// <summary>Overrides the fixed demo operator ID (for example with a real Keycloak subject).</summary>
    public string? DemoOperatorSubject { get; set; }
}
