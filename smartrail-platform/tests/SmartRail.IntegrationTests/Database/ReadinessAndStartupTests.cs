using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace SmartRail.IntegrationTests.Database;

/// <summary>/health/ready and the startup step (rules R5 and R8, section 4 of the spec), with the real API against a real Postgres.</summary>
[Collection(PostgresCollection.Name)]
public sealed class ReadinessAndStartupTests(PostgresFixture postgres)
{
    private static WebApplicationFactory<Program> Api(params (string Key, string Value)[] settings) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            foreach (var (key, value) in settings)
            {
                builder.UseSetting(key, value);
            }
        });

    private static async Task<HttpStatusCode> Get(WebApplicationFactory<Program> api, string path) =>
        (await api.CreateClient().GetAsync(path)).StatusCode;

    [Fact]
    public async Task With_no_database_configured_the_api_is_ready_exactly_as_before()
    {
        // The cluster today: no connection string, so no database check and no database registration.
        await using var api = Api();

        Assert.Equal(HttpStatusCode.OK, await Get(api, "/health/ready"));
        Assert.Equal(HttpStatusCode.OK, await Get(api, "/health/live"));
        Assert.Equal(HttpStatusCode.OK, await Get(api, "/api/v1/status"));
    }

    [Fact]
    public async Task With_a_migrated_database_the_api_is_ready()
    {
        var database = await postgres.CreateDatabaseAsync();
        await using var api = Api(("ConnectionStrings:Smartrail", database.AppConnection));

        Assert.Equal(HttpStatusCode.OK, await Get(api, "/health/ready"));
    }

    [Fact]
    public async Task With_migrations_pending_the_api_is_not_ready_but_is_alive()
    {
        var database = await postgres.CreateDatabaseAsync(migrate: false);
        await using var api = Api(("ConnectionStrings:Smartrail", database.AppConnection));

        Assert.Equal(HttpStatusCode.ServiceUnavailable, await Get(api, "/health/ready"));
        Assert.Equal(HttpStatusCode.OK, await Get(api, "/health/live"));
    }

    [Fact]
    public async Task With_an_unreachable_database_the_api_is_not_ready_but_stays_alive()
    {
        // Port 1 has nothing listening. Liveness must not depend on the database, or a database outage would restart every pod.
        await using var api = Api(("ConnectionStrings:Smartrail", "Host=127.0.0.1;Port=1;Database=x;Username=u;Password=p;Timeout=2"));

        Assert.Equal(HttpStatusCode.ServiceUnavailable, await Get(api, "/health/ready"));
        Assert.Equal(HttpStatusCode.OK, await Get(api, "/health/live"));
    }

    [Fact]
    public async Task The_ready_response_never_contains_connection_details()
    {
        await using var api = Api(("ConnectionStrings:Smartrail", "Host=127.0.0.1;Port=1;Database=x;Username=secretuser;Password=secretpassword;Timeout=2"));

        var body = await (await api.CreateClient().GetAsync("/health/ready")).Content.ReadAsStringAsync();

        Assert.DoesNotContain("secretpassword", body);
        Assert.DoesNotContain("secretuser", body);
        Assert.DoesNotContain("127.0.0.1", body);
    }

    [Fact]
    public async Task With_the_flags_off_the_api_leaves_the_database_alone()
    {
        // Rule R5: connection strings alone never apply migrations or seed demo data.
        var database = await postgres.CreateDatabaseAsync(migrate: false);
        await using var api = Api(
            ("ConnectionStrings:Smartrail", database.AppConnection),
            ("ConnectionStrings:SmartrailMigrations", database.AdminConnection));

        _ = api.CreateClient();

        Assert.Equal(0, await database.ScalarAsync<long>(
            "select count(*) from information_schema.tables where table_schema = 'public'"));
    }

    [Fact]
    public async Task With_the_flags_on_the_api_migrates_and_seeds_the_demo_journey_on_startup()
    {
        var database = await postgres.CreateDatabaseAsync(migrate: false);
        await using var api = Api(
            ("ConnectionStrings:Smartrail", database.AppConnection),
            ("ConnectionStrings:SmartrailMigrations", database.AdminConnection),
            ("Database:ApplyMigrationsOnStartup", "true"),
            ("Seeding:DemoData", "true"));

        Assert.Equal(HttpStatusCode.OK, await Get(api, "/health/ready"));

        await using var admin = database.AdminContext();
        // Today's 8058 with its five stop states and the operator's assignment, yesterday's finished 8058 (spec 12), and the demo trains of
        // spec 10 (how many depends on the time of day, so only "at least" is checked).
        Assert.True(await admin.Journeys.CountAsync() >= 2);
        Assert.True(await admin.JourneyStopStates.CountAsync() >= 10);
        Assert.True(await admin.OperatorAssignments.CountAsync() >= 2);
    }

    [Fact]
    public async Task Turning_migrations_on_without_the_admin_connection_stops_startup_with_a_clear_message()
    {
        var database = await postgres.CreateDatabaseAsync(migrate: false);
        await using var api = Api(
            ("ConnectionStrings:Smartrail", database.AppConnection),
            ("Database:ApplyMigrationsOnStartup", "true"));

        var error = Assert.ThrowsAny<Exception>(() => api.CreateClient());

        Assert.Contains("ConnectionStrings:SmartrailMigrations", Flatten(error));
    }

    private static string Flatten(Exception exception) =>
        string.Join(" | ", Enumerable.Range(0, 5).Aggregate(
            (Current: (Exception?)exception, Messages: new List<string>()),
            (state, _) =>
            {
                if (state.Current is not null)
                {
                    state.Messages.Add(state.Current.Message);
                }

                return (state.Current?.InnerException, state.Messages);
            }).Messages);
}
