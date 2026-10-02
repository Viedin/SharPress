using SharPress.Lib.Services;

namespace SharPress.Lib.Tests;

public class MarkdownTests
{
    [Fact]
    public void Heading_ids_follow_GitHub_rules()
    {
        var page = MarkdownPageService.Render("## Hello, World! `x.y` 1.0");

        Assert.Contains("id=\"hello-world-xy-10\"", page.Html);
    }

    [Fact]
    public void Title_is_the_first_h1_and_the_outline_has_h2_and_h3_only()
    {
        var page = MarkdownPageService.Render("# Title\n\n## Two\n\n### Three\n\n#### Four\n");

        Assert.Equal("Title", page.Title);
        Assert.Equal(
            [new PageHeading(2, "Two", "two"), new PageHeading(3, "Three", "three")],
            page.Headings);
    }

    [Fact]
    public void Only_site_root_links_get_the_path_base()
    {
        var page = MarkdownPageService.Render(
            "[a](/docs/x) [b](relative) [c](#id) [d](https://example.com) ![e](/logo.svg)",
            pathBase: "/myapp");

        Assert.Contains("href=\"/myapp/docs/x\"", page.Html);
        Assert.Contains("href=\"relative\"", page.Html);
        Assert.Contains("href=\"#id\"", page.Html);
        Assert.Contains("href=\"https://example.com\"", page.Html);
        Assert.Contains("src=\"/myapp/logo.svg\"", page.Html);
    }
}
