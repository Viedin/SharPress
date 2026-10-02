using SharPress;

var builder = WebApplication.CreateBuilder(args);
builder.AddSharPress(options =>
{
    // Where the site lives and what its files are called. Everything is optional; these are the defaults.
    // options.RootFolder = "SharpLib";            // the whole site, relative to the project (or an absolute path)
    // options.DocsFolder = "docs";                // docs pages, inside RootFolder
    // options.DocsUrl = "docs";                   // the URL docs pages are served under, e.g. /docs/getting-started
    // options.StaticFolder = "public";            // served from the site root: logo, favicon, images, custom.css
    // options.IndexFile = "Index.md";             // the home page
    // options.SettingsFile = "sharpress.json";    // site title, sidebar and home layout
    // options.CustomCssFile = "custom.css";       // inside StaticFolder
    // options.CreateStarterFiles = true;          // create a starter site on first run
    // options.RequireAuthorization = false;       // require a signed-in user (register AddAuthentication/AddAuthorization)
    // options.AuthorizationPolicy = null;         // a named policy users must meet; turns on RequireAuthorization
});

var app = builder.Build();
app.UseSharPress();
app.Run();
