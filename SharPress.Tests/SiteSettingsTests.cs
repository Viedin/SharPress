using SharPress.Services;

namespace SharPress.Tests;

public class SiteSettingsTests
{
    [Fact]
    public async Task Null_lists_become_empty_lists()
    {
        await using var site = await TestSite.StartAsync();
        site.WriteFile("sharpress.json", """
            {
              "sidebar": [{ "text": "Group", "items": null }],
              "home": { "hero": { "actions": null }, "features": null }
            }
            """);

        var settings = await site.GetService<SiteSettingsService>().GetAsync();

        Assert.Empty(settings.Sidebar[0].Items);
        Assert.Empty(settings.Home!.Features);
        Assert.Empty(settings.Home.Hero!.Actions);
    }

    [Fact]
    public async Task Null_sidebar_still_renders_docs_pages()
    {
        await using var site = await TestSite.StartAsync();
        site.WriteFile("sharpress.json", """{ "sidebar": null }""");

        var response = await site.Client.GetAsync("/docs/getting-started");

        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task Allows_comments_trailing_commas_and_any_casing()
    {
        await using var site = await TestSite.StartAsync();
        site.WriteFile("sharpress.json", """
            {
              // A comment
              "TITLE": "Docs",
            }
            """);

        var settings = await site.GetService<SiteSettingsService>().GetAsync();

        Assert.Equal("Docs", settings.Title);
    }

    [Fact]
    public async Task Invalid_json_falls_back_to_defaults()
    {
        await using var site = await TestSite.StartAsync();
        site.WriteFile("sharpress.json", "{ not json");

        var settings = await site.GetService<SiteSettingsService>().GetAsync();

        Assert.Null(settings.Title);
        Assert.Empty(settings.Sidebar);
    }

    [Fact]
    public async Task Missing_file_gives_defaults()
    {
        await using var site = await TestSite.StartAsync(options => options.CreateStarterFiles = false);

        var settings = await site.GetService<SiteSettingsService>().GetAsync();

        Assert.Null(settings.Title);
    }
}
