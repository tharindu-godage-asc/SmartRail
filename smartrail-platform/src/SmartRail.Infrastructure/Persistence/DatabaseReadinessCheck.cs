using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

namespace SmartRail.Infrastructure.Persistence;

/// <summary>Ready only when the database answers and no migration is pending. Registered only when a connection string is configured.</summary>
public sealed class DatabaseReadinessCheck(SmartrailDbContext db, ILogger<DatabaseReadinessCheck> logger) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!await db.Database.CanConnectAsync(cancellationToken))
            {
                return HealthCheckResult.Unhealthy("Database unreachable.");
            }

            var pending = await db.Database.GetPendingMigrationsAsync(cancellationToken);
            return pending.Any()
                ? HealthCheckResult.Unhealthy("Database migrations pending.")
                : HealthCheckResult.Healthy();
        }
        catch (Exception ex)
        {
            // The details go to the log only; the probe response never carries connection information.
            logger.LogWarning(ex, "Database readiness check failed.");
            return HealthCheckResult.Unhealthy("Database check failed.");
        }
    }
}
