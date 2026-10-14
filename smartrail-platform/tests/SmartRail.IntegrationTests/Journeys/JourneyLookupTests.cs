using System.Net;
using Microsoft.EntityFrameworkCore;
using SmartRail.Domain;
using SmartRail.Domain.Journeys;
using SmartRail.Domain.Reference;
using SmartRail.Infrastructure.Seeding;
using SmartRail.IntegrationTests.Database;
using Xunit;

namespace SmartRail.IntegrationTests.Journeys;

/// <summary>The read side: status by journey and by train number (R15), the operator's own journey, and the 24 hour night train.</summary>
public sealed class JourneyLookupTests(PostgresFixture postgres) : JourneyApiTestBase(postgres)
{
    [Fact]
    public async Task A_journey_with_no_report_is_scheduled_and_normal_with_the_scheduled_times_as_estimates()
    {
        var status = await StatusAsync();

        Assert.Equal("8058", status.GetProperty("trainNumber").GetString());
        Assert.Equal("Scheduled", status.GetProperty("statusKey").GetString());
        Assert.Equal("Normal", status.GetProperty("statusGroup").GetString());
        Assert.Equal(0, status.GetProperty("version").GetInt32());
        Assert.Equal(Local(8, 28), status.GetProperty("serverTime").GetDateTimeOffset());
        Assert.Equal(System.Text.Json.JsonValueKind.Null, status.GetProperty("primaryEta").ValueKind);
        Assert.Equal(System.Text.Json.JsonValueKind.Null, status.GetProperty("lastKnownPosition").ValueKind);
        Assert.Equal(5, status.GetProperty("stops").GetArrayLength());
        Assert.Equal(Local(8, 30), Time(StopAt(status, 2), "scheduledArrival"));
        Assert.Equal(Local(8, 30), Time(StopAt(status, 2), "estimatedArrival"));
    }

    [Fact]
    public async Task The_status_endpoints_are_public()
    {
        Assert.Equal(HttpStatusCode.OK, (await Get($"/api/v1/journeys/{JourneyId}/status")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Get("/api/v1/trains/8058/status")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Get("/api/v1/trains/8058/status", token: "not-a-token")).StatusCode); // a bad token never blocks a public endpoint
    }

    [Fact]
    public async Task An_unknown_journey_or_train_is_a_404_with_a_reason_code()
    {
        var journey = await Get($"/api/v1/journeys/{Guid.NewGuid()}/status");
        var train = await Get("/api/v1/trains/0000/status");

        Assert.Equal(HttpStatusCode.NotFound, journey.StatusCode);
        Assert.Equal("journey_not_found", (await ReadAsync(journey)).GetProperty("reason").GetString());
        Assert.Equal(HttpStatusCode.NotFound, train.StatusCode);
        Assert.Equal("no_journey_for_train", (await ReadAsync(train)).GetProperty("reason").GetString());
    }

    [Fact]
    public async Task The_train_lookup_returns_todays_journey_and_follows_a_report()
    {
        await Report(Stops[0], Local(8, 28));

        var response = await Get("/api/v1/trains/8058/status");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var status = await ReadAsync(response);
        Assert.Equal(JourneyId, status.GetProperty("journeyId").GetGuid());
        Assert.Equal(8, status.GetProperty("delayMinutes").GetInt32());
    }

    [Fact]
    public async Task A_day_train_that_finished_is_still_todays_journey_and_then_gone_after_midnight()
    {
        Clock.SetUtcNow(Local(15, 0)); // long after the 08:55 arrival and the grace period
        Assert.Equal(HttpStatusCode.OK, (await Get("/api/v1/trains/8058/status")).StatusCode);

        var wednesday = Tuesday.AddDays(1);
        Clock.SetUtcNow(Local(0, 1, date: wednesday)); // the date changed and there is no journey for Wednesday
        Assert.Equal(HttpStatusCode.NotFound, (await Get("/api/v1/trains/8058/status")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Get($"/api/v1/journeys/{JourneyId}/status")).StatusCode); // still readable by ID
    }

    // ---- GET /api/v1/operator/journey ----

