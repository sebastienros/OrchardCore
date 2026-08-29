# Static-file management API

The static-file management API lists, inspects, and uploads tenant-owned web
assets. Files are stored beneath the current tenant's `App_Data` static-file
root and are also exposed by the tenant's public static-file middleware.

Enable the **Static File Provider** feature
(`OrchardCore.Tenants.FileProvider`) on each tenant that needs these
operations. The feature advertises remote management capability
`static-files`, version `1.0`. Remote Management must also be enabled and
configured on that tenant before a client can authenticate.

## Authentication and authorization

All three management operations use the Orchard Core API authentication scheme
and require:

- an authenticated bearer token; and
- the **Access remote management API** permission
  (`AccessRemoteManagement`).

List and show additionally require **View tenant static files**
(`ViewTenantStaticFiles`). Upload and overwrite require the security-critical
**Manage tenant static files** (`ManageTenantStaticFiles`) permission, which
implies the view permission. Obtain a token and configure authentication as described in
[Remote Management](../../modules/RemoteManagement/README.md#contexts-and-login).
Antiforgery validation is disabled.

```http
Authorization: Bearer <access-token>
```

The management request operates on the tenant selected by its base URL and
token. Unlike tenant management, it is not restricted to the `Default` tenant.

## Base route

Routes are relative to the URL of the tenant being managed:

```text
/api/static-files
```

JSON property names use the exact camel casing shown below. JSON responses use
`application/json`; Problem Details responses use
`application/problem+json`.

## Endpoint summary

| Operation | Method | Path | Success |
| --- | --- | --- | --- |
| List directory | `GET` | `/api/static-files` | `200 OK` |
| Get file metadata | `GET` | `/api/static-files/file` | `200 OK` |
| Upload file content | `PUT` | `/api/static-files/content` | `200 OK` or `201 Created` |

## Paths, storage, and public content

The `path` query parameter is always relative to the tenant static-file root:

- an empty path is accepted only by the list operation and means the root;
- `/styles/site.css` and other rooted paths are rejected;
- leading and trailing `/` characters are trimmed only after the rooted-path
  check;
- `.` and `..` segments and empty interior segments such as in
  `styles//site.css` are rejected;
- `\` is normalized to `/`; and
- path matching follows the host file system's behavior.

For uploads, the resolved path must remain below the static-file root. Existing
symbolic links in any parent directory and an existing symbolic-link file at
the destination are rejected.

Uploaded bytes are copied unchanged from the request body. The endpoint does
not impose a file-extension allow list or an application-level size limit, and
an empty body creates a zero-byte file. Parent directories are created
automatically.

The returned `url` and upload `Location` identify the public static-file route,
not the management route. Every segment is percent-encoded and the tenant's
request path base is prepended. For example, `styles/site.css` is
`/tenant-a/styles/site.css` when the tenant path base is `/tenant-a`.

The public static-file middleware:

- uses its normal content-type mappings and falls back to
  `application/octet-stream` for unknown file types;
- serves unknown file types; and
- sets
  `Cache-Control: public, max-age=2592000, s-max-age=31557600` on public file
  responses (30 days for clients and 365.25 days for shared caches).

These public URLs are served by static-file middleware; the bearer-token
requirements above apply to the management endpoints, not to public file
delivery.

## Static-file representation

| JSON property | Type | Nullable | Meaning |
| --- | --- | --- | --- |
| `path` | string | No | Normalized path relative to the static-file root. |
| `name` | string | No | Final file or directory name. |
| `isDirectory` | boolean | No | Whether the item is a directory. |
| `length` | integer (64-bit) | Yes | File length in bytes; `null` for a directory. |
| `lastModified` | string (`date-time`) | No | Last-modified timestamp with offset. |
| `url` | string | Yes | Public URL for a file; `null` for a directory. |

File example:

```json
{
  "path": "styles/site.css",
  "name": "site.css",
  "isDirectory": false,
  "length": 1842,
  "lastModified": "2026-08-29T01:25:26.949+00:00",
  "url": "/tenant-a/styles/site.css"
}
```

Directory example:

```json
{
  "path": "styles",
  "name": "styles",
  "isDirectory": true,
  "length": null,
  "lastModified": "2026-08-29T01:20:00+00:00",
  "url": null
}
```

## List a directory

```http
GET /api/static-files
```

Lists only the immediate children of one directory. Directories sort before
files; each group is ordered by name case-insensitively. Paging is applied
after sorting.

### Parameters

| Location | Name | Type | Required | Default | Constraints |
| --- | --- | --- | --- | --- | --- |
| Header | `Authorization` | string | Yes | — | `Bearer <access-token>`. |
| Query | `path` | string | No | Empty | Relative directory path. Empty selects the root. |
| Query | `skip` | integer | No | `0` | Zero or greater. |
| Query | `take` | integer | No | `50` | From `1` through `200`. |

There is no request body. Query parameter binding is case-insensitive.

### Request

```bash
curl --get 'https://cms.example.com/tenant-a/api/static-files' \
  --header 'Authorization: Bearer <access-token>' \
  --data-urlencode 'path=styles' \
  --data-urlencode 'skip=0' \
  --data-urlencode 'take=50'
```

### Responses

- `200 OK` returns the paging envelope.
- `400 Bad Request` is returned for an unsafe path, invalid paging, or an
  unparseable query value.
- `401 Unauthorized` is returned when bearer authentication is absent or
  fails.
- `403 Forbidden` is returned when the token lacks **Access remote management
  API** or **View tenant static files**.
- `404 Not Found` is returned when the directory does not exist.

```json
{
  "skip": 0,
  "take": 50,
  "totalCount": 2,
  "items": [
    {
      "path": "styles/vendor",
      "name": "vendor",
      "isDirectory": true,
      "length": null,
      "lastModified": "2026-08-29T01:20:00+00:00",
      "url": null
    },
    {
      "path": "styles/site.css",
      "name": "site.css",
      "isDirectory": false,
      "length": 1842,
      "lastModified": "2026-08-29T01:25:26.949+00:00",
      "url": "/tenant-a/styles/site.css"
    }
  ]
}
```

`totalCount` is the number of immediate children before paging.

```json
{
  "status": 404,
  "detail": "Static-file directory 'missing' was not found."
}
```

## Get file metadata

```http
GET /api/static-files/file
```

Returns metadata and the public URL. It does not return file content and does
not return directory metadata.

### Parameters

| Location | Name | Type | Required | Constraints |
| --- | --- | --- | --- | --- |
| Header | `Authorization` | string | Yes | `Bearer <access-token>`. |
| Query | `path` | string | Yes | Nonempty relative file path. |

There are no path parameters and no request body.

### Request

```bash
curl --get 'https://cms.example.com/tenant-a/api/static-files/file' \
  --header 'Authorization: Bearer <access-token>' \
  --data-urlencode 'path=styles/site.css'
```

### Responses

- `200 OK` returns a [static-file representation](#static-file-representation).
- `400 Bad Request` is returned for a missing, empty, rooted, or otherwise
  unsafe `path`.
- `401 Unauthorized` and `403 Forbidden` are returned for authentication and
  **View tenant static files** permission failures.
- `404 Not Found` is returned when the path is absent or identifies a
  directory.

```json
{
  "path": "styles/site.css",
  "name": "site.css",
  "isDirectory": false,
  "length": 1842,
  "lastModified": "2026-08-29T01:25:26.949+00:00",
  "url": "/tenant-a/styles/site.css"
}
```

```json
{
  "status": 404,
  "detail": "Static file 'styles/missing.css' was not found."
}
```

## Upload file content

```http
PUT /api/static-files/content
Content-Type: application/octet-stream
```

### Parameters

| Location | Name | Type | Required | Default | Constraints |
| --- | --- | --- | --- | --- | --- |
| Header | `Authorization` | string | Yes | — | `Bearer <access-token>`. |
| Header | `Content-Type` | string | No | — | The endpoint advertises `application/octet-stream`; the handler streams the body without inspecting this header. |
| Query | `path` | string | Yes | — | Nonempty relative destination file path. |
| Query | `overwrite` | boolean | No | `false` | `true` replaces an existing regular file. With `false`, identical existing content returns `200`; different content returns `409`. |
| Body | — | binary stream | No | Zero bytes | Raw file bytes; an absent or zero-length body creates a zero-byte file. |

There are no path parameters.

### Request

```bash
curl --request PUT \
  'https://cms.example.com/tenant-a/api/static-files/content?path=styles%2Fsite.css&overwrite=false' \
  --header 'Authorization: Bearer <access-token>' \
  --header 'Content-Type: application/octet-stream' \
  --data-binary '@site.css'
```

### Responses

- `201 Created` returns newly uploaded file metadata. The `Location` header and
  response `url` are the same tenant-relative public URL.
- `200 OK` returns the existing metadata when the destination already contains
  the exact request bytes with `overwrite=false`, making an identical retry successful without
  rewriting. With `overwrite=true`, it returns the metadata after atomic replacement.
- `400 Bad Request` is returned for an unsafe logical path or a physical path
  that escapes the root or traverses a symbolic link.
- `401 Unauthorized` and `403 Forbidden` are returned for authentication and
  **Manage tenant static files** permission failures.
- `409 Conflict` is returned when a regular file with different content exists
  and `overwrite` is `false`.

```http
HTTP/1.1 201 Created
Content-Type: application/json
Location: /tenant-a/styles/site.css
```

```json
{
  "path": "styles/site.css",
  "name": "site.css",
  "isDirectory": false,
  "length": 1842,
  "lastModified": "2026-08-29T01:25:26.949+00:00",
  "url": "/tenant-a/styles/site.css"
}
```

```json
{
  "status": 409,
  "detail": "Static file 'styles/site.css' already exists. Pass --overwrite true to replace it."
}
```

The endpoint stages the complete body in a same-directory temporary file using write-through,
then installs it with `File.Move`. With `overwrite=true`, replacement occurs only after the body
is fully staged; the destination is never truncated or streamed in place.

With `overwrite=false`, concurrent creates use conditional move semantics. A losing request
compares its fully staged bytes with the winner: identical content returns `200`, while different
content returns `409` with
`Static file '<path>' already exists with different content.`. Existing different content uses
the conflict response shown above.

## Errors

All logical path validation failures use this detail:

```json
{
  "status": 400,
  "detail": "The static-file path must be relative and cannot contain '.' or '..' segments."
}
```

Upload physical-path validation uses:

```json
{
  "status": 400,
  "detail": "The static-file path resolves outside the tenant static-file root or traverses a symbolic link."
}
```

Paging failures use:

```json
{
  "status": 400,
  "detail": "Skip must be zero or greater and take must be greater than zero."
}
```

or:

```json
{
  "status": 400,
  "detail": "Take cannot exceed 200."
}
```

The framework may add standard Problem Details fields such as `type` and
`title`; the examples show the status and endpoint-assigned detail. Bearer
authentication and authorization failures occur before the endpoint handler,
so they do not have an endpoint-defined JSON body.

## Endpoint coverage and sources

This page covers all **3** routes mapped by
`StaticFileManagementEndpoints.AddStaticFileManagementEndpoints`. It was
derived from `StaticFileManagementEndpoints.cs`, `TenantFileProvider.cs`,
`StaticFileRemoteManagementCapabilityProvider.cs`, the
`OrchardCore.Tenants.FileProvider` startup and manifest declarations, endpoint
metadata tests, and static-file path-resolution tests.
