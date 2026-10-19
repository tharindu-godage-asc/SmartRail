using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SmartRail.Infrastructure.Seeding;

namespace SmartRail.Infrastructure.Persistence;

/// <summary>
/// Applies migrations and the demo seed before the API starts serving, but only when the flags say so (rule R5).
/// It uses the admin connection (ConnectionStrings:SmartrailMigrations), never the runtime role. If the database
/// cannot be reached the exception stops startup, so the process exits and Compose restarts it (rule R8).
/// </summary>
public sealed class DatabaseStartupService(
    IConfiguration configuration,
    IOptions<DatabaseOptions> database,
    IOptions<SeedingOptions> seeding,
    TimeProvider clock,
    ILogger<DatabaseStartupService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var migrate = database.Value.ApplyMigrationsOnStartup;
        var seedDemo = seeding.Value.DemoData;
        if (!migrate && !seedDemo)
        {
            return;
        }

        var adminConnection = configuration.GetConnectionString("SmartrailMigrations");
        if (string.IsNullOrWhiteSpace(adminConnection))
        {
            throw new InvalidOperationException(
                "ConnectionStrings:SmartrailMigrations is required when Database:ApplyMigrationsOnStartup or Seeding:DemoData is enabled.");
        }

        await using var db = new SmartrailDbContext(SmartrailDbContextOptions.Create(adminConnection));

        if (migrate)
        {
            logger.LogInformation("Applying pending database migrations.");
            await db.Database.MigrateAsync(cancellationToken);
        }

        if (seedDemo)
        {
            var created = await new DemoSeeder(db, clock).SeedAsync(seeding.Value.DemoOperatorSubject, cancellationToken);
            logger.LogInformation("Demo seed applied (journey {Outcome}).", created ? "created" : "already existed");

            // Spec 09 section 16: a demo train that leaves a few minutes after every start, so a station display always has one soon.
            var soon = await new DemoSeeder(db, clock).SeedSoonTrainAsync(cancellationToken);
            logger.LogInformation("Demo train 8070 leaves Veyangoda at {Departure}.", soon);

            // Spec 10: eight more demo journeys, both directions, some delayed, for the passenger app.
            await new DemoSeeder(db, clock).SeedDemoTrainsAsync(seeding.Value.DemoOperatorSubject, cancellationToken);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
