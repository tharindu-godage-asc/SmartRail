using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using SmartRail.IntegrationTests.Database;
using Xunit;

namespace SmartRail.IntegrationTests.Journeys;

/// <summary>
/// Cancel one journey (just that day's run). The clock is Tuesday 6 Oct 2026 08:28 and the seeded 8058 has today's journey. It is the service
/// cancel for a single date, so the rules are those of spec 14: a reason of 1 to 200 characters, and none of it may have reports.
/// </summary>
public sealed class CancelJourneyTests(PostgresFixture postgres) : JourneyApiTestBase(postgres)
{
    private async Task<HttpResponseMessage> CancelJourney(Guid journeyId, string? reason, string? token = null, bool noToken = false)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/journeys/{journeyId}/cancel") { Content = JsonContent.Create(new { reason }) };
        if (!noToken)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token ?? Issuer.Controller());
        }

        return await Client.SendAsync(request);
    }

    private Task<string?> StatusOfJourney() => Database.ScalarAsync<string>($"select status from journey where id = '{JourneyId}'");

    [Fact]
    public async Task Cancelling_a_journey_without_reports_cancels_only_that_run_and_it_shows_under_cancelled_services()
    {
        var response = await CancelJourney(JourneyId, "Rolling stock not available");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(1, (await ReadAsync(response)).GetProperty("cancelledJourneys").GetInt32());
        Assert.Equal("Cancelled", await StatusOfJourney());
        var cancelled = await ReadAsync(await Get("/api/v1/timetable/services?tab=cancelled", Issuer.Controller()));
        Assert.Equal(1, cancelled.GetArrayLength());
    }

    [Fact]
    public async Task A_cancelled_journey_comes_back_when_the_cancellation_is_restored()
    {
        var cancelled = await ReadAsync(await CancelJourney(JourneyId, "Weather"));
        var cancellationId = cancelled.GetProperty("cancellationId").GetGuid();

        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/timetable/cancellations/{cancellationId}/restore") { Content = JsonContent.Create(new { }) };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Issuer.Controller());
        Assert.Equal(HttpStatusCode.OK, (await Client.SendAsync(request)).StatusCode);

        Assert.NotEqual("Cancelled", await StatusOfJourney());
    }

    [Fact]
    public async Task A_journey_with_reports_is_refused_and_nothing_changes()
    {
        Assert.Equal(HttpStatusCode.Created, (await Report(Stops[0], Local(8, 28), eventType: "Departed")).StatusCode);

        var response = await CancelJourney(JourneyId, "Too late");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("journey_has_events", (await ReadAsync(response)).GetProperty("reason").GetString());
        Assert.Equal("Running", await StatusOfJourney());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    public async Task A_missing_reason_is_refused(string? reason)
    {
        var response = await CancelJourney(JourneyId, reason);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("invalid_reason", (await ReadAsync(response)).GetProperty("reason").GetString());
        Assert.NotEqual("Cancelled", await StatusOfJourney());
    }

    [Fact]
    public async Task A_reason_over_200_characters_is_refused()
    {
        var response = await CancelJourney(JourneyId, new string('x', 201));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task An_unknown_journey_is_404()
    {
        var response = await CancelJourney(Guid.NewGuid(), "Weather");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("journey_not_found", (await ReadAsync(response)).GetProperty("reason").GetString());
    }

    [Fact]
    public async Task Only_a_controller_may_cancel_a_journey()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await CancelJourney(JourneyId, "Weather", noToken: true)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await CancelJourney(JourneyId, "Weather", Issuer.Operator())).StatusCode);
    }
}
