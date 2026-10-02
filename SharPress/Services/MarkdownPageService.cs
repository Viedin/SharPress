using System.Collections.Concurrent;
using Markdig;
using Markdig.Extensions.AutoIdentifiers;
using Markdig.Renderers.Html;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SharPress.Startup;

namespace SharPress.Services;

/// <summary>A heading found in a markdown document, used to build the "On this page" outline.</summary>
public sealed record PageHeading(int Level, string Text, string Id);

/// <summary>A rendered markdown page together with its heading outline.</summary>
public sealed record MarkdownPage(string? Title, string Html, IReadOnlyList<PageHeading> Headings);

/// <summary>
/// A link in the docs navigation. <paramref name="Href"/> is a site-root URL such as "/docs/intro", without the
/// app's path base, or an external link.
/// </summary>
public sealed record NavItem(string Title, string Href);

/// <summary>A set of navigation links, optionally under a heading.</summary>
public sealed record NavGroup(string? Title, IReadOnlyList<NavItem> Items);

/// <summary>The docs navigation: an optional label above the groups of links.</summary>
public sealed record DocsNavigation(string? SiteTitle, string? SiteLogo, string? Title, IReadOnlyList<NavGroup> Groups)
{
    /// <summary>Whether the site has a home page, so the site title links to it.</summary>
    public bool HasHomePage { get; init; }

    /// <summary>
    /// The first link in the navigation to a docs page that exists, or null if there is none. Links to the home
    /// page, other sites or missing pages are skipped, so a docs-only site never redirects its root to itself or
    /// to "Page not found".
    /// </summary>
    public NavItem? FirstPage { get; init; }

    /// <summary>
    /// Finds the pages before and after the given docs page, following the order of the navigation.
    /// Both are null if the page isn't in the navigation.
    /// </summary>
    /// <param name="href">The page's site-root URL, for example "/docs/getting-started".</param>
    public (NavItem? Previous, NavItem? Next) GetNeighbours(string href)
    {
        var current = href.Trim('/');
        var pages = Groups
            .SelectMany(group => group.Items)
            .Where(item => item.Href.StartsWith('/'))
            .ToList();

        var index = pages.FindIndex(item => string.Equals(item.Href.Trim('/'), current, StringComparison.OrdinalIgnoreCase));
        if (index < 0)
        {
            return (null, null);
        }

        return (index > 0 ? pages[index - 1] : null, index < pages.Count - 1 ? pages[index + 1] : null);
    }
}

