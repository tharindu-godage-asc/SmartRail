using SmartRail.Application.Features.Catering;
using SmartRail.Domain.Catering;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace SmartRail.UnitTests;

/// <summary>Spec 11: which stops get a menu, the link, and when a menu is marked out of date.</summary>
public sealed class CateringTests
{
    private static FoodStop Stop(int seq, bool catering, int? arrMin = null, int? depMin = null) =>
        new(seq, catering, arrMin is { } a ? new TimeOnly(8, 0).AddMinutes(a) : null, depMin is { } d ? new TimeOnly(8, 0).AddMinutes(d) : null);

    [Fact]
    public void The_destination_is_listed_when_it_has_catering()
    {
        var stops = new[] { Stop(1, false, null, 0), Stop(2, true, 10, null) };

        Assert.Equal([(2, "Destination")], FoodStopRule.Eligible(stops, 1, 2, 5));
    }

    [Fact]
    public void A_stop_of_five_minutes_is_listed_and_one_of_four_is_not()
    {
        var stops = new[] { Stop(1, false, null, 0), Stop(2, true, 10, 15), Stop(3, true, 20, 24), Stop(4, false, 30, null) };

        Assert.Equal([(2, "LongStop")], FoodStopRule.Eligible(stops, 1, 4, 5));
    }

    [Fact]
    public void The_boarding_station_and_stops_after_the_destination_are_not_listed()
    {
        var stops = new[] { Stop(1, true, null, 0), Stop(2, true, 10, 11), Stop(3, true, 20, null) };

        Assert.Equal([(2, "Destination")], FoodStopRule.Eligible(stops, 1, 2, 5));
    }

    [Fact]
    public void A_stop_with_no_arrival_or_departure_is_not_a_long_stop_and_a_station_without_catering_is_never_listed()
    {
        var stops = new[] { Stop(1, false, null, 0), Stop(2, true, null, 15), Stop(3, false, 30, null) };

        Assert.Empty(FoodStopRule.Eligible(stops, 1, 3, 5));
    }

    [Fact]
    public void The_link_carries_only_the_station_and_the_train()
    {
        Assert.Equal("http://localhost:8090/?station=CF&train=8058", GetTrainMenuHandler.BuildOrderUrl("http://localhost:8090/", "CF", "8058"));
        Assert.Equal("https://shop.example/menu?station=CF&train=a%26b%3D1", GetTrainMenuHandler.BuildOrderUrl("https://shop.example/menu?x=1", "CF", "a&b=1"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not a url")]
    [InlineData("javascript:alert(1)")]
    public void No_usable_setting_means_no_link(string setting) => Assert.Null(GetTrainMenuHandler.BuildOrderUrl(setting, "CF", "8058"));

    private sealed class FakeTrips(IReadOnlyList<MenuStopRecord>? stops) : ITripStops
    {
        public Task<IReadOnlyList<MenuStopRecord>?> GetStopsAsync(string trainNumber, CancellationToken cancellationToken) => Task.FromResult(stops);
    }

    private sealed class FakeSource(MenuSnapshot? snapshot) : IMenuSource
    {
        public Task<MenuSnapshot?> GetMenuAsync(string stationCode, CancellationToken cancellationToken) => Task.FromResult(snapshot);
    }

    private static readonly IReadOnlyList<MenuStopRecord> Trip =
    [
        new(1, "VYG", new Dictionary<string, string> { ["en"] = "Veyangoda" }, false, null, new TimeOnly(8, 20)),
        new(2, "CF", new Dictionary<string, string> { ["en"] = "Colombo Fort" }, true, new TimeOnly(8, 55), null),
    ];

    private static readonly DateTimeOffset T0 = new(2026, 10, 16, 3, 0, 0, TimeSpan.Zero);

    private static GetTrainMenuHandler Handler(FakeTimeProvider clock, MenuSnapshot? menu, IReadOnlyList<MenuStopRecord>? trip = null, bool unknownTrain = false) =>
        new(new FakeTrips(unknownTrain ? null : trip ?? Trip), new FakeSource(menu), new CateringSettings { WebsiteUrl = "http://localhost:8090/", MenuFreshnessMinutes = 60 }, clock);

    private static MenuSnapshot Menu(DateTimeOffset fetched) =>
        new(fetched, fetched, [new MenuItemView("fruit-cup", new Dictionary<string, string> { ["en"] = "Fruit cup" }, 180m, false)]);

    [Fact]
    public async Task A_fresh_menu_is_not_stale_and_an_old_one_is_after_the_limit()
    {
        var clock = new FakeTimeProvider(T0);
        var handler = Handler(clock, Menu(T0));

        Assert.False((await handler.HandleAsync("8058", null, null, default)).Response!.Stations[0].Stale);

        clock.Advance(TimeSpan.FromMinutes(60));
        Assert.False((await handler.HandleAsync("8058", null, null, default)).Response!.Stations[0].Stale);
        clock.Advance(TimeSpan.FromSeconds(1));
        Assert.True((await handler.HandleAsync("8058", null, null, default)).Response!.Stations[0].Stale);
    }

    [Fact]
    public async Task The_list_holds_the_destination_with_its_items_and_the_order_link()
    {
        var result = await Handler(new FakeTimeProvider(T0), Menu(T0)).HandleAsync("8058", "VYG", "CF", default);

        var station = Assert.Single(result.Response!.Stations);
        Assert.Equal("CF", station.Code);
        Assert.Equal("Destination", station.Reason);
        Assert.False(station.Items[0].Available);
        Assert.Equal("http://localhost:8090/?station=CF&train=8058", result.Response.OrderUrl);
    }

    [Fact]
    public async Task No_menu_fetched_yet_means_no_station_and_no_link()
    {
        var result = await Handler(new FakeTimeProvider(T0), null).HandleAsync("8058", null, null, default);

        Assert.Empty(result.Response!.Stations);
        Assert.Null(result.Response.OrderUrl);
    }

    [Fact]
    public async Task A_destination_without_catering_gives_an_empty_list()
    {
        var trip = new[] { Trip[0], Trip[1] with { StationCode = "MDA", HasCatering = false } };

        var result = await Handler(new FakeTimeProvider(T0), Menu(T0), trip).HandleAsync("8058", null, null, default);

        Assert.Empty(result.Response!.Stations);
    }

    [Fact]
    public async Task Unknown_train_unknown_station_and_wrong_order_are_errors()
    {
        var handler = Handler(new FakeTimeProvider(T0), Menu(T0));

        Assert.Equal(MenuError.StationNotFound, (await handler.HandleAsync("8058", "XXX", null, default)).Error);
        Assert.Equal(MenuError.InvalidStopOrder, (await handler.HandleAsync("8058", "CF", "VYG", default)).Error);
        Assert.Equal(MenuError.TrainNotFound, (await Handler(new FakeTimeProvider(T0), Menu(T0), unknownTrain: true).HandleAsync("9999", null, null, default)).Error);
    }
}
