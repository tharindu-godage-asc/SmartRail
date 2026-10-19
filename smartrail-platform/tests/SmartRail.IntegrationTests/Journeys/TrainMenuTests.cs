using System.Net;
using System.Text.Json;
using SmartRail.IntegrationTests.Database;
using Xunit;

namespace SmartRail.IntegrationTests.Journeys;

/// <summary>
/// Spec 11: GET /api/v1/trains/{trainNumber}/menu, public. The menu itself comes from SmallBites, which is not configured here, so these
/// tests cover the timetable side: the routes, the errors, and that no menu means an empty list. The menu rules are unit tested.
/// </summary>
public sealed class TrainMenuTests(PostgresFixture postgres) : JourneyApiTestBase(postgres)
{
    [Fact]
    public async Task With_no_caterer_configured_the_list_is_empty_and_needs_no_token()
    {
        var response = await Get("/api/v1/trains/8058/menu?from=VYG&to=CF");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("public, max-age=15", response.Headers.CacheControl?.ToString());
        var body = await ReadAsync(response);
        Assert.Equal("8058", body.GetProperty("trainNumber").GetString());
        Assert.Empty(body.GetProperty("stations").EnumerateArray());
        Assert.Equal(JsonValueKind.Null, body.GetProperty("orderUrl").ValueKind);
    }

    [Fact]
    public async Task Unknown_train_unknown_station_and_wrong_order_are_rejected()
    {
        Assert.Equal(HttpStatusCode.NotFound, (await Get("/api/v1/trains/9999/menu")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Get("/api/v1/trains/8058/menu?from=XXX")).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await Get("/api/v1/trains/8058/menu?from=CF&to=VYG")).StatusCode);
    }
}
