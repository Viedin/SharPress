# SharPress

Turn a folder of Markdown files into a documentation site in your ASP.NET Core app.

- Pages from Markdown, with tables, task lists, footnotes and code blocks
- A sidebar you order yourself, plus previous and next links
- An **On this page** outline that follows your scrolling
- A home page with a hero and feature cards
- Light and dark themes, custom CSS, a logo and a favicon

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

| Option               | Default          |
| -------------------- | ---------------- |
| `RootFolder`         | `SharpLib`       |
| `DocsFolder`         | `docs`           |
| `DocsUrl`            | `docs`           |
| `StaticFolder`       | `public`         |
| `IndexFile`          | `Index.md`       |
| `SettingsFile`       | `sharpress.json` |
| `CustomCssFile`      | `custom.css`     |
| `CreateStarterFiles` | `true`           |

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
