using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Time.Testing;
using Npgsql;
using SmartRail.Domain;
using SmartRail.Infrastructure.Seeding;
using SmartRail.IntegrationTests.Authentication;
using SmartRail.IntegrationTests.Database;
using Xunit;

namespace SmartRail.IntegrationTests.Journeys;

/// <summary>
/// One test = one fresh database, the real API with test tokens, and a clock the test controls. Train 8058 for Tuesday
/// 6 Oct 2026 is seeded the way Compose does it (journey, five Pending stop states, Nimal assigned for the whole day).
/// </summary>
[Collection(PostgresCollection.Name)]
public abstract class JourneyApiTestBase(PostgresFixture postgres) : IAsyncLifetime
{
    protected static readonly DateOnly Tuesday = new(2026, 10, 6);
    protected const string NimalSubject = "d0d0d0d0-0000-4000-8000-000000000001";

    protected TestTokenIssuer Issuer { get; } = new();
    protected TestDatabase Database { get; private set; } = null!;
    protected FakeTimeProvider Clock { get; private set; } = null!;
    protected AuthApiFactory Api { get; private set; } = null!;
    protected HttpClient Client { get; private set; } = null!;
    protected Guid JourneyId { get; private set; }

    /// <summary>The five stops of train 8058 in order: Veyangoda, Gampaha, Ragama, Maradana, Colombo Fort.</summary>
    protected IReadOnlyList<Guid> Stops { get; private set; } = [];

    /// <summary>The clock when the test starts. 08:28 is the happy-path report time, so nothing is "from the future" (R3).</summary>
    protected virtual DateTimeOffset StartClock => Local(8, 28);

    public async Task InitializeAsync()
    {
        Database = await postgres.CreateDatabaseAsync();
        Clock = new FakeTimeProvider(StartClock);

        await using (var admin = Database.AdminContext())
        {
            await new DemoSeeder(admin, Clock).SeedAsync(null);
        }

        JourneyId = await Database.ScalarAsync<Guid>("select id from journey limit 1");
        Stops = Enumerable.Range(1, 5).Select(sequence => ReferenceData.StopId(ReferenceData.Service8058, sequence)).ToList();

        await SetUpAsync();

        Api = new AuthApiFactory(Issuer, databaseConnection: Database.AppConnection, clock: Clock);
        Client = Api.CreateClient();
    }

    /// <summary>Extra data a test class needs before the API starts.</summary>
    protected virtual Task SetUpAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        Client.Dispose();
        await Api.DisposeAsync();
        Issuer.Dispose();

        // Every test has its own database; without this the idle pools of ~100 tests would exhaust the server's connections.
        NpgsqlConnection.ClearAllPools();
    }

    protected static DateTimeOffset Local(int hour, int minute, int second = 0, DateOnly? date = null) =>
        OperatingTime.ToInstant(date ?? Tuesday, new TimeOnly(hour, minute, second));

    protected string OperatorToken => Issuer.Operator();

    protected string UnassignedOperatorToken => Issuer.Create(subject: DemoIds.UnassignedOperator.ToString(), roles: ["operator"]);

    protected async Task<HttpResponseMessage> Report(
        Guid stopId,
        DateTimeOffset occurredAt,
        Guid? clientEventId = null,
        string? token = null,
        string? eventType = "Departed",
        Guid? journeyId = null,
        bool noToken = false)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/journeys/{journeyId ?? JourneyId}/events")
        {
            Content = JsonContent.Create(new
            {
                clientEventId = clientEventId ?? Guid.NewGuid(),
                eventType,
                serviceStopId = stopId,
                occurredAt,
            }),
        };
        if (!noToken)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token ?? OperatorToken);
        }

        return await Client.SendAsync(request);
    }

    protected async Task<HttpResponseMessage> Get(string path, string? token = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        if (token is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return await Client.SendAsync(request);
    }

    protected async Task<JsonElement> StatusAsync(Guid? journeyId = null)
    {
        var response = await Get($"/api/v1/journeys/{journeyId ?? JourneyId}/status");
        response.EnsureSuccessStatusCode();
        return await ReadAsync(response);
    }

    protected static async Task<JsonElement> ReadAsync(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.Clone();

    protected static JsonElement StopAt(JsonElement snapshot, int sequence) =>
        snapshot.GetProperty("stops").EnumerateArray().Single(s => s.GetProperty("sequence").GetInt32() == sequence);

    protected static DateTimeOffset Time(JsonElement element, string property) => element.GetProperty(property).GetDateTimeOffset();

    protected async Task<long> EventCountAsync() => await Database.ScalarAsync<long>("select count(*) from train_event");
}
