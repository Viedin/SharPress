using SharPress.Services;

namespace SharPress;

/// <summary>
/// Where SharPress reads the site's content from: the home page, the docs pages and the settings. By default they
/// are files under <see cref="SharPressOptions.RootFolder"/>; implement this to read them from a database or a
/// CMS instead, and register it with <c>AddSharPressContentSource</c>:
/// <code>
/// builder.AddSharPress();
/// builder.AddSharPressContentSource&lt;MyDatabaseSource&gt;();
/// </code>
/// The static folder (custom.css, the logo, images) is still served from disk.
/// <para>
/// SharPress calls the source on every request and doesn't cache what it returns, so edits show up on refresh.
/// Cache in the source if reading is slow, for example with <c>HybridCache</c>.
/// </para>
/// </summary>
/// <remarks>
/// A slug is a docs page's path under <see cref="SharPressOptions.DocsUrl"/> without leading or trailing slashes,
/// for example "getting-started" or "guide/install". Slugs are matched ignoring case: SharPress passes the
/// requested slug lower-cased, and lower-cases the slugs the source lists.
/// </remarks>
public interface ISharPressContentSource
{
    /// <summary>
    /// Returns the Markdown of the home page, or null if there is none. Without a home page and without a "home"
    /// section in the settings, the site root redirects to the first docs page.
    /// </summary>
    /// <param name="cancellationToken">Cancelled when the request is aborted.</param>
    Task<string?> GetHomePageAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Whether there is a home page. The navigation asks on every docs page view, to decide whether the site title
    /// links home. The default loads the home page with <see cref="GetHomePageAsync"/>; override it with a cheaper
    /// check, such as an <c>Any()</c> query, if that is slow.
    /// </summary>
    /// <param name="cancellationToken">Cancelled when the request is aborted.</param>
    async Task<bool> HasHomePageAsync(CancellationToken cancellationToken) =>
        await GetHomePageAsync(cancellationToken) is not null;

    /// <summary>Returns the Markdown of a docs page, or null if there is no such page ("Page not found").</summary>
    /// <param name="slug">The page's slug, lower-cased, such as "guide/install".</param>
    /// <param name="cancellationToken">Cancelled when the request is aborted.</param>
    Task<string?> GetDocsPageAsync(string slug, CancellationToken cancellationToken);

    /// <summary>
    /// Lists every docs page with its title. It is called on every page view to build the navigation, so it
    /// shouldn't load the pages' content. The order doesn't matter: pages are sorted by slug when there is no
    /// sidebar in the settings.
    /// </summary>
    /// <param name="cancellationToken">Cancelled when the request is aborted.</param>
    Task<IReadOnlyList<DocsPageEntry>> GetDocsPagesAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Returns the site settings (title, logo, sidebar, home page layout), or null for the defaults. Called once
    /// per request.
    /// </summary>
    /// <param name="cancellationToken">Cancelled when the request is aborted.</param>
    Task<SiteSettings?> GetSettingsAsync(CancellationToken cancellationToken);
}

/// <summary>A docs page listed by <see cref="ISharPressContentSource.GetDocsPagesAsync"/>.</summary>
/// <param name="Slug">The page's path under the docs URL, such as "guide/install".</param>
/// <param name="Title">
/// The title shown in the navigation, or null to use the last part of the slug with dashes as spaces.
/// </param>
public sealed record DocsPageEntry(string Slug, string? Title);
