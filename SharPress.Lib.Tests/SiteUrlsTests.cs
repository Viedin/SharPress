using SharPress.Lib.Services;

namespace SharPress.Lib.Tests;

public class SiteUrlsTests
{
    [Theory]
    [InlineData("/docs/intro", "", "/docs/intro")]
    [InlineData("/docs/intro", "/myapp", "/myapp/docs/intro")]
    [InlineData("relative", "/myapp", "relative")]
    [InlineData("#id", "/myapp", "#id")]
    [InlineData("//cdn.example.com/x.js", "/myapp", "//cdn.example.com/x.js")]
    [InlineData("https://example.com", "/myapp", "https://example.com")]
    public void WithPathBase_only_changes_site_root_urls(string href, string pathBase, string expected)
    {
        Assert.Equal(expected, SiteUrls.WithPathBase(href, pathBase));
    }
}
