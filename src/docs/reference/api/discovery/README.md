# Remote management discovery (`OrchardCore.RemoteManagement`)

## Purpose and feature

Remote management discovery gives a client the tenant-specific authentication coordinates, management manifest, OpenAPI URL, protocol versions, and enabled capabilities needed to invoke Orchard Core management APIs.

Enable **Remote Management** (`OrchardCore.RemoteManagement`). The feature depends on `OrchardCore.OpenApi` and the dependency-only `OrchardCore.OpenId.RemoteManagement` feature.

## Authentication and authorization

The bootstrap document is anonymous. The authenticated manifest requires:

```http
Authorization: Bearer ACCESS_TOKEN
```

The token is authenticated by the Orchard Core `Api` scheme and must grant `AccessRemoteManagement`. A missing or invalid token produces `401 Unauthorized` with a `WWW-Authenticate` challenge. An authenticated principal without the permission produces `403 Forbidden`.

The `ManageRemoteManagementConfiguration` permission controls the separate OpenID configuration UI; it does not authorize the manifest.

## Base route

Both routes are relative to the tenant base URL. For a path-based tenant:

```text
https://host/{tenant-prefix}/
```

The service builds absolute URLs from the effective request scheme, host, and `PathBase`. `authentication.authority` instead uses the configured OpenID issuer when one is set.

## Endpoint summary

| Method | Path | Authentication | Purpose |
| --- | --- | --- | --- |
| `GET` | `/.well-known/orchardcore-management` | Anonymous | Bootstrap authentication and manifest coordinates |
| `GET` | `/api/management/manifest` | `Api` bearer + `AccessRemoteManagement` | Full tenant manifest and capabilities |

## Manifest schema

JSON property names are camel-cased.

| Property | Type | Bootstrap | Authenticated manifest | Meaning |
| --- | --- | --- | --- | --- |
| `protocolMajorVersion` | integer | `1` | `1` | Management protocol major version. |
| `protocolMinorVersion` | integer | `0` | `0` | Management protocol minor version. |
| `productVersion` | string or null | null | Set | Orchard Core build version. |
| `tenantId` | string or null | null | Set | Current `ShellSettings.Name`. |
| `authentication` | object | Set | Set | OAuth/OpenID client coordinates. |
| `managementManifestUrl` | absolute URI | Set | Set | Authenticated manifest URL. |
| `openApiUrl` | absolute URI or null | null | Set | `/openapi/v1.json` below the tenant base. |
| `openApiETag` | string or null | null | Currently null | Reserved OpenAPI entity tag coordinate. |
| `capabilities` | array | Empty | Set | Enabled capability descriptors. |
| `jsonSchemaDialect` | absolute URI or null | null | `https://json-schema.org/draft/2020-12/schema` | Dialect used by resource schemas. |
| `minimumCliVersion` | string or null | null | `1.0.0` | Oldest supported CLI version. |
| `recommendedCliVersion` | string or null | null | `1.0.0` | Recommended CLI version. |
| `documentationIndexUrl` | absolute URI or null | null | `https://docs.orchardcore.net/en/latest/search/search_index.json` | Documentation search index. |

The HTTP JSON serializer may omit null properties when an application customizes `Microsoft.AspNetCore.Http.Json.JsonOptions`; clients must accept both an absent property and JSON `null` for optional coordinates.

### Authentication object

| Property | Type | Meaning |
| --- | --- | --- |
| `authority` | absolute URI | Configured OpenID issuer, otherwise the request-derived tenant base URL. |
| `clientId` | string | Always `orchardcore-cli` for the default public native client. |
| `grantTypes` | array of string | Enabled remote-use grants, selected only from `authorization_code`, `urn:ietf:params:oauth:grant-type:device_code`, and `client_credentials`. |
| `scopes` | array of string | `openid`, `profile`, `roles`, optionally `offline_access` when refresh tokens are enabled, and always `orchardcore.management`. |

`refresh_token` is not itself advertised in `grantTypes`; its availability is represented by `offline_access` in `scopes`.

### Capability object

| Property | Type | Required | Meaning |
| --- | --- | --- | --- |
| `id` | string | Yes | Stable capability identifier. |
| `version` | string | Yes | Capability version. |
| `displayName` | string or null | No | Human-readable name. |

The Remote Management feature always contributes:

```json
{
  "id": "remote-management",
  "version": "1.0",
  "displayName": "Remote Management"
}
```

Enabled modules contribute further values. The API groups covered alongside this page advertise:

```json
[
  {
    "id": "media",
    "version": "1.0",
    "displayName": "Media"
  },
  {
    "id": "queries",
    "version": "1.0",
    "displayName": "Queries"
  }
]
```

Capability order follows registered provider enumeration and is not a compatibility contract.

## Operations

### Get the anonymous bootstrap document

```http
GET /.well-known/orchardcore-management
```

There are no path, query, header, or body parameters.

```bash
curl 'https://cms.example.com/tenant-a/.well-known/orchardcore-management'
```

`200 OK`:

