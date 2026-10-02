# Getting started

Your site lives in the `{{RootFolder}}` folder of this project. SharPress created it the first time
the app started.

## What is in the folder

| Path | What it does |
| ---- | ------------ |
| `{{IndexFile}}` | The home page |
| `{{SettingsFile}}` | The site name, the navigation, and the hero and cards on the home page |
| `{{DocsFolder}}/` | One Markdown file per page, served under `/{{DocsUrl}}` |
| `{{StaticFolder}}/` | Your logo, favicon, images and `{{CustomCssFile}}`, served from the site root |

## Make it yours

1. Change `title` in `{{SettingsFile}}`.
2. Replace `{{StaticFolder}}/logo.svg` and `{{StaticFolder}}/favicon.svg` with your own images.
3. Edit this page and `{{IndexFile}}`.

There is no build step. Save a file and refresh the browser.

## Change the colors

Open `{{StaticFolder}}/{{CustomCssFile}}` and uncomment a variable:

```css
:root {
  --sp-brand: #0f766e;
}
```

## Next

Continue with [Writing content](/{{DocsUrl}}/writing-content).
