---
name: orchardcore-cli-templates
description: Creates and manages Orchard Core custom Liquid templates and conditional layers through `pomi`. Use for content, summary, widget, field, and layout shape overrides; layer definitions and visibility rules; rendering content models; registering CSS/media; and validating output for remotely managed tenants.
---

# Pomi CLI Templates

Before issuing commands, read the [shared context, authentication, and output rules](../orchardcore-cli/references/shared-rules.md).
For first-time access or login problems, follow [authentication and contexts](../orchardcore-cli/references/authentication.md).
These rules apply even when this specialist is selected directly.

Use the Templates module for tenant-stored Liquid shape overrides. Keep content
structure in definitions, content values in items, and presentation in Liquid.
An active site theme is required: custom templates extend the active theme's
shape table and cannot replace the absence of a site theme.

Render content-driven composition rather than rebuilding it in the page
template. A page template should render its `FlowPart`; each section should use
a `Widget__<SectionType>` template; collection-section templates should render
or iterate their named Bag. Keep grids, classes, spacing, breakpoints, and
client behavior in these Liquid templates, never in editor content.

Ensure `OrchardCore.Templates`, `OrchardCore.Liquid`, and
`OrchardCore.Resources` are enabled. Enable the part/field modules used by the
model, then refresh discovery.

For a tenant created with the `Blank` recipe, start from Safe Mode:

```bash
pomi themes list --admin false --take 200
pomi themes set-current TheTheme
pomi features enable OrchardCore.Templates
pomi features enable OrchardCore.Liquid
pomi features enable OrchardCore.Resources
pomi api refresh --force
```

Use a theme ID returned by `themes list`; `TheTheme` is the standard host
example. Confirm the root page renders before adding a custom `Layout`
template. If the root is still in Safe Mode, fix theme selection first.

## Workflow

1. Inspect the target type, schema, and representative content item.
2. Determine the exact shape/alternate template name.
3. Create the complete template JSON in a local file.
4. Create or replace the template.
5. Render an item and verify its public route, media, and CSS.

```bash
pomi content types show Article
pomi content items schema Article
pomi content items show <id> --version published
pomi templates schema --operation create
pomi templates list --search Article
```

## Manage templates

A complete minimal `Layout` template must render document metadata, resource
zones, messages, and the content section:

```liquid
<!DOCTYPE html>
<html lang="{{ Culture.Name }}" dir="{{ Culture.Dir }}">
<head>
  <meta charset="utf-8">
  <meta name="viewport" content="width=device-width, initial-scale=1">
  <title>{{ "PageTitle" | shape_new | shape_stringify }}</title>
  {% resources type: "Meta" %}
  {% resources type: "HeadLink" %}
  {% resources type: "HeadScript" %}
  {% resources type: "Stylesheet" %}
</head>
<body>
  {% render_section "Header", required: false %}
  {% render_section "Messages", required: false %}
  <main id="main-content">
    {% render_section "Content" %}
  </main>
  {% render_section "Footer", required: false %}
  {% resources type: "FootScript" %}
</body>
</html>
```

Create it as template name `Layout` before relying on tenant CSS or scripts.

Upload `assets/styles/site-v1.css` through Media first (see
[orchardcore-cli-media](../orchardcore-cli-media/SKILL.md)). Resolve it with `asset_url` so tenant prefixes and
remote storage providers work.

`article-template.json`:

```json
{
  "name": "Content__Article",
  "content": "{% assign stylesheet = \"assets/styles/site-v1.css\" | asset_url %}{% style name:\"tenant-site\", src:stylesheet %}<article class=\"article\"><h1>{{ Model.ContentItem.DisplayText }}</h1>{{ Model.Content.HtmlBodyPart | shape_render }}</article>",
  "description": "Article detail template."
}
```

```bash
pomi templates create --body-file article-template.json
pomi templates show Content__Article
pomi templates update Content__Article --body-file article-template.json
pomi templates delete Content__Article --force
```

Create uses a stable case-insensitive name. An identical retry converges;
different content under the same name conflicts. Update is a complete
replacement and the route/body names must match exactly.

## Conditional layers

Use `OrchardCore.Layers` for rules that control the visibility of widgets placed
in theme zones. A layer definition does not place a widget. Refresh discovery
and inspect `pomi layers --help` and `pomi layers conditions --output json`.
Use only conditions marked `canWrite`, with properties matching their live
schemas. Prefer the built-in URL, culture, role and authentication conditions
when they express the requested behavior.

