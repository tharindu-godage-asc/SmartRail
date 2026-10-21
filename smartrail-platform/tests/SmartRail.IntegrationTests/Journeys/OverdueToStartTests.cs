using System.Net;
using System.Text.Json;
using SmartRail.IntegrationTests.Database;
using Xunit;

namespace SmartRail.IntegrationTests.Journeys;

/// <summary>
/// Overdue to start. Train 8058 leaves Veyangoda at 08:20 and Nimal is assigned all day (status Scheduled). The clock starts at 08:28,
/// eight minutes after the departure time, so with the default grace of five minutes it is overdue until the departure is reported.
/// </summary>
public sealed class OverdueToStartTests(PostgresFixture postgres) : JourneyApiTestBase(postgres)
{
    private string ControllerToken => Issuer.Controller();

    private async Task<JsonElement> ListAll()
    {
        var response = await Get("/api/v1/assignments/journeys?date=2026-10-06&filter=all", ControllerToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await ReadAsync(response);
    }

    private static JsonElement Train(JsonElement list, string number) => list.EnumerateArray().First(j => j.GetProperty("trainNumber").GetString() == number);

    [Fact]
    public async Task A_not_started_journey_past_its_departure_is_flagged_in_the_journey_list_and_the_overview_and_stops_when_the_departure_is_reported()
    {
        var before = Train(await ListAll(), "8058");
        Assert.True(before.GetProperty("overdueToStart").GetBoolean());

        var overview = await ReadAsync(await Get("/api/v1/overview", ControllerToken));
        var overdue = overview.GetProperty("overdueToStart").EnumerateArray().Single();
        Assert.Equal("8058", overdue.GetProperty("trainNumber").GetString());
        Assert.Equal(8, overdue.GetProperty("minutesLate").GetInt32());
        Assert.True(overdue.GetProperty("hasOperator").GetBoolean()); // Nimal is assigned: he has not reported the departure

        Assert.Equal(HttpStatusCode.Created, (await Report(Stops[0], Local(8, 28), eventType: "Departed")).StatusCode);

        Assert.False(Train(await ListAll(), "8058").GetProperty("overdueToStart").GetBoolean());
        Assert.Equal(0, (await ReadAsync(await Get("/api/v1/overview", ControllerToken))).GetProperty("overdueToStart").GetArrayLength());
    }

    [Fact]
    public async Task A_journey_with_nobody_assigned_is_overdue_and_says_so()
    {
        await Database.ExecuteAsync("update journey set status = 'Unassigned'; delete from operator_assignment;");

        var overdue = (await ReadAsync(await Get("/api/v1/overview", ControllerToken))).GetProperty("overdueToStart").EnumerateArray().Single();

        Assert.False(overdue.GetProperty("hasOperator").GetBoolean());
    }

    [Fact]
    public async Task All_journeys_also_lists_completed_and_cancelled_ones_but_the_unassigned_list_does_not()
    {
        await Database.ExecuteAsync("update journey set status = 'Completed'");

        var all = await ListAll();
        Assert.Equal("Completed", Train(all, "8058").GetProperty("status").GetString());
        Assert.False(Train(all, "8058").GetProperty("overdueToStart").GetBoolean()); // finished journeys are never overdue
        var unassigned = await ReadAsync(await Get("/api/v1/assignments/journeys?date=2026-10-06&filter=unassigned", ControllerToken));
        Assert.Equal(0, unassigned.GetArrayLength());
    }
}

/// <summary>Four minutes after the departure time of 8058: still inside the five minute grace period, so nothing is flagged.</summary>
public sealed class OverdueGraceTests(PostgresFixture postgres) : JourneyApiTestBase(postgres)
{
    protected override DateTimeOffset StartClock => Local(8, 24);

    [Fact]
    public async Task Within_the_grace_period_nothing_is_flagged()
    {
        var response = await Get("/api/v1/assignments/journeys?date=2026-10-06&filter=all", Issuer.Controller());
        var journey = (await ReadAsync(response)).EnumerateArray().First(j => j.GetProperty("trainNumber").GetString() == "8058");

        Assert.False(journey.GetProperty("overdueToStart").GetBoolean());
        Assert.Equal(0, (await ReadAsync(await Get("/api/v1/overview", Issuer.Controller()))).GetProperty("overdueToStart").GetArrayLength());
    }
}
