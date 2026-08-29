---
name: orchardcore-cli-templates
description: Creates and manages Orchard Core custom Liquid templates through `oc`. Use for content, summary, widget, field, and layout shape overrides; rendering content models; registering CSS/media; and validating template output for remotely managed tenants.
---

# Orchard Core CLI Templates

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
oc themes list --admin false --take 200
oc themes set-current TheTheme
oc features enable OrchardCore.Templates
oc features enable OrchardCore.Liquid
oc features enable OrchardCore.Resources
oc api refresh --force
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
oc content types show Article
oc content items schema Article
oc content items show <id> --version published
oc templates schema --operation create
oc templates list --search Article
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

`article-template.json`:

```json
{
  "name": "Content__Article",
  "content": "{% style name:\"tenant-site\", src:\"~/styles/site.css?v=1\" %}<article class=\"article\"><h1>{{ Model.ContentItem.DisplayText }}</h1>{{ Model.Content.HtmlBodyPart | shape_render }}</article>",
  "description": "Article detail template."
}
```

```bash
oc templates create --body-file article-template.json
oc templates show Content__Article
oc templates update Content__Article --body-file article-template.json
oc templates delete Content__Article --yes
```

Create uses a stable case-insensitive name. An identical retry converges;
different content under the same name conflicts. Update is a complete
replacement and the route/body names must match exactly.

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
confirm the override with `oc content items render`.

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

Read `references/liquid-patterns.md` for content, summary, Flow/Bag, list, menu,
and resource patterns.

## Verify

```bash
oc content items render <id> --version draft --display-type Detail
oc content items render <id> --version published --display-type Summary
curl -fsS 'https://cms.example.com/tenant-a/article-path'
curl -fsSI 'https://cms.example.com/tenant-a/styles/site.css?v=1'
```

Check semantic HTML, encoded values, image alternative text, tenant-prefixed
URLs, missing shapes, stylesheet requests, and responsive behavior. Require:

- root route and representative content route return successful HTML;
- CSS and JavaScript URLs return the expected content type;
- every internal anchor target exists and keyboard focus reaches it;
- browser console has no errors;
- layout works at approximately 375px, 768px, and 1440px viewport widths.

Canonical references:
`src/docs/reference/api/templates/README.md`,
`src/docs/reference/modules/Templates/README.md`, and
`src/docs/reference/modules/Liquid/README.md`.
