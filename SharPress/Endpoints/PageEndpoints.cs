using System.Diagnostics;
using System.Text;
using System.Xml;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.StaticFiles;
using SharPress.Components.Pages;
using SharPress.Services;
using SharPress.Startup;

namespace SharPress.Endpoints;

/// <summary>
/// Maps the site's pages as minimal API endpoints that render Razor components. The routes are built at runtime,
/// so the docs can be served under any URL (<see cref="SharPressOptions.DocsUrl"/>), including the site root.
/// </summary>
internal static class PageEndpoints
{
    // Component parameters are passed as dictionaries rather than anonymous objects, which would need reflection.
    // The handlers return IResult because the endpoint code generator can't see the Razor-generated page types.
    public static void Map(WebApplication app, SiteFolders folders)
    {
        // The pages are grouped under the base URL so they can share an authorization requirement. The error
        // page isn't in the group: it has to render for everyone.
        var pages = app.MapGroup(folders.BasePath);
        if (folders.RequireAuthorization)
        {
            if (folders.Options.AuthorizationPolicy is { } policy)
            {
                pages.RequireAuthorization(policy);
            }
            else
            {
                pages.RequireAuthorization();
            }
        }

        // A literal route wins over the docs catch-all, so this works with the docs at the root of the site too.
        pages.MapGet("/sitemap.xml", (HttpContext context, MarkdownPageService markdown, CancellationToken cancellationToken) =>
            SitemapAsync(context, folders, markdown, cancellationToken));

        if (folders.DocsPath.Length == 0)
        {
            // The docs are at the root of the site, so the docs route also matches the root. A separate "/" route
            // would compete with it; the docs route renders the home page itself when there is no slug.
            pages.MapGet("/{*slug}", (string? slug, HttpContext context, MarkdownPageService markdown, SiteSettingsService settings, CancellationToken cancellationToken) =>
                string.IsNullOrEmpty(slug)
                    ? HomeAsync(context, folders, markdown, settings, cancellationToken)
                    : DocsAsync(slug, context, folders, markdown, cancellationToken));
        }
        else
        {
            pages.MapGet("/", (HttpContext context, MarkdownPageService markdown, SiteSettingsService settings, CancellationToken cancellationToken) =>
                HomeAsync(context, folders, markdown, settings, cancellationToken));
            pages.MapGet($"{folders.DocsPath}/{{*slug}}", (string? slug, HttpContext context, MarkdownPageService markdown, CancellationToken cancellationToken) =>
                DocsAsync(slug, context, folders, markdown, cancellationToken));
        }
    }

    private static async Task<IResult> HomeAsync(HttpContext context, SiteFolders folders, MarkdownPageService markdown, SiteSettingsService settingsService, CancellationToken cancellationToken)
    {
        var settings = await settingsService.GetAsync(cancellationToken);
        var page = await markdown.GetIndexPageAsync(SiteBase(context, folders), cancellationToken);

        // A docs-only site has no home page content and no "home" section, so the root goes to the first docs
        // page instead of an empty page. The redirect is temporary: adding a home page later takes it back.
        if (page is null && settings.Home is null)
        {
            var navigation = await markdown.GetDocsNavigationAsync(cancellationToken);
            return navigation.FirstPage is { } first
                ? TypedResults.Redirect(SiteBase(context, folders) + first.Href)
                : TypedResults.NotFound();
        }

        return new RazorComponentResult<Home>(new Dictionary<string, object?>
        {
            [nameof(Home.Page)] = page,
            [nameof(Home.Settings)] = settings.Home,
        });
    }

