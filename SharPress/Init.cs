using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using SharPress.Endpoints;
using SharPress.Services;
using SharPress.Startup;

namespace SharPress;

/// <summary>Extension methods that add SharPress to an ASP.NET Core app.</summary>
public static class Init
{
    /// <summary>Registers SharPress services.</summary>
    /// <param name="services">The service collection to add to.</param>
    /// <param name="configure">Changes where the site lives and what its files are called. Optional.</param>
    public static IServiceCollection AddSharPress(this IServiceCollection services, Action<SharPressOptions>? configure = null)
    {
        services.AddOptions<SharPressOptions>();
        if (configure is not null)
        {
            services.Configure(configure);
        }

        services.AddSingleton<SiteFolders>();
        services.AddSingleton<SiteSettingsService>();
        services.AddSingleton<MarkdownPageService>();
        services.AddRazorComponents();
        return services;
    }

    /// <summary>Registers SharPress services.</summary>
    /// <param name="builder">The application builder.</param>
    /// <param name="configure">Changes where the site lives and what its files are called. Optional.</param>
    public static WebApplicationBuilder AddSharPress(this WebApplicationBuilder builder, Action<SharPressOptions>? configure = null)
    {
        builder.Services.AddSharPress(configure);
        return builder;
    }

    /// <summary>Adds the middleware and maps the SharPress pages.</summary>
    public static WebApplication UseSharPress(this WebApplication app)
    {
        var folders = app.Services.GetRequiredService<SiteFolders>();
        InitFolderStructure.Run(folders);

        if (!app.Environment.IsDevelopment())
        {
            app.UseExceptionHandler("/Error", createScopeForErrors: true);
            app.UseHsts();
        }

        app.UseHttpsRedirection();
        app.UseAntiforgery();
        app.MapStaticAssets();

        // The user's static folder (custom.css, icons, images) is served from the site root.
        app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = new PhysicalFileProvider(folders.Static),
        });

        PageEndpoints.Map(app, folders);

        return app;
    }
}
