using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using SmartRail.IntegrationTests.Database;
using Xunit;

namespace SmartRail.IntegrationTests.Journeys;

/// <summary>Spec 07 section 14: the closed list (GET /api/v1/exceptions?status=resolved) and the name stored at resolve time.</summary>
public sealed class ClosedExceptionsTests(PostgresFixture postgres) : JourneyApiTestBase(postgres)
{
    private string ControllerToken => Issuer.Controller();

    private async Task<HttpResponseMessage> Resolve(Guid id, string action, string? token = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/exceptions/{id}/resolve") { Content = JsonContent.Create(new { action }) };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token ?? ControllerToken);
        return await Client.SendAsync(request);
    }

    /// <summary>
    /// A minute passes, then Nimal reports a delay with this reason and total. Returns the ID of the exception it raised (found by its
    /// raised time, which is the clock's time, so each call needs its own minute).
    /// </summary>
    private async Task<Guid> RaiseAsync(string reason, int total)
    {
        Clock.Advance(TimeSpan.FromMinutes(1));
        Assert.Equal(HttpStatusCode.Created, (await ReportDelay(Clock.GetUtcNow(), reason, total)).StatusCode);
        var open = await ReadAsync(await Get("/api/v1/exceptions?status=open", ControllerToken));
        return open.GetProperty("items").EnumerateArray()
            .Single(item => item.GetProperty("createdAt").GetDateTimeOffset() == Clock.GetUtcNow())
            .GetProperty("id").GetGuid();
    }

