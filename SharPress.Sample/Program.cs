using SharPress;
using SharPress.Sample;

var builder = WebApplication.CreateBuilder(args);
builder.AddSharPress(options =>
{
    // Where the site lives and what its files are called. Everything is optional; these are the defaults.
    // options.RootFolder = "SharpLib";            // the whole site, relative to the project (or an absolute path)
    // options.DocsFolder = "docs";                // docs pages, inside RootFolder
    // options.BaseUrl = "";                       // serve the whole site under a URL, e.g. "faq" for /faq (apps with their own pages)
    // options.DocsUrl = "docs";                   // the URL docs pages are served under, e.g. /docs/getting-started
    // options.StaticFolder = "public";            // served from the site root: logo, favicon, images, custom.css
    // options.IndexFile = "Index.md";             // the home page
    // options.SettingsFile = "sharpress.json";    // site title, sidebar and home layout
    // options.CustomCssFile = "custom.css";       // inside StaticFolder
    // options.CreateStarterFiles = true;          // create a starter site on first run
    // options.RequireAuthorization = false;       // require a signed-in user (register AddAuthentication/AddAuthorization)
    // options.AuthorizationPolicy = null;         // a named policy users must meet; turns on RequireAuthorization
});

// Read the home page, docs and settings from code instead of SharpLib, the way a database or CMS source would:
//   dotnet run --project SharPress.Sample -- --ContentSource=Demo
if (builder.Configuration["ContentSource"] == "Demo")
{
    builder.AddSharPressContentSource<DemoContentSource>();
}

var app = builder.Build();
app.UseSharPress();
app.Run();
