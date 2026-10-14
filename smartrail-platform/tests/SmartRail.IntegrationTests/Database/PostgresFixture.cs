using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

namespace SmartRail.IntegrationTests.Database;

[CollectionDefinition(Name)]
public sealed class PostgresCollection : ICollectionFixture<PostgresFixture>
{
    public const string Name = "postgres";
}

/// <summary>
/// One Postgres container per test run, started with the same image tag as Docker Compose (PostgreSQL 13 or later is
/// required for gen_random_uuid()). It runs the Compose init SQL, so the roles are exactly the ones Compose creates.
/// Every test class then asks for its own fresh database: the append-only trigger blocks TRUNCATE, so tests cannot
/// clean up between themselves.
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:17").Build();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        // 01 creates roles and the keycloak database. CREATE DATABASE cannot share a transaction, so run statement by statement.
        var script = await File.ReadAllTextAsync(InitScript("01-roles-and-databases.sql"));
        await using var connection = new NpgsqlConnection(AdminConnection("postgres"));
        await connection.OpenAsync();
        foreach (var statement in SplitStatements(script))
        {
            await using var command = new NpgsqlCommand(statement, connection);
            await command.ExecuteNonQueryAsync();
        }
    }

    public async Task DisposeAsync() => await _container.DisposeAsync();

    /// <summary>A new, empty database with the Compose privileges (02 script), optionally migrated as the admin user.</summary>
    public async Task<TestDatabase> CreateDatabaseAsync(bool migrate = true)
    {
        var name = $"smartrail_test_{Guid.NewGuid():N}";
        await Execute(AdminConnection("postgres"), $"CREATE DATABASE \"{name}\"");
        await Execute(AdminConnection(name), await File.ReadAllTextAsync(InitScript("02-smartrail-privileges.sql")));

        var database = new TestDatabase(name, AdminConnection(name), RoleConnection(name, "smartrail_app", "smartrail-app-dev-only"));
        if (migrate)
        {
            await database.MigrateAsync();
        }

        return database;
    }

    public string AdminConnection(string database) =>
        new NpgsqlConnectionStringBuilder(_container.GetConnectionString()) { Database = database, MaxPoolSize = 8 }.ConnectionString;

    public string RoleConnection(string database, string user, string password) =>
        new NpgsqlConnectionStringBuilder(_container.GetConnectionString())
        {
            Database = database, Username = user, Password = password, MaxPoolSize = 8,
        }.ConnectionString;

    public static async Task Execute(string connectionString, string sql)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    private static string InitScript(string name) => Path.Combine(AppContext.BaseDirectory, "init", name);

    private static IEnumerable<string> SplitStatements(string script) =>
        string.Join('\n', script.Split('\n').Where(line => !line.TrimStart().StartsWith("--", StringComparison.Ordinal)))
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
