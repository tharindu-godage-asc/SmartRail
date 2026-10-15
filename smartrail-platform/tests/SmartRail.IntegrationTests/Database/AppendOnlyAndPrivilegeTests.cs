using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;
using Npgsql;
using SmartRail.Infrastructure.Seeding;
using Xunit;

namespace SmartRail.IntegrationTests.Database;

[Collection(PostgresCollection.Name)]
public sealed class AppendOnlyAndPrivilegeTests(PostgresFixture postgres) : IAsyncLifetime
{
    private static readonly DateTimeOffset Tuesday0820 = new(2026, 10, 6, 2, 50, 0, TimeSpan.Zero);

    private TestDatabase _db = null!;
    private Guid _journeyId;
    private readonly Guid _clientEventId = Guid.NewGuid();

    public async Task InitializeAsync()
    {
        _db = await postgres.CreateDatabaseAsync();

        await using var admin = _db.AdminContext();
        await new DemoSeeder(admin, new FakeTimeProvider(Tuesday0820)).SeedAsync(null);
        _journeyId = await admin.Journeys.Select(j => j.Id).SingleAsync();

        // The API role (smartrail_app) writes the event, exactly as the application will.
        await InsertEventAsync(_db.AppConnection, _clientEventId);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private Task InsertEventAsync(string connection, Guid clientEventId) => _db.ExecuteAsync(
        $"insert into train_event (journey_id, client_event_id, event_type, occurred_at, source, created_by, journey_version_after) " +
        $"values ('{_journeyId}', '{clientEventId}', 'Departed', now(), 'Operator', 'test', 1)", connection);

    // ---- append-only (NFR-17, D-36, EC-132, AC-E04-02 5) ----

    [Theory]
    [InlineData("update train_event set created_by = 'someone else'")]
    [InlineData("delete from train_event")]
    [InlineData("truncate table train_event")]
    public async Task The_trigger_refuses_update_delete_and_truncate_even_for_the_admin_user(string statement)
    {
        var error = await Assert.ThrowsAsync<PostgresException>(() => _db.ExecuteAsync(statement));

        if (statement.StartsWith("truncate", StringComparison.Ordinal))
        {
            // Since journey_exception points at train_event, Postgres refuses TRUNCATE itself (0A000) before the trigger can.
            Assert.Contains(error.SqlState, new[] { "23001", "0A000" });
        }
        else
        {
            Assert.Equal("23001", error.SqlState); // restrict_violation, raised by train_event_block_change()
            Assert.Contains("append-only", error.MessageText);
        }

        Assert.Equal(1, await _db.ScalarAsync<long>("select count(*) from train_event"));
    }

    [Theory]
    [InlineData("update train_event set created_by = 'someone else'")]
    [InlineData("delete from train_event")]
    [InlineData("truncate table train_event")]
    public async Task The_api_role_has_no_right_to_update_delete_or_truncate_train_events(string statement)
    {
        var error = await Assert.ThrowsAsync<PostgresException>(() => _db.ExecuteAsync(statement, _db.AppConnection));

        Assert.Equal("42501", error.SqlState); // insufficient_privilege
        Assert.Equal(1, await _db.ScalarAsync<long>("select count(*) from train_event"));
    }

    [Fact]
    public async Task The_api_role_can_select_and_insert_train_events()
    {
        await InsertEventAsync(_db.AppConnection, Guid.NewGuid());

        Assert.Equal(2, await _db.ScalarAsync<long>("select count(*) from train_event", _db.AppConnection));
    }

    [Fact]
    public async Task The_same_client_event_id_cannot_be_recorded_twice_for_a_journey()
    {
        var error = await Assert.ThrowsAsync<PostgresException>(() => InsertEventAsync(_db.AppConnection, _clientEventId));

        Assert.Equal("23505", error.SqlState); // unique_violation on (journey_id, client_event_id)
    }

    [Fact]
    public async Task Events_with_an_unknown_type_or_source_are_refused()
    {
        var badType = await Assert.ThrowsAsync<PostgresException>(() => _db.ExecuteAsync(
            $"insert into train_event (journey_id, client_event_id, event_type, occurred_at, source, created_by, journey_version_after) " +
            $"values ('{_journeyId}', '{Guid.NewGuid()}', 'Exploded', now(), 'Operator', 'test', 1)"));
        var badSource = await Assert.ThrowsAsync<PostgresException>(() => _db.ExecuteAsync(
            $"insert into train_event (journey_id, client_event_id, event_type, occurred_at, source, created_by, journey_version_after) " +
            $"values ('{_journeyId}', '{Guid.NewGuid()}', 'Departed', now(), 'Passenger', 'test', 1)"));

        Assert.Equal("23514", badType.SqlState);   // check_violation
        Assert.Equal("23514", badSource.SqlState);
    }

    // ---- the runtime role (NFR-10, rule R9) ----

    [Fact]
    public async Task The_api_role_can_read_and_write_ordinary_tables()
    {
        await using var app = _db.AppContext();

        var journey = await app.Journeys.SingleAsync();
        journey.CurrentDelayMinutes = 8;
        journey.Version = 1;
        await app.SaveChangesAsync();

        Assert.Equal(8, await _db.ScalarAsync<int>("select current_delay_minutes from journey"));
    }

    [Fact]
    public async Task A_table_the_admin_creates_later_is_usable_by_the_api_role_without_any_grant()
    {
        await _db.ExecuteAsync("create table later_slice_table (id int primary key, note text)");

        await _db.ExecuteAsync("insert into later_slice_table values (1, 'hello')", _db.AppConnection);
        await _db.ExecuteAsync("update later_slice_table set note = 'changed'", _db.AppConnection);

        Assert.Equal("changed", await _db.ScalarAsync<string>("select note from later_slice_table", _db.AppConnection));
    }

    [Fact]
    public async Task The_api_role_can_only_read_the_migration_history()
    {
        Assert.True(await _db.ScalarAsync<long>("""select count(*) from "__EFMigrationsHistory" """, _db.AppConnection) >= 1);

        var error = await Assert.ThrowsAsync<PostgresException>(() => _db.ExecuteAsync(
            """insert into "__EFMigrationsHistory" (migration_id, product_version) values ('fake', '0')""", _db.AppConnection));
        Assert.Equal("42501", error.SqlState);
    }

    [Fact]
    public async Task The_api_role_cannot_change_the_schema()
    {
        var error = await Assert.ThrowsAsync<PostgresException>(() =>
            _db.ExecuteAsync("create table sneaky (id int)", _db.AppConnection));

        Assert.Equal("42501", error.SqlState);
    }

    // ---- Keycloak is separate (ADR-08) ----

    [Fact]
    public async Task The_keycloak_role_owns_its_own_database_and_cannot_connect_to_smartrails()
    {
        var keycloakOwn = postgres.RoleConnection("keycloak", "keycloak", "keycloak-dev-only");
        Assert.Equal(1, await _db.ScalarAsync<int>("select 1", keycloakOwn));

        var intoSmartrail = postgres.RoleConnection(_db.Name, "keycloak", "keycloak-dev-only");
        var error = await Assert.ThrowsAsync<PostgresException>(() => _db.ExecuteAsync("select 1", intoSmartrail));
        Assert.Equal("42501", error.SqlState);

        var apiIntoKeycloak = postgres.RoleConnection("keycloak", "smartrail_app", "smartrail-app-dev-only");
        var error2 = await Assert.ThrowsAsync<PostgresException>(() => _db.ExecuteAsync("select 1", apiIntoKeycloak));
        Assert.Equal("42501", error2.SqlState);
    }

    [Fact]
    public async Task The_keycloak_database_has_no_smartrail_tables()
    {
        var tables = await _db.ScalarAsync<long>(
            "select count(*) from information_schema.tables where table_schema = 'public'",
            postgres.RoleConnection("keycloak", "keycloak", "keycloak-dev-only"));

        Assert.Equal(0, tables);
    }
}