    private async Task<JsonElement> ClosedAsync()
    {
        var response = await Get("/api/v1/exceptions?status=resolved", ControllerToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await ReadAsync(response);
    }

    [Fact]
    public async Task A_resolved_exception_is_listed_with_its_details_and_the_controllers_name()
    {
        var id = await RaiseAsync("Weather", 10);
        Clock.Advance(TimeSpan.FromMinutes(95));
        Assert.Equal(HttpStatusCode.NoContent, (await Resolve(id, "Escalated")).StatusCode);

        var closed = await ClosedAsync();

        Assert.Equal(1, closed.GetProperty("total").GetInt32());
        Assert.Equal(50, closed.GetProperty("limit").GetInt32());
        var item = Assert.Single(closed.GetProperty("items").EnumerateArray());
        Assert.Equal(id, item.GetProperty("id").GetGuid());
        Assert.Equal("8058", item.GetProperty("trainNumber").GetString());
        Assert.Equal("ReasonAwaitingVerification", item.GetProperty("type").GetString());
        Assert.Equal("Critical", item.GetProperty("severity").GetString());
        Assert.Equal("Escalated", item.GetProperty("resolution").GetString());
        Assert.Equal("jayasinghe", item.GetProperty("resolvedByName").GetString());
        Assert.Equal(Local(8, 29), item.GetProperty("createdAt").GetDateTimeOffset());
        Assert.Equal(Local(8, 29).AddMinutes(95), item.GetProperty("resolvedAt").GetDateTimeOffset());
        Assert.Equal(95 * 60, item.GetProperty("secondsToResolve").GetInt64());
    }

    [Fact]
    public async Task An_open_exception_is_not_in_the_closed_list_and_a_closed_one_is_not_in_the_open_list()
    {
        var closedId = await RaiseAsync("Weather", 10);
        await RaiseAsync("Obstruction", 15);
        await Resolve(closedId, "Acknowledged");

        var closed = await ClosedAsync();
        var open = await ReadAsync(await Get("/api/v1/exceptions?status=open", ControllerToken));

        Assert.Equal(closedId, Assert.Single(closed.GetProperty("items").EnumerateArray()).GetProperty("id").GetGuid());
        Assert.Equal(1, open.GetProperty("openCount").GetInt32());
        Assert.DoesNotContain(open.GetProperty("items").EnumerateArray(), item => item.GetProperty("id").GetGuid() == closedId);
    }

    [Fact]
    public async Task The_newest_closure_comes_first_whatever_the_order_they_were_raised_in()
    {
        var first = await RaiseAsync("Weather", 10);
        var second = await RaiseAsync("Obstruction", 15);
        await Resolve(second, "NoActionNeeded"); // closed first
        Clock.Advance(TimeSpan.FromMinutes(5));
        await Resolve(first, "Acknowledged"); // closed last: must be on top

        var ids = (await ClosedAsync()).GetProperty("items").EnumerateArray().Select(item => item.GetProperty("id").GetGuid()).ToList();

        Assert.Equal([first, second], ids);
    }

    [Fact]
    public async Task A_token_without_a_user_name_still_resolves_and_the_name_is_null()
    {
        var id = await RaiseAsync("Weather", 10);
        var nameless = Issuer.Controller(username: null);

        Assert.Equal(HttpStatusCode.NoContent, (await Resolve(id, "Acknowledged", nameless)).StatusCode);

        var item = Assert.Single((await ClosedAsync()).GetProperty("items").EnumerateArray());
        Assert.Equal(JsonValueKind.Null, item.GetProperty("resolvedByName").ValueKind);
        Assert.Equal("d0d0d0d0-0000-4000-8000-000000000003", await Database.ScalarAsync<string>("select resolved_by from journey_exception"));
    }

    [Fact]
    public async Task A_very_long_name_is_cut_to_the_column_size_instead_of_failing()
    {
        var id = await RaiseAsync("Weather", 10);

        Assert.Equal(HttpStatusCode.NoContent, (await Resolve(id, "Acknowledged", Issuer.Controller(new string('x', 200)))).StatusCode);

        Assert.Equal(64, (await Database.ScalarAsync<string>("select resolved_by_name from journey_exception"))!.Length);
    }

    [Fact]
    public async Task Only_the_latest_50_are_listed_and_the_total_says_how_many_exist()
    {
        await Database.ExecuteAsync(
            $"insert into journey_exception (journey_id, type, severity, created_at, resolved_at, resolved_by, resolved_by_name, resolution) " +
            $"select '{JourneyId}', 'EarlyDeparture', 'High', now() - interval '3 hours', now() - (g || ' minutes')::interval, 'sub', 'jayasinghe', 'Acknowledged' " +
            "from generate_series(1, 55) g");

        var closed = await ClosedAsync();

        Assert.Equal(55, closed.GetProperty("total").GetInt32());
        Assert.Equal(50, closed.GetProperty("items").GetArrayLength());
        var times = closed.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("resolvedAt").GetDateTimeOffset()).ToList();
        Assert.Equal(times.OrderByDescending(t => t), times);
    }

    [Fact]
    public async Task An_exception_with_no_station_is_listed_with_a_null_station()
    {
        var id = await RaiseAsync("SignalIssue", 10); // nothing reported yet, so no station
        await Resolve(id, "Acknowledged");

        var item = Assert.Single((await ClosedAsync()).GetProperty("items").EnumerateArray());

        Assert.Equal(JsonValueKind.Null, item.GetProperty("stationName").ValueKind);
    }

    [Fact]
    public async Task Only_a_controller_may_read_the_closed_list_and_unknown_statuses_are_422()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await Get("/api/v1/exceptions?status=resolved")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Get("/api/v1/exceptions?status=resolved", OperatorToken)).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await Get("/api/v1/exceptions?status=closed", ControllerToken)).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await Get("/api/v1/exceptions?status=all", ControllerToken)).StatusCode);
    }

    [Fact]
    public async Task Reading_the_closed_list_changes_nothing_a_passenger_sees()
    {
        var id = await RaiseAsync("Weather", 10);
        await Resolve(id, "Acknowledged");
        var before = await StatusAsync();

        await ClosedAsync();
        await ClosedAsync();

        Assert.Equal(before.GetProperty("version").GetInt32(), (await StatusAsync()).GetProperty("version").GetInt32());
    }
}