    [Fact]
    public async Task Nimal_gets_his_journey_with_the_stop_ids_the_app_needs()
    {
        var response = await Get("/api/v1/operator/journey", OperatorToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await ReadAsync(response);
        Assert.Equal(JourneyId, body.GetProperty("journeyId").GetGuid());
        Assert.Equal("8058", body.GetProperty("trainNumber").GetString());
        Assert.Equal("Scheduled", body.GetProperty("status").GetString());
        var stops = body.GetProperty("stops").EnumerateArray().ToList();
        Assert.Equal(5, stops.Count);
        Assert.Equal(Stops[0], stops[0].GetProperty("serviceStopId").GetGuid());
        Assert.Equal(Local(8, 20), stops[0].GetProperty("scheduledDeparture").GetDateTimeOffset());
        Assert.False(stops[0].GetProperty("isFinalStop").GetBoolean());
        Assert.True(stops[4].GetProperty("isFinalStop").GetBoolean());
        Assert.False(body.TryGetProperty("assignedFrom", out _));
    }

    [Fact]
    public async Task An_operator_with_no_assignment_now_gets_404_and_other_roles_are_refused()
    {
        var none = await Get("/api/v1/operator/journey", UnassignedOperatorToken);

        Assert.Equal(HttpStatusCode.NotFound, none.StatusCode);
        Assert.Equal("no_assignment_now", (await ReadAsync(none)).GetProperty("reason").GetString());
        Assert.Equal(HttpStatusCode.Unauthorized, (await Get("/api/v1/operator/journey")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Get("/api/v1/operator/journey", Issuer.Controller())).StatusCode);
    }

    [Fact]
    public async Task The_assignment_is_only_returned_while_it_is_valid()
    {
        Clock.SetUtcNow(Local(0, 0, date: Tuesday.AddDays(1))); // the whole-day assignment ends at 00:00 (exclusive)

        Assert.Equal(HttpStatusCode.NotFound, (await Get("/api/v1/operator/journey", OperatorToken)).StatusCode);
    }

    [Fact]
    public async Task The_operator_journey_reflects_a_report()
    {
        await Report(Stops[0], Local(8, 28));

        var body = await ReadAsync(await Get("/api/v1/operator/journey", OperatorToken));

        Assert.Equal("Running", body.GetProperty("status").GetString());
        Assert.Equal(1, body.GetProperty("version").GetInt32());
    }
}

/// <summary>
/// Trains that cross midnight (R15, R16): train 9900 leaves Veyangoda on Thursday 8 Oct at 23:50, reaches Gampaha at 00:20 and
/// Colombo Fort at 08:00 on Friday. The clock starts on Friday at 00:10.
/// </summary>
public sealed class NightTrainTests(PostgresFixture postgres) : JourneyApiTestBase(postgres)
{
    private static readonly DateOnly Thursday = new(2026, 10, 8);
    private static readonly DateOnly Friday = new(2026, 10, 9);
    private static readonly Guid NightService = Guid.Parse("99000000-0000-4000-8000-000000000001");

    protected override DateTimeOffset StartClock => Local(0, 10, date: Friday);

    private Guid ThursdayJourney { get; set; }

    protected override async Task SetUpAsync()
    {
        await using var db = Database.AdminContext();
        db.TrainServices.Add(new TrainService
        {
            Id = NightService, TrainNumber = "9900", LineId = ReferenceData.LineMain, ServiceType = ServiceType.Slow,
            Direction = ServiceDirection.Inbound, RunsOn = "Daily",
        });
        db.ServiceStops.AddRange(
            new ServiceStop { Id = Night(1), ServiceId = NightService, StationId = ReferenceData.StationVeyangoda, Sequence = 1, ScheduledDeparture = new(23, 50) },
            new ServiceStop { Id = Night(2), ServiceId = NightService, StationId = ReferenceData.StationGampaha, Sequence = 2, ScheduledArrival = new(0, 20), ScheduledDeparture = new(0, 20) },
            new ServiceStop { Id = Night(3), ServiceId = NightService, StationId = ReferenceData.StationColomboFort, Sequence = 3, ScheduledArrival = new(8, 0) });
        await db.SaveChangesAsync();

        ThursdayJourney = await AddJourneyAsync(Thursday);
    }

    private static Guid Night(int sequence) => new($"99000000-0000-4000-8000-{sequence:x12}");

    private async Task<Guid> AddJourneyAsync(DateOnly date)
    {
        await using var db = Database.AdminContext();
        var journey = new Journey { Id = Guid.NewGuid(), ServiceId = NightService, OperatingDate = date, Status = JourneyStatus.Scheduled };
        db.Journeys.Add(journey);

        var stops = await db.ServiceStops.Where(s => s.ServiceId == NightService).OrderBy(s => s.Sequence).ToListAsync();
        var schedule = ScheduleInstants.For(date, stops.Select(s => new StopTimes(s.Id, s.Sequence, s.ScheduledArrival, s.ScheduledDeparture))).ToDictionary(s => s.ServiceStopId);
        foreach (var stop in stops)
        {
            db.JourneyStopStates.Add(new JourneyStopState
            {
                JourneyId = journey.Id, ServiceStopId = stop.Id,
                EstimatedArrival = schedule[stop.Id].Arrival, EstimatedDeparture = schedule[stop.Id].Departure,
            });
        }

        // Nimal is assigned from the evening before until the morning after (the demo seed only covers one operating date).
        db.OperatorAssignments.Add(new OperatorAssignment
        {
            Id = Guid.NewGuid(), JourneyId = journey.Id, OperatorUserId = NimalSubject,
            ValidFrom = Local(22, 0, date: date), ValidTo = Local(10, 0, date: date.AddDays(1)), AssignedBy = "test",
        });
        await db.SaveChangesAsync();
        return journey.Id;
    }

