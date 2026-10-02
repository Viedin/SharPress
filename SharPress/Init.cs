using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using SharPress.Content;
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
        services.AddSingleton<MissingLinkWarnings>();

        // Scoped, so a content source can be scoped too (e.g. to use a DbContext), and the settings are read once
        // per request. TryAdd keeps a source registered with AddSharPressContentSource before this call.
        services.TryAddScoped<ISharPressContentSource, FileContentSource>();
        services.AddScoped<SiteSettingsService>();
        services.AddScoped<MarkdownPageService>();
        services.AddRazorComponents();
        return services;
    }

    /// <summary>
    /// Reads the home page, the docs pages and the settings from <typeparamref name="TSource"/> instead of the
    /// files under <see cref="SharPressOptions.RootFolder"/>, for example from a database or a CMS. No starter
    /// files are created; the static folder is still served from disk. It can be called before or after
    /// <c>AddSharPress</c>.
    /// </summary>
    /// <typeparam name="TSource">The content source.</typeparam>
    /// <param name="services">The service collection to add to.</param>
    /// <param name="lifetime">
    /// The source's lifetime. Scoped (the default) creates one per request, so it can use scoped services such as
    /// a DbContext; use Singleton for a source that keeps its own cache.
    /// </param>
    public static IServiceCollection AddSharPressContentSource<TSource>(this IServiceCollection services, ServiceLifetime lifetime = ServiceLifetime.Scoped)
        where TSource : class, ISharPressContentSource
    {
        services.Replace(ServiceDescriptor.Describe(typeof(ISharPressContentSource), typeof(TSource), lifetime));
        services.Configure<SharPressOptions>(options => options.UsesCustomContentSource = true);
        return services;
    }

    /// <summary>
    /// Reads the home page, the docs pages and the settings from <typeparamref name="TSource"/> instead of files.
    /// See <see cref="AddSharPressContentSource{TSource}(IServiceCollection, ServiceLifetime)"/>.
    /// </summary>
    /// <typeparam name="TSource">The content source.</typeparam>
    /// <param name="builder">The application builder.</param>
    /// <param name="lifetime">
    /// The source's lifetime. Scoped (the default) creates one per request, so it can use scoped services such as
    /// a DbContext; use Singleton for a source that keeps its own cache.
    /// </param>
    public static WebApplicationBuilder AddSharPressContentSource<TSource>(this WebApplicationBuilder builder, ServiceLifetime lifetime = ServiceLifetime.Scoped)
        where TSource : class, ISharPressContentSource
    {
        builder.Services.AddSharPressContentSource<TSource>(lifetime);
        return builder;
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
                site.UseExceptionHandler(new ExceptionHandlerOptions
                {
                    ExceptionHandler = PageEndpoints.RenderErrorAsync,
                    CreateScopeForErrors = true,
                });
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