```bash
pomi layers list --output json
pomi layers conditions --output json
pomi layers schema --operation create
pomi layers validate --body-file layer.json --output json
pomi layers create --body-file layer.json --output json
pomi layers show News --output json
pomi layers update News --body-file layer.json --output json
```

Layer JSON contains `name`, `description` and `conditions`. Each condition has
`name`, `properties`, optional `conditionId` and group-only `conditions` children.
For an always-matching layer, use
`{"name":"Always","conditions":[{"name":"BooleanCondition","properties":{"value":true}}]}`.
An empty conditions array does not match. All root conditions must match;
All/Any groups express nested logic. JavaScript validation checks syntax without
running the script, so verify its behavior on the actual page.

Updates replace the complete description and conditions. Read back first,
preserve returned condition IDs and retain every condition that should remain.
Identical creates converge; a different definition under an existing name
conflicts. Names cannot be renamed through update. Deletion requires explicit
confirmation and is refused while widgets reference the layer; do not delete
widgets merely to bypass that refusal. Verify public output for the intended
URL and visitor identity after saving, including a case that should hide the
widget. A successful validation or save does not prove visibility.

## Naming

Common alternates include:

```text
Content__Article
Content_Summary__Article
Widget__Hero
```

Field alternates depend on part name, field name, field shape type, content
type, and display type. Do not guess them. Follow the Templates module's field
alternate table, inspect rendered shape metadata or existing templates, and
confirm the override with `pomi content items render`.

## Liquid rules

- Escape editor-provided text by default.
- Avoid `raw` and `liquid` for ordinary values.
- Prefer `shape_render` for HTML-bearing parts and fields.
- Read direct values from
  `Model.ContentItem.Content.<Part>.<Field>.<ValueProperty>` only after checking
  the live content schema.
- Resolve media paths through Orchard filters rather than concatenating public
  URLs.
- Use `~` application-relative static paths so tenant prefixes are preserved.
- Register resources with Liquid tags and ensure the active layout renders
  `{% resources type: "Stylesheet" %}` and scripts where applicable.
- Keep query/filter business logic outside templates when a query or content
  list can provide the intended collection.
- Keep page templates structural and small. Put section design in widget
  templates and inner-card design in inner widget templates.
- Map semantic content values to design classes explicitly; never render an
  editor-provided CSS class with `raw`.

Example media rendering:

```liquid
{% assign image = Model.ContentItem.Content.ArticleDetails.HeroImage %}
{% if image.Paths.first %}
  {% assign image_url = image.Paths.first | asset_url | append_version %}
  <img src="{{ image_url }}" alt="{{ image.MediaTexts.first }}" class="article__hero">
{% endif %}
```

Do not pass editor-controlled alternative text or other attribute values to
`img_tag`; write them in normal Liquid HTML attributes so Liquid encodes them.

Read [liquid-patterns](references/liquid-patterns.md) for content, summary, Flow/Bag, list, menu,
and resource patterns.

## Verify

```bash
pomi content items render <id> --version draft --display-type Detail
pomi content items render <id> --version published --display-type Summary
curl -fsS 'https://cms.example.com/tenant-a/article-path'
pomi media files show assets/styles/site-v1.css --output json
```

Fetch the returned Media `url` to check the stylesheet response.
Check semantic HTML, encoded values, image alternative text, tenant-prefixed
URLs, missing shapes, stylesheet requests, and responsive behavior. Require:

- root route and representative content route return successful HTML;
- CSS and JavaScript URLs return the expected content type;
- every internal anchor target exists and keyboard focus reaches it;
- browser console has no errors;
- layout works at approximately 375px, 768px, and 1440px viewport widths.

Versioned references (live tenant schemas take precedence):
[Layers API](https://github.com/sebastienros/OrchardCore/blob/fa5d091e6ec67dc3e4b2ccafb0af49db45298e23/src/docs/reference/api/layers/README.md),
[templates API](https://github.com/sebastienros/OrchardCore/blob/4d4fc0fb66a5d789dff6d8057bbc074107918533/src/docs/reference/api/templates/README.md),
[Templates module](https://github.com/sebastienros/OrchardCore/blob/4d4fc0fb66a5d789dff6d8057bbc074107918533/src/docs/reference/modules/Templates/README.md), and
[Liquid module](https://github.com/sebastienros/OrchardCore/blob/4d4fc0fb66a5d789dff6d8057bbc074107918533/src/docs/reference/modules/Liquid/README.md).
