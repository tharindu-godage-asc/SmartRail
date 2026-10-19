using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace SmallBites.Tests;

public sealed class SmallBitesTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private static readonly string[] Languages = ["en", "si", "ta"];

    private static async Task<JsonElement> MenuAsync(HttpClient c, string code = "CF") =>
        JsonDocument.Parse(await c.GetStringAsync($"/api/menus/{code}")).RootElement;

    [Fact]
    public async Task Every_demo_item_has_en_si_and_ta_names()
    {
        var items = (await MenuAsync(factory.CreateClient())).GetProperty("items").EnumerateArray().ToList();

        Assert.NotEmpty(items);
        foreach (var item in items)
        {
            foreach (var language in Languages)
            {
                Assert.False(string.IsNullOrWhiteSpace(item.GetProperty("names").GetProperty(language).GetString()), $"{item.GetProperty("id")} {language}");
            }
        }
    }

    [Fact]
    public async Task Marking_an_item_sold_out_changes_it_and_raises_the_version()
    {
        var client = factory.WithWebHostBuilder(_ => { }).CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var before = (await MenuAsync(client)).GetProperty("version").GetInt64();

        var response = await client.PostAsync("/admin/items/fruit-cup/available?value=false", null);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var after = await MenuAsync(client);
        Assert.True(after.GetProperty("version").GetInt64() > before);
        Assert.False(after.GetProperty("items").EnumerateArray().Single(i => i.GetProperty("id").GetString() == "fruit-cup").GetProperty("available").GetBoolean());
    }

    [Fact]
    public async Task A_station_without_a_menu_is_404_and_so_is_an_unknown_item()
    {
        var client = factory.CreateClient();

        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/menus/VYG")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsync("/admin/items/nothing/available?value=true", null)).StatusCode);
    }

    [Fact]
    public async Task The_website_shows_the_station_and_train_it_was_opened_with_and_says_nothing_can_be_ordered()
    {
        var html = await factory.CreateClient().GetStringAsync("/?station=CF&train=8058");

        Assert.Contains("demonstration", html);
        Assert.Contains("CF", html);
        Assert.Contains("8058", html);
        Assert.DoesNotContain("<script", html);
    }

    [Fact]
    public async Task The_website_encodes_what_it_is_given()
    {
        var html = await factory.CreateClient().GetStringAsync("/?station=%3Cscript%3E&train=1");

        Assert.DoesNotContain("<script>", html);
    }
}
