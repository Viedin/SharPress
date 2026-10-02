# SharPress

Serve a folder of Markdown files as a documentation site from your ASP.NET Core app. No Node toolchain, no
build step: edit a file, save, refresh.

- Pages from Markdown, with tables, task lists, footnotes and code blocks
- A sidebar you order yourself, plus previous and next links
- An **On this page** outline that tracks where you are on the page
- An optional home page with a hero and feature cards
- Light and dark themes, custom CSS, a logo and a favicon
- A sitemap at `/sitemap.xml` for search engines
- Works on phones, under any URL, and behind your app's sign-in if you want
- Pages from files by default, or from your own database or CMS

## Install

```
dotnet add package SharPress --prerelease
```

## Use

```csharp
using SharPress;

var builder = WebApplication.CreateBuilder(args);
builder.AddSharPress();

var app = builder.Build();
app.UseSharPress();
app.Run();
```

On first run SharPress creates a `SharpLib` folder in your project with a starter site:

| Path                      | Purpose                                                     |
| ------------------------- | ----------------------------------------------------------- |
| `Index.md`                | The home page                                               |
| `sharpress.json`          | Site title, sidebar order, logo and the home page layout    |
| `docs/*.md`               | One page each, served under `/docs` (see `DocsUrl`)          |
| `public/`                 | Logo, favicon, images and `custom.css`, served from the root |

Edit a file, save, and refresh. There is no build step.

Only want the docs? Delete `Index.md` and remove `home` from `sharpress.json`. The site then has no home
page: `/` redirects to the first docs page and the site title in the sidebar isn't a link. SharPress
doesn't recreate a deleted `Index.md`.

## Options

Change where the site lives, and what its files are called, in `Program.cs`:

```csharp
builder.AddSharPress(options =>
{
    options.RootFolder = "Content";
    options.DocsFolder = "guides";
    options.DocsUrl = "guide"; // serve the docs under /guide instead of /docs
});
```

`DocsUrl` changes the URL only. Links in `sharpress.json` must use the same URL, for example
`"/guide/getting-started"`.

Set `DocsUrl = "/"` to serve the docs from the root of the site, such as `/getting-started`. The docs then
answer every URL that nothing else in your app matches, so if the app has pages of its own, also set
`BaseUrl` (for example `/faq/getting-started`).

| Option                 | Default          |
| ---------------------- | ---------------- |
| `RootFolder`           | `SharpLib`       |
| `DocsFolder`           | `docs`           |
| `BaseUrl`              | (empty)          |
| `DocsUrl`              | `docs`           |
| `StaticFolder`         | `public`         |
| `IndexFile`            | `Index.md`       |
| `SettingsFile`         | `sharpress.json` |
| `CustomCssFile`        | `custom.css`     |
| `CreateStarterFiles`   | `true`           |
| `RequireAuthorization` | `false`          |
| `AuthorizationPolicy`  | `null`           |

## Sitemap

`/sitemap.xml` lists the home page and every docs page, including pages the sidebar doesn't link to. It
moves with `BaseUrl` (`/faq/sitemap.xml`) and needs sign-in like the rest of the site when
`RequireAuthorization` is on. To write your own, put a `sitemap.xml` in `public/`; it replaces the generated one.

Sitemap URLs must be absolute, so SharPress builds them from the request's scheme and host. Behind a reverse
proxy, add `app.UseForwardedHeaders()` so they show your public address instead of the proxy's.

## Requiring sign-in

To keep the docs private, turn on `RequireAuthorization`. SharPress uses the authentication your app already
has (cookies, Identity, OpenID Connect, ...), so register it as usual:

```csharp
builder.Services.AddAuthentication().AddCookie(); // or your existing setup
builder.Services.AddAuthorization();
builder.AddSharPress(options => options.RequireAuthorization = true);
```

