# SharPress

<!-- TODO: replace OWNER with the GitHub user or organization once the repository exists. -->
[![CI](https://github.com/OWNER/SharPress/actions/workflows/ci.yml/badge.svg)](https://github.com/OWNER/SharPress/actions/workflows/ci.yml)
[![NuGet](https://img.shields.io/nuget/vpre/SharPress.Lib)](https://www.nuget.org/packages/SharPress.Lib)

Turn a folder of Markdown files into a documentation site inside your ASP.NET Core app. Docs live next to your
API, in the same deployment, with no Node toolchain and no build step: edit a file, save, refresh.

- Pages from Markdown, with tables, task lists, footnotes and code blocks
- A sidebar you order yourself, plus previous and next links
- An **On this page** outline that follows your scrolling
- A home page with a hero and feature cards
- Light and dark themes, custom CSS, a logo and a favicon
- Served under any URL (`/docs`, `/guide`, ...) and under a path base

## Quick start

```
dotnet add package SharPress.Lib --prerelease
```

```csharp
using SharPress.Lib;

var builder = WebApplication.CreateBuilder(args);
builder.AddSharPress();

var app = builder.Build();
app.UseSharPress();
app.Run();
```

On first run SharPress creates a `SharpLib` folder with a starter site. Open `/` for the home page and
`/docs/getting-started` for the docs.

The full usage guide, including every option, is in the [package README](SharPress.Lib/README.md).

## Repository layout

| Path                   | What it is                                                         |
| ---------------------- | ------------------------------------------------------------------ |
| `SharPress.Lib/`       | The library and NuGet package                                      |
| `SharPress.Lib.Tests/` | xUnit tests, including endpoint tests on an in-memory test server  |
| `SharPress.Api/`       | A sample app that hosts SharPress                                  |
| `pack-local.sh`        | Packs the library and runs the sample against the package          |

## Development

Requires the .NET 10 SDK.

```bash
dotnet build SharPress.slnx
dotnet test SharPress.slnx
dotnet run --project SharPress.Api      # the sample, built against the library source
```

To check what consumers actually get, run `./pack-local.sh`. It packs the library into `./local-packages` and
runs the sample against that package instead of the source.

## Releasing

Pushing a version tag builds, tests and publishes the package to nuget.org, then creates a GitHub release with
the package attached:

```bash
git tag v0.1.0
git push origin v0.1.0
```

The tag is the version; a suffix such as `v0.2.0-preview.1` publishes a prerelease. Publishing uses
[NuGet trusted publishing](https://learn.microsoft.com/en-us/nuget/nuget-org/trusted-publishing), so no API
key is stored in the repository. See [`.github/workflows/publish.yml`](.github/workflows/publish.yml).

## License

[MIT](LICENSE)
