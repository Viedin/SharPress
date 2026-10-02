using SharPress.Services;

namespace SharPress.Tests;

public class NavigationTests
{
    [Fact]
    public async Task Without_a_sidebar_lists_every_page_by_path_with_its_title()
    {
        await using var site = await TestSite.StartAsync();
        site.WriteFile("sharpress.json", "{}");
        site.WriteFile("docs/guides/setup.md", "# Setting up\n");

        var navigation = await site.GetService<MarkdownPageService>().GetDocsNavigationAsync();

        var items = Assert.Single(navigation.Groups).Items;
        Assert.Equal(
            ["/docs/getting-started", "/docs/guides/setup", "/docs/writing-content"],
            items.Select(item => item.Href));
        Assert.Equal("Setting up", items[1].Title);
    }

    [Fact]
    public async Task Sidebar_links_become_site_root_urls_and_external_links_are_kept()
    {
        await using var site = await TestSite.StartAsync();
        site.WriteFile("sharpress.json", """
            {
              "sidebar": [
                { "link": "docs/getting-started" },
                { "text": "GitHub", "link": "https://github.com" }
              ]
            }
            """);

        var navigation = await site.GetService<MarkdownPageService>().GetDocsNavigationAsync();

        var items = Assert.Single(navigation.Groups).Items;
        Assert.Equal(new NavItem("Getting started", "/docs/getting-started"), items[0]);
        Assert.Equal(new NavItem("GitHub", "https://github.com"), items[1]);
    }

    [Fact]
    public void Neighbours_follow_the_navigation_order_and_skip_external_links()
    {
        var navigation = new DocsNavigation(null, null, null,
        [
            new NavGroup("A", [new NavItem("One", "/docs/one"), new NavItem("Out", "https://example.com")]),
            new NavGroup("B", [new NavItem("Two", "/docs/two"), new NavItem("Three", "/docs/three")]),
        ]);

        var (previous, next) = navigation.GetNeighbours("/docs/Two");

        Assert.Equal("One", previous?.Title);
        Assert.Equal("Three", next?.Title);
        Assert.Equal((null, null), navigation.GetNeighbours("/docs/missing"));
    }
}
