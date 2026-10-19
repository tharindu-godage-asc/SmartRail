using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;
using SmartRail.Domain;
using SmartRail.Domain.Journeys;
using SmartRail.Infrastructure.Seeding;
using Xunit;

namespace SmartRail.IntegrationTests.Database;

[Collection(PostgresCollection.Name)]
public sealed class MigrationAndSeedTests(PostgresFixture postgres) : IAsyncLifetime
{
    // A Tuesday, 08:20 in Colombo (02:50 UTC): the fixed clock from the test plan.
    private static readonly DateTimeOffset Tuesday0820 = new(2026, 10, 6, 2, 50, 0, TimeSpan.Zero);
    private static readonly DateOnly TuesdayDate = new(2026, 10, 6);

    private TestDatabase _db = null!;

    public async Task InitializeAsync() => _db = await postgres.CreateDatabaseAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Migration_creates_exactly_the_core_tables_and_the_exception_table_and_no_others()
    {
        var tables = await _db.ColumnAsync(
            "select table_name from information_schema.tables where table_schema = 'public' and table_type = 'BASE TABLE'");

        string[] expected =
        [
            "__EFMigrationsHistory", "language", "line", "line_translation", "station", "station_translation", "platform",
            "line_station", "train_service", "service_stop", "journey", "operator_assignment", "journey_stop_state", "train_event",
            "journey_exception", "service_change_log",
        ];
        Assert.Equal(expected.Order(StringComparer.Ordinal), tables.Order(StringComparer.Ordinal));
        Assert.DoesNotContain("controller_assignment", tables);
        Assert.DoesNotContain("order_reference", tables);
    }

    [Fact]
    public async Task Reference_seed_has_languages_line_stations_platforms_and_the_three_services()
    {
        await using var db = _db.AdminContext();

        Assert.Equal(["en", "si", "ta"], await db.Languages.OrderBy(l => l.Code).Select(l => l.Code).ToListAsync());
        Assert.Equal(["en"], await db.Languages.Where(l => l.IsDefault).Select(l => l.Code).ToListAsync());
        Assert.Equal(1, await db.Lines.CountAsync());
        Assert.Equal(5, await db.Stations.CountAsync());
        Assert.Equal(10, await db.StationTranslations.CountAsync()); // Sinhala and Tamil for each of the 5 stations
        Assert.Equal(2, await db.LineTranslations.CountAsync());
        Assert.Equal(10, await db.Platforms.CountAsync());           // platforms 1 and 2 at each station
        Assert.Equal(3, await db.TrainServices.CountAsync()); // 8058, the 8061 express and the 8075 night train (spec 09 section 16)

        // Colombo Fort is the line origin.
        var origin = await db.LineStations.SingleAsync(l => l.Sequence == 1);
        Assert.Equal(ReferenceData.StationColomboFort, origin.StationId);
        Assert.Equal(0.0m, origin.DistanceKm);
    }

    [Fact]
    public async Task Service_8058_has_five_stops_and_the_express_8061_skips_ragama_and_maradana()
    {
        await using var db = _db.AdminContext();

        var stops8058 = await db.ServiceStops.Where(s => s.ServiceId == ReferenceData.Service8058).OrderBy(s => s.Sequence).ToListAsync();
        Assert.Equal(
            [ReferenceData.StationVeyangoda, ReferenceData.StationGampaha, ReferenceData.StationRagama, ReferenceData.StationMaradana, ReferenceData.StationColomboFort],
            stops8058.Select(s => s.StationId));
        Assert.Equal(new TimeOnly(8, 20), stops8058[0].ScheduledDeparture);
        Assert.Null(stops8058[0].ScheduledArrival);
        Assert.Equal(new TimeOnly(8, 30), stops8058[1].ScheduledArrival);
        Assert.True(stops8058[1].ArrivalReportRequired); // Gampaha is the designated arrival stop (D-08)
        Assert.Null(stops8058[4].ScheduledDeparture);

        var stops8061 = await db.ServiceStops.Where(s => s.ServiceId == ReferenceData.Service8061).Select(s => s.StationId).ToListAsync();
        Assert.Equal(3, stops8061.Count);
        Assert.DoesNotContain(ReferenceData.StationRagama, stops8061);
        Assert.DoesNotContain(ReferenceData.StationMaradana, stops8061);
    }

    [Fact]
    public async Task Without_the_demo_seed_no_journey_exists()
    {
        await using var db = _db.AdminContext();

        Assert.Equal(0, await db.Journeys.CountAsync());
        Assert.Equal(0, await db.OperatorAssignments.CountAsync());
    }

