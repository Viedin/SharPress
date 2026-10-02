namespace SharPress;

/// <summary>
/// Controls where SharPress finds your site on disk. Every property has a default, so you only set what you
/// want to change:
/// <code>
/// builder.AddSharPress(options =&gt;
/// {
///     options.RootFolder = "Content";
///     options.DocsFolder = "guides";
/// });
/// </code>
/// </summary>
public sealed class SharPressOptions
{
    /// <summary>
    /// The folder that holds the whole site. A relative path is resolved against the project's content root;
    /// an absolute path is used as it is.
    /// </summary>
    public string RootFolder { get; set; } = "SharpLib";

    /// <summary>
    /// The folder inside <see cref="RootFolder"/> with the docs pages. It only sets where the files are read from;
    /// the URL is set by <see cref="DocsUrl"/>.
    /// </summary>
    public string DocsFolder { get; set; } = "docs";

    /// <summary>
    /// The URL the whole site is served under, for example "faq" to put the home page at /faq and the docs at
    /// /faq/docs. Set it when SharPress shares the app with other pages, such as MVC controllers or Razor Pages,
    /// that already use the site root. Empty (the default) serves the site from the root.
    /// <para>
    /// Links in the settings file and in your Markdown that start with "/" are relative to this URL, so
    /// "/docs/getting-started" still works after you set it. Link to the app's other pages with a full URL.
    /// The error page, the static folder and the error handling also move under this URL and leave the rest of
    /// the app alone.
    /// </para>
    /// </summary>
    public string BaseUrl { get; set; } = "";

    /// <summary>
    /// The URL the docs pages are served under, inside <see cref="BaseUrl"/>, for example "guide" to serve
    /// getting-started.md at /guide/getting-started. It may have more than one segment, such as "guide/v2". Links
    /// in the settings file must use the same URL.
    /// </summary>
    public string DocsUrl { get; set; } = "docs";

    /// <summary>
    /// The folder inside <see cref="RootFolder"/> that is served from the site root (<see cref="BaseUrl"/>):
    /// custom.css, the favicon, the logo and any other images.
    /// </summary>
    public string StaticFolder { get; set; } = "public";

    /// <summary>The home page, relative to <see cref="RootFolder"/>.</summary>
    public string IndexFile { get; set; } = "Index.md";

    /// <summary>The site settings, relative to <see cref="RootFolder"/>.</summary>
    public string SettingsFile { get; set; } = "sharpress.json";

    /// <summary>The stylesheet inside <see cref="StaticFolder"/> that is added to every page after the built-in styles.</summary>
    public string CustomCssFile { get; set; } = "custom.css";

    /// <summary>
    /// Whether to create a starter site on first run. Turn it off if you create the files yourself. The static
    /// folder is created either way, because it has to exist to be served.
    /// </summary>
    public bool CreateStarterFiles { get; set; } = true;

    /// <summary>
    /// Whether every page needs an authorized user, using the authentication your app already has. It covers the
    /// home page, the docs pages and the files in <see cref="StaticFolder"/>; the error page and SharPress's own
    /// stylesheet and script stay public. Users who aren't signed in get your sign-in scheme's challenge, such as
    /// a redirect to your login page; SharPress doesn't add one. Register authentication and authorization first:
    /// <code>
    /// builder.Services.AddAuthentication().AddCookie();
    /// builder.Services.AddAuthorization();
    /// builder.AddSharPress(options =&gt; options.RequireAuthorization = true);
    /// </code>
    /// Without <see cref="AuthorizationPolicy"/>, your app's default policy is used: any signed-in user.
    /// </summary>
    public bool RequireAuthorization { get; set; }

    /// <summary>
    /// The name of an authorization policy, registered with <c>AddAuthorization</c>, that users must meet to see
    /// the site. Setting it turns on <see cref="RequireAuthorization"/>.
    /// </summary>
    public string? AuthorizationPolicy { get; set; }
}