    private async Task<(HttpStatusCode Status, string? Train, string? OperatingDate)> LookupAsync(DateTimeOffset at)
    {
        Clock.SetUtcNow(at);
        var response = await Get("/api/v1/trains/9900/status");
        if (response.StatusCode != HttpStatusCode.OK)
        {
            return (response.StatusCode, null, null);
        }

        var body = await ReadAsync(response);
        return (response.StatusCode, body.GetProperty("trainNumber").GetString(), body.GetProperty("operatingDate").GetString());
    }

    [Fact]
    public async Task The_scheduled_times_are_on_the_right_calendar_days()
    {
        var status = await StatusAsync(ThursdayJourney);

        Assert.Equal(Local(23, 50, date: Thursday), Time(StopAt(status, 1), "scheduledDeparture"));
        Assert.Equal(Local(0, 20, date: Friday), Time(StopAt(status, 2), "scheduledArrival"));
        Assert.Equal(Local(8, 0, date: Friday), Time(StopAt(status, 3), "scheduledArrival"));
    }

    [Fact]
    public async Task A_train_that_left_on_thursday_is_still_returned_after_midnight_and_at_08_00()
    {
        foreach (var (hour, minute) in new[] { (0, 10), (3, 30), (8, 0) })
        {
            var (code, train, date) = await LookupAsync(Local(hour, minute, date: Friday));

            Assert.Equal(HttpStatusCode.OK, code);
            Assert.Equal("9900", train);
            Assert.Equal("2026-10-08", date);
        }
    }

    [Fact]
    public async Task The_train_is_visible_until_the_end_plus_15_minutes_and_not_one_minute_longer()
    {
        Assert.Equal(HttpStatusCode.OK, (await LookupAsync(Local(8, 15, date: Friday))).Status);
        Assert.Equal(HttpStatusCode.NotFound, (await LookupAsync(Local(8, 16, date: Friday))).Status);
    }

    [Fact]
    public async Task A_train_running_late_stays_visible_for_as_long_as_the_delay_plus_the_grace()
    {
        Clock.SetUtcNow(Local(0, 30, date: Friday));
        var reported = await Report(Night(2), Local(0, 28, date: Friday), journeyId: ThursdayJourney); // scheduled 00:20, so 8 minutes late
        Assert.Equal(HttpStatusCode.Created, reported.StatusCode);
        Assert.Equal(8, (await StatusAsync(ThursdayJourney)).GetProperty("delayMinutes").GetInt32());

        // Expected end is 08:00 plus 8 minutes, plus the 15 minute grace: 08:23.
        Assert.Equal(HttpStatusCode.OK, (await LookupAsync(Local(8, 23, date: Friday))).Status);
        Assert.Equal(HttpStatusCode.NotFound, (await LookupAsync(Local(8, 24, date: Friday))).Status);
    }

    [Fact]
    public async Task A_report_after_midnight_for_the_midnight_stop_is_judged_against_the_next_calendar_day()
    {
        Clock.SetUtcNow(Local(0, 30, date: Friday));

        var onTime = await Report(Night(2), Local(0, 21, date: Friday), journeyId: ThursdayJourney);

        Assert.Equal(HttpStatusCode.Created, onTime.StatusCode);
        var status = await StatusAsync(ThursdayJourney);
        Assert.Equal(1, status.GetProperty("delayMinutes").GetInt32());
        Assert.Equal(Local(8, 1, date: Friday), Time(StopAt(status, 3), "estimatedArrival"));
    }

    [Fact]
    public async Task When_the_night_train_is_over_the_same_trains_journey_for_the_new_date_takes_over()
    {
        await AddJourneyAsync(Friday); // Friday's own run, which leaves at 23:50 on Friday

        // At 00:10 Thursday's journey is in progress and Friday's has not started.
        var early = await LookupAsync(Local(0, 10, date: Friday));
        Assert.Equal("2026-10-08", early.OperatingDate);

        // At 09:00 Thursday's journey (ended 08:00, grace until 08:15) is over, and Friday's is today's journey.
        var later = await LookupAsync(Local(9, 0, date: Friday));
        Assert.Equal(HttpStatusCode.OK, later.Status);
        Assert.Equal("2026-10-09", later.OperatingDate);
    }

