using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace SmartRail.IntegrationTests.Database;

[Collection(PostgresCollection.Name)]
public sealed class MigrationPrerequisiteTests(PostgresFixture postgres)
{
    [Fact]
    public async Task Migration_fails_with_a_clear_error_when_the_api_role_does_not_exist()
    {
        // Rule R7: a missing smartrail_app role must stop the migration, not be worked around.
        var database = await postgres.CreateDatabaseAsync(migrate: false);
        var admin = postgres.AdminConnection("postgres");

        await PostgresFixture.Execute(admin, "alter role smartrail_app rename to smartrail_app_away");
        try
        {
            var error = await Assert.ThrowsAnyAsync<Exception>(database.MigrateAsync);

            var postgresError = Assert.IsType<PostgresException>(error is PostgresException ? error : error.InnerException);
            Assert.Contains("smartrail_app", postgresError.MessageText);
            Assert.Contains("does not exist", postgresError.MessageText);
        }
        finally
        {
            await PostgresFixture.Execute(admin, "alter role smartrail_app_away rename to smartrail_app");
        }
    }

    [Fact]
    public async Task Migrating_twice_is_harmless()
    {
        var database = await postgres.CreateDatabaseAsync();

        await database.MigrateAsync();

        await using var context = database.AdminContext();
        Assert.Empty(await context.Database.GetPendingMigrationsAsync());
    }
}
