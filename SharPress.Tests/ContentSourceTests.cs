using System.Net;
using Microsoft.Extensions.DependencyInjection;
using SharPress.Content;
using SharPress.Services;

namespace SharPress.Tests;

public class ContentSourceTests
{
    [Fact]
    public async Task Serves_the_home_page_and_docs_from_the_source()
    {
        var content = new InMemoryContent
        {
            Home = "# Welcome\n\nFrom the database.",
            Pages = { ["getting-started"] = "# Getting started\n\nHello from the source." },
        };
        await using var site = await StartAsync(content);

        var home = await site.Client.GetStringAsync("/");
        var docs = await site.Client.GetStringAsync("/docs/getting-started");

        Assert.Contains("From the database.", home);
        Assert.Contains("Hello from the source.", docs);
        Assert.Contains("<title>Getting started</title>", docs);
    }

    [Fact]
    public async Task Passes_the_slug_lower_cased_and_returns_404_for_a_missing_page()
    {
        var content = new InMemoryContent { Pages = { ["guide/install"] = "# Install\n" } };
        await using var site = await StartAsync(content);

        var found = await site.Client.GetAsync("/docs/Guide/Install/");
        var missing = await site.Client.GetAsync("/docs/nope");

        Assert.Equal(HttpStatusCode.OK, found.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Contains("guide/install", content.RequestedSlugs);
    }

    [Fact]
    public async Task Navigation_uses_the_listed_titles_and_falls_back_to_the_slug()
    {
        var content = new InMemoryContent
        {
            Pages = { ["setup/first-steps"] = "# Ignored\n", ["Alpha"] = "# Alpha\n" },
            Titles = { ["Alpha"] = "The alpha page" },
        };
        await using var site = await StartAsync(content);

        var navigation = await site.GetService<MarkdownPageService>().GetDocsNavigationAsync();

        var items = Assert.Single(navigation.Groups).Items;
        Assert.Equal(
            [new NavItem("The alpha page", "/docs/alpha"), new NavItem("first steps", "/docs/setup/first-steps")],
            items);
    }

    [Fact]
    public async Task Settings_come_from_the_source_and_are_read_once_per_request()
    {
        var content = new InMemoryContent
        {
            Pages = { ["intro"] = "# Intro\n" },
            Settings = new SiteSettings
            {
                Title = "Source Docs",
                Sidebar = [new SidebarItem { Text = "Start here", Link = "/docs/intro" }],
            },
        };
        await using var site = await StartAsync(content);

        var docs = await site.Client.GetStringAsync("/docs/intro");

        Assert.Contains("Source Docs", docs);
        Assert.Contains("Start here", docs);
        Assert.Equal(1, content.SettingsReads);
    }

    [Fact]
    public async Task Without_a_home_page_the_root_redirects_to_the_first_page()
    {
        var content = new InMemoryContent { Pages = { ["b"] = "# B\n", ["a"] = "# A\n" } };
        await using var site = await StartAsync(content);

        var root = await site.Client.GetAsync("/");

        Assert.Equal(HttpStatusCode.Redirect, root.StatusCode);
        Assert.Equal("/docs/a", root.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task Creates_no_starter_files_but_still_creates_the_static_folder()
    {
        await using var site = await StartAsync(new InMemoryContent());

        Assert.False(File.Exists(site.Folders.SettingsFile));
        Assert.False(File.Exists(site.Folders.IndexFile));
        Assert.False(Directory.Exists(site.Folders.Docs));
        Assert.True(Directory.Exists(site.Folders.Static));
    }

    [Fact]
    public async Task A_failing_source_still_gets_the_error_page()
    {
        var content = new InMemoryContent { Pages = { ["intro"] = "# Intro\n" }, FailSettings = true };
        await using var site = await StartAsync(content, environment: "Production");

        var response = await site.Client.GetAsync("/docs/intro");

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Contains("Something went wrong", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Docs_pages_ask_whether_there_is_a_home_page_without_loading_it()
    {
        var content = new InMemoryContent { Home = "# Home\n", Pages = { ["intro"] = "# Intro\n" } };
        await using var site = await StartAsync(content);

        var docs = await site.Client.GetStringAsync("/docs/intro");

        Assert.Contains("class=\"sp-nav-home\" href", docs);
        Assert.Equal(0, content.HomePageReads);
    }

    [Fact]
    public void Can_be_registered_before_AddSharPress()
    {
        var services = new ServiceCollection()
            .AddSharPressContentSource<InMemoryContentSource>(ServiceLifetime.Singleton)
            .AddSharPress();

        var registration = Assert.Single(services, service => service.ServiceType == typeof(ISharPressContentSource));
        Assert.Equal(typeof(InMemoryContentSource), registration.ImplementationType);
        Assert.Equal(ServiceLifetime.Singleton, registration.Lifetime);
    }

    [Fact]
    public async Task Defaults_to_the_files()
    {
        await using var site = await TestSite.StartAsync();

        Assert.IsType<FileContentSource>(site.GetService<ISharPressContentSource>());
    }

    private static Task<TestSite> StartAsync(InMemoryContent content, string environment = "Development") =>
        TestSite.StartAsync(environment: environment, configureBuilder: builder =>
        {
            builder.Services.AddSingleton(content);
            builder.AddSharPressContentSource<InMemoryContentSource>();
        });
}

/// <summary>The data behind <see cref="InMemoryContentSource"/>, shared by every request.</summary>
internal sealed class InMemoryContent
{
    private int _settingsReads;
    private int _homePageReads;

    public string? Home { get; init; }

    /// <summary>Makes reading the settings throw, like a database that is down.</summary>
    public bool FailSettings { get; init; }

    /// <summary>Markdown by slug, as a database might store it (any case).</summary>
    public Dictionary<string, string> Pages { get; } = [];

    /// <summary>Navigation titles by slug; pages without one get a null title.</summary>
    public Dictionary<string, string> Titles { get; } = [];

    public SiteSettings? Settings { get; init; }

    public List<string> RequestedSlugs { get; } = [];

    public int SettingsReads => _settingsReads;

    public void CountSettingsRead() => Interlocked.Increment(ref _settingsReads);

    public int HomePageReads => _homePageReads;

    public void CountHomePageRead() => Interlocked.Increment(ref _homePageReads);
}

/// <summary>A content source like one backed by a database: scoped, reading from shared data.</summary>
internal sealed class InMemoryContentSource(InMemoryContent content) : ISharPressContentSource
{
    public Task<string?> GetHomePageAsync(CancellationToken cancellationToken)
    {
        content.CountHomePageRead();
        return Task.FromResult(content.Home);
    }

    public Task<bool> HasHomePageAsync(CancellationToken cancellationToken) => Task.FromResult(content.Home is not null);

    public Task<string?> GetDocsPageAsync(string slug, CancellationToken cancellationToken)
    {
        lock (content.RequestedSlugs)
        {
            content.RequestedSlugs.Add(slug);
        }

        var match = content.Pages.FirstOrDefault(page => string.Equals(page.Key, slug, StringComparison.OrdinalIgnoreCase));
        return Task.FromResult<string?>(match.Value);
    }

    public Task<IReadOnlyList<DocsPageEntry>> GetDocsPagesAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<DocsPageEntry>>(
            [.. content.Pages.Keys.Select(slug => new DocsPageEntry(slug, content.Titles.GetValueOrDefault(slug)))]);

    public Task<SiteSettings?> GetSettingsAsync(CancellationToken cancellationToken)
    {
        content.CountSettingsRead();
        return content.FailSettings
            ? Task.FromException<SiteSettings?>(new InvalidOperationException("The database is down."))
            : Task.FromResult(content.Settings);
    }
}
