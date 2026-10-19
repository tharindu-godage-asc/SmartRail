using System.Net;
using System.Text.Json;
using SmartRail.IntegrationTests.Database;
using Xunit;

namespace SmartRail.IntegrationTests.Journeys;

/// <summary>
/// Spec 12 section 4: GET /api/v1/operator/journeys/history and /operator/journeys/{id}/record. Train 8058 on Tuesday 6 Oct 2026, Nimal
/// assigned for the whole day (valid_to is midnight), so on Wednesday it is a past journey for him.
/// </summary>
public sealed class OperatorHistoryTests(PostgresFixture postgres) : JourneyApiTestBase(postgres)
{
    private static readonly DateTimeOffset Wednesday = Local(9, 0, date: new DateOnly(2026, 10, 7));

    private string AnotherOperatorToken => UnassignedOperatorToken;

    private async Task<JsonElement> HistoryAsync(string query = "", string? token = null)
    {
        var response = await Get($"/api/v1/operator/journeys/history{query}", token ?? OperatorToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await ReadAsync(response);
    }

    private async Task Complete(int lateMinutes)
    {
        Clock.SetUtcNow(Local(8, 28));
        Assert.Equal(HttpStatusCode.Created, (await Report(Stops[0], Local(8, 28))).StatusCode);
        var arrival = Local(8, 55).AddMinutes(lateMinutes);
        Clock.SetUtcNow(arrival);
        Assert.Equal(HttpStatusCode.Created, (await Report(Stops[4], arrival, eventType: "Arrived")).StatusCode);
    }

    // ---- who may call ----

    [Fact]
    public async Task No_token_is_401_and_other_roles_are_403_on_both_routes()
    {
        foreach (var path in new[] { "/api/v1/operator/journeys/history", $"/api/v1/operator/journeys/{JourneyId}/record" })
        {
            Assert.Equal(HttpStatusCode.Unauthorized, (await Get(path)).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await Get(path, Issuer.Create(roles: ["controller"]))).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await Get(path, Issuer.Create(roles: ["caterer"]))).StatusCode);
        }
    }

    // ---- the list ----

    [Fact]
    public async Task A_journey_in_progress_for_him_is_not_in_the_history()
    {
        var history = await HistoryAsync();

        Assert.Empty(history.GetProperty("journeys").EnumerateArray());
        Assert.Equal(JsonValueKind.Null, history.GetProperty("next").ValueKind);
    }

    [Fact]
    public async Task When_the_assignment_has_ended_the_journey_is_listed_with_his_report_count()
    {
        Clock.SetUtcNow(Local(8, 28));
        Assert.Equal(HttpStatusCode.Created, (await Report(Stops[0], Local(8, 28))).StatusCode);
        Clock.SetUtcNow(Local(8, 40));
        Assert.Equal(HttpStatusCode.Created, (await ReportDelay(Local(8, 40))).StatusCode);
        Clock.SetUtcNow(Wednesday);

        var journey = (await HistoryAsync()).GetProperty("journeys")[0];

        Assert.Equal(JourneyId, journey.GetProperty("journeyId").GetGuid());
        Assert.Equal("8058", journey.GetProperty("trainNumber").GetString());
        Assert.Equal("Veyangoda", journey.GetProperty("fromName").GetString());
        Assert.Equal("Colombo Fort", journey.GetProperty("toName").GetString());
        Assert.Equal("2026-10-06", journey.GetProperty("operatingDate").GetString());
        Assert.Equal(2, journey.GetProperty("myReportCount").GetInt32());
        Assert.Equal(JsonValueKind.Null, journey.GetProperty("arrivalDelayMinutes").ValueKind);
        Assert.NotEqual(JsonValueKind.Null, journey.GetProperty("assignedUntil").ValueKind);
    }

    [Fact]
    public async Task A_completed_journey_is_listed_at_once_with_its_arrival_delay()
    {
        await Complete(lateMinutes: 6);

        var journey = (await HistoryAsync()).GetProperty("journeys")[0];

        Assert.Equal("Completed", journey.GetProperty("status").GetString());
        Assert.Equal(6, journey.GetProperty("arrivalDelayMinutes").GetInt32());
        Assert.Equal(2, journey.GetProperty("myReportCount").GetInt32());
    }

    [Fact]
    public async Task Another_operator_sees_none_of_his_journeys()
    {
        await Complete(lateMinutes: 0);

        var history = await HistoryAsync(token: AnotherOperatorToken);

        Assert.Empty(history.GetProperty("journeys").EnumerateArray());
    }

    [Theory]
    [InlineData("?limit=0")]
    [InlineData("?limit=51")]
    [InlineData("?limit=abc")]
    public async Task A_bad_limit_is_400(string query)
    {
        var response = await Get($"/api/v1/operator/journeys/history{query}", OperatorToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("invalid_limit", (await ReadAsync(response)).GetProperty("reason").GetString());
    }

    [Fact]
    public async Task A_bad_before_is_400_and_a_before_in_the_past_pages_past_the_journey()
    {
        await Complete(lateMinutes: 0);

        var bad = await Get("/api/v1/operator/journeys/history?before=yesterday-ish", OperatorToken);
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
        Assert.Equal("invalid_before", (await ReadAsync(bad)).GetProperty("reason").GetString());

        var page = await HistoryAsync("?limit=1&before=2020-01-01T00:00:00Z");
        Assert.Empty(page.GetProperty("journeys").EnumerateArray());
    }

    // ---- the record ----

    [Fact]
    public async Task The_record_has_the_stops_and_only_his_reports_with_his_own_delay_reason()
    {
        Clock.SetUtcNow(Local(8, 28));
        Assert.Equal(HttpStatusCode.Created, (await Report(Stops[0], Local(8, 28))).StatusCode);
        Clock.SetUtcNow(Local(8, 40));
        Assert.Equal(HttpStatusCode.Created, (await ReportDelay(Local(8, 40), reason: "Weather", total: 15)).StatusCode);
        Clock.SetUtcNow(Wednesday);

        var response = await Get($"/api/v1/operator/journeys/{JourneyId}/record", OperatorToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var record = await ReadAsync(response);
        Assert.Equal("8058", record.GetProperty("trainNumber").GetString());
        var stops = record.GetProperty("stops").EnumerateArray().ToList();
        Assert.Equal(5, stops.Count);
        Assert.Equal("Veyangoda", stops[0].GetProperty("stationName").GetString());
        Assert.Equal("Reported", stops[0].GetProperty("confirmation").GetString());
        Assert.NotEqual(JsonValueKind.Null, stops[0].GetProperty("actualDeparture").ValueKind);
        Assert.Equal("Pending", stops[4].GetProperty("confirmation").GetString());
        var reports = record.GetProperty("myReports").EnumerateArray().ToList();
        Assert.Equal(["Departed", "DelayReported"], reports.Select(r => r.GetProperty("type").GetString()).ToList());
        Assert.Equal("Weather", reports[1].GetProperty("delayReason").GetString());
        Assert.Equal(15, reports[1].GetProperty("totalDelayMinutes").GetInt32());
        Assert.Equal("Veyangoda", reports[0].GetProperty("stationName").GetString());
    }

    [Fact]
    public async Task Another_operators_journey_and_a_missing_one_give_the_same_404()
    {
        var notMine = await Get($"/api/v1/operator/journeys/{JourneyId}/record", AnotherOperatorToken);
        var missing = await Get($"/api/v1/operator/journeys/{Guid.NewGuid()}/record", OperatorToken);

        Assert.Equal(HttpStatusCode.NotFound, notMine.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Equal("journey_not_found", (await ReadAsync(notMine)).GetProperty("reason").GetString());
        Assert.Equal("journey_not_found", (await ReadAsync(missing)).GetProperty("reason").GetString());
    }
}
