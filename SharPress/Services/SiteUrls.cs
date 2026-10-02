using Microsoft.AspNetCore.Components;
using SharPress.Startup;

namespace SharPress.Services;

/// <summary>
/// Builds links that keep working when the app is hosted under a path base, such as /myapp, and when the site is
/// served under a base URL (<see cref="SharPressOptions.BaseUrl"/>), such as /faq.
/// </summary>
internal static class SiteUrls
{
    /// <summary>
    /// Puts <paramref name="siteBase"/> ("" or e.g. "/myapp/faq") in front of a site-root URL such as "/docs/intro".
    /// Anything else (relative links, external links, "#id") is returned as it is.
    /// </summary>
    public static string? WithPathBase(string? href, string siteBase) =>
        href is not null && href.StartsWith('/') && !href.StartsWith("//", StringComparison.Ordinal) ? siteBase + href : href;

    /// <summary>The app's path base without a trailing slash, e.g. "" or "/myapp".</summary>
    public static string PathBase(this NavigationManager navigation) =>
        new Uri(navigation.BaseUri).AbsolutePath.TrimEnd('/');

    /// <summary>
    /// Makes a link from the settings file, or one SharPress builds, work from any page: "/logo.svg" and
    /// "logo.svg" both become the site-root URL with the path base and the base URL in front. External links
    /// (anything with a scheme, such as https: or mailto:) and "#id" are returned as they are.
    /// </summary>
    public static string? SiteUrl(this NavigationManager navigation, SiteFolders folders, string? href)
    {
        if (string.IsNullOrEmpty(href) || href.StartsWith('#') || href.StartsWith("//", StringComparison.Ordinal) || href.Contains(':'))
        {
            return href;
        }

        return $"{navigation.PathBase()}{folders.BasePath}/{href.TrimStart('/')}";
    }

    /// <summary>
    /// The URL of one of SharPress's own files, such as "_content/SharPress/sharpress.css". These are served from
    /// the app's root whatever the base URL is, so only the path base goes in front.
    /// </summary>
    public static string AssetUrl(this NavigationManager navigation, string path) =>
        $"{navigation.PathBase()}/{path.TrimStart('/')}";
}
