using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
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

        if (folders.RequireAuthorization
            && (app.Services.GetService<IAuthorizationPolicyProvider>() is null || app.Services.GetService<IAuthenticationSchemeProvider>() is null))
        {
            // Without this the endpoints would fail on every request with a less helpful message.
            throw new InvalidOperationException(
                $"{nameof(SharPressOptions)}.{nameof(SharPressOptions.RequireAuthorization)} uses the app's authentication. " +
                "Call builder.Services.AddAuthentication(...) and builder.Services.AddAuthorization() before building the app.");
        }

        // Only requests under the base URL get SharPress's error page and HTTPS rules, so an app that hosts
        // SharPress under e.g. /faq keeps its own for everything else. Without a base URL this is every request.
        app.UseWhen(context => context.Request.Path.StartsWithSegments(folders.BasePath), site =>
        {
            if (!app.Environment.IsDevelopment())
            {
                site.UseExceptionHandler($"{folders.BasePath}/Error", createScopeForErrors: true);
                site.UseHsts();
            }

            site.UseHttpsRedirection();
            site.UseAntiforgery();
        });

        app.MapStaticAssets();

        // The user's static folder (custom.css, icons, images) is served from the site root.
        if (folders.RequireAuthorization)
        {
            StaticFileAuthorization.Use(app, folders);
        }

        app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = new PhysicalFileProvider(folders.Static),
            RequestPath = folders.BasePath,
        });

        PageEndpoints.Map(app, folders);

        return app;
    }
}
