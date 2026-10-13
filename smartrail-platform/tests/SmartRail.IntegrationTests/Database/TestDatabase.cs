using Microsoft.EntityFrameworkCore;
using Npgsql;
using SmartRail.Infrastructure.Persistence;

namespace SmartRail.IntegrationTests.Database;

/// <summary>One fresh database for one test class, with the admin (migration) and runtime (smartrail_app) connections.</summary>
public sealed record TestDatabase(string Name, string AdminConnection, string AppConnection)
{
    public SmartrailDbContext AdminContext() => new(SmartrailDbContextOptions.Create(AdminConnection));

    public SmartrailDbContext AppContext() => new(SmartrailDbContextOptions.Create(AppConnection));

    public async Task MigrateAsync()
    {
        await using var context = AdminContext();
        await context.Database.MigrateAsync();
    }

    public Task ExecuteAsync(string sql, string? connection = null) => PostgresFixture.Execute(connection ?? AdminConnection, sql);

    public async Task<T?> ScalarAsync<T>(string sql, string? connection = null, params NpgsqlParameter[] parameters)
    {
        await using var npgsql = new NpgsqlConnection(connection ?? AdminConnection);
        await npgsql.OpenAsync();
        await using var command = new NpgsqlCommand(sql, npgsql);
        command.Parameters.AddRange(parameters);
        var value = await command.ExecuteScalarAsync();
        return value is null or DBNull ? default : (T)value;
    }

    public async Task<List<string>> ColumnAsync(string sql)
    {
        await using var npgsql = new NpgsqlConnection(AdminConnection);
        await npgsql.OpenAsync();
        await using var command = new NpgsqlCommand(sql, npgsql);
        await using var reader = await command.ExecuteReaderAsync();
        var values = new List<string>();
        while (await reader.ReadAsync())
        {
            values.Add(reader.GetString(0));
        }

        return values;
    }
}
