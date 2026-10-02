# Writing content

Pages are Markdown files in `{{RootFolder}}/{{DocsFolder}}`.

## Add a page

1. Create `{{RootFolder}}/{{DocsFolder}}/my-page.md`.
2. Start it with a `#` heading. That is the page title.
3. Open `{{DocsPath}}/my-page`.

Subfolders work too: `{{DocsFolder}}/tutorials/setup.md` is served at `{{DocsPath}}/tutorials/setup`.

## Add it to the navigation

A page that is not in the sidebar still works, but nobody can find it. List it in
`{{RootFolder}}/{{SettingsFile}}`:

```json
{
  "sidebar": [
    {
      "text": "Introduction",
      "items": [
        { "link": "{{DocsPath}}/getting-started" },
        { "link": "{{DocsPath}}/writing-content" },
        { "link": "{{DocsPath}}/my-page" }
      ]
    }
  ]
}
```

The order here is the order in the navigation, and the order of the previous and next links
at the bottom of each page.

## Headings

`##` and `###` headings on a page are listed in the **On this page** outline on the right.

## Markdown

Tables, task lists, footnotes and fenced code blocks all work:

- [x] Write a page
- [ ] Add it to the sidebar

```csharp
Console.WriteLine("Hello from SharPress");
```

Name the language after the opening fence (`csharp`, `json`, `bash`, `html`, ...) to get syntax
highlighting. Blocks without a language are shown plain.
