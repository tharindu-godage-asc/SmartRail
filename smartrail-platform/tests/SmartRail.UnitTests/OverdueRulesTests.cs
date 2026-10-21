using SmartRail.Domain.Journeys;
using Xunit;

namespace SmartRail.UnitTests;

/// <summary>"Overdue to start": a journey nothing has started although its first departure time has passed by more than the grace period.</summary>
public sealed class OverdueRulesTests
{
    private static readonly DateTimeOffset Departure = new(2026, 10, 6, 2, 50, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(JourneyStatus.Unassigned, 6, true)]
    [InlineData(JourneyStatus.Scheduled, 6, true)]
    [InlineData(JourneyStatus.Scheduled, 5, false)] // exactly at the grace period: not yet
    [InlineData(JourneyStatus.Scheduled, 0, false)]
    [InlineData(JourneyStatus.Scheduled, -30, false)] // departure still ahead
    [InlineData(JourneyStatus.Running, 60, false)]
    [InlineData(JourneyStatus.Completed, 600, false)]
    [InlineData(JourneyStatus.Cancelled, 60, false)]
    public void Only_a_not_started_journey_past_its_departure_and_the_grace_is_overdue(JourneyStatus status, int minutesAfterDeparture, bool overdue)
    {
        Assert.Equal(overdue, OverdueRules.IsOverdueToStart(status, Departure, Departure.AddMinutes(minutesAfterDeparture), 5));
    }

    [Fact]
    public void Minutes_late_are_whole_minutes_and_never_negative()
    {
        Assert.Equal(7, OverdueRules.MinutesLate(Departure, Departure.AddSeconds(7 * 60 + 59)));
        Assert.Equal(0, OverdueRules.MinutesLate(Departure, Departure.AddMinutes(-3)));
    }
}
