using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using SharPress.Startup;

namespace SharPress.Services;

/// <summary>Source-generated JSON metadata for the settings file, so reading it needs no reflection.</summary>
[JsonSourceGenerationOptions(
    PropertyNameCaseInsensitive = true,
    ReadCommentHandling = JsonCommentHandling.Skip,
    AllowTrailingCommas = true)]
[JsonSerializable(typeof(SiteSettings))]
internal sealed partial class SiteSettingsJsonContext : JsonSerializerContext;

internal sealed class SiteSettingsService(SiteFolders folders, ILogger<SiteSettingsService> logger)
{
    /// <summary>
    /// Reads the settings file (sharpress.json by default). The file is read on every call so edits show up on refresh.
    /// Returns default settings if the file is missing, invalid, or can't be read right now (e.g. locked mid-save).
    /// </summary>
    public async Task<SiteSettings> GetAsync(CancellationToken cancellationToken = default)
    {
        var path = folders.SettingsFile;
        if (!File.Exists(path))
        {
            return new SiteSettings();
        }

        try
        {
            await using var stream = File.OpenRead(path);
            return await JsonSerializer.DeserializeAsync(stream, SiteSettingsJsonContext.Default.SiteSettings, cancellationToken) ?? new SiteSettings();
        }
        catch (FileNotFoundException)
        {
            // Deleted after the check above.
            return new SiteSettings();
        }
        catch (Exception exception) when (exception is JsonException or IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(exception, "Could not read {File}; using default settings.", path);
            return new SiteSettings();
        }
    }
}
