namespace SharPress.Services;

/// <summary>
/// The site configuration read from the settings file (sharpress.json by default), or returned by a custom
/// <see cref="ISharPressContentSource"/>. List properties turn a JSON null into an empty list, so the components
/// can use them without null checks.
/// </summary>
public sealed class SiteSettings
{
    /// <summary>The name of the site, shown at the top of the docs navigation as a link to the home page.</summary>
    public string? Title { get; set; }

    /// <summary>An image shown next to the site name in the docs navigation, for example "/logo.svg".</summary>
    public string? Logo { get; set; }

    /// <summary>The icon shown in the browser tab, for example "/favicon.svg".</summary>
    public string? Favicon { get; set; }

    /// <summary>A label shown above the docs navigation. No label is shown when it is empty.</summary>
    public string? SidebarTitle { get; set; }

    /// <summary>
    /// The docs navigation, in the order it should be rendered. When empty, every docs page is listed
    /// alphabetically by path.
    /// </summary>
    public List<SidebarItem> Sidebar { get; set => field = value ?? []; } = [];

    /// <summary>A landing section shown above the content of the home page. Nothing extra is shown when it is null.</summary>
    public HomeSettings? Home { get; set; }
}

/// <summary>The landing section of the home page, similar to VitePress' home layout.</summary>
public sealed class HomeSettings
{
    /// <summary>The large block at the top of the page: headline, text and buttons.</summary>
    public HeroSettings? Hero { get; set; }

    /// <summary>Cards linking to the main parts of the site.</summary>
    public List<FeatureItem> Features { get; set => field = value ?? []; } = [];
}

/// <summary>The hero block at the top of the home page.</summary>
public sealed class HeroSettings
{
    /// <summary>An image shown above the headline, for example "/hero.svg".</summary>
    public string? Image { get; set; }

    /// <summary>The large headline, shown in the brand color.</summary>
    public string? Name { get; set; }

    /// <summary>A second line under the headline.</summary>
    public string? Text { get; set; }

    /// <summary>A smaller line of supporting text.</summary>
    public string? Tagline { get; set; }

    /// <summary>Buttons under the headline.</summary>
    public List<HeroAction> Actions { get; set => field = value ?? []; } = [];
}

/// <summary>A button in the hero block.</summary>
public sealed class HeroAction
{
    /// <summary>The label on the button.</summary>
    public string? Text { get; set; }

    /// <summary>The page to link to, for example "/docs/getting-started".</summary>
    public string? Link { get; set; }

    /// <summary>"brand" (the default) for a filled button, or "alt" for an outlined one.</summary>
    public string? Theme { get; set; }
}

/// <summary>A card under the hero block.</summary>
public sealed class FeatureItem
{
    /// <summary>The card's heading.</summary>
    public string? Title { get; set; }

    /// <summary>A sentence or two below the heading.</summary>
    public string? Details { get; set; }

    /// <summary>The name of a Bootstrap Icons icon without the "bi-" prefix, for example "rocket-takeoff".</summary>
    public string? Icon { get; set; }

    /// <summary>When set, the whole card links to this page.</summary>
    public string? Link { get; set; }
}

/// <summary>
/// An entry in the sidebar. An entry with <see cref="Items"/> is a group heading; an entry with a
/// <see cref="Link"/> is a link to a page.
/// </summary>
public sealed class SidebarItem
{
    /// <summary>The label. For links it defaults to the page's own title.</summary>
    public string? Text { get; set; }

    /// <summary>The page to link to, for example "/docs/getting-started".</summary>
    public string? Link { get; set; }

    /// <summary>The links in a group. Groups inside groups are flattened into their parent.</summary>
    public List<SidebarItem> Items { get; set => field = value ?? []; } = [];
}
