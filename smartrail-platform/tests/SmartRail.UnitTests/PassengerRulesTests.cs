using SmartRail.Application.Features.Passenger;
using SmartRail.Domain.Journeys;
using Xunit;

namespace SmartRail.UnitTests;

public class ServiceCalendarTests
{
    private static readonly DateOnly Monday = new(2026, 10, 5);

    [Theory]
    [InlineData("Daily", true, true, true, true, true, true, true)]
    [InlineData("Weekdays", true, true, true, true, true, false, false)]
    [InlineData("Weekends", false, false, false, false, false, true, true)]
    public void Each_supported_calendar_gives_the_right_answer_for_every_weekday(
        string runsOn, bool mon, bool tue, bool wed, bool thu, bool fri, bool sat, bool sun)
    {
        var expected = new[] { mon, tue, wed, thu, fri, sat, sun };

        for (var offset = 0; offset < 7; offset++)
        {
            Assert.Equal(expected[offset], ServiceCalendar.Runs(runsOn, Monday.AddDays(offset)));
        }
    }

    [Theory]
    [InlineData("weekdays")]
    [InlineData("WEEKDAYS")]
    [InlineData("  Weekdays  ")]
    public void Case_and_spacing_do_not_matter(string runsOn) => Assert.True(ServiceCalendar.Runs(runsOn, Monday));

    [Theory]
    [InlineData("Mon-Sat")]
    [InlineData("Except holidays")]
    [InlineData("")]
    [InlineData(null)]
    public void Unsupported_text_is_never_guessed(string? runsOn) => Assert.Null(ServiceCalendar.Runs(runsOn, Monday));
}

public class StopPassedTests
{
    private static StopMark[] Marks(params StopConfirmation[] confirmations) =>
        confirmations.Select((c, i) => new StopMark(i + 1, c)).ToArray();

    [Fact]
    public void Nothing_reported_means_nothing_has_left()
    {
        Assert.False(StopPassed.HasDeparted(Marks(StopConfirmation.Pending, StopConfirmation.Pending, StopConfirmation.Pending), 1));
    }

    [Fact]
    public void A_reported_departure_from_the_stop_means_it_has_left()
    {
        var marks = Marks(StopConfirmation.Reported, StopConfirmation.Pending, StopConfirmation.Pending);

        Assert.True(StopPassed.HasDeparted(marks, 1));
        Assert.False(StopPassed.HasDeparted(marks, 2));
    }

    [Fact]
    public void A_report_from_a_later_stop_means_the_earlier_stops_were_left_even_if_skipped()
    {
        var marks = Marks(StopConfirmation.Inferred, StopConfirmation.Reported, StopConfirmation.Pending);

        Assert.True(StopPassed.HasDeparted(marks, 1));
        Assert.True(StopPassed.HasDeparted(marks, 2));
        Assert.False(StopPassed.HasDeparted(marks, 3));
    }
}

public class EntityTagMatcherTests
{
    private const string Current = "\"abc123\"";

    [Theory]
    [InlineData("\"abc123\"", true)]
    [InlineData("W/\"abc123\"", true)]
    [InlineData("w/\"abc123\"", true)]
    [InlineData("\"zzz\", \"abc123\"", true)]
    [InlineData("\"zzz\",W/\"abc123\", \"yyy\"", true)]
    [InlineData("*", true)]
    [InlineData("\"zzz\"", false)]
    [InlineData("\"zzz\", \"yyy\"", false)]
    [InlineData("W/\"zzz\"", false)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData(",,,", false)]
    public void The_header_is_compared_as_the_spec_says(string header, bool expected) =>
        Assert.Equal(expected, EntityTagMatcher.Matches(header, Current));

    [Fact]
    public void A_missing_header_never_matches() => Assert.False(EntityTagMatcher.Matches(null, Current));

    [Fact]
    public void The_current_tag_may_itself_be_weak() => Assert.True(EntityTagMatcher.Matches("\"abc123\"", "W/\"abc123\""));
}

public class StationListVersionTests
{
    [Fact]
    public void Equal_content_gives_an_equal_version_and_it_is_16_hex_characters()
    {
        var a = StationListVersion.Compute(["CF|Colombo Fort|x|y|MAIN:1", "MDA|Maradana|x|y|MAIN:2"]);
        var b = StationListVersion.Compute(["CF|Colombo Fort|x|y|MAIN:1", "MDA|Maradana|x|y|MAIN:2"]);

        Assert.Equal(a, b);
        Assert.Matches("^[0-9a-f]{16}$", a);
    }

    [Fact]
    public void Any_change_gives_a_different_version()
    {
        var original = StationListVersion.Compute(["CF|Colombo Fort|කොළඹ|கொழும்பு|MAIN:1"]);

        Assert.NotEqual(original, StationListVersion.Compute(["CF|Colombo Fort|කොළඹ|கொழும்பு|MAIN:2"]));
        Assert.NotEqual(original, StationListVersion.Compute(["CF|Colombo Fort|කොළඹ කොටුව|கொழும்பு|MAIN:1"]));
        Assert.NotEqual(original, StationListVersion.Compute(["CF|Colombo Fort|කොළඹ|கொழும்பு|MAIN:1", "MDA|Maradana|a|b|MAIN:2"]));
    }

    [Fact]
    public async Task The_handler_lists_stations_from_the_line_origin_and_falls_back_to_english()
    {
        var queries = new FakePassengerQueries();
        var handler = new GetStationsHandler(queries);

        var response = await handler.HandleAsync(CancellationToken.None);

        Assert.Equal(["CF", "MDA", "RGM", "GPH", "VYG"], response.Stations.Select(s => s.Code));
        var maradana = response.Stations.Single(s => s.Code == "MDA");
        Assert.Equal("Maradana", maradana.Name.En);
        Assert.Equal("Maradana", maradana.Name.Si); // no translation in the fake: the English name is the safety net
        Assert.Equal(1, response.Stations[0].Lines.Single().Sequence);
    }

    [Fact]
    public async Task The_version_is_the_same_on_every_call_and_changes_when_a_name_changes()
    {
        var queries = new FakePassengerQueries();
        var first = await new GetStationsHandler(queries).HandleAsync(CancellationToken.None);
        var second = await new GetStationsHandler(queries).HandleAsync(CancellationToken.None);

        queries.RenameStation("GPH", "Gampaha Town");
        var changed = await new GetStationsHandler(queries).HandleAsync(CancellationToken.None);

        Assert.Equal(first.Version, second.Version);
        Assert.NotEqual(first.Version, changed.Version);
    }
}
