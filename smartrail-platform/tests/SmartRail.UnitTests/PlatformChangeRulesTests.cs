using SmartRail.Domain.Journeys;
using Xunit;

namespace SmartRail.UnitTests;

/// <summary>Spec 20: the platform advice (FR-57) at its edges. It is advice only, so these tests only decide what is reported.</summary>
public sealed class PlatformChangeRulesTests
{
    private static readonly DateTimeOffset T = new(2026, 10, 6, 3, 10, 0, TimeSpan.Zero);

    private static PlatformUse Use(string train, int minutes, string direction = "Inbound") => new(train, direction, T.AddMinutes(minutes));

    [Fact]
    public void A_train_exactly_at_the_window_is_reported_and_one_minute_beyond_is_not()
    {
        var found = PlatformChangeRules.Clashes(T, "Inbound", [Use("A", 10), Use("B", -10), Use("C", 11)], 10);

        Assert.Equal(["A", "B"], found.Select(c => c.TrainNumber).Order().ToArray());
        Assert.All(found, c => Assert.Equal(10, c.MinutesApart));
    }

    [Fact]
    public void Opposite_directions_are_marked_and_the_same_direction_is_not()
    {
        var found = PlatformChangeRules.Clashes(T, "Inbound", [Use("same", 3, "Inbound"), Use("opposite", 4, "Outbound")], 10);

        Assert.False(found.Single(c => c.TrainNumber == "same").OppositeDirection);
        Assert.True(found.Single(c => c.TrainNumber == "opposite").OppositeDirection);
    }

    [Fact]
    public void The_nearest_train_comes_first_and_no_other_train_means_no_advice()
    {
        var found = PlatformChangeRules.Clashes(T, "Inbound", [Use("far", 9), Use("near", -2)], 10);

        Assert.Equal(["near", "far"], found.Select(c => c.TrainNumber).ToArray());
        Assert.Empty(PlatformChangeRules.Clashes(T, "Inbound", [], 10));
    }
}
