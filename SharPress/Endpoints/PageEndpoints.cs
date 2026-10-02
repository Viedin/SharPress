using System.Diagnostics;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.StaticFiles;
using SharPress.Components.Pages;
using SharPress.Services;
using SharPress.Startup;

namespace SharPress.Endpoints;

/// <summary>
/// Maps the site's pages as minimal API endpoints that render Razor components. The routes are built at runtime,
/// so the docs can be served under any URL (<see cref="SharPressOptions.DocsUrl"/>).
/// </summary>
internal static class PageEndpoints
{
    // Component parameters are passed as dictionaries rather than anonymous objects, which would need reflection.
    // The handlers return IResult because the endpoint code generator can't see the Razor-generated page types.
    public static void Map(WebApplication app, SiteFolders folders)
    {
        // The pages are grouped so they can share an authorization requirement. The error page isn't in the
        // group: it has to render for everyone.
        var pages = app.MapGroup("");
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

        pages.MapGet("/", async Task<IResult> (HttpContext context, MarkdownPageService markdown, SiteSettingsService settings, CancellationToken cancellationToken) =>
            new RazorComponentResult<Home>(new Dictionary<string, object?>
            {
                [nameof(Home.Page)] = await markdown.GetIndexPageAsync(PathBase(context), cancellationToken),
                [nameof(Home.Settings)] = (await settings.GetAsync(cancellationToken)).Home,
            }));

        pages.MapGet($"/{folders.DocsUrl}/{{*slug}}", async Task<IResult> (string? slug, HttpContext context, MarkdownPageService markdown, CancellationToken cancellationToken) =>
        {
            // UseStaticFiles skips requests that matched an endpoint, so a file in the static folder under the
            // docs URL (e.g. public/docs/images/diagram.png) would otherwise get "Page not found".
            if (FindStaticFile(folders, context.Request.Path) is { } file)
            {
                return file;
            }

            var page = await markdown.GetDocsPageAsync(slug, PathBase(context), cancellationToken);
            var navigation = await markdown.GetDocsNavigationAsync(cancellationToken);
            var (previous, next) = navigation.GetNeighbours($"/{folders.DocsUrl}/{slug}");

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
        });

        // Map, not MapGet: the exception handler re-executes the failed request with its original method.
        app.Map("/Error", IResult (HttpContext context) =>
            new RazorComponentResult<Error>(new Dictionary<string, object?>
            {
                [nameof(Error.RequestId)] = Activity.Current?.Id ?? context.TraceIdentifier,
            }));
    }

    private static readonly FileExtensionContentTypeProvider ContentTypes = new();

    private static string PathBase(HttpContext context) => context.Request.PathBase.Value ?? string.Empty;

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
