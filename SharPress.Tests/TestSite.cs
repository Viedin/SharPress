using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using SharPress.Startup;

namespace SharPress.Tests;

/// <summary>
/// A SharPress app running on an in-memory test server, with its own temporary content root. The starter site is
/// created on start, like on a real first run; tests add or change files afterwards, since pages are read per request.
/// </summary>
internal sealed class TestSite : IAsyncDisposable
{
    private TestSite(string contentRoot, WebApplication app)
    {
        ContentRoot = contentRoot;
        App = app;
        Client = app.GetTestClient();
    }

    public string ContentRoot { get; }

    public WebApplication App { get; }

    public HttpClient Client { get; }

    public SiteFolders Folders => App.Services.GetRequiredService<SiteFolders>();

    public T GetService<T>() where T : notnull => App.Services.GetRequiredService<T>();

    /// <param name="configure">SharPress options.</param>
    /// <param name="beforeSharPress">Runs before UseSharPress, e.g. to add UsePathBase or a test endpoint.</param>
    /// <param name="environment">"Production" turns on the error page.</param>
    public static async Task<TestSite> StartAsync(
        Action<SharPressOptions>? configure = null,
        Action<WebApplication>? beforeSharPress = null,
        string environment = "Development")
    {
        var contentRoot = Path.Combine(Path.GetTempPath(), "sharpress-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(contentRoot);

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            // UseSharPress maps static assets from the "<application>.staticwebassets.endpoints.json" manifest,
            // which the Web SDK writes for this project. Without this the name would be the test runner's.
            ApplicationName = typeof(TestSite).Assembly.GetName().Name,
            ContentRootPath = contentRoot,
            EnvironmentName = environment,
        });
        builder.WebHost.UseTestServer();
        builder.AddSharPress(configure);

        var app = builder.Build();
        beforeSharPress?.Invoke(app);
        app.UseSharPress();
        await app.StartAsync();

        return new TestSite(contentRoot, app);
    }

    /// <summary>Writes a file relative to the site's root folder, creating folders as needed.</summary>
    public void WriteFile(string relativePath, string content)
    {
        var path = Path.Combine(Folders.Root, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }

    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        await App.DisposeAsync();
        try
        {
            Directory.Delete(ContentRoot, recursive: true);
        }
        catch (IOException)
        {
            // A leftover temp folder isn't worth failing a test over.
        }
    }
}
