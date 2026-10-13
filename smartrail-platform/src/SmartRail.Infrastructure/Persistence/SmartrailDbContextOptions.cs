using Microsoft.EntityFrameworkCore;

namespace SmartRail.Infrastructure.Persistence;

/// <summary>One place that decides how the context talks to Postgres, so the app, the migration step, the design-time tools and the tests agree.</summary>
public static class SmartrailDbContextOptions
{
    public static DbContextOptionsBuilder<SmartrailDbContext> Configure(DbContextOptionsBuilder<SmartrailDbContext> builder, string connectionString)
    {
        ConfigureCore(builder, connectionString);
        return builder;
    }

    public static void Configure(DbContextOptionsBuilder builder, string connectionString) => ConfigureCore(builder, connectionString);

    public static DbContextOptions<SmartrailDbContext> Create(string connectionString) =>
        Configure(new DbContextOptionsBuilder<SmartrailDbContext>(), connectionString).Options;

    private static void ConfigureCore(DbContextOptionsBuilder builder, string connectionString) =>
        builder
            .UseNpgsql(connectionString, npgsql => npgsql.MigrationsAssembly(typeof(SmartrailDbContext).Assembly.GetName().Name))
            .UseSnakeCaseNamingConvention();
}