```json
{
  "protocolMajorVersion": 1,
  "protocolMinorVersion": 0,
  "productVersion": null,
  "tenantId": null,
  "authentication": {
    "authority": "https://cms.example.com/tenant-a/",
    "clientId": "orchardcore-cli",
    "grantTypes": [
      "authorization_code",
      "urn:ietf:params:oauth:grant-type:device_code",
      "client_credentials"
    ],
    "scopes": [
      "openid",
      "profile",
      "roles",
      "offline_access",
      "orchardcore.management"
    ]
  },
  "managementManifestUrl": "https://cms.example.com/tenant-a/api/management/manifest",
  "openApiUrl": null,
  "openApiETag": null,
  "capabilities": [],
  "jsonSchemaDialect": null,
  "minimumCliVersion": null,
  "recommendedCliVersion": null,
  "documentationIndexUrl": null
}
```

Only grant types enabled in the OpenIddict server options are included. For example, a tenant without client credentials omits `client_credentials`; a tenant without refresh tokens omits `offline_access`. A custom OpenID issuer changes only `authentication.authority`, not request-derived management URLs.

The bootstrap endpoint is excluded from OpenAPI because it is the pre-authentication entry point used to locate the authenticated document.

### Get the authenticated management manifest

```http
GET /api/management/manifest
Authorization: Bearer ACCESS_TOKEN
```

There are no path, query, or body parameters.

```bash
curl -H 'Authorization: Bearer ACCESS_TOKEN' \
  'https://cms.example.com/tenant-a/api/management/manifest'
```

`200 OK`:

```json
{
  "protocolMajorVersion": 1,
  "protocolMinorVersion": 0,
  "productVersion": "4.0.0",
  "tenantId": "TenantA",
  "authentication": {
    "authority": "https://cms.example.com/tenant-a/",
    "clientId": "orchardcore-cli",
    "grantTypes": [
      "authorization_code",
      "urn:ietf:params:oauth:grant-type:device_code",
      "client_credentials"
    ],
    "scopes": [
      "openid",
      "profile",
      "roles",
      "offline_access",
      "orchardcore.management"
    ]
  },
  "managementManifestUrl": "https://cms.example.com/tenant-a/api/management/manifest",
  "openApiUrl": "https://cms.example.com/tenant-a/openapi/v1.json",
  "openApiETag": null,
  "capabilities": [
    {
      "id": "remote-management",
      "version": "1.0",
      "displayName": "Remote Management"
    },
    {
      "id": "media",
      "version": "1.0",
      "displayName": "Media"
    },
    {
      "id": "queries",
      "version": "1.0",
      "displayName": "Queries"
    }
  ],
  "jsonSchemaDialect": "https://json-schema.org/draft/2020-12/schema",
  "minimumCliVersion": "1.0.0",
  "recommendedCliVersion": "1.0.0",
  "documentationIndexUrl": "https://docs.orchardcore.net/en/latest/search/search_index.json"
}
```

`productVersion`, `tenantId`, and capability membership are server-specific; the values above are representative. Other responses are `401` and `403`.

Representative `403`:

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.4",
  "title": "Forbidden",
  "status": 403,
  "detail": "You do not have sufficient permissions to complete this request"
}
```

## Compatibility

Compatibility is body-based; these endpoints define no custom compatibility request or response headers.

- A client is protocol-compatible when `protocolMajorVersion` equals its supported major version.
- A newer server minor version under the same major version is compatible but can warrant a warning.
- `minimumCliVersion` is the hard client floor.
- `recommendedCliVersion` is advisory.
- Capability versions are evaluated independently. Built-in capability major versions currently align with protocol major `1`.

The Orchard Core CLI fetches bootstrap first, then the authenticated manifest, and refuses a different protocol major or a CLI version below `minimumCliVersion`.

`openApiETag` exists in the schema so a client can seed conditional OpenAPI retrieval, but the current manifest service does not populate it. The manifest endpoints themselves do not set an ETag or implement `If-None-Match`. When a fetched resource supplies an ordinary HTTP `ETag`, the CLI cache can send `If-None-Match`; that is standard HTTP caching, not a protocol compatibility header.

## Errors

| Status | Endpoint | Meaning |
| --- | --- | --- |
| `200 OK` | Both | Document returned. |
| `401 Unauthorized` | Authenticated manifest | No valid `Api` bearer identity; includes an authentication challenge. |
| `403 Forbidden` | Authenticated manifest | Authenticated identity lacks `AccessRemoteManagement`. |

No request validation errors are defined because neither operation accepts input parameters.

## Endpoint coverage and sources

This page covers all **2 Remote Management discovery route/method operations**.

Source-derived from:

- `OrchardCore.RemoteManagement/Startup.cs`, `Manifest.cs`, `RemoteManagementManifestService.cs`, `DefaultCapabilityProvider.cs`, and `Permissions.cs`
- `OrchardCore.RemoteManagement.Abstractions/RemoteManagementManifest.cs`, authentication/capability models, constants, and permissions
- Media and Queries capability providers
- `OrchardCore.Cli/CliApplication.cs`, `CliUtilities.cs`, `CompatibilityService.cs`, and `CacheService.cs`
- `RemoteManagementManifestServiceTests.cs`

!!! note
    `RemoteManagementManifest.TenantId` says the property is omitted from anonymous responses, while the current endpoint returns the model through the default minimal-API JSON result and the property has no per-property ignore attribute. Consequently, default ASP.NET Core JSON options serialize it as `null`; applications can globally omit nulls. Consumers should accept either form.
