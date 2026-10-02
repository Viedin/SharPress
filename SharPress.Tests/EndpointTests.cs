using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace SharPress.Tests;

public class EndpointTests
{
    [Fact]
    public async Task Serves_the_home_page_and_starter_docs()
    {
        await using var site = await TestSite.StartAsync();

        var home = await site.Client.GetAsync("/");
        var docs = await site.Client.GetAsync("/docs/getting-started");

        Assert.Equal(HttpStatusCode.OK, home.StatusCode);
        Assert.Equal(HttpStatusCode.OK, docs.StatusCode);
        Assert.Contains("<title>Getting started</title>", await docs.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Missing_docs_page_returns_404_with_the_navigation()
    {
        await using var site = await TestSite.StartAsync();

        var response = await site.Client.GetAsync("/docs/does-not-exist");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("Page not found.", html);
        Assert.Contains("href=\"/docs/getting-started\"", html);
    }

    [Fact]
    public async Task DocsUrl_moves_the_docs_and_the_starter_links()
    {
        await using var site = await TestSite.StartAsync(options => options.DocsUrl = "/guide/");

        Assert.Equal(HttpStatusCode.OK, (await site.Client.GetAsync("/guide/getting-started")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await site.Client.GetAsync("/docs/getting-started")).StatusCode);

        // The starter settings were written with the configured URL, so the hero links work.
        var home = await site.Client.GetStringAsync("/");
        Assert.Contains("href=\"/guide/getting-started\"", home);
    }

    [Fact]
    public async Task Marks_the_current_page_in_the_navigation_and_links_its_neighbours()
    {
        await using var site = await TestSite.StartAsync();

        var html = await site.Client.GetStringAsync("/docs/getting-started");

        Assert.Contains("<a href=\"/docs/getting-started\" class=\"active\"", html);
        Assert.Contains("class=\"sp-neighbour sp-neighbour-next\" href=\"/docs/writing-content\"", html);
    }

    [Fact]
    public async Task Serves_static_files_under_the_docs_url()
    {
        await using var site = await TestSite.StartAsync();
        site.WriteFile("public/docs/images/diagram.svg", "<svg xmlns=\"http://www.w3.org/2000/svg\"/>");

        var response = await site.Client.GetAsync("/docs/images/diagram.svg");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("image/svg+xml", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Does_not_serve_files_outside_the_static_folder()
    {
        await using var site = await TestSite.StartAsync();

        // %2E%2E is "..", which the server doesn't normalize away when it's encoded.
        var response = await site.Client.GetAsync("/docs/%2E%2E/%2E%2E/sharpress.json");

        Assert.NotEqual("application/json", response.Content.Headers.ContentType?.MediaType);
        Assert.DoesNotContain("\"sidebar\"", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Links_include_the_path_base()
    {
        await using var site = await TestSite.StartAsync(beforeSharPress: app =>
        {
            app.UsePathBase("/myapp");
            app.UseRouting();
        });
        site.WriteFile("docs/links.md", "# Links\n\n[root](/docs/getting-started) [relative](other) [anchor](#here)\n");

        var html = await site.Client.GetStringAsync("/myapp/docs/links");

        Assert.Contains("href=\"/myapp/_content/SharPress/sharpress.css\"", html);
        Assert.Contains("href=\"/myapp/docs/getting-started\"", html);
        Assert.Contains("href=\"other\"", html);
        Assert.Contains("href=\"#here\"", html);
    }

    [Fact]
    public async Task Shows_the_error_page_outside_development()
    {
        await using var site = await TestSite.StartAsync(
            beforeSharPress: app => app.MapGet("/boom", (HttpContext _) => throw new InvalidOperationException("test")),
            environment: "Production");

        var response = await site.Client.GetAsync("/boom");

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Contains("Something went wrong", await response.Content.ReadAsStringAsync());
    }
}
