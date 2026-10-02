using System.Text.Json;
using Microsoft.Extensions.Logging;
using SharPress.Services;
using SharPress.Startup;

namespace SharPress.Content;

/// <summary>
/// The default content source: the home page, the docs folder and the settings file under
/// <see cref="SharPressOptions.RootFolder"/>. Files are read on every call so edits show up on refresh.
/// </summary>
internal sealed class FileContentSource(SiteFolders folders, ILogger<FileContentSource> logger) : ISharPressContentSource
{
    public async Task<string?> GetHomePageAsync(CancellationToken cancellationToken)
    {
        var path = folders.IndexFile;
        return File.Exists(path) ? await File.ReadAllTextAsync(path, cancellationToken) : null;
    }

    /// <summary>Checks that the home page file exists, without reading it.</summary>
    public Task<bool> HasHomePageAsync(CancellationToken cancellationToken) => Task.FromResult(File.Exists(folders.IndexFile));

    /// <summary>
    /// Matches the slug against the files that actually exist, so lookups are case-insensitive on every OS
    /// and the request can never address a path outside the docs folder.
    /// </summary>
    public async Task<string?> GetDocsPageAsync(string slug, CancellationToken cancellationToken)
    {
        var path = FindDocsFiles().GetValueOrDefault(slug);
        return path is null ? null : await File.ReadAllTextAsync(path, cancellationToken);
    }

    /// <summary>Lists every page in the docs folder, titled by its first # heading.</summary>
    public async Task<IReadOnlyList<DocsPageEntry>> GetDocsPagesAsync(CancellationToken cancellationToken)
    {
        var pages = new List<DocsPageEntry>();

        foreach (var (slug, file) in FindDocsFiles())
        {
            var markdown = await File.ReadAllTextAsync(file, cancellationToken);
            pages.Add(new DocsPageEntry(slug, MarkdownPageService.GetTitle(markdown)));
        }

        return pages;
    }

    /// <summary>
    /// Reads the settings file (sharpress.json by default). Returns null, for the default settings, if the file is
    /// missing, invalid, or can't be read right now (e.g. locked mid-save).
    /// </summary>
    public async Task<SiteSettings?> GetSettingsAsync(CancellationToken cancellationToken)
    {
        var path = folders.SettingsFile;
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            await using var stream = File.OpenRead(path);
            return await JsonSerializer.DeserializeAsync(stream, SiteSettingsJsonContext.Default.SiteSettings, cancellationToken);
        }
        catch (FileNotFoundException)
        {
            // Deleted after the check above.
            return null;
        }
        catch (Exception exception) when (exception is JsonException or IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(exception, "Could not read {File}; using default settings.", path);
            return null;
        }
    }

    /// <summary>Maps each docs page slug (lower-cased path without extension) to its file.</summary>
    private Dictionary<string, string> FindDocsFiles()
    {
        var pages = new Dictionary<string, string>(StringComparer.Ordinal);
        var docsPath = folders.Docs;
        if (!Directory.Exists(docsPath))
        {
            return pages;
        }

        foreach (var file in Directory.EnumerateFiles(docsPath, "*.md", SearchOption.AllDirectories))
        {
            var slug = Path.ChangeExtension(Path.GetRelativePath(docsPath, file), null).Replace('\\', '/').ToLowerInvariant();
            pages[slug] = file;
        }

        return pages;
    }
}
