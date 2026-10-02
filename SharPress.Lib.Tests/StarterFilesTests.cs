using SharPress.Lib.Services;

namespace SharPress.Lib.Tests;

public class StarterFilesTests
{
    [Fact]
    public async Task Uses_the_configured_names_and_escapes_them_in_json()
    {
        await using var site = await TestSite.StartAsync(options =>
        {
            // Quotes and a backslash would make the settings file invalid JSON if they weren't escaped.
            options.RootFolder = "My \"Docs\"\\v1";
            options.DocsFolder = "guides";
            options.StaticFolder = "assets";
            options.SettingsFile = "site.json";
            options.DocsUrl = "guide";
        });

        var files = Directory.EnumerateFiles(site.Folders.Root, "*", SearchOption.AllDirectories)
            .Select(file => Path.GetRelativePath(site.Folders.Root, file).Replace('\\', '/'))
            .Order(StringComparer.Ordinal)
            .ToList();
        Assert.Equal(
            ["Index.md", "assets/custom.css", "assets/favicon.svg", "assets/logo.svg", "guides/getting-started.md", "guides/writing-content.md", "site.json"],
            files);

        foreach (var file in files.Where(file => !file.EndsWith(".svg")))
        {
            Assert.DoesNotContain("{{", File.ReadAllText(Path.Combine(site.Folders.Root, file)));
        }

        var settings = await site.GetService<SiteSettingsService>().GetAsync();
        Assert.Equal("My Site", settings.Title);
        Assert.Equal("/guide/getting-started", settings.Sidebar[0].Items[0].Link);
    }

    [Fact]
    public async Task Does_not_overwrite_existing_files()
    {
        await using var site = await TestSite.StartAsync();
        site.WriteFile("Index.md", "# Mine\n");

        // Starting another app on the same folder runs the starter step again.
        await using var second = await TestSite.StartAsync(options => options.RootFolder = site.Folders.Root);

        Assert.Equal("# Mine\n", File.ReadAllText(Path.Combine(site.Folders.Root, "Index.md")));
    }

    [Fact]
    public async Task Empty_DocsUrl_is_rejected()
    {
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => TestSite.StartAsync(options => options.DocsUrl = "/"));

        Assert.Contains("DocsUrl", exception.Message);
    }
}
