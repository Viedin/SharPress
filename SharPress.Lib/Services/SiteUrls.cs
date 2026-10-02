using Microsoft.AspNetCore.Components;

namespace SharPress.Lib.Services;

/// <summary>Builds links that keep working when the app is hosted under a path base, such as /myapp.</summary>
internal static class SiteUrls
{
    /// <summary>
    /// Puts <paramref name="pathBase"/> ("" or e.g. "/myapp") in front of a site-root URL such as "/docs/intro".
    /// Anything else (relative links, external links, "#id") is returned as it is.
    /// </summary>
    public static string? WithPathBase(string? href, string pathBase) =>
        href is not null && href.StartsWith('/') && !href.StartsWith("//", StringComparison.Ordinal) ? pathBase + href : href;

    /// <summary>The app's path base without a trailing slash, e.g. "" or "/myapp".</summary>
    public static string PathBase(this NavigationManager navigation) =>
        new Uri(navigation.BaseUri).AbsolutePath.TrimEnd('/');

    /// <summary>
    /// Makes a link from the settings file, or one SharPress builds, work from any page: "/logo.svg" and
    /// "logo.svg" both become the site-root URL with the path base in front. External links (anything with a
    /// scheme, such as https: or mailto:) and "#id" are returned as they are.
    /// </summary>
    public static string? SiteUrl(this NavigationManager navigation, string? href)
    {
        if (string.IsNullOrEmpty(href) || href.StartsWith('#') || href.StartsWith("//", StringComparison.Ordinal) || href.Contains(':'))
        {
            return href;
        }

        return $"{navigation.PathBase()}/{href.TrimStart('/')}";
    }
}
