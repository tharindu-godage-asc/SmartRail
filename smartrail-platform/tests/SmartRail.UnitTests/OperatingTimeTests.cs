using Microsoft.Extensions.Time.Testing;
using SmartRail.Domain;
using Xunit;

namespace SmartRail.UnitTests;

public class OperatingTimeTests
{
    private static readonly DateOnly Tuesday = new(2026, 10, 6);

    [Fact]
    public void Local_time_converts_to_utc_with_the_colombo_offset()
    {
        // 08:20 in Asia/Colombo (UTC+05:30) is 02:50 UTC.
        var instant = OperatingTime.ToInstant(Tuesday, new TimeOnly(8, 20));

        Assert.Equal(new DateTimeOffset(2026, 10, 6, 2, 50, 0, TimeSpan.Zero), instant);
        Assert.Equal(TimeSpan.Zero, instant.Offset);
    }

    [Fact]
    public void Operating_day_runs_from_local_midnight_to_the_next_local_midnight()
    {
        Assert.Equal(new DateTimeOffset(2026, 10, 5, 18, 30, 0, TimeSpan.Zero), OperatingTime.StartOfDay(Tuesday));
        Assert.Equal(new DateTimeOffset(2026, 10, 6, 18, 30, 0, TimeSpan.Zero), OperatingTime.StartOfNextDay(Tuesday));
    }

    [Fact]
    public void Demo_assignment_window_contains_every_time_of_the_day_and_nothing_outside_it()
    {
        var start = OperatingTime.StartOfDay(Tuesday);
        var end = OperatingTime.StartOfNextDay(Tuesday);

        foreach (var time in new[] { new TimeOnly(0, 0), new TimeOnly(8, 20), new TimeOnly(14, 0), new TimeOnly(23, 59) })
        {
            var instant = OperatingTime.ToInstant(Tuesday, time);
            Assert.True(start <= instant && instant < end, $"{time} should be inside the operating day");
        }

        Assert.False(OperatingTime.ToInstant(Tuesday.AddDays(1), new TimeOnly(0, 0)) < end); // the next day starts at the exclusive end
    }

    [Theory]
    [InlineData(2026, 10, 5, 19, 0, 2026, 10, 6)] // 19:00 UTC on the 5th is 00:30 on the 6th in Colombo
    [InlineData(2026, 10, 6, 2, 50, 2026, 10, 6)] // 08:20 local
    [InlineData(2026, 10, 6, 18, 29, 2026, 10, 6)] // 23:59 local, still the 6th
    [InlineData(2026, 10, 6, 18, 30, 2026, 10, 7)] // exactly local midnight
    public void Today_is_the_operating_date_in_the_colombo_zone(int y, int mo, int d, int h, int mi, int ey, int emo, int ed)
    {
        var clock = new FakeTimeProvider(new DateTimeOffset(y, mo, d, h, mi, 0, TimeSpan.Zero));

        Assert.Equal(new DateOnly(ey, emo, ed), OperatingTime.Today(clock));
    }
}
