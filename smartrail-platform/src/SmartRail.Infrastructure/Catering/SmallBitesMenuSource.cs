using System.Collections.Concurrent;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using SmartRail.Application.Features.Catering;

namespace SmartRail.Infrastructure.Catering;

/// <summary>
/// Reads station menus from SmallBites, the caterer's own system (spec 11 section 14). It keeps the last good menu per station in memory
/// with the time it was fetched and asks again at most every <see cref="CateringSettings.RefetchSeconds"/> seconds. When SmallBites does
/// not answer, the last good menu is returned and keeps ageing, so the read marks it stale after the freshness limit. SmartRail never
/// waits on the caterer for anything else (rule 11).
/// </summary>
public sealed class SmallBitesMenuSource(HttpClient http, CateringSettings settings, TimeProvider clock, ILogger<SmallBitesMenuSource> logger) : IMenuSource
{
    private sealed record Entry(MenuSnapshot? Snapshot, DateTimeOffset CheckedAt);

    private readonly ConcurrentDictionary<string, Entry> _cache = new();

    public async Task<MenuSnapshot?> GetMenuAsync(string stationCode, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(settings.SmallBitesUrl))
        {
            return null;
        }

        var now = clock.GetUtcNow();
        _cache.TryGetValue(stationCode, out var cached);
        if (cached is not null && now - cached.CheckedAt < TimeSpan.FromSeconds(settings.RefetchSeconds))
        {
            return cached.Snapshot;
        }

        try
        {
            using var response = await http.GetAsync($"{settings.SmallBitesUrl.TrimEnd('/')}/api/menus/{Uri.EscapeDataString(stationCode)}", cancellationToken);
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                CateringMetrics.Fetches.Add(1, new KeyValuePair<string, object?>("result", "none"));
                _cache[stationCode] = new Entry(null, now);
                return null;
            }

            response.EnsureSuccessStatusCode();
            var body = await response.Content.ReadFromJsonAsync<SmallBitesMenu>(cancellationToken)
                ?? throw new InvalidOperationException("Empty menu answer");
            var snapshot = new MenuSnapshot(
                body.UpdatedAt, now,
                body.Items.Select(i => new MenuItemView(i.Id, i.Names, i.PriceLkr, i.Available)).ToList());
            CateringMetrics.Fetches.Add(1, new KeyValuePair<string, object?>("result", "ok"));
            _cache[stationCode] = new Entry(snapshot, now);
            return snapshot;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidOperationException or System.Text.Json.JsonException)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                throw;
            }

            CateringMetrics.Fetches.Add(1, new KeyValuePair<string, object?>("result", "failed"));
            logger.LogWarning(ex, "SmallBites did not answer for station {StationCode}; the last good menu is used", stationCode);
            _cache[stationCode] = new Entry(cached?.Snapshot, now);
            return cached?.Snapshot;
        }
    }

    private sealed record SmallBitesMenu(
        [property: JsonPropertyName("updatedAt")] DateTimeOffset UpdatedAt,
        [property: JsonPropertyName("items")] List<SmallBitesItem> Items);

    private sealed record SmallBitesItem(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("names")] Dictionary<string, string> Names,
        [property: JsonPropertyName("priceLkr")] decimal PriceLkr,
        [property: JsonPropertyName("available")] bool Available);
}