    private static async Task<IResult> DocsAsync(string? slug, HttpContext context, SiteFolders folders, MarkdownPageService markdown, CancellationToken cancellationToken)
    {
        // UseStaticFiles skips requests that matched an endpoint, so a file in the static folder under the
        // docs URL (e.g. public/docs/images/diagram.png) would otherwise get "Page not found".
        if (FindStaticFile(folders, context.Request.Path) is { } file)
        {
            return file;
        }

        var page = await markdown.GetDocsPageAsync(slug, SiteBase(context, folders), cancellationToken);
        var navigation = await markdown.GetDocsNavigationAsync(cancellationToken);
        var (previous, next) = navigation.GetNeighbours($"{folders.DocsPath}/{slug}");

        return new RazorComponentResult<Docs>(new Dictionary<string, object?>
        {
            [nameof(Docs.Page)] = page,
            [nameof(Docs.Navigation)] = navigation,
            [nameof(Docs.Previous)] = previous,
            [nameof(Docs.Next)] = next,
        })
        {
            // The page still renders "Page not found" with the navigation, but with the right status code.
            StatusCode = page is null ? StatusCodes.Status404NotFound : null,
        };
    }

    /// <summary>
    /// Lists the home page and every docs page for search engines. The URLs must be absolute, so they are built from
    /// the request's scheme and host; behind a proxy that needs forwarded headers (UseForwardedHeaders).
    /// </summary>
    private static async Task<IResult> SitemapAsync(HttpContext context, SiteFolders folders, MarkdownPageService markdown, CancellationToken cancellationToken)
    {
        // A sitemap.xml in the static folder replaces the generated one. UseStaticFiles would never serve it,
        // because this route matches first.
        if (FindStaticFile(folders, context.Request.Path) is { } file)
        {
            return file;
        }

        var request = context.Request;
        var urls = await markdown.GetPageUrlsAsync(cancellationToken);
        var locations = urls.Select(url => UriHelper.BuildAbsolute(request.Scheme, request.Host, request.PathBase, new PathString(folders.BasePath + url)));
        return TypedResults.Bytes(WriteSitemap(locations), "application/xml");
    }

    private const string SitemapNamespace = "http://www.sitemaps.org/schemas/sitemap/0.9";

    /// <summary>Writes a sitemap (sitemaps.org) with one &lt;url&gt; per location, as UTF-8 without a byte order mark.</summary>
    private static byte[] WriteSitemap(IEnumerable<string> locations)
    {
        using var stream = new MemoryStream();
        using (var writer = XmlWriter.Create(stream, new XmlWriterSettings { Encoding = new UTF8Encoding(false), Indent = true }))
        {
            writer.WriteStartDocument();
            writer.WriteStartElement("urlset", SitemapNamespace);
            foreach (var location in locations)
            {
                writer.WriteStartElement("url", SitemapNamespace);
                writer.WriteElementString("loc", SitemapNamespace, location);
                writer.WriteEndElement();
            }

            writer.WriteEndElement();
        }

        return stream.ToArray();
    }

    /// <summary>
    /// Renders the error page for a failed request. The exception handler calls this directly rather than
    /// re-executing an /Error route: it runs in a UseWhen branch, where it can't route the request again.
    /// </summary>
    public static Task RenderErrorAsync(HttpContext context) =>
        new RazorComponentResult<Error>(new Dictionary<string, object?>
        {
            [nameof(Error.RequestId)] = Activity.Current?.Id ?? context.TraceIdentifier,
        }).ExecuteAsync(context);

    private static readonly FileExtensionContentTypeProvider ContentTypes = new();

    /// <summary>The URL of the site's root: the app's path base and the base URL, e.g. "" or "/myapp/faq".</summary>
    private static string SiteBase(HttpContext context, SiteFolders folders) => context.Request.PathBase.Value + folders.BasePath;

    /// <summary>
    /// Finds a file in the static folder at the requested path. Like UseStaticFiles, files with an unknown type
    /// aren't served. Returns null if there is no such file.
    /// </summary>
    private static PhysicalFileHttpResult? FindStaticFile(SiteFolders folders, PathString requestPath)
    {
        if (folders.FindStaticFile(requestPath) is not { } path)
        {
            return null;
        }

        return ContentTypes.TryGetContentType(path, out var contentType)
            ? TypedResults.PhysicalFile(path, contentType, enableRangeProcessing: true)
            : null;
    }
}
