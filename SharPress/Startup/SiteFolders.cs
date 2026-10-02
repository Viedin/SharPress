using Microsoft.AspNetCore.Hosting;
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

        Root = Path.GetFullPath(Path.Combine(environment.ContentRootPath, Options.RootFolder));
        IndexFile = Path.Combine(Root, Options.IndexFile);
        SettingsFile = Path.Combine(Root, Options.SettingsFile);
        Docs = Path.Combine(Root, Options.DocsFolder);
        Static = Path.Combine(Root, Options.StaticFolder);
        CustomCssFile = Path.Combine(Static, Options.CustomCssFile);
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
}
