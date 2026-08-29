---
name: orchardcore-cli-content-items
description: Authors and manages Orchard Core content items through `oc`. Use for schema-driven JSON creation, drafts, updates, validation, publishing, unpublishing, rendering, deletion, ownership, and organizing tenant content by type, route, alias, taxonomy, and containment.
---

# Orchard Core CLI Content Items

Author against the live content-type schema. Content item JSON uses Pascal-cased
well-known properties and type-specific part/field members.

## Authoring workflow

```bash
oc --context site content items schema Article > article.schema.json
oc --context site content items validate --body-file article.json
oc --context site content items create-draft --body-file article.json
```

Read the returned `ContentItemId`, inspect the draft, render it, then publish:

```bash
oc content items show <id> --version draft
oc content items render <id> --version draft --display-type Detail
oc content items publish <id>
```

Do not derive payloads from examples alone. Regenerate the schema after changing
definitions or enabled features.

## Minimal payload pattern

```json
{
  "ContentType": "Article",
  "DisplayText": "CLI-managed article",
  "TitlePart": {
    "Title": "CLI-managed article"
  },
  "AutoroutePart": {
    "Path": "news/cli-managed-article"
  },
  "ArticleDetails": {
    "Summary": {
      "Text": "A concise summary."
    }
  }
}
```

Use the actual part attachment names from `oc content types show Article`, not
only CLR type names. Use the schema descriptions to select the correct field
value property, such as `Text`, `Html`, `Markdown`, `Paths`, or referenced IDs.

## Lifecycle commands

```bash
oc content items list --content-type Article --status published --skip 0 --take 50
oc content items show <id> --version latest
oc content items save --body-file item.json
oc content items create-draft --body-file item.json
oc content items update <id> --body-file item.json
oc content items update-draft <id> --body-file item.json
oc content items draft <id>
oc content items publish <id>
oc content items unpublish <id>
oc content items validate --body-file item.json
oc content items validate-update <id> --body-file item.json
oc content items render <id> --version draft --display-type Detail
oc content items delete <id> --yes
```

Run `oc content items --help` for installation-specific filters and options.

## State and identity rules

- Omit generated version IDs, state flags, and timestamps from new payloads.
- Keep `ContentType` on creates; it cannot be changed by update.
- Treat `Author` as server-controlled.
- Omit `Owner` to use or preserve the authenticated user. Changing it requires
  `EditContentOwner`.
- Preserve returned unknown part/field data when performing a full update.
- Use a stable client-supplied `ContentItemId` only when intentionally relying
  on retry-safe creation and the live schema/endpoint permits it.
- Prefer `create-draft` followed by validation/render/publish for editorial
  workflows.

## Organize content

- Use Autoroute paths for public information architecture.
- Use aliases for stable machine lookup independent of route changes.
- Store taxonomy term content item IDs in taxonomy fields.
- For List children, include `ContainedPart.ListContentItemId`,
  `ContainedPart.ListContentType`, and `ContainedPart.Order` in the child
  payload; there is no dedicated remote list-membership command.
- Keep embedded Flow/Bag widgets inside the parent payload.
- Reference uploaded media by the exact media path returned by
  `oc media files upload`.
- Use `ContentPickerField` IDs for explicit related-content links.

For bulk authoring, keep one JSON file per logical item, capture returned IDs in
an external deployment manifest, and order operations so referenced taxonomy,
media, menu, or parent items exist first.

## Verify

```bash
oc content items show <id> --version latest
oc content items render <id> --version latest --display-type Detail
oc content items list --content-type Article --status published
```

Inspect public URLs separately; a successful API save does not prove template,
media, navigation, or CSS correctness.

Canonical reference: `src/docs/reference/api/content-items/README.md`.
