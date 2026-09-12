---
name: orchardcore-cli-settings
description: Reads and updates Orchard Core Site Settings, typed module sections, Custom Settings, cultures, and URL rewrite rules through `pomi`. Use for tenant-wide configuration, schema-safe partial settings updates, custom-settings content types, feature-contributed settings, and validating settings without overwriting protected or unknown values.
---

# Pomi CLI Settings

Before issuing commands, read the [shared context, authentication, and output rules](../orchardcore-cli/references/shared-rules.md).
For first-time access or login problems, follow [authentication and contexts](../orchardcore-cli/references/authentication.md).
These rules apply even when this specialist is selected directly.

Site Settings are one tenant document with a safe management projection. Custom
Settings are named content-type-backed sections embedded in that document.
Typed module sections have explicit providers and their own permissions; they are
not arbitrary site properties or Custom Settings content types.

## URL rewrite rules

Enable `OrchardCore.UrlRewriting` and use an application with
`ManageUrlRewritingRules` to manage `url-rewriting rules`. These are ordered
resources, separate from the site settings document.

```bash
pomi url-rewriting rules sources
pomi url-rewriting rules list --take 200
pomi url-rewriting rules show redirect-about
pomi url-rewriting rules validate --body-file rule.json
pomi url-rewriting rules create --body-file rule.json
```

Use the built-in `Rewrite` or `Redirect` source. The complete definition includes
`name`, `source`, `pattern` and `substitutionPattern`, plus the source's options.
Use a stable explicit `id` when automating creation: identical retries return the
stored rule; reusing an ID for different data returns 409. Without an ID, each
creation generates a new rule. Display names need not be unique.

Read `definition` from `show` before `update <id>`. Updates replace the complete
definition and preserve source/order. Omitted options reset to case-sensitive
matching, `Append`, `Found` (Redirect) and false `skipFurtherRules` (Rewrite).
Query and redirect enums use names. Null does not clear a required field. Extension
sources without an API definition remain opaque; do not send arbitrary metadata.

Use `move <id> --body '{"position":0}'` to move a rule to the beginning of the
complete list. Verify persisted order and actual HTTP behavior, including status,
capture substitution and query-string handling. Rewrites retain the target
endpoint's authorization. The configured admin prefix is excluded from matching.
Delete with `delete <id> --force`; missing deletes and unchanged updates are no-ops.
Use narrow test paths so a rule does not unintentionally intercept management URLs.

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

## Typed module settings sections

Discover the enabled sections available to this identity, then read the selected
section's values, ownership and actual update schema:

```bash
pomi settings sections list
pomi settings sections show https
pomi settings sections schema https
pomi settings sections update https --body-file https-settings.json
```

Send only the fields to change, without a `values` wrapper. Omission preserves
existing values; arrays replace the supplied field. Use null only when the live
section schema explicitly permits reset/clear. Do not copy redacted or read-only
properties into an update. `source`, `isReadOnly`, `readOnlyReason`,
`redactedProperties` and `readOnlyProperties` describe what this API can manage.
An omitted secret is not evidence that no secret is configured.

The `https` section requires `OrchardCore.Https` and `ManageHttps`, plus remote
management access. Select a working HTTPS context before changing it. The API
preserves the admin editor's refusal to make HTTPS changes over HTTP; it does not
configure host certificates, listeners or proxy trust. An explicit redirect port
must be 1–65535. For example, this changes HSTS mode and restores automatic port
detection while retaining the current redirection flags:

```json
{
  "strictTransportSecurityMode": "Disabled",
  "sslPort": null
}
```

Use `Enabled` to enable HSTS or `FromConfiguration` to follow the environment
(enabled in Production). `requireHttps` controls redirects;
`requireHttpsPermanent` selects 308 rather than 307. Read back the result and
verify actual HTTPS/HTTP responses. The update reports `changed` and
`reloadRequested`; an equivalent retry should report both false. A tenant reload
rebuilds its pipeline and does not restart the host process.

Treat missing/disabled sections as unavailable. Do not fall back to writing
arbitrary settings JSON or modifying configuration owned by the host. The core
`settings schema` provider list is discovery-only and does not grant write access
to a module section.

### Security headers

Discover `security-headers` using `settings sections list` after enabling
`OrchardCore.Security`. Read the schema and existing values before updating. The
application needs `ManageSecurityHeadersSettings`; the section may be read-only
when host configuration owns it. A read-only update returns 409.

