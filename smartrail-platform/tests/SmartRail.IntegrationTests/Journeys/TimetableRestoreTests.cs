using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using SmartRail.IntegrationTests.Database;
using Xunit;

namespace SmartRail.IntegrationTests.Journeys;

/// <summary>
/// Restore a cancelled service. The clock is Tuesday 6 Oct 2026 08:28. A weekly 8072 from Wednesday 7 Oct (Mon to Fri) has journeys on 7, 8, 9,
/// 12 and 13 October. A service cancelled from today onward leaves the weekly list and shows under Cancelled until it is restored.
/// </summary>
public sealed class TimetableRestoreTests(PostgresFixture postgres) : JourneyApiTestBase(postgres)
{
    private string ControllerToken => Issuer.Create(roles: ["controller"]);

    private static readonly object[] GoodStops =
    [
        new { stationCode = "VYG", arrival = (string?)null, departure = "17:25", platform = "1" },
        new { stationCode = "GPH", arrival = "17:35", departure = "17:36", platform = "2" },
        new { stationCode = "CF", arrival = "17:58", departure = (string?)null, platform = "1" },
    ];

    private async Task<HttpResponseMessage> Post(string path, object body, string? token = null, bool noToken = false)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(body) };
        if (!noToken)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token ?? ControllerToken);
        }

        return await Client.SendAsync(request);
    }

    private async Task<Guid> CreateWeekly()
    {
        var response = await Post("/api/v1/timetable/services", new
        {
            kind = "Weekly", trainNumber = "8072", lineCode = "MAIN", category = "Regional", serviceType = "Express",
            days = new[] { "Mon", "Tue", "Wed", "Thu", "Fri" }, effectiveFrom = "2026-10-07", stops = GoodStops,
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await ReadAsync(response)).GetProperty("serviceIds")[0].GetGuid();
    }

    private async Task<HttpResponseMessage> Cancel(Guid serviceId, string from, string? to) =>
        await Post($"/api/v1/timetable/services/{serviceId}/cancel", new { from, to, reason = "Rolling stock not available" });

    private async Task<List<string>> WeeklyTrains() =>
        (await ReadAsync(await Get("/api/v1/timetable/services?tab=weekly", ControllerToken))).EnumerateArray().Select(r => r.GetProperty("trainNumber").GetString()!).ToList();

    private async Task<JsonElement> CancelledTab() => await ReadAsync(await Get("/api/v1/timetable/services?tab=cancelled", ControllerToken));

    private Task<string?> StatusOn(string date) => Database.ScalarAsync<string>(
        $"select coalesce((select j.status from journey j join train_service s on s.id = j.service_id where s.train_number = '8072' and j.operating_date = date '{date}'), 'none')");

    private Task<HttpResponseMessage> Restore(Guid cancellationId, string? token = null, bool noToken = false) =>
        Post($"/api/v1/timetable/cancellations/{cancellationId}/restore", new { }, token, noToken);

    [Fact]
    public async Task A_service_cancelled_from_today_onward_leaves_the_weekly_list_and_is_restored_with_its_journeys()
    {
        var serviceId = await CreateWeekly();
        Assert.Contains("8072", await WeeklyTrains());
        var cancelled = await Cancel(serviceId, "2026-10-06", null);
        Assert.Equal(HttpStatusCode.OK, cancelled.StatusCode);

        Assert.DoesNotContain("8072", await WeeklyTrains()); // gone from the weekly schedule
        var tab = await CancelledTab();
        Assert.Equal("8072", tab[0].GetProperty("trainNumber").GetString());
        Assert.Equal("Cancelled", await StatusOn("2026-10-07"));
        var cancellationId = tab[0].GetProperty("cancellationId").GetGuid();

        var restored = await Restore(cancellationId);

        Assert.Equal(HttpStatusCode.OK, restored.StatusCode);
        Assert.True((await ReadAsync(restored)).GetProperty("restoredJourneys").GetInt32() >= 4);
        Assert.Contains("8072", await WeeklyTrains()); // back
        Assert.Equal(0, (await CancelledTab()).GetArrayLength()); // no longer under Cancelled
        Assert.Equal("Unassigned", await StatusOn("2026-10-07")); // its journeys are back
        Assert.Equal(1, await Database.ScalarAsync<long>("select count(*) from service_cancellation where restored_at is not null and restored_by is not null"));
        Assert.Equal(1, await Database.ScalarAsync<long>("select count(*) from service_change_log where action = 'Restored'"));
        Assert.Equal(1, await Database.ScalarAsync<long>("select count(*) from service_cancellation")); // nothing deleted
        Assert.Contains("8072", string.Join(",", (await ReadAsync(await Get("/api/v1/trains?from=VYG&to=CF&date=2026-10-07"))).GetProperty("trains").EnumerateArray().Select(t => t.GetProperty("trainNumber").GetString())));
    }

    [Fact]
    public async Task A_service_cancelled_for_one_date_stays_in_the_weekly_list()
    {
        var serviceId = await CreateWeekly();

        await Cancel(serviceId, "2026-10-08", "2026-10-08");

        Assert.Contains("8072", await WeeklyTrains());
        Assert.Equal(1, (await CancelledTab()).GetArrayLength());
    }

    [Fact]
    public async Task Restoring_one_cancellation_keeps_a_journey_that_another_cancellation_still_covers()
    {
        var serviceId = await CreateWeekly();
        await Cancel(serviceId, "2026-10-08", "2026-10-08");
        await Cancel(serviceId, "2026-10-06", null);
        var onward = (await CancelledTab()).EnumerateArray().First(c => c.GetProperty("to").ValueKind == JsonValueKind.Null).GetProperty("cancellationId").GetGuid();

        await Restore(onward);

        Assert.Equal("Cancelled", await StatusOn("2026-10-08")); // the one-date cancellation still applies
        Assert.Equal("Unassigned", await StatusOn("2026-10-09"));
    }

    [Fact]
    public async Task A_second_restore_is_a_conflict_and_the_service_can_be_cancelled_again()
    {
        var serviceId = await CreateWeekly();
        await Cancel(serviceId, "2026-10-06", null);
        var id = (await CancelledTab())[0].GetProperty("cancellationId").GetGuid();
        Assert.Equal(HttpStatusCode.OK, (await Restore(id)).StatusCode);

        var again = await Restore(id);

        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        Assert.Equal("already_restored", (await ReadAsync(again)).GetProperty("reason").GetString());
        Assert.Equal(HttpStatusCode.OK, (await Cancel(serviceId, "2026-10-06", null)).StatusCode); // a new cancellation, not "already cancelled"
        Assert.DoesNotContain("8072", await WeeklyTrains());
    }

    [Fact]
    public async Task Only_a_controller_may_restore_and_an_unknown_cancellation_is_404()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await Restore(Guid.NewGuid(), noToken: true)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Restore(Guid.NewGuid(), token: Issuer.Operator())).StatusCode);

        var missing = await Restore(Guid.NewGuid());

        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Equal("cancellation_not_found", (await ReadAsync(missing)).GetProperty("reason").GetString());
    }
}
