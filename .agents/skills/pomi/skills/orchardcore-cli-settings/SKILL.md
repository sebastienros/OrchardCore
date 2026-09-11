---
name: orchardcore-cli-settings
description: Reads and updates Orchard Core Site Settings, Custom Settings, and cultures through `pomi`. Use for tenant-wide configuration, schema-safe partial settings updates, custom-settings content types, feature-contributed settings, and validating settings without overwriting protected or unknown values.
---

# Pomi CLI Settings

Before issuing commands, read the [shared context, authentication, and output rules](../orchardcore-cli/references/shared-rules.md).
For first-time access or login problems, follow [authentication and contexts](../orchardcore-cli/references/authentication.md).
These rules apply even when this specialist is selected directly.

Site Settings are one tenant document with a safe management projection. Custom
Settings are named content-type-backed sections embedded in that document.

## Site Settings

```bash
pomi settings show
pomi settings schema
pomi settings update --body-file settings.json
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
pomi settings show > current-settings.json
pomi settings update --body-file desired-settings.json
pomi settings show
```

## Custom Settings

Discover only sections the current identity is authorized to manage:

```bash
pomi custom-settings list --skip 0 --take 200
pomi custom-settings show BlogSettings
pomi custom-settings schema BlogSettings
pomi custom-settings update BlogSettings --body-file blog-settings.json
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

## Localization

Discover `pomi localization --help` after enabling `OrchardCore.Localization`
and refreshing metadata. It exposes culture and settings management only.

```bash
pomi localization cultures list
pomi localization cultures available --take 200
pomi localization cultures add fr
pomi localization cultures remove de --force
pomi localization settings show --output json
pomi localization settings schema --operation update
pomi localization settings update --body-file cultures.json
```

`cultures list` returns enabled cultures; `cultures available` discovers names
that can be enabled (page with `--skip` and `--take`). Prefer `cultures add`
and `cultures remove` for individual changes; adding is idempotent. Removing
the default requires selecting a different default in settings first.

Culture settings **replace** the supported-culture list. Preserve existing
cultures unless removal is requested; the default must remain in the list.
Use names returned by culture discovery, not time zone identifiers. These
operations require `ManageCultures` and remote-management access.

There are no generic string, PO catalog, or database translation commands.
For Media UI labels, use `pomi media localizations show`. This reads the
server-resolved labels, not PO catalog entries. For translation editing, use
the Data Localization admin UI or its documented HTTP API; enabling that feature
does not add translation commands to Pomi.

## Safety

- Operate on one explicit context at a time.
- Do not submit secrets unless the owning module explicitly exposes a secure
  writable contract.
- Preserve settings not represented by the safe schema.
- Treat nested JSON type failures as validation errors; correct the exact path.
- Read back after updates and verify user-facing behavior separately.

Versioned references (live tenant schemas take precedence):
[localization API](https://github.com/sebastienros/OrchardCore/blob/4d4fc0fb66a5d789dff6d8057bbc074107918533/src/docs/reference/api/localization/README.md),
[settings API](https://github.com/sebastienros/OrchardCore/blob/4d4fc0fb66a5d789dff6d8057bbc074107918533/src/docs/reference/api/settings/README.md),
[custom-settings API](https://github.com/sebastienros/OrchardCore/blob/4d4fc0fb66a5d789dff6d8057bbc074107918533/src/docs/reference/api/custom-settings/README.md),
[Settings module](https://github.com/sebastienros/OrchardCore/blob/4d4fc0fb66a5d789dff6d8057bbc074107918533/src/docs/reference/modules/Settings/README.md), and
[CustomSettings module](https://github.com/sebastienros/OrchardCore/blob/4d4fc0fb66a5d789dff6d8057bbc074107918533/src/docs/reference/modules/CustomSettings/README.md).
