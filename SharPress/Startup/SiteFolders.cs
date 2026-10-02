using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace SharPress.Startup;

/// <summary>The full paths of the site's files and folders, worked out from <see cref="SharPressOptions"/>.</summary>
internal sealed class SiteFolders
{
    public SiteFolders(IOptions<SharPressOptions> options, IWebHostEnvironment environment)
    {
        Options = options.Value;

        var baseUrl = Options.BaseUrl.Trim('/');
        BasePath = baseUrl.Length == 0 ? string.Empty : $"/{baseUrl}";

        var docsUrl = Options.DocsUrl.Trim('/');
        DocsPath = docsUrl.Length == 0 ? string.Empty : $"/{docsUrl}";

        // Every path is normalized, so options such as "./public" compare equal to the paths they resolve to.
        // FindStaticFile relies on this: an unnormalized static folder would match no file, and with
        // RequireAuthorization on, the static files would then skip the check but still be served.
        Root = Path.GetFullPath(Path.Combine(environment.ContentRootPath, Options.RootFolder));
        IndexFile = Path.GetFullPath(Path.Combine(Root, Options.IndexFile));
        SettingsFile = Path.GetFullPath(Path.Combine(Root, Options.SettingsFile));
        Docs = Path.GetFullPath(Path.Combine(Root, Options.DocsFolder));
        Static = Path.GetFullPath(Path.Combine(Root, Options.StaticFolder));
        CustomCssFile = Path.GetFullPath(Path.Combine(Static, Options.CustomCssFile));
    }

    public SharPressOptions Options { get; }

    /// <summary>
    /// The URL the site is served under (<see cref="SharPressOptions.BaseUrl"/>), with a leading slash and no
    /// trailing slash, e.g. "/faq"; empty when the site is at the root.
    /// </summary>
    public string BasePath { get; }

    /// <summary>
    /// The URL the docs pages are served under inside <see cref="BasePath"/> (<see cref="SharPressOptions.DocsUrl"/>),
    /// with a leading slash and no trailing slash, e.g. "/docs" for /docs/getting-started; empty when the docs are
    /// at the root of the site, next to the home page.
    /// </summary>
    public string DocsPath { get; }

    public string Root { get; }

    public string IndexFile { get; }

    public string SettingsFile { get; }

    public string Docs { get; }

    public string Static { get; }

    public string CustomCssFile { get; }

    /// <summary>Whether the site needs an authorized user (<see cref="SharPressOptions.RequireAuthorization"/>).</summary>
    public bool RequireAuthorization => Options.RequireAuthorization || Options.AuthorizationPolicy is not null;

    /// <summary>
    /// Finds the file in the static folder at a request path, such as "/docs/images/diagram.png" (or
    /// "/faq/docs/images/diagram.png" with <see cref="BasePath"/> "/faq"). Returns its full path, or null if
    /// there is no such file.
    /// </summary>
    public string? FindStaticFile(PathString requestPath)
    {
        if (!requestPath.StartsWithSegments(BasePath, out var sitePath))
        {
            return null;
        }

        var relativePath = sitePath.Value?.TrimStart('/');
        if (string.IsNullOrEmpty(relativePath))
        {
            return null;
        }

        // The full path must stay inside the static folder, so "../" can't reach other files.
        var root = Path.TrimEndingDirectorySeparator(Static) + Path.DirectorySeparatorChar;
        var path = Path.GetFullPath(Path.Combine(root, relativePath));
        return path.StartsWith(root, StringComparison.Ordinal) && File.Exists(path) ? path : null;
    }
}
