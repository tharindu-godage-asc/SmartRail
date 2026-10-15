using SmartRail.Application.Features.Events.ReportEvent;
using SmartRail.Application.Features.Journeys;
using Xunit;

namespace SmartRail.UnitTests;

/// <summary>R1 and R3 at their exact edges (spec section 10.2).</summary>
public class EventRulesTests
{
    private static readonly DateTimeOffset From = new(2026, 10, 6, 2, 55, 0, TimeSpan.Zero); // 08:25 Colombo
    private static readonly DateTimeOffset To = new(2026, 10, 6, 3, 10, 0, TimeSpan.Zero); // 08:40 Colombo

    private static AssignmentInfo[] Window(string subject = "nimal") => [new(subject, From, To)];

    [Fact]
    public void An_assignment_includes_its_start_and_excludes_its_end()
    {
        Assert.True(EventRules.IsAssignedAt(Window(), "nimal", From));
        Assert.True(EventRules.IsAssignedAt(Window(), "nimal", To - TimeSpan.FromTicks(1)));
        Assert.False(EventRules.IsAssignedAt(Window(), "nimal", From - TimeSpan.FromTicks(1)));
        Assert.False(EventRules.IsAssignedAt(Window(), "nimal", To));
    }

    [Fact]
    public void An_assignment_with_no_end_runs_until_the_journey_ends()
    {
        AssignmentInfo[] open = [new("nimal", From, null)];

        Assert.True(EventRules.IsAssignedAt(open, "nimal", From.AddDays(30)));
        Assert.False(EventRules.IsAssignedAt(open, "nimal", From.AddTicks(-1)));
    }

    [Fact]
    public void Only_the_assigned_operator_is_authorised()
    {
        Assert.False(EventRules.IsAssignedAt(Window(), "someone-else", From.AddMinutes(1)));
        Assert.False(EventRules.IsAssignedAt([], "nimal", From.AddMinutes(1)));
    }

    [Fact]
    public void Any_one_of_several_assignments_is_enough()
    {
        AssignmentInfo[] two = [new("nimal", From, From.AddMinutes(5)), new("nimal", To, null)];

        Assert.True(EventRules.IsAssignedAt(two, "nimal", To.AddHours(1)));
        Assert.False(EventRules.IsAssignedAt(two, "nimal", From.AddMinutes(10)));
    }

    [Fact]
    public void The_time_limits_are_inclusive_at_one_minute_ahead_and_eight_hours_ago()
    {
        var limits = new EventLimits();
        var now = new DateTimeOffset(2026, 10, 6, 3, 0, 0, TimeSpan.Zero);

        Assert.True(EventRules.IsWithinTimeLimits(now.AddMinutes(1), now, limits));
        Assert.False(EventRules.IsWithinTimeLimits(now.AddMinutes(1).AddTicks(1), now, limits));
        Assert.True(EventRules.IsWithinTimeLimits(now.AddHours(-8), now, limits));
        Assert.False(EventRules.IsWithinTimeLimits(now.AddHours(-8).AddTicks(-1), now, limits));
        Assert.True(EventRules.IsWithinTimeLimits(now, now, limits));
    }

    [Fact]
    public void The_limits_follow_the_configured_values()
    {
        var limits = new EventLimits { MaxFutureMinutes = 5, MaxAgeHours = 1 };
        var now = new DateTimeOffset(2026, 10, 6, 3, 0, 0, TimeSpan.Zero);

        Assert.True(EventRules.IsWithinTimeLimits(now.AddMinutes(5), now, limits));
        Assert.False(EventRules.IsWithinTimeLimits(now.AddHours(-2), now, limits));
    }

    [Fact]
    public void The_defaults_are_the_values_in_the_spec()
    {
        var limits = new EventLimits();
        var settings = new JourneySettings();

        Assert.Equal((1, 8), (limits.MaxFutureMinutes, limits.MaxAgeHours));
        Assert.Equal((2, 15), (settings.OnTimeToleranceMinutes, settings.VisibleAfterEndMinutes));
    }
}
