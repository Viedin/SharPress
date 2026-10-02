<picture>
  <source media="(prefers-color-scheme: dark)" srcset=".github/assets/banner-dark.svg">
  <img alt="SharPress: Markdown docs inside your ASP.NET Core app" src=".github/assets/banner-light.svg" width="100%">
</picture>

<p align="center">
  <a href="https://github.com/Viedin/SharPress/actions/workflows/ci.yml"><img alt="CI" src="https://github.com/Viedin/SharPress/actions/workflows/ci.yml/badge.svg"></a>
  <a href="https://www.nuget.org/packages/SharPress"><img alt="NuGet" src="https://img.shields.io/nuget/vpre/SharPress?logo=nuget&color=3451b2"></a>
  <a href="LICENSE"><img alt="MIT license" src="https://img.shields.io/badge/license-MIT-green"></a>
</p>

Serve a folder of Markdown files as a docs site from your ASP.NET Core app. No Node, no build step: edit a
file, save, refresh.

## Quick start

```
dotnet add package SharPress --prerelease
```

```csharp
using SharPress;

var builder = WebApplication.CreateBuilder(args);
builder.AddSharPress();

var app = builder.Build();
app.UseSharPress();
app.Run();
```

On first run SharPress creates a `SharpLib` folder with a starter site. Open `/` for the home page and
`/docs/getting-started` for the docs.

Options, sign-in and sub-path hosting are covered in the [package README](SharPress/README.md).

## Development

Requires the .NET 10 SDK.

```bash
dotnet build SharPress.slnx
dotnet test SharPress.slnx
dotnet run --project SharPress.Sample
```

## License

[MIT](LICENSE)
