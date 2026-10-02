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

/// <summary>
/// Sidebar links already warned about. The navigation is built on every request, so without this a broken link
/// would be logged on every page view. A singleton, unlike <see cref="MarkdownPageService"/>.
/// </summary>
internal sealed class MissingLinkWarnings
{
    private readonly ConcurrentDictionary<string, byte> _reported = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Returns true the first time it is called for a link.</summary>
    public bool TryReport(string href) => _reported.TryAdd(href, 0);
}

/// <summary>
/// Renders the pages and builds the navigation from the <see cref="ISharPressContentSource"/>. It is scoped, so a
/// content source can be scoped too, e.g. to use a DbContext.
/// </summary>
internal sealed class MarkdownPageService(
    ISharPressContentSource source,
    SiteFolders folders,
    SiteSettingsService settingsService,
    MissingLinkWarnings missingLinkWarnings,
    IHostEnvironment environment,
    ILogger<MarkdownPageService> logger)
{
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
    /// Renders the home page to HTML, with <paramref name="pathBase"/> (the app's path base and the base URL) in
    /// front of site-root links. Returns null if the site has no home page content.
    /// </summary>
    public async Task<MarkdownPage?> GetIndexPageAsync(string pathBase, CancellationToken cancellationToken = default) =>
        await source.GetHomePageAsync(cancellationToken) is { } markdown ? Render(markdown, pathBase) : null;

    /// <summary>
    /// Whether the site has a home page: home page content or a "home" section in the settings. A docs-only site
    /// has neither, and its root redirects to the first docs page.
    /// </summary>
    private async Task<bool> HasHomePageAsync(SiteSettings settings, CancellationToken cancellationToken) =>
        settings.Home is not null || await source.HasHomePageAsync(cancellationToken);

    /// <summary>
    /// Renders a docs page. The slug is the page's path under the docs URL (e.g. "getting-started" for
    /// docs/getting-started.md), and <paramref name="pathBase"/> (the app's path base and the base URL) is put in
    /// front of site-root links. Returns null if there is no matching page.
    /// </summary>
    public async Task<MarkdownPage?> GetDocsPageAsync(string? slug, string pathBase, CancellationToken cancellationToken = default)
    {
        var requested = (slug ?? string.Empty).Trim('/').ToLowerInvariant();
        if (requested.Length == 0)
        {
            return null;
        }

        return await source.GetDocsPageAsync(requested, cancellationToken) is { } markdown ? Render(markdown, pathBase) : null;
    }

    /// <summary>
    /// Builds the docs navigation from the sidebar in the settings. Without a sidebar, every docs page is listed,
    /// sorted by path.
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
                HasHomePage = await HasHomePageAsync(settings, cancellationToken),
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
            HasHomePage = await HasHomePageAsync(settings, cancellationToken),
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
            || !missingLinkWarnings.TryReport(href))
        {
            return;
        }

        if (folders.Options.UsesCustomContentSource)
        {
            logger.LogWarning(
                "The sidebar link {Link} doesn't match a docs page from the content source or a file in {StaticFolder}. Docs pages are served under \"{DocsPath}/\" (SharPressOptions.DocsUrl).",
                href, folders.Options.StaticFolder, folders.DocsPath);
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

    /// <summary>
    /// Maps each docs page's lower-cased slug to its title (from the source, or the last part of the slug),
    /// in slug order.
    /// </summary>
    private async Task<Dictionary<string, string>> GetDocsTitlesAsync(CancellationToken cancellationToken)
    {
        var titles = new Dictionary<string, string>();
        var pages = (await source.GetDocsPagesAsync(cancellationToken))
            .Select(page => (Slug: page.Slug.Trim('/').ToLowerInvariant(), page.Title))
            .Where(page => page.Slug.Length > 0)
            .OrderBy(page => page.Slug, StringComparer.Ordinal);

        // Filled in sorted order, which a dictionary keeps as long as nothing is removed. TryAdd keeps the
        // first of two entries that differ only in case.
        foreach (var (slug, title) in pages)
        {
            titles.TryAdd(slug, string.IsNullOrWhiteSpace(title) ? slug[(slug.LastIndexOf('/') + 1)..].Replace('-', ' ') : title);
        }

        return titles;
    }

    /// <summary>The plain text of the first # heading in the Markdown, or null if there is none.</summary>
    internal static string? GetTitle(string markdown)
    {
        var h1 = Markdown.Parse(markdown, Pipeline).Descendants<HeadingBlock>().FirstOrDefault(heading => heading.Level == 1);
        var title = h1 is null ? string.Empty : GetPlainText(h1);
        return title.Length > 0 ? title : null;
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
