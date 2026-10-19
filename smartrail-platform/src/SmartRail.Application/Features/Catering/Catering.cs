using System.Diagnostics.Metrics;
using SmartRail.Domain.Catering;

namespace SmartRail.Application.Features.Catering;

/// <summary>Catering:* settings (spec 11 section 14). The link is built from one setting, so the app has no address (FR-124).</summary>
public sealed class CateringSettings
{
    public const string Section = "Catering";

    /// <summary>The caterer website (SmallBites). Empty means no link, and the app then shows no button.</summary>
    public string WebsiteUrl { get; set; } = "";

    /// <summary>Where SmartRail reads the menu from (the SmallBites API). Empty means no menus.</summary>
    public string SmallBitesUrl { get; set; } = "";

    public int MenuFreshnessMinutes { get; set; } = 60;
    public int MinLongStopMinutes { get; set; } = 5;

    /// <summary>A station menu is fetched again at most this often.</summary>
    public int RefetchSeconds { get; set; } = 15;
}

public sealed record MenuItemView(string ItemId, IReadOnlyDictionary<string, string> Names, decimal PriceLkr, bool Available);

/// <summary>A menu as SmallBites sent it, with the time SmartRail last got it (not the time SmallBites changed it).</summary>
public sealed record MenuSnapshot(DateTimeOffset UpdatedAt, DateTimeOffset FetchedAt, IReadOnlyList<MenuItemView> Items);

public sealed record MenuStationView(
    string Code,
    IReadOnlyDictionary<string, string> Names,
    string Reason,
    DateTimeOffset MenuUpdatedAt,
    DateTimeOffset FetchedAt,
    bool Stale,
    IReadOnlyList<MenuItemView> Items);

public sealed record TrainMenuResponse(
    DateTimeOffset ServerTime,
    string TrainNumber,
    string? OrderUrl,
    int StaleAfterMinutes,
    IReadOnlyList<MenuStationView> Stations);

public enum MenuError { None, TrainNotFound, StationNotFound, InvalidStopOrder }

public sealed record TrainMenuResult(MenuError Error, TrainMenuResponse? Response)
{
    public static TrainMenuResult Fail(MenuError error) => new(error, null);
}

/// <summary>A stop of the service with what the menu rule needs.</summary>
public sealed record MenuStopRecord(int Sequence, string StationCode, IReadOnlyDictionary<string, string> StationNames, bool HasCatering, TimeOnly? Arrival, TimeOnly? Departure);

public interface ITripStops
{
    /// <summary>The stops of a train in order, or null when no service has that number.</summary>
    Task<IReadOnlyList<MenuStopRecord>?> GetStopsAsync(string trainNumber, CancellationToken cancellationToken);
}

/// <summary>The caterer system (SmallBites). Every external call goes through here (architecture rules).</summary>
public interface IMenuSource
{
    /// <summary>The station menu, the last good one when the caterer does not answer, or null when none was ever fetched.</summary>
    Task<MenuSnapshot?> GetMenuAsync(string stationCode, CancellationToken cancellationToken);
}

public static class CateringMetrics
{
    public static readonly Meter Meter = new("SmartRail.Catering");
    public static readonly Counter<long> Reads = Meter.CreateCounter<long>("smartrail.menu.reads", description: "Menu reads by outcome");
    public static readonly Counter<long> Fetches = Meter.CreateCounter<long>("smartrail.menu.fetches", description: "Menu fetches from the caterer by result");
}

/// <summary>GET /api/v1/trains/{trainNumber}/menu: the read-only list for the stations of a trip (FR-90, FR-91).</summary>
public sealed class GetTrainMenuHandler(ITripStops trips, IMenuSource menus, CateringSettings settings, TimeProvider clock)
{
    public async Task<TrainMenuResult> HandleAsync(string trainNumber, string? fromCode, string? toCode, CancellationToken cancellationToken)
    {
        var number = trainNumber.Trim();
        var stops = await trips.GetStopsAsync(number, cancellationToken);
        if (stops is null || stops.Count == 0)
        {
            return Fail(MenuError.TrainNotFound);
        }

        var from = string.IsNullOrWhiteSpace(fromCode) ? stops[0] : stops.FirstOrDefault(s => s.StationCode.Equals(fromCode.Trim(), StringComparison.OrdinalIgnoreCase));
        var to = string.IsNullOrWhiteSpace(toCode) ? stops[^1] : stops.FirstOrDefault(s => s.StationCode.Equals(toCode.Trim(), StringComparison.OrdinalIgnoreCase));
        if (from is null || to is null)
        {
            return Fail(MenuError.StationNotFound);
        }

        if (from.Sequence >= to.Sequence)
        {
            return Fail(MenuError.InvalidStopOrder);
        }

        var eligible = FoodStopRule.Eligible(
            stops.Select(s => new FoodStop(s.Sequence, s.HasCatering, s.Arrival, s.Departure)).ToList(),
            from.Sequence, to.Sequence, settings.MinLongStopMinutes);

        var now = clock.GetUtcNow();
        var views = new List<MenuStationView>();
        foreach (var (sequence, reason) in eligible)
        {
            var stop = stops.First(s => s.Sequence == sequence);
            var menu = await menus.GetMenuAsync(stop.StationCode, cancellationToken);
            if (menu is null)
            {
                continue; // a station with catering but no menu yet is left out, so no empty section
            }

            views.Add(new MenuStationView(
                stop.StationCode, stop.StationNames, reason, menu.UpdatedAt, menu.FetchedAt,
                now - menu.FetchedAt > TimeSpan.FromMinutes(settings.MenuFreshnessMinutes), menu.Items));
        }

        // The link is for the first listed station only and carries the station and the train, nothing else (NFR-21, EC-186).
        var orderUrl = views.Count == 0 ? null : BuildOrderUrl(settings.WebsiteUrl, views[0].Code, number);
        CateringMetrics.Reads.Add(1, new KeyValuePair<string, object?>("outcome", views.Count == 0 ? "empty" : "menu"));
        return new TrainMenuResult(MenuError.None, new TrainMenuResponse(now, number, orderUrl, settings.MenuFreshnessMinutes, views));
    }

    private static TrainMenuResult Fail(MenuError error)
    {
        CateringMetrics.Reads.Add(1, new KeyValuePair<string, object?>("outcome", error.ToString()));
        return TrainMenuResult.Fail(error);
    }

    public static string? BuildOrderUrl(string websiteUrl, string stationCode, string trainNumber)
    {
        if (!Uri.TryCreate(websiteUrl?.Trim(), UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            return null;
        }

        var builder = new UriBuilder(uri) { Query = $"station={Uri.EscapeDataString(stationCode)}&train={Uri.EscapeDataString(trainNumber)}" };
        return builder.Uri.ToString();
    }
}
