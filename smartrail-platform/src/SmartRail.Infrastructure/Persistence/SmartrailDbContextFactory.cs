using Microsoft.EntityFrameworkCore.Design;

namespace SmartRail.Infrastructure.Persistence;

/// <summary>
/// Used only by the dotnet ef tools (add, script, update). Reads SMARTRAIL_MIGRATIONS_CONNECTION, and falls back to
/// the development-only Compose admin connection. Adding a migration or printing a script does not connect.
/// </summary>
public sealed class SmartrailDbContextFactory : IDesignTimeDbContextFactory<SmartrailDbContext>
{
    private const string DevelopmentConnection =
        "Host=localhost;Port=5432;Database=smartrail;Username=smartrail;Password=smartrail-dev-only";

    public SmartrailDbContext CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("SMARTRAIL_MIGRATIONS_CONNECTION") ?? DevelopmentConnection;
        return new SmartrailDbContext(SmartrailDbContextOptions.Create(connection));
    }
}
