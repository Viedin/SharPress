using System.Net;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace SharPress.Tests;

public class AuthorizationTests
{
    private const string UserHeader = "X-Test-User";
    private const string RoleHeader = "X-Test-Role";

    [Fact]
    public async Task RequireAuthorization_challenges_anonymous_users_on_pages_and_static_files()
    {
        await using var site = await StartAsync(options => options.RequireAuthorization = true);
        site.WriteFile("public/docs/images/diagram.svg", "<svg xmlns=\"http://www.w3.org/2000/svg\"/>");

        foreach (var url in new[] { "/", "/docs/getting-started", "/docs/images/diagram.svg", "/logo.svg" })
        {
            Assert.Equal(HttpStatusCode.Unauthorized, (await site.Client.GetAsync(url)).StatusCode);
        }
    }

    [Fact]
    public async Task RequireAuthorization_serves_signed_in_users()
    {
        await using var site = await StartAsync(options => options.RequireAuthorization = true);
        site.WriteFile("public/docs/images/diagram.svg", "<svg xmlns=\"http://www.w3.org/2000/svg\"/>");

        foreach (var url in new[] { "/", "/docs/getting-started", "/docs/images/diagram.svg", "/logo.svg" })
        {
            Assert.Equal(HttpStatusCode.OK, (await site.Client.SendAsync(Get(url, user: "ada"))).StatusCode);
        }
    }

    [Fact]
    public async Task RequireAuthorization_leaves_built_in_assets_and_unknown_files_alone()
    {
        await using var site = await StartAsync(options => options.RequireAuthorization = true);

        Assert.Equal(HttpStatusCode.OK, (await site.Client.GetAsync("/_content/SharPress/sharpress.css")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await site.Client.GetAsync("/does-not-exist.png")).StatusCode);
    }

    [Fact]
    public async Task RequireAuthorization_covers_a_static_folder_given_as_a_relative_path()
    {
        await using var site = await StartAsync(options =>
        {
            options.RequireAuthorization = true;
            options.StaticFolder = "./public";
        });
        site.WriteFile("public/docs/images/diagram.svg", "<svg xmlns=\"http://www.w3.org/2000/svg\"/>");

        foreach (var url in new[] { "/logo.svg", "/docs/images/diagram.svg" })
        {
            Assert.Equal(HttpStatusCode.Unauthorized, (await site.Client.GetAsync(url)).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await site.Client.SendAsync(Get(url, user: "ada"))).StatusCode);
        }
    }

    [Fact]
    public async Task RequireAuthorization_leaves_the_apps_own_endpoints_alone()
    {
        // The app's public endpoint shares its path with a file in the static folder.
        await using var site = await StartAsync(
            options => options.RequireAuthorization = true,
            beforeSharPress: app => app.MapGet("/robots.txt", () => "from the app"));
        site.WriteFile("public/robots.txt", "from the static folder");

        var response = await site.Client.GetAsync("/robots.txt");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("from the app", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task AuthorizationPolicy_forbids_users_who_do_not_meet_it()
    {
        await using var site = await StartAsync(
            options => options.AuthorizationPolicy = "DocsReaders",
            services => services.AddAuthorization(o => o.AddPolicy("DocsReaders", p => p.RequireRole("reader"))));

        foreach (var url in new[] { "/docs/getting-started", "/logo.svg" })
        {
            Assert.Equal(HttpStatusCode.Unauthorized, (await site.Client.GetAsync(url)).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await site.Client.SendAsync(Get(url, user: "ada"))).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await site.Client.SendAsync(Get(url, user: "ada", role: "reader"))).StatusCode);
        }
    }

    [Fact]
    public async Task RequireAuthorization_without_authentication_fails_at_startup()
    {
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            TestSite.StartAsync(options => options.RequireAuthorization = true));

        Assert.Contains("AddAuthentication", exception.Message);
    }

    private static Task<TestSite> StartAsync(
        Action<SharPressOptions> configure,
        Action<IServiceCollection>? services = null,
        Action<WebApplication>? beforeSharPress = null) =>
        TestSite.StartAsync(configure, beforeSharPress, configureBuilder: builder =>
        {
            builder.Services.AddAuthentication(TestAuthHandler.SchemeName)
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, null);
            builder.Services.AddAuthorization();
            services?.Invoke(builder.Services);
        });

    private static HttpRequestMessage Get(string url, string user, string? role = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add(UserHeader, user);
        if (role is not null)
        {
            request.Headers.Add(RoleHeader, role);
        }

        return request;
    }

    /// <summary>Signs in whoever is named in the user header. The default challenge and forbid give 401 and 403.</summary>
    private sealed class TestAuthHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        public const string SchemeName = "Test";

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (Request.Headers[UserHeader].ToString() is not { Length: > 0 } user)
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            var claims = new List<Claim> { new(ClaimTypes.Name, user) };
            if (Request.Headers[RoleHeader].ToString() is { Length: > 0 } role)
            {
                claims.Add(new Claim(ClaimTypes.Role, role));
            }

            var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, SchemeName));
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, SchemeName)));
        }
    }
}
