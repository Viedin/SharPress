using SharPress;
using SharPress.Services;

namespace SharPress.Sample;

/// <summary>
/// A content source that stands in for a database or CMS: the pages are rows in a list instead of files in
/// SharpLib. A real source would query a DbContext or call an API in the same four methods. Run the sample with
/// <c>--ContentSource=Demo</c> to use it.
/// </summary>
public sealed class DemoContentSource : ISharPressContentSource
{
    /// <summary>
    /// A row in the pretend "pages" table. <paramref name="Position"/> orders the pages within their section in
    /// the sidebar, lowest first; sections are shown in the order their first page appears in the table.
    /// </summary>
    private sealed record DemoPage(string Slug, string Section, int Position, string Title, string Markdown);

    private static readonly DemoPage[] Pages =
    [
        new("getting-started", "Introduction", 1, "Getting started", """
            # Getting started

            This page doesn't exist on disk. It comes from `DemoContentSource`, registered in `Program.cs` with:

            ```csharp
            builder.AddSharPressContentSource<DemoContentSource>();
            ```

            Next, see how a source [builds the sidebar](/docs/guide/sidebar).
            """),
        new("guide/sidebar", "Guide", 2, "Building the sidebar", """
            # Building the sidebar

            `GetSettingsAsync` returns a `SiteSettings` object, the same one `sharpress.json` is read into. This
            source groups its pages by section to build the sidebar, ordered by each page's position in its section.

            ## Without a sidebar

            Return no sidebar and SharPress lists every page from `GetDocsPagesAsync`, sorted by slug.
            """),
        new("guide/caching", "Guide", 1, "Caching", """
            # Caching

            SharPress asks the source for the settings and the page list on every request, and for the page that is
            being viewed. It doesn't cache anything, so changes show up on the next refresh.

            ## When reading is slow

            Cache inside the source, for example with `HybridCache`, and clear the cache when content is published.
            """),
    ];

    /// <inheritdoc />
    public Task<string?> GetHomePageAsync(CancellationToken cancellationToken) =>
        Task.FromResult<string?>("""
            ## Content from anywhere

            This home page, the docs pages and the sidebar all come from `DemoContentSource` instead of the
            `SharpLib` folder. Replace it with a source that reads your database or CMS.
            """);

    /// <inheritdoc />
    public Task<string?> GetDocsPageAsync(string slug, CancellationToken cancellationToken) =>
        Task.FromResult(Pages.FirstOrDefault(page => page.Slug == slug)?.Markdown);

    /// <inheritdoc />
    public Task<IReadOnlyList<DocsPageEntry>> GetDocsPagesAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<DocsPageEntry>>([.. Pages.Select(page => new DocsPageEntry(page.Slug, page.Title))]);

    /// <inheritdoc />
    public Task<SiteSettings?> GetSettingsAsync(CancellationToken cancellationToken) =>
        Task.FromResult<SiteSettings?>(new SiteSettings
        {
            Title = "Demo source",
            Sidebar =
            [
                .. Pages.GroupBy(page => page.Section).Select(section => new SidebarItem
                {
                    Text = section.Key,
                    Items = [.. section.OrderBy(page => page.Position).Select(page => new SidebarItem { Link = $"/docs/{page.Slug}" })],
                }),
            ],
            Home = new HomeSettings
            {
                Hero = new HeroSettings
                {
                    Name = "SharPress",
                    Text = "Docs from a content source",
                    Tagline = "No Markdown files on disk.",
                    Actions = [new HeroAction { Text = "Get started", Link = "/docs/getting-started" }],
                },
            },
        });
}
