# Site settings management API (`OrchardCore.Settings`)

The site settings management API reads and updates the safe core properties of the current
tenant's site settings document. The **Settings** feature (`OrchardCore.Settings`) is always
enabled and advertises the Remote Management capability `settings`.

## Authentication and permissions

Every endpoint uses Orchard Core's `Api` authentication scheme, requires
`AccessRemoteManagement`, requires `ManageSettings`, and disables antiforgery validation.
Neither the remote-management permission nor a valid bearer token grants settings access by
itself.

The API never returns or accepts the site salt, super-user identity, home route, document
identity, or arbitrary site settings properties.

## Base route

`/api/settings`

## Endpoints

| Method | Path | Description | Permission |
| --- | --- | --- | --- |
| `GET` | `/api/settings` | Get safe core site settings | `ManageSettings` |
| `PUT` | `/api/settings` | Merge safe core site settings | `ManageSettings` |
| `GET` | `/api/settings/schema` | Get the core update schema and explicitly contributed discovery schemas | `ManageSettings` |

## Site settings representation

Responses use lower-camel property names:

| Property | Type | Writable | Description |
| --- | --- | --- | --- |
| `siteName` | string or null | Yes | Site display name |
| `pageTitleFormat` | string or null | Yes | Liquid page-title format |
| `baseUrl` | string, empty string, or null | Yes | Fully qualified public base URL |
| `timeZoneId` | string or null | Yes | Site time-zone identifier |
| `pageSize` | integer | Yes | Default page size; minimum `1` |
| `maxPageSize` | integer | Yes | Maximum page size; `0` means unlimited |
| `maxPagedCount` | integer | Yes | Maximum paged item count; `0` means unlimited |
| `calendar` | string or null | Yes | Calendar identifier |
| `resourceDebugMode` | string | Yes | `FromConfiguration`, `Enabled`, or `Disabled` |
| `useCdn` | boolean | Yes | Whether resources use the configured CDN |
| `cdnBaseUrl` | string or null | Yes | CDN base URL |
| `appendVersion` | boolean | Yes | Whether resource URLs include a version query string |
| `cacheMode` | string | Yes | `FromConfiguration`, `Enabled`, `DebugEnabled`, or `Disabled` |

Unknown properties are rejected. In particular, `siteSalt`, `superUser`, `homeRoute`,
`properties`, and document identity or version fields are not part of this contract.

## Get site settings

`GET /api/settings`

This operation has no request body or query parameters.

```bash
oc settings show
```

`200 OK` returns the current safe core values:

```json
{
  "siteName": "Acme",
  "pageTitleFormat": "{% page_title Site.SiteName, position: \"after\", separator: \" - \" %}",
  "baseUrl": "https://cms.example.com",
  "timeZoneId": "Etc/UTC",
  "pageSize": 10,
  "maxPageSize": 100,
  "maxPagedCount": 0,
  "calendar": null,
  "resourceDebugMode": "FromConfiguration",
  "useCdn": false,
  "cdnBaseUrl": null,
  "appendVersion": true,
  "cacheMode": "FromConfiguration"
}
```

## Update site settings

`PUT /api/settings`

The request body is required and must be a JSON object. Every property is optional. An omitted
property preserves its current value; an explicit `null` is applied only to nullable string
properties. Unknown properties and `null` for value types are rejected during JSON binding.

```bash
oc settings update --json '{"siteName":"Acme","pageSize":25}'
```

Equivalent HTTP request:

```http
PUT /api/settings HTTP/1.1
Authorization: ******
Content-Type: application/json

{
  "siteName": "Acme",
  "pageSize": 25
}
```

The complete merged state is validated before the mutable site settings document is changed.
`pageSize` must be at least `1` and must not exceed a positive `maxPageSize`.
`maxPageSize` and `maxPagedCount` must be nonnegative. A nonempty `baseUrl` must be an absolute
URI. Enum values must use one of the names listed in the representation table; matching is
case-insensitive.

`200 OK` returns the complete safe representation after the merge. Repeating an identical
request also returns `200`; when the requested values already match, the document is not written
and no tenant reload is requested. A successful changed update writes the single site settings
document through `ISiteService`, so the operation has no partial per-property persistence.

Validation failures return `400 Bad Request` validation Problem Details and do not persist or
request a tenant reload:

```json
{
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": {
    "pageSize": [
      "The page size must be greater than zero."
    ]
  }
}
```

## Get the settings schema

`GET /api/settings/schema`

```bash
oc settings schema
```

`200 OK` returns:

```json
{
  "core": {
    "$schema": "https://json-schema.org/draft/2020-12/schema",
    "title": "Site settings update",
    "type": "object",
    "additionalProperties": false,
    "properties": {}
  },
  "sections": []
}
```

`core` is the authoritative schema for `PUT /api/settings`. It includes descriptions, defaults,
enum values, URI format, and numeric constraints.

Enabled features can implement `ISiteSettingsManagementSchemaProvider` to contribute explicit
schemas to `sections`. Providers receive the authenticated principal and must return only schemas
that principal may discover. These entries are marked `readable: false` and `writable: false`
because the core endpoint does not expose their values or update them. The API never infers a
schema by reflecting over, enumerating, or serializing the site settings `Properties` bag.

## Errors

| Status | Meaning |
| --- | --- |
| `400 Bad Request` | Missing or malformed JSON, unknown property, invalid null, or validation failure |
| `401 Unauthorized` | Missing or invalid `Api` bearer credentials |
| `403 Forbidden` | Missing `AccessRemoteManagement` or `ManageSettings` |

## Endpoint coverage and sources

This page covers all three endpoints mapped by
`SiteSettingsManagementEndpoints`. The contract is implemented by
`SiteSettingsManagementService`, `SiteSettingsManagementModels`, and `SiteSettingsValidator`,
and is covered by `SiteSettingsManagementTests`.