    [Fact]
    public async Task A_late_24_hour_journey_with_an_operating_date_two_days_ago_is_still_found_while_it_runs()
    {
        // Operating date Wednesday, 24 hours long (ends Thursday 23:50), running 120 minutes late, so it ends Friday 01:50.
        // Today is Friday, so its operating date is exactly two days before today: the oldest date the lookup still searches.
        await using var db = Database.AdminContext();
        var service = Guid.Parse("99000000-0000-4000-8000-0000000000a2");
        db.TrainServices.Add(new TrainService
        {
            Id = service, TrainNumber = "9902", LineId = ReferenceData.LineMain, ServiceType = ServiceType.Slow,
            Direction = ServiceDirection.Inbound, RunsOn = "Daily",
        });
        db.ServiceStops.AddRange(
            new ServiceStop { Id = new Guid("99000002-0000-4000-8000-000000000001"), ServiceId = service, StationId = ReferenceData.StationVeyangoda, Sequence = 1, ScheduledDeparture = new(23, 50) },
            new ServiceStop { Id = new Guid("99000002-0000-4000-8000-000000000002"), ServiceId = service, StationId = ReferenceData.StationGampaha, Sequence = 2, ScheduledArrival = new(12, 0), ScheduledDeparture = new(12, 5) },
            new ServiceStop { Id = new Guid("99000002-0000-4000-8000-000000000003"), ServiceId = service, StationId = ReferenceData.StationColomboFort, Sequence = 3, ScheduledArrival = new(23, 50) });
        db.Journeys.Add(new Journey
        {
            Id = Guid.NewGuid(), ServiceId = service, OperatingDate = Friday.AddDays(-2), Status = JourneyStatus.Running, CurrentDelayMinutes = 120,
        });
        await db.SaveChangesAsync();

        Clock.SetUtcNow(Local(1, 50, date: Friday).AddMinutes(15)); // the expected end plus the grace, inclusive
        Assert.Equal(HttpStatusCode.OK, (await Get("/api/v1/trains/9902/status")).StatusCode);

        Clock.SetUtcNow(Local(2, 6, date: Friday)); // one minute later it is over
        Assert.Equal(HttpStatusCode.NotFound, (await Get("/api/v1/trains/9902/status")).StatusCode);
    }

    [Fact]
    public async Task A_24_hour_journey_that_started_two_dates_ago_is_still_found_while_it_runs()
    {
        // Leaves at 23:50 two days before, with the only other stop at 23:50 the day after (exactly 24 hours later).
        await using var db = Database.AdminContext();
        var service = Guid.Parse("99000000-0000-4000-8000-000000000002");
        db.TrainServices.Add(new TrainService
        {
            Id = service, TrainNumber = "9901", LineId = ReferenceData.LineMain, ServiceType = ServiceType.Slow,
            Direction = ServiceDirection.Inbound, RunsOn = "Daily",
        });
        var first = new Guid("99000001-0000-4000-8000-000000000001");
        var middle = new Guid("99000001-0000-4000-8000-000000000002");
        var last = new Guid("99000001-0000-4000-8000-000000000003");
        db.ServiceStops.AddRange(
            new ServiceStop { Id = first, ServiceId = service, StationId = ReferenceData.StationVeyangoda, Sequence = 1, ScheduledDeparture = new(23, 50) },
            new ServiceStop { Id = middle, ServiceId = service, StationId = ReferenceData.StationGampaha, Sequence = 2, ScheduledArrival = new(12, 0), ScheduledDeparture = new(12, 5) },
            new ServiceStop { Id = last, ServiceId = service, StationId = ReferenceData.StationColomboFort, Sequence = 3, ScheduledArrival = new(23, 50) });
        var startDate = Friday.AddDays(-1); // Thursday: the journey ends exactly 24 hours later, on Friday at 23:50
        db.Journeys.Add(new Journey { Id = Guid.NewGuid(), ServiceId = service, OperatingDate = startDate, Status = JourneyStatus.Running, CurrentDelayMinutes = 0 });
        await db.SaveChangesAsync();

        Clock.SetUtcNow(Local(20, 0, date: Friday)); // today is Friday; this journey's operating date is Thursday
        Assert.Equal(HttpStatusCode.OK, (await Get("/api/v1/trains/9901/status")).StatusCode);

        Clock.SetUtcNow(Local(23, 50, date: Friday).AddMinutes(16)); // after the end and the grace period
        Assert.Equal(HttpStatusCode.NotFound, (await Get("/api/v1/trains/9901/status")).StatusCode);
    }
}
