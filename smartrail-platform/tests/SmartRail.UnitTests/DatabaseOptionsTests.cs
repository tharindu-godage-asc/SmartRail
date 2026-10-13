using SmartRail.Infrastructure.Persistence;
using Xunit;

namespace SmartRail.UnitTests;

public class DatabaseOptionsTests
{
    [Fact]
    public void Migrations_and_demo_data_are_off_by_default()
    {
        // Rule R5: only Docker Compose turns these on; the cluster and prod must never apply either by accident.
        Assert.False(new DatabaseOptions().ApplyMigrationsOnStartup);
        Assert.False(new SeedingOptions().DemoData);
        Assert.Null(new SeedingOptions().DemoOperatorSubject);
    }

    [Fact]
    public void Settings_are_read_from_the_documented_sections()
    {
        Assert.Equal("Database", DatabaseOptions.Section);
        Assert.Equal("Seeding", SeedingOptions.Section);
    }
}