Any signed-in user can then read the site (your app's default policy). To limit it further, name a policy;
setting `AuthorizationPolicy` turns on `RequireAuthorization` by itself:

```csharp
builder.Services.AddAuthorization(o => o.AddPolicy("DocsReaders", p => p.RequireRole("staff")));
builder.AddSharPress(options => options.AuthorizationPolicy = "DocsReaders");
```

The home page, the docs pages and the files in `public/` are locked. The error page and SharPress's own
stylesheet and script stay public. Visitors who aren't signed in get your sign-in scheme's challenge, for
example a redirect to your login page; SharPress doesn't add a login page of its own.

## Adding docs to an existing app

If your app already has its own pages at the root, such as MVC controllers or Razor Pages, serve SharPress
under a URL of its own with `BaseUrl`. Otherwise both try to answer `/` and the request fails with
`AmbiguousMatchException`:

```csharp
builder.Services.AddControllersWithViews();
builder.AddSharPress(options => options.BaseUrl = "faq");

var app = builder.Build();
app.UseSharPress(); // /faq, /faq/docs/getting-started, /faq/logo.svg
app.MapDefaultControllerRoute();
app.Run();
```

Links in `sharpress.json` and in your Markdown that start with `/` are relative to the base URL, so
`"/docs/getting-started"` keeps working. Use a full URL to link to the rest of the app. SharPress's error page
and HTTPS rules only apply to requests under the base URL.

## Reading content from a database or CMS

By default the pages and settings are files. To keep them somewhere else, implement `ISharPressContentSource`
and register it:

```csharp
using SharPress;
using SharPress.Services;

public sealed class DbContentSource(DocsDbContext db) : ISharPressContentSource
{
    public Task<string?> GetHomePageAsync(CancellationToken ct) =>
        db.Pages.Where(p => p.IsHome).Select(p => p.Markdown).FirstOrDefaultAsync(ct);

    // The slug is lower-cased, e.g. "guide/install" for /docs/Guide/Install.
    public Task<string?> GetDocsPageAsync(string slug, CancellationToken ct) =>
        db.Pages.Where(p => p.Slug == slug).Select(p => p.Markdown).FirstOrDefaultAsync(ct);

    // Used for the navigation on every page view, so only load slugs and titles.
    public async Task<IReadOnlyList<DocsPageEntry>> GetDocsPagesAsync(CancellationToken ct) =>
        await db.Pages.Where(p => !p.IsHome).Select(p => new DocsPageEntry(p.Slug, p.Title)).ToListAsync(ct);

    // The same settings sharpress.json holds; null uses the defaults.
    public Task<SiteSettings?> GetSettingsAsync(CancellationToken ct) =>
        Task.FromResult<SiteSettings?>(new SiteSettings { Title = "Acme Docs" });
}
```

```csharp
builder.AddSharPress();
builder.AddSharPressContentSource<DbContentSource>();
```

The source is scoped by default, so it can use a `DbContext`; pass `ServiceLifetime.Singleton` for one that keeps
its own cache. SharPress calls it on every request and caches nothing, so edits show up on refresh. If reading is
slow, cache inside the source (for example with `HybridCache`). Docs pages also ask `HasHomePageAsync`, which loads
the home page by default; override it with a cheaper check if that matters. With a custom source no starter files are
created. The static folder (`public/`) is still served from disk, so put images there or link them by full URL.

## Hosting under a sub-path

SharPress works when the app is hosted under a path such as `/myapp`: every link it generates, and every
link in your Markdown that starts with `/`, gets the path in front. Call `UseRouting` right after
`UsePathBase`. Otherwise ASP.NET Core matches routes before the path base is removed, and no SharPress page
is found:

```csharp
var app = builder.Build();
app.UsePathBase("/myapp");
app.UseRouting();
app.UseSharPress();
app.Run();
```

## Requirements

.NET 10 and ASP.NET Core. Pages are rendered with Razor components on the server, so native AOT isn't
supported.
