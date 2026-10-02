using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace SharPress.Startup;

/// <summary>
/// Applies <see cref="SharPressOptions.RequireAuthorization"/> to the files in the static folder. They are served
/// by UseStaticFiles rather than an endpoint, so endpoint authorization doesn't reach them.
/// </summary>
internal static class StaticFileAuthorization
{
    public static void Use(WebApplication app, SiteFolders folders)
    {
        app.Use(async (context, next) =>
        {
            // Only requests UseStaticFiles would serve are checked: no endpoint matched, and the file exists.
            // A matched endpoint is either the app's own (which keeps its own authorization, e.g. a public
            // /robots.txt) or the docs endpoint, which already requires authorization for its files.
            if (context.GetEndpoint() is not null || folders.FindStaticFile(context.Request.Path) is null)
            {
                await next(context);
                return;
            }

            var policy = await GetPolicyAsync(context.RequestServices, folders.Options.AuthorizationPolicy);

            // Authenticates with the policy's schemes the same way the authorization middleware does, so it works
            // wherever the app put UseAuthentication.
            var evaluator = context.RequestServices.GetRequiredService<IPolicyEvaluator>();
            var authentication = await evaluator.AuthenticateAsync(policy, context);
            var authorization = await evaluator.AuthorizeAsync(policy, authentication, context, resource: null);

            if (authorization.Succeeded)
            {
                await next(context);
            }
            else if (authorization.Challenged)
            {
                await ForEachScheme(policy, scheme => context.ChallengeAsync(scheme));
            }
            else
            {
                await ForEachScheme(policy, scheme => context.ForbidAsync(scheme));
            }
        });
    }

    private static async Task<AuthorizationPolicy> GetPolicyAsync(IServiceProvider services, string? name)
    {
        var provider = services.GetRequiredService<IAuthorizationPolicyProvider>();
        if (name is null)
        {
            return await provider.GetDefaultPolicyAsync();
        }

        return await provider.GetPolicyAsync(name)
            ?? throw new InvalidOperationException($"The authorization policy '{name}' was not found.");
    }

    /// <summary>Runs the action for each of the policy's schemes, or once with the default scheme if it has none.</summary>
    private static async Task ForEachScheme(AuthorizationPolicy policy, Func<string?, Task> action)
    {
        if (policy.AuthenticationSchemes.Count == 0)
        {
            await action(null);
            return;
        }

        foreach (var scheme in policy.AuthenticationSchemes)
        {
            await action(scheme);
        }
    }
}
