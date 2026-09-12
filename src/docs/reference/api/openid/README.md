# OpenID management API

The `OrchardCore.OpenId.Management` feature exposes application discovery and scope administration
through the `openid-management` capability. These reads use the same OpenID managers
and configured stores as the administration UI. Application reads do not change
applications, credentials, authorizations or tokens. Scope mutations share the
existing admin and recipe descriptor editor.

## Authentication and permissions

All operations require the `Api` bearer scheme and `AccessRemoteManagement`.
Application reads additionally require `ManageApplications`; scope operations require
`ManageScopes`. An application principal uses its assigned Orchard roles, just as
other remote management operations do. Discovery does not require impersonating a user.

## Commands and routes

| Command | HTTP request |
| --- | --- |
| `pomi openid applications list --skip 0 --take 50` | `GET /api/openid/applications?skip=0&take=50` |
| `pomi openid applications show my-client` | `GET /api/openid/applications/by-client-id?clientId=my-client` |
| `pomi openid scopes list --skip 0 --take 50` | `GET /api/openid/scopes?skip=0&take=50` |
| `pomi openid scopes show orchardcore.management` | `GET /api/openid/scopes/by-name?name=orchardcore.management` |
| `pomi openid scopes create --body-file scope.json` | `POST /api/openid/scopes` |
| `pomi openid scopes update reporting --body-file scope.json` | `PUT /api/openid/scopes/by-name?name=reporting` |
| `pomi openid scopes delete reporting --force` | `DELETE /api/openid/scopes/by-name?name=reporting` |

Identifiers are query parameters so client IDs and scope names containing reserved
URL characters can be represented without treating them as route segments. Commands
encode those values. Application lookup uses the client ID; scope lookup uses the
scope name. The returned `id` is the physical identifier used by the admin UI.

Lists return `items`, `totalCount`, `skip` and `take`. Offset defaults to zero and
must be nonnegative; page size defaults to 50 and must be between 1 and 200. Ordering
comes from the configured OpenID store, matching its administrative paging behavior.
Separate count and page reads are not a snapshot when another request changes data.

An application response contains `id`, `clientId`, `displayName`, `clientType`,
`applicationType`, `consentType`, `roles`, `permissions`, `requirements`,
`redirectUris` and `postLogoutRedirectUris`. Permissions and requirements are the
registered OpenIddict strings, including grant and scope permissions. They describe
configuration; they do not prove that a grant is enabled by the current server.
Optional scalar values can be null when not configured.

A scope response contains `id`, `name`, `displayName`, `description` and `resources`.
Resources are the registered resource identifiers, including tenant resource entries
where configured. These reads do not expand or change scope registration.

Responses omit client secrets and their hashes, signing/key material, arbitrary
custom properties and private application settings. They never serialize the raw
application or scope entity or a complete descriptor.

Invalid paging or empty identifiers return `400`. Unknown identifiers return `404`.
Authentication and permission failures return `401` and `403` respectively.

## Create or update a scope

```json
{
  "name": "reporting",
  "displayName": "Reporting API",
  "description": "Read reporting data",
  "resources": ["reporting-api"]
}
```

`name` and `displayName` are required. Updates replace all these editable fields:
omitting `description` clears it, and omitting `resources` or sending `[]` clears
the resource list. `null` resources, empty resource identifiers, identifiers with
spaces, and unknown body properties are invalid. Duplicate resources are collapsed.
The current tenant's reserved `oct:<tenant-name>` resource is already added by the
authorization server; the same resource restriction as the admin editor applies.

Creation returns `201` and a location pointing to the name-based read operation.
Repeating creation when the name and editable fields already match returns `200`
with the existing scope. A different definition under that name returns `409`.
Updates return `200`, preserve the physical ID and unedited extension properties,
and do not save again when the editable fields already match. The body name must
match the query name; this API does not rename scopes. An unknown update target
returns `404`. Manager validation failures return `400` without retaining rejected
values in the tracked scope.

Scope deletion returns `204`, including when the scope is already absent. Deleting
or changing a scope does not remove application permission strings or revoke
previously issued tokens. These operations manage the registered scope definition;
application grant configuration and credential lifecycle are separate concerns.

## MCP

With the tenant MCP feature enabled, the same reads are available as
`openid_applications_list`, `openid_applications_show`, `openid_scopes_list` and
`openid_scopes_show`. Scope mutations add `openid_scopes_create`,
`openid_scopes_update` and `openid_scopes_delete`. All use the same permission
checks and response contracts,
including when the Pomi CLI feature is disabled.