internal sealed class MarkdownPageService(
    SiteFolders folders,
    SiteSettingsService settingsService,
    IHostEnvironment environment,
    ILogger<MarkdownPageService> logger)
{
    /// <summary>
    /// Sidebar links already warned about. The navigation is built on every request, so without this a broken
    /// link would be logged on every page view.
    /// </summary>
    private readonly ConcurrentDictionary<string, byte> _reportedMissingLinks = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Headings outside this level range (e.g. the h1 page title) are left out of the outline.</summary>
    private const int MinOutlineLevel = 2;
    private const int MaxOutlineLevel = 3;

    // AutoIdentifiers gives every heading a stable id we can link to. It must be added before
    // UseAdvancedExtensions, which otherwise adds it with default options and the GitHub options are ignored.
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UseAutoIdentifiers(AutoIdentifierOptions.GitHub)
        .UseAdvancedExtensions()
        .Build();

    /// <summary>
    /// Reads the home page file and renders it to HTML, with <paramref name="pathBase"/> (the app's path base and
    /// the base URL) in front of site-root links. Returns null if the file doesn't exist.
    /// </summary>
    public async Task<MarkdownPage?> GetIndexPageAsync(string pathBase, CancellationToken cancellationToken = default)
    {
        var path = folders.IndexFile;
        return File.Exists(path) ? await RenderFileAsync(path, pathBase, cancellationToken) : null;
    }

    /// <summary>
    /// Whether the site has a home page: the home page file or a "home" section in the settings. A docs-only site
    /// has neither, and its root redirects to the first docs page.
    /// </summary>
    public bool HasHomePage(SiteSettings settings) => settings.Home is not null || File.Exists(folders.IndexFile);

    /// <summary>
    /// Renders a page from the docs folder. The slug is the file path relative to the docs folder without the
    /// extension (e.g. "getting-started" for docs/getting-started.md), and <paramref name="pathBase"/> (the app's path base
    /// and the base URL) is put in front of site-root links. Returns null if there is no matching page.
    /// </summary>
    public async Task<MarkdownPage?> GetDocsPageAsync(string? slug, string pathBase, CancellationToken cancellationToken = default)
    {
        var path = FindDocsFile(slug);
        return path is null ? null : await RenderFileAsync(path, pathBase, cancellationToken);
    }

    /// <summary>
    /// Matches the slug against the files that actually exist, so lookups are case-insensitive on every OS
    /// and the request can never address a path outside the docs folder.
    /// </summary>
    private string? FindDocsFile(string? slug)
    {
        var requested = (slug ?? string.Empty).Trim('/').ToLowerInvariant();
        return requested.Length == 0 ? null : FindDocsFiles().GetValueOrDefault(requested);
    }

    /// <summary>
    /// Builds the docs navigation from the sidebar in sharpress.json. Without a sidebar, every page in
    /// the docs folder is listed, sorted by path.
    /// </summary>
    public async Task<DocsNavigation> GetDocsNavigationAsync(CancellationToken cancellationToken = default)
    {
        var settings = await settingsService.GetAsync(cancellationToken);
        var pageTitles = await GetDocsTitlesAsync(cancellationToken);

        if (settings.Sidebar.Count == 0)
        {
            var items = pageTitles
                .Select(page => new NavItem(page.Value, $"{folders.DocsPath}/{page.Key}"))
                .ToList();
            return new DocsNavigation(settings.Title, settings.Logo, settings.SidebarTitle, items.Count == 0 ? [] : [new NavGroup(null, items)])
            {
                HasHomePage = HasHomePage(settings),
                FirstPage = items.FirstOrDefault(),
            };
        }

        var groups = new List<NavGroup>();
        List<NavItem>? looseLinks = null;

        foreach (var entry in settings.Sidebar)
        {
            if (entry.Items.Count > 0)
            {
                looseLinks = null;
                groups.Add(new NavGroup(entry.Text, ToNavItems(entry.Items, pageTitles)));
            }
            else if (ToNavItem(entry, pageTitles) is { } item)
            {
                // Top-level links that sit next to each other share one untitled group.
                if (looseLinks is null)
                {
                    looseLinks = [];
                    groups.Add(new NavGroup(null, looseLinks));
                }

                looseLinks.Add(item);
            }
        }

        return new DocsNavigation(settings.Title, settings.Logo, settings.SidebarTitle, groups)
        {
            HasHomePage = HasHomePage(settings),
            FirstPage = groups
                .SelectMany(group => group.Items)
                .FirstOrDefault(item => DocsSlug(item.Href) is { } slug && pageTitles.ContainsKey(slug)),
        };
    }

    /// <summary>
    /// The docs page slug a site-root link points to, such as "getting-started" for "/docs/getting-started", or
    /// null if the link isn't under the docs URL. Only says where the link points; the page may not exist.
    /// </summary>
    private string? DocsSlug(string href)
    {
        // "//host/path" is another site, not a page under the docs at the root.
        if (href.StartsWith("//", StringComparison.Ordinal))
        {
            return null;
        }

        var docsPrefix = $"{folders.DocsPath}/";
        if (!href.StartsWith(docsPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var slug = href[docsPrefix.Length..].ToLowerInvariant();
        return slug.Length == 0 ? null : slug;
    }

    /// <summary>
    /// Warns, once per link and only in development, about a sidebar link on this site that matches neither a docs
    /// page nor a static file. The usual cause is changing <see cref="SharPressOptions.DocsUrl"/> after the
    /// settings file was written: its links still use the old URL, and every page shows "Page not found". Any
    /// site-root link is checked, not just those under the docs URL, so links left over from either side of the
    /// change are caught. The home page ("/") and links with a query or fragment are left alone.
    /// </summary>
    private void WarnIfMissing(string href, string? slug, Dictionary<string, string> pageTitles)
    {
        var isSiteLink = href.StartsWith('/') && !href.StartsWith("//", StringComparison.Ordinal) && href != "/";
        if (!environment.IsDevelopment() || !isSiteLink || (slug is not null && pageTitles.ContainsKey(slug))
            || href.Contains('#') || href.Contains('?') || folders.FindStaticFile(folders.BasePath + href) is not null
            || !_reportedMissingLinks.TryAdd(href, 0))
        {
            return;
        }

        logger.LogWarning(
            "The sidebar link {Link} in {SettingsFile} doesn't match a page in {DocsFolder} or a file in {StaticFolder}. Docs pages are served under \"{DocsPath}/\" (SharPressOptions.DocsUrl).",
            href, folders.Options.SettingsFile, folders.Options.DocsFolder, folders.Options.StaticFolder, folders.DocsPath);
    }

    private List<NavItem> ToNavItems(IEnumerable<SidebarItem> entries, Dictionary<string, string> pageTitles)
    {
        // Groups nested inside groups are flattened into their parent.
        return entries
            .SelectMany(entry => entry.Items.Count > 0 ? ToNavItems(entry.Items, pageTitles) : [..new[] { ToNavItem(entry, pageTitles) }.OfType<NavItem>()])
            .ToList();
    }

    private NavItem? ToNavItem(SidebarItem entry, Dictionary<string, string> pageTitles)
    {
        if (string.IsNullOrWhiteSpace(entry.Link))
        {
            return null;
        }

        // Internal links are site-root URLs, so "docs/intro" is treated as "/docs/intro". Links with a scheme
        // (https:, mailto:) and in-page anchors are kept as they are.
        var link = entry.Link;
        var href = link.StartsWith('/') || link.StartsWith('#') || link.Contains(':') ? link : $"/{link}";

        var slug = DocsSlug(href);
        WarnIfMissing(href, slug, pageTitles);

        var text = entry.Text;
        if (string.IsNullOrWhiteSpace(text))
        {
            text = slug is not null && pageTitles.TryGetValue(slug, out var title) ? title : href;
        }

        return new NavItem(text, href);
    }

    /// <summary>Maps each docs page slug to its title (the first # heading, or the file name), sorted by slug.</summary>
    private async Task<Dictionary<string, string>> GetDocsTitlesAsync(CancellationToken cancellationToken)
    {
        var titles = new Dictionary<string, string>();

        foreach (var (slug, file) in FindDocsFiles())
        {
            var markdown = await File.ReadAllTextAsync(file, cancellationToken);
            var h1 = Markdown.Parse(markdown, Pipeline).Descendants<HeadingBlock>().FirstOrDefault(heading => heading.Level == 1);
            var title = h1 is null ? string.Empty : GetPlainText(h1);
            titles[slug] = title.Length > 0 ? title : slug[(slug.LastIndexOf('/') + 1)..].Replace('-', ' ');
        }

        return titles;
    }

    /// <summary>Maps each docs page slug (lower-cased path without extension) to its file, sorted by slug.</summary>
    private SortedDictionary<string, string> FindDocsFiles()
    {
        var pages = new SortedDictionary<string, string>(StringComparer.Ordinal);
        var docsPath = folders.Docs;
        if (!Directory.Exists(docsPath))
        {
            return pages;
        }

        foreach (var file in Directory.EnumerateFiles(docsPath, "*.md", SearchOption.AllDirectories))
        {
            var slug = Path.ChangeExtension(Path.GetRelativePath(docsPath, file), null).Replace('\\', '/').ToLowerInvariant();
            pages[slug] = file;
        }

        return pages;
    }

    private static async Task<MarkdownPage> RenderFileAsync(string path, string pathBase, CancellationToken cancellationToken)
    {
        var markdown = await File.ReadAllTextAsync(path, cancellationToken);
        return Render(markdown, pathBase);
    }

    /// <param name="markdown">The Markdown to render.</param>
    /// <param name="pathBase">The app's path base and the base URL, put in front of site-root links such as "/docs/intro".</param>
    internal static MarkdownPage Render(string markdown, string pathBase = "")
    {
        var document = Markdown.Parse(markdown, Pipeline);
        foreach (var link in document.Descendants<LinkInline>())
        {
            link.Url = SiteUrls.WithPathBase(link.Url, pathBase);
        }

        var html = document.ToHtml(Pipeline);
        var title = document.Descendants<HeadingBlock>().FirstOrDefault(heading => heading.Level == 1);
        return new MarkdownPage(title is null ? null : GetPlainText(title), html, ExtractHeadings(document));
    }

    private static List<PageHeading> ExtractHeadings(MarkdownDocument document)
    {
        var headings = new List<PageHeading>();

        foreach (var heading in document.Descendants<HeadingBlock>())
        {
            if (heading.Level is < MinOutlineLevel or > MaxOutlineLevel)
            {
                continue;
            }

            var id = heading.GetAttributes().Id;
            var text = GetPlainText(heading);
            if (string.IsNullOrEmpty(id) || text.Length == 0)
            {
                continue;
            }

            headings.Add(new PageHeading(heading.Level, text, id));
        }

        return headings;
    }

    private static string GetPlainText(HeadingBlock heading)
    {
        if (heading.Inline is null)
        {
            return string.Empty;
        }

        var builder = new System.Text.StringBuilder();
        foreach (var inline in heading.Inline.Descendants())
        {
            switch (inline)
            {
                case LiteralInline literal:
                    builder.Append(literal.Content.ToString());
                    break;
                case CodeInline code:
                    builder.Append(code.Content);
                    break;
            }
        }

        return builder.ToString().Trim();
    }
}