    [Fact]
    public async Task Demo_seed_creates_todays_journey_with_pending_stop_states_and_a_whole_day_assignment()
    {
        await using var db = _db.AdminContext();

        var created = await new DemoSeeder(db, new FakeTimeProvider(Tuesday0820)).SeedAsync(null);

        Assert.True(created);
        var journey = await db.Journeys.SingleAsync();
        Assert.Equal(ReferenceData.Service8058, journey.ServiceId);
        Assert.Equal(TuesdayDate, journey.OperatingDate);
        Assert.Equal(JourneyStatus.Scheduled, journey.Status);
        Assert.Equal(0, journey.Version);

        // One Pending stop state per stop of the service (AC-E02-01).
        var states = await db.JourneyStopStates.Where(s => s.JourneyId == journey.Id).ToListAsync();
        Assert.Equal(5, states.Count);
        Assert.All(states, s => Assert.Equal(StopConfirmation.Pending, s.Confirmation));
        var veyangodaDeparture = states.Single(s => s.ServiceStopId == ReferenceData.StopId(ReferenceData.Service8058, 1));
        Assert.Equal(Tuesday0820, veyangodaDeparture.EstimatedDeparture);
        Assert.Null(veyangodaDeparture.EstimatedArrival);

        // The assignment covers the whole operating day, so manual testing works at any hour.
        var assignment = await db.OperatorAssignments.SingleAsync();
        Assert.Equal(DemoIds.Nimal.ToString(), assignment.OperatorUserId);
        Assert.Equal(OperatingTime.StartOfDay(TuesdayDate), assignment.ValidFrom);
        Assert.Equal(OperatingTime.StartOfNextDay(TuesdayDate), assignment.ValidTo);
        Assert.True(assignment.ValidFrom <= Tuesday0820 && Tuesday0820 < assignment.ValidTo);
        Assert.True(assignment.ValidFrom <= Tuesday0820.AddHours(5.7) && Tuesday0820.AddHours(5.7) < assignment.ValidTo); // 14:00 local
    }

    [Fact]
    public async Task Running_the_demo_seed_twice_changes_nothing()
    {
        await using var db = _db.AdminContext();
        var clock = new FakeTimeProvider(Tuesday0820);
        await new DemoSeeder(db, clock).SeedAsync(null);

        var createdAgain = await new DemoSeeder(db, clock).SeedAsync(null);

        Assert.False(createdAgain);
        Assert.Equal(1, await db.Journeys.CountAsync());
        Assert.Equal(5, await db.JourneyStopStates.CountAsync());
        Assert.Equal(1, await db.OperatorAssignments.CountAsync());
    }

    [Fact]
    public async Task Demo_operator_subject_can_be_overridden()
    {
        await using var db = _db.AdminContext();

        await new DemoSeeder(db, new FakeTimeProvider(Tuesday0820)).SeedAsync("keycloak-subject-123");

        Assert.Equal("keycloak-subject-123", (await db.OperatorAssignments.SingleAsync()).OperatorUserId);
    }

    [Fact]
    public async Task The_second_demo_operator_has_no_assignment()
    {
        await using var db = _db.AdminContext();
        await new DemoSeeder(db, new FakeTimeProvider(Tuesday0820)).SeedAsync(null);

        Assert.DoesNotContain(await db.OperatorAssignments.ToListAsync(), a => a.OperatorUserId == DemoIds.UnassignedOperator.ToString());
    }

    [Fact]
    public async Task A_planned_platform_must_belong_to_the_stops_own_station()
    {
        // A platform of Maradana used as the planned platform of a Gampaha stop breaks the composite key.
        var maradanaPlatform = await _db.ScalarAsync<Guid>(
            "select id from platform where station_id = @s limit 1",
            parameters: new Npgsql.NpgsqlParameter("s", ReferenceData.StationMaradana));
        var gampahaStop = ReferenceData.StopId(ReferenceData.Service8058, 2);

        var error = await Assert.ThrowsAsync<Npgsql.PostgresException>(() =>
            _db.ExecuteAsync($"update service_stop set planned_platform_id = '{maradanaPlatform}' where id = '{gampahaStop}'"));

        Assert.Equal("23503", error.SqlState); // foreign_key_violation

        // The same stop with a Gampaha platform is accepted.
        var gampahaPlatform = await _db.ScalarAsync<Guid>(
            "select id from platform where station_id = @s limit 1",
            parameters: new Npgsql.NpgsqlParameter("s", ReferenceData.StationGampaha));
        await _db.ExecuteAsync($"update service_stop set planned_platform_id = '{gampahaPlatform}' where id = '{gampahaStop}'");
    }

    [Fact]
    public async Task Only_one_default_language_is_allowed()
    {
        var error = await Assert.ThrowsAsync<Npgsql.PostgresException>(() =>
            _db.ExecuteAsync("insert into language (code, name, is_default) values ('xx', 'Test', true)"));

        Assert.Equal("23505", error.SqlState); // unique_violation on ux_language_default
    }
}
