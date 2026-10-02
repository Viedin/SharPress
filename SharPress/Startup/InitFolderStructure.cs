using System.Text.Encodings.Web;
using System.Text.Json;

namespace SharPress.Startup;

internal static class InitFolderStructure
{
    /// <summary>The starter files are embedded from the Templates folder of this project, with this prefix.</summary>
    private const string TemplatePrefix = "Templates/";

    // The names used inside the Templates folder. They are mapped to whatever the options say on the way out.
    private const string TemplateIndexFile = "Index.md";
    private const string TemplateSettingsFile = "sharpress.json";
    private const string TemplateDocsFolder = "docs";
    private const string TemplateStaticFolder = "public";
    private const string TemplateCustomCssFile = "custom.css";

    /// <summary>Templates with these extensions are text and get their placeholders filled in; the rest are copied as-is.</summary>
    private static readonly HashSet<string> TextTemplateExtensions = new(StringComparer.OrdinalIgnoreCase) { ".md", ".json", ".css" };

    /// <summary>
    /// Creates the site folder with a starter site: a home page, the settings file, a few docs pages and a static
    /// folder with a logo, favicon and custom.css. Nothing that already exists is touched.
    /// </summary>
    /// <remarks>
    /// Top-level files such as the home page and the settings are recreated if they are missing. The starter
    /// files inside a folder (docs, static) are only created when the folder itself doesn't exist yet,
    /// so a page or image you deleted on purpose doesn't come back.
    /// </remarks>
    public static void Run(SiteFolders folders)
    {
        Directory.CreateDirectory(folders.Root);

        if (folders.Options.CreateStarterFiles)
        {
            CopyTemplates(folders);
        }

        // The static folder is served as-is, so it has to exist even if there is nothing in it.
        Directory.CreateDirectory(folders.Static);
    }

    private static void CopyTemplates(SiteFolders folders)
    {
        var assembly = typeof(InitFolderStructure).Assembly;
        var templates = assembly.GetManifestResourceNames()
            .Where(name => name.StartsWith(TemplatePrefix, StringComparison.Ordinal))
            .Select(name => (Resource: name, Path: name[TemplatePrefix.Length..].Replace('\\', '/')))
            .ToList();

        // Decided up front, before the first folder is created.
        var newFolders = templates
            .Select(template => TopLevelFolder(template.Path))
            .OfType<string>()
            .Distinct()
            .Where(folder => !Directory.Exists(FolderPath(folders, folder)))
            .ToHashSet();

        foreach (var (resource, relativePath) in templates)
        {
            var folder = TopLevelFolder(relativePath);
            var destination = DestinationPath(folders, relativePath);
            var shouldCreate = folder is null ? !File.Exists(destination) : newFolders.Contains(folder);
            if (!shouldCreate)
            {
                continue;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            using var source = assembly.GetManifestResourceStream(resource)!;

            if (TextTemplateExtensions.Contains(Path.GetExtension(relativePath)))
            {
                using var reader = new StreamReader(source);
                var isJson = Path.GetExtension(relativePath).Equals(".json", StringComparison.OrdinalIgnoreCase);
                File.WriteAllText(destination, FillPlaceholders(reader.ReadToEnd(), folders, isJson));
            }
            else
            {
                using var target = File.Create(destination);
                source.CopyTo(target);
            }
        }
    }

    /// <summary>
    /// The templates mention the site's folders, files and docs URL by placeholder, such as {{DocsFolder}}, so the
    /// starter files use the names and links the options actually set. With <paramref name="isJson"/> the values
    /// are escaped for use inside JSON strings, so a Windows path such as C:\Sites doesn't make the settings file
    /// invalid.
    /// </summary>
    private static string FillPlaceholders(string text, SiteFolders folders, bool isJson)
    {
        string Value(string value) =>
            isJson ? JsonEncodedText.Encode(value, JavaScriptEncoder.UnsafeRelaxedJsonEscaping).ToString() : value;

        return text
            .Replace("{{RootFolder}}", Value(folders.Options.RootFolder))
            .Replace("{{DocsFolder}}", Value(folders.Options.DocsFolder))
            .Replace("{{StaticFolder}}", Value(folders.Options.StaticFolder))
            .Replace("{{IndexFile}}", Value(folders.Options.IndexFile))
            .Replace("{{SettingsFile}}", Value(folders.Options.SettingsFile))
            .Replace("{{CustomCssFile}}", Value(folders.Options.CustomCssFile))
            .Replace("{{DocsUrl}}", Value(folders.DocsUrl));
    }

    private static string? TopLevelFolder(string templatePath)
    {
        var separator = templatePath.IndexOf('/');
        return separator < 0 ? null : templatePath[..separator];
    }

    private static string FolderPath(SiteFolders folders, string templateFolder) => templateFolder switch
    {
        TemplateDocsFolder => folders.Docs,
        TemplateStaticFolder => folders.Static,
        _ => Path.Combine(folders.Root, templateFolder),
    };

    private static string DestinationPath(SiteFolders folders, string templatePath)
    {
        var folder = TopLevelFolder(templatePath);
        if (folder is null)
        {
            return templatePath switch
            {
                TemplateIndexFile => folders.IndexFile,
                TemplateSettingsFile => folders.SettingsFile,
                _ => Path.Combine(folders.Root, templatePath),
            };
        }

        var rest = templatePath[(folder.Length + 1)..];
        if (folder == TemplateStaticFolder && rest == TemplateCustomCssFile)
        {
            return folders.CustomCssFile;
        }

        return Path.Combine(FolderPath(folders, folder), rest);
    }
}
