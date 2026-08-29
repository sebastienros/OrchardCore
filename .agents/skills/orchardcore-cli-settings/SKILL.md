---
name: orchardcore-cli-settings
description: Reads and updates Orchard Core Site Settings and Custom Settings through `oc`. Use for tenant-wide configuration, schema-safe partial settings updates, custom-settings content types, feature-contributed settings, and validating settings without overwriting protected or unknown values.
---

# Orchard Core CLI Settings

Site Settings are one tenant document with a safe management projection. Custom
Settings are named content-type-backed sections embedded in that document.

## Site Settings

```bash
oc settings show
oc settings schema
oc settings update --body-file settings.json
```

Example:

```json
{
  "siteName": "Contoso News",
  "pageSize": 25,
  "timeZoneId": "America/Los_Angeles"
}
```

Use `--body`, `--body-file`, or `--stdin`; the current CLI does not define a
`--json` option. Read the live schema because enabled features contribute
settings and the API intentionally omits unsafe server internals.

Read the current representation first, change only documented writable
properties, update, then read it back:

```bash
oc settings show > current-settings.json
oc settings update --body-file desired-settings.json
oc settings show
```

## Custom Settings

Discover only sections the current identity is authorized to manage:

```bash
oc custom-settings list --skip 0 --take 200
oc custom-settings show BlogSettings
oc custom-settings schema BlogSettings
oc custom-settings update BlogSettings --body-file blog-settings.json
```

Construct the payload from the named section's schema. Unknown and unauthorized
names both return `404`; do not use the distinction to infer hidden resources.

## Design custom settings

1. Define a content type with
   `ContentTypeSettings.stereotype` set exactly to `CustomSettings`.
2. Attach focused reusable parts/fields.
3. Confirm the section appears in `custom-settings list`.
4. Read its dynamic schema.
5. Update the entire named section with valid nested values.

Use Custom Settings for tenant-wide editorial configuration such as branding
media, contact information, social links, feature flags, feed limits, or footer
content. Do not use them for collections of independently managed content.

## Safety

- Operate on one explicit context at a time.
- Do not submit secrets unless the owning module explicitly exposes a secure
  writable contract.
- Preserve settings not represented by the safe schema.
- Treat nested JSON type failures as validation errors; correct the exact path.
- Read back after updates and verify user-facing behavior separately.

Canonical references:
`src/docs/reference/api/settings/README.md`,
`src/docs/reference/api/custom-settings/README.md`,
`src/docs/reference/modules/Settings/README.md`, and
`src/docs/reference/modules/CustomSettings/README.md`.
