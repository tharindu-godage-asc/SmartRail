using System.Net;
using System.Text;
using System.Text.Json;

// SmallBites: a stand-in for a railway caterer (spec 11 section 14). Its own system: its own API, website and admin page, no code or
// data shared with SmartRail. The menu lives in memory, loaded from menus.json, and resets on restart. It takes no orders or payments
// and receives nothing from SmartRail.

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton(sp => MenuStore.Load(Path.Combine(AppContext.BaseDirectory, "menus.json"), TimeProvider.System));
var app = builder.Build();

app.MapGet("/health", () => Results.Ok("ok"));

// The API SmartRail reads. Public and read-only (a real caterer would require a key; not built).
app.MapGet("/api/menus/{stationCode}", (string stationCode, MenuStore store) =>
    store.Get(stationCode) is { } menu ? Results.Ok(menu) : Results.NotFound());

// The customer website the app's button opens. A demonstration page: nothing can be ordered or paid here.
app.MapGet("/", (string? station, string? train, MenuStore store) =>
{
    var code = (station ?? "CF").ToUpperInvariant();
    var menu = store.Get(code);
    var html = new StringBuilder();
    html.Append("<h1>SmallBites</h1><p><strong>This is a demonstration page.</strong> No orders or payments are taken here.</p>");
    html.Append($"<p>Opened for station <b>{Html(station)}</b> and train <b>{Html(train)}</b>.</p>");
    if (menu is null)
    {
        html.Append("<p>No menu for this station.</p>");
    }
    else
    {
        html.Append("<ul>");
        foreach (var item in menu.Items)
        {
            html.Append($"<li>{Html(item.Names["en"])}: Rs {item.PriceLkr:0.00} ({(item.Available ? "available" : "sold out")})</li>");
        }

        html.Append("</ul>");
    }

    return Page("SmallBites", html.ToString());
});

// The admin page of the demonstration: toggle an item, which raises the menu version.
app.MapGet("/admin", (MenuStore store) =>
{
    var html = new StringBuilder("<h1>SmallBites admin</h1><p>Colombo Fort menu</p>");
    var menu = store.Get("CF")!;
    html.Append($"<p>Version {menu.Version}, updated {menu.UpdatedAt:HH:mm:ss} UTC</p>");
    foreach (var item in menu.Items)
    {
        var next = item.Available ? "false" : "true";
        html.Append($"<form method='post' action='/admin/items/{Html(item.Id)}/available?value={next}'><b>{Html(item.Names["en"])}</b> Rs {item.PriceLkr:0.00}: {(item.Available ? "Available" : "Sold out")} <button>{(item.Available ? "Mark sold out" : "Mark available")}</button></form>");
    }

    return Page("SmallBites admin", html.ToString());
});

app.MapPost("/admin/items/{id}/available", (string id, bool value, MenuStore store) =>
    store.SetAvailable("CF", id, value) ? Results.Redirect("/admin") : Results.NotFound());

app.Run();

static string Html(string? text) => WebUtility.HtmlEncode(text ?? "");

static IResult Page(string title, string body) =>
    Results.Content($"<!doctype html><html><head><meta charset='utf-8'><meta name='viewport' content='width=device-width,initial-scale=1'><title>{title}</title><style>body{{font-family:sans-serif;max-width:40rem;margin:1rem auto;padding:0 1rem}}form{{margin:.5rem 0}}</style></head><body>{body}</body></html>", "text/html; charset=utf-8");

public sealed record MenuItemDto(string Id, Dictionary<string, string> Names, decimal PriceLkr, bool Available);

public sealed record MenuDto(string Station, long Version, DateTimeOffset UpdatedAt, List<MenuItemDto> Items);

/// <summary>The in-memory menus. Changing an item raises the version and the updated time.</summary>
public sealed class MenuStore
{
    private readonly object _lock = new();
    private readonly Dictionary<string, MenuDto> _menus;
    private readonly TimeProvider _clock;

    private MenuStore(Dictionary<string, MenuDto> menus, TimeProvider clock)
    {
        _menus = menus;
        _clock = clock;
    }

    public static MenuStore Load(string path, TimeProvider clock)
    {
        var raw = JsonSerializer.Deserialize<Dictionary<string, List<MenuItemDto>>>(File.ReadAllText(path), new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        var now = clock.GetUtcNow();
        return new MenuStore(raw.ToDictionary(kv => kv.Key.ToUpperInvariant(), kv => new MenuDto(kv.Key.ToUpperInvariant(), 1, now, kv.Value)), clock);
    }

    public MenuDto? Get(string stationCode)
    {
        lock (_lock)
        {
            return _menus.TryGetValue(stationCode.ToUpperInvariant(), out var m) ? m with { Items = m.Items.ToList() } : null;
        }
    }

    public bool SetAvailable(string stationCode, string itemId, bool available)
    {
        lock (_lock)
        {
            if (!_menus.TryGetValue(stationCode.ToUpperInvariant(), out var menu))
            {
                return false;
            }

            var index = menu.Items.FindIndex(i => i.Id == itemId);
            if (index < 0)
            {
                return false;
            }

            if (menu.Items[index].Available != available)
            {
                var items = menu.Items.ToList();
                items[index] = items[index] with { Available = available };
                _menus[menu.Station] = menu with { Items = items, Version = menu.Version + 1, UpdatedAt = _clock.GetUtcNow() };
            }

            return true;
        }
    }
}

public partial class Program;
