---
name: orchardcore-cli-content-definitions
description: Designs and manages Orchard Core content types, reusable parts, fields, and modeling patterns through `oc`. Use for Content Types, Flow, Bag, Lists, Taxonomies, Menus, Alias, Autoroute, Sitemaps, widgets, or planning carousel, blog, news, and landing-page models rendered with Liquid.
---

# Orchard Core CLI Content Definitions

Design the model before authoring content or templates. Work against the target
tenant context because registered parts, fields, settings, and schemas depend on
its enabled features.

For a composable content site, inspect and enable the required subset of:
`OrchardCore.Contents`, `OrchardCore.ContentTypes`,
`OrchardCore.ContentFields`, `OrchardCore.Title`,
`OrchardCore.Autoroute`, `OrchardCore.Alias`, `OrchardCore.Flows`,
`OrchardCore.Lists`, `OrchardCore.Widgets`, `OrchardCore.Taxonomies`,
`OrchardCore.Menu`, and `OrchardCore.Sitemaps`. Refresh discovery afterward.

## Schema-first workflow

```bash
oc --context site content part-types list --take 200
oc --context site content field-types list --take 200
oc --context site content types schema
oc --context site content parts schema
oc --context site content fields schema
```

Enable missing features, run `oc api refresh`, and repeat discovery. Never copy
settings blindly from another tenant.

Create definitions in dependency order:

1. Create reusable custom parts and their fields.
2. Create widget/content types consumed by Flow, Bag, List, Menu, or Taxonomy.
3. Create aggregate/root content types and attach parts.
4. Read every stored definition back before authoring content.

## Commands

```bash
oc content types list --skip 0 --take 200
oc content types show Article
oc content types create --body-file article-type.json
oc content types update Article --body-file article-type.json
oc content types delete Article --yes

oc content parts list --skip 0 --take 200
oc content parts show ArticleDetails
oc content parts create --body-file article-details.json
oc content parts update ArticleDetails --body-file article-details.json
oc content parts delete ArticleDetails --yes

oc content fields list ArticleDetails
oc content fields show ArticleDetails Summary
oc content fields create ArticleDetails --body-file summary-field.json
oc content fields update ArticleDetails Summary --body-file summary-field.json
oc content fields delete ArticleDetails Summary --yes
```

Create/update bodies are full definitions, not patches. Keep technical names
stable and retry the identical request after ambiguous failures.

## Base definition shapes

Custom reusable part:

```json
{
  "name": "ArticleDetails",
  "settings": {
    "ContentPartSettings": {
      "attachable": true,
      "reusable": true,
      "displayName": "Article details"
    }
  },
  "fields": [
    {
      "name": "Summary",
      "fieldName": "TextField",
      "settings": {
        "ContentPartFieldSettings": {
          "displayName": "Summary",
          "position": "0"
        }
      }
    }
  ]
}
```

Content type:

```json
{
  "name": "Article",
  "displayName": "Article",
  "settings": {
    "ContentTypeSettings": {
      "creatable": true,
      "listable": true,
      "draftable": true,
      "versionable": true,
      "stereotype": "Content"
    }
  },
  "parts": [
    { "name": "TitlePart", "partName": "TitlePart", "settings": {} },
    { "name": "AutoroutePart", "partName": "AutoroutePart", "settings": {} },
    { "name": "ArticleDetails", "partName": "ArticleDetails", "settings": {} }
  ]
}
```

Validate these examples against the live schema. Field-specific settings such
as required, length, editor, multiple-selection, and content-type restrictions
are feature-contributed and tenant-specific.

## Modeling decisions

- Use **TitlePart** for editor-facing and display titles.
- Use **AutoroutePart** for public URLs; use **AliasPart** for stable lookup keys.
- Use **FlowPart** for ordered heterogeneous page sections editable as widgets.
- Use **BagPart** for ordered embedded items constrained to specific content
  types; use it for structured repeated components.
- Use **ListPart** plus **ContainedPart** for independently stored children with
  their own lifecycle and routes.
- Use **TaxonomyField** to classify content with reusable hierarchical terms.
- Use **ContentPickerField** for explicit editorial relationships.
- Use **MenuPart** and menu-item stereotypes for navigation, not as a generic
  page-section container.
- Use sitemap source/content-type settings to publish public routable content;
  do not treat a sitemap as the primary content hierarchy.
- Prefer small reusable parts over one content type containing many unrelated
  fields.
- Put layout composition in Liquid templates, not in stored HTML fields.

Every type placed in Flow must use the exact Widget stereotype:

```json
{
  "name": "Hero",
  "displayName": "Hero",
  "settings": {
    "ContentTypeSettings": {
      "stereotype": "Widget",
      "creatable": false,
      "listable": false
    }
  },
  "parts": []
}
```

Apply this to `Hero`, `FeatureGrid`, `FeatureCard`, `Carousel`, and
`CarouselSlide` when they are embedded widgets.

The remote content API has no dedicated "add to list" operation. Include a
`ContainedPart` object in each child payload with the parent
`ListContentItemId`, `ListContentType`, and order. If the live authoring path
cannot weld this registered part, provision the relationship through a recipe
or the admin list editor.

Flow and named Bag settings live under the attachment's settings object:

```json
{
  "name": "FlowPart",
  "partName": "FlowPart",
  "settings": {
    "FlowPartSettings": {
      "ContainedContentTypes": ["Hero", "Carousel", "FeatureGrid"]
    }
  }
}
```

```json
{
  "name": "Slides",
  "partName": "BagPart",
  "settings": {
    "BagPartSettings": {
      "ContainedContentTypes": ["CarouselSlide"],
      "DisplayType": "Detail"
    }
  }
}
```

Confirm exact JSON casing against the stored definition and live schema because
definition settings are extensible.

Read `references/modeling-patterns.md` for concrete carousel, blog/news, landing
page, taxonomy, menu, and Liquid-shape designs.

## Verify the model

```bash
oc content parts show ArticleDetails
oc content types show Article
oc content items schema Article
```

The content-item schema is the authoritative authoring contract and includes
the attached CLR-backed part and field descriptions. Confirm that privileged or
transient editor commands are absent.

Remote management currently has no dedicated sitemap document/source API.
`SitemapPart` can model per-item inclusion settings, but creating a sitemap and
its content-type source requires a recipe, deployment plan, admin UI, or a
future sitemap management API.

Canonical references:
`src/docs/reference/api/content-definitions/README.md` and the module pages for
Content Types, Flows, Lists, Taxonomies, Menu, Alias, and Sitemaps.
