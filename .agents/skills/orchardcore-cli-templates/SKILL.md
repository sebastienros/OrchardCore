---
name: orchardcore-cli-templates
description: Creates and manages Orchard Core custom Liquid templates through `oc`. Use for content, summary, widget, field, and layout shape overrides; rendering content models; registering CSS/media; and validating template output for remotely managed tenants.
---

# Orchard Core CLI Templates

Use the Templates module for tenant-stored Liquid shape overrides. Keep content
structure in definitions, content values in items, and presentation in Liquid.

Ensure `OrchardCore.Templates`, `OrchardCore.Liquid`, and
`OrchardCore.Resources` are enabled. Enable the part/field modules used by the
model, then refresh discovery.

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
URLs, missing shapes, stylesheet requests, and responsive behavior.

Canonical references:
`src/docs/reference/api/templates/README.md`,
`src/docs/reference/modules/Templates/README.md`, and
`src/docs/reference/modules/Liquid/README.md`.
