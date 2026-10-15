using SmartRail.Domain;
using SmartRail.Domain.Journeys;
using Xunit;

namespace SmartRail.UnitTests;

/// <summary>Spec 07: which reports raise an exception, and how severe it is (AC-E05-02 1, 2, 5; Q7). No database.</summary>
public class ExceptionPolicyTests
{
    private static readonly DateOnly Tuesday = new(2026, 10, 6);

    private static DateTimeOffset At(int hour, int minute, int second = 0) =>
        OperatingTime.ToInstant(Tuesday, new TimeOnly(hour, minute, second));

    // ---- delay reports (AC-E05-02 1) ----

    [Theory]
    [InlineData("TechnicalFault")]
    [InlineData("Obstruction")]
    [InlineData("Weather")]
    public void A_fault_an_obstruction_or_the_weather_is_Critical(string reason)
    {
        var raised = ExceptionPolicy.ForDelay(reason);

        Assert.Equal(ExceptionType.ReasonAwaitingVerification, raised.Type);
        Assert.Equal(ExceptionSeverity.Critical, raised.Severity);
    }

    [Theory]
    [InlineData("SignalIssue")]
    [InlineData("Unknown")]
    public void A_signal_issue_or_an_unknown_reason_is_High(string reason)
    {
        var raised = ExceptionPolicy.ForDelay(reason);

        Assert.Equal(ExceptionType.ReasonAwaitingVerification, raised.Type);
        Assert.Equal(ExceptionSeverity.High, raised.Severity);
    }

    [Fact]
    public void Every_reason_the_API_accepts_has_a_severity()
    {
        // If a reason is added to the accepted list, this test says it needs a decision here too.
        var known = new[] { "SignalIssue", "TechnicalFault", "Obstruction", "Weather", "Unknown" };
        Assert.All(known, reason => Assert.NotNull(ExceptionPolicy.ForDelay(reason)));
    }

    // ---- early departures (Q7: at least 30 seconds early) ----

    [Fact]
    public void A_departure_three_minutes_early_raises_a_High_early_departure()
    {
        var raised = ExceptionPolicy.ForDeparture(At(8, 27), At(8, 30));

        Assert.Equal(new RaisedException(ExceptionType.EarlyDeparture, ExceptionSeverity.High), raised);
    }

    [Fact]
    public void Exactly_thirty_seconds_early_raises_one()
    {
        Assert.NotNull(ExceptionPolicy.ForDeparture(At(8, 29, 30), At(8, 30)));
    }

    [Fact]
    public void Twenty_nine_seconds_early_raises_none()
    {
        Assert.Null(ExceptionPolicy.ForDeparture(At(8, 29, 31), At(8, 30)));
    }

    [Theory]
    [InlineData(8, 30, 0)]  // exactly on time
    [InlineData(8, 31, 0)]  // late
    [InlineData(9, 0, 0)]   // very late
    public void An_on_time_or_late_departure_raises_none(int hour, int minute, int second)
    {
        Assert.Null(ExceptionPolicy.ForDeparture(At(hour, minute, second), At(8, 30)));
    }

    [Fact]
    public void A_stop_with_no_scheduled_departure_cannot_be_early()
    {
        Assert.Null(ExceptionPolicy.ForDeparture(At(8, 0), null));
    }

    [Fact]
    public void The_threshold_is_the_one_the_owner_chose()
    {
        Assert.Equal(TimeSpan.FromSeconds(30), ExceptionPolicy.EarlyDepartureThreshold);
    }

    // ---- the fixed resolve actions (Q3) ----

    [Fact]
    public void There_are_exactly_three_resolve_actions()
    {
        Assert.Equal(["Acknowledged", "NoActionNeeded", "Escalated"], ExceptionResolution.All);
    }
}
