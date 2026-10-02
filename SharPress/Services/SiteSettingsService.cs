using System.Text.Json;
using System.Text.Json.Serialization;

namespace SharPress.Services;

/// <summary>Source-generated JSON metadata for the settings file, so reading it needs no reflection.</summary>
[JsonSourceGenerationOptions(
    PropertyNameCaseInsensitive = true,
    ReadCommentHandling = JsonCommentHandling.Skip,
    AllowTrailingCommas = true)]
[JsonSerializable(typeof(SiteSettings))]
internal sealed partial class SiteSettingsJsonContext : JsonSerializerContext;

/// <summary>
/// The settings for the current request. It is scoped: the endpoint, the navigation and the page shell all need
/// the settings, and with a database or CMS source each read could be a round trip.
/// </summary>
internal sealed class SiteSettingsService(ISharPressContentSource source)
{
    private Task<SiteSettings>? _settings;

    /// <summary>
    /// Returns the site settings from the content source, read once per request. Returns default settings if the
    /// source has none.
    /// </summary>
    public Task<SiteSettings> GetAsync(CancellationToken cancellationToken = default) =>
        _settings ??= LoadAsync(cancellationToken);

    private async Task<SiteSettings> LoadAsync(CancellationToken cancellationToken) =>
        await source.GetSettingsAsync(cancellationToken) ?? new SiteSettings();
}