Use `settings sections update security-headers --stdin` with optional
`contentSecurityPolicy`, `permissionsPolicy` and `referrerPolicy` properties.
Supplied policy maps replace the whole map; omission preserves it and `{}` clears
it. Null maps are invalid. For CSP, null removes a directive except `sandbox` and
`upgrade-insecure-requests`, where it enables the flag. The existing Permissions
Policy editor's `()` sentinel removes a directive; it does not explicitly deny
that permission in the emitted header. Inspect the schema's supported referrer
policy names. The module always emits `X-Content-Type-Options: nosniff`.

Confirm readback and real response headers after changes. The policy applies to
admin and API responses too. Successful unchanged retries do not reload the tenant.
The admin editor and API share validation; do not bypass it with a recipe to work
around an invalid value.

### CORS policies

The `cors` section requires `OrchardCore.Cors` and `ManageCorsSettings` in addition
to remote-management access. Read its current policies and schema before updating:

```bash
pomi settings sections show cors
pomi settings sections schema cors
pomi settings sections update cors --body-file cors.json
```

```json
{
  "policies": [
    {
      "name": "Frontend",
      "allowedOrigins": ["https://frontend.example.com"],
      "allowedMethods": ["GET"],
      "allowedHeaders": ["Authorization"],
      "isDefaultPolicy": true
    }
  ]
}
```

A supplied `policies` array replaces the complete collection; preserve other
policies that should remain. Omission preserves it, `[]` removes all tenant
policies, and null is invalid. Each supplied policy is complete: omitted flags
are false and lists empty. Names must be unique and at most one policy can be
default; otherwise the first policy is used. Use HTTP(S) origins without paths
or trailing slashes and HTTP tokens for methods/headers. Do not combine any
origin (including literal `*`) with credentials.

Read back the stored tenant policy fields and verify actual preflight/simple
response headers from the expected browser origin. A changed policy reloads the
tenant; an equivalent retry does not. This section does not enumerate host-added
CORS options. CORS does not grant authentication or API permissions.

## Layer zones

With `OrchardCore.Layers` enabled, discover `layer-zones` through `settings sections`.
Read the existing list before replacing it; preserve zones unless removal is intended.

```bash
pomi settings sections show layer-zones
pomi settings sections schema layer-zones
pomi settings sections update layer-zones --body-file zones.json
pomi layers widgets zones
```

Use `{"zones":["Content","Footer"]}` to replace the list. Omission preserves it;
`[]` clears it; null is invalid. Names follow the admin editor's space/comma splitting,
with order, case and duplicates preserved. Equivalent retries do not save or reload.
Changing available zones neither creates theme sections nor moves/deletes existing
widgets. Verify the theme provides the corresponding sections before placing widgets.
Requires `ManageLayers` and remote-management access.

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
[Layer zone settings](https://github.com/sebastienros/OrchardCore/blob/6b6a83808e1eb19800c99734869cd8474d0d695b/src/docs/reference/api/settings/README.md#layer-zones-section),
[localization API](https://github.com/sebastienros/OrchardCore/blob/4d4fc0fb66a5d789dff6d8057bbc074107918533/src/docs/reference/api/localization/README.md),
[settings API](https://github.com/sebastienros/OrchardCore/blob/5bb6c301c6fa9b795717d6a7906d7cb8626fe33c/src/docs/reference/api/settings/README.md),
[custom-settings API](https://github.com/sebastienros/OrchardCore/blob/4d4fc0fb66a5d789dff6d8057bbc074107918533/src/docs/reference/api/custom-settings/README.md),
[CORS module](https://github.com/sebastienros/OrchardCore/blob/4020c67d5b920d7a26ca175420683793b033fb08/src/docs/reference/modules/Cors/README.md),
[URL Rewriting module](https://github.com/sebastienros/OrchardCore/blob/92ac82a188ce0525e2b9072543451757d39f3032/src/docs/reference/modules/UrlRewriting/README.md),
[Security module](https://github.com/sebastienros/OrchardCore/blob/5bb6c301c6fa9b795717d6a7906d7cb8626fe33c/src/docs/reference/modules/Security/README.md),
[Settings module](https://github.com/sebastienros/OrchardCore/blob/4d4fc0fb66a5d789dff6d8057bbc074107918533/src/docs/reference/modules/Settings/README.md), and
[CustomSettings module](https://github.com/sebastienros/OrchardCore/blob/4d4fc0fb66a5d789dff6d8057bbc074107918533/src/docs/reference/modules/CustomSettings/README.md).
