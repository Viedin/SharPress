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

        DocsUrl = Options.DocsUrl.Trim('/');
        if (DocsUrl.Length == 0)
        {
            // An empty URL would put the docs route at the site root, where it would catch every request.
            throw new InvalidOperationException($"{nameof(SharPressOptions)}.{nameof(SharPressOptions.DocsUrl)} must not be empty.");
        }

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

    /// <summary>The part of the URL the docs pages are served under, without slashes, e.g. "docs" for /docs/getting-started.</summary>
    public string DocsUrl { get; }

    public string Root { get; }

    public string IndexFile { get; }

    public string SettingsFile { get; }

    public string Docs { get; }

    public string Static { get; }

    public string CustomCssFile { get; }

    /// <summary>Whether the site needs an authorized user (<see cref="SharPressOptions.RequireAuthorization"/>).</summary>
    public bool RequireAuthorization => Options.RequireAuthorization || Options.AuthorizationPolicy is not null;

    /// <summary>
    /// Finds the file in the static folder at a request path, such as "/docs/images/diagram.png". Returns its full
    /// path, or null if there is no such file.
    /// </summary>
    public string? FindStaticFile(PathString requestPath)
    {
        var relativePath = requestPath.Value?.TrimStart('/');
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
