# Management API reference

The Orchard Core management APIs expose tenant administration operations through a tenant-specific OpenAPI document. The [`OrchardCore.RemoteManagement`](../modules/RemoteManagement/README.md) feature publishes discovery metadata and adds command-line projection metadata for the `oc` CLI.

Each API belongs to the feature that owns the underlying resource. Consequently, the operations available to a tenant depend on its enabled features.

## Before you begin

1. Enable and configure [Remote Management](../modules/RemoteManagement/README.md#enable-and-configure).
2. Use the public [discovery endpoint](discovery/README.md) to locate the tenant's authentication authority and authenticated management manifest.
3. Request the `orchardcore.management` scope and authenticate with a supported OpenID Connect grant.
4. Fetch the OpenAPI document identified by the authenticated manifest.

All paths in this reference are relative to the tenant URL. For example, a tenant mounted at `https://cms.example.com/acme` exposes `/api/features` at `https://cms.example.com/acme/api/features`.

## Authentication

Except where a page explicitly marks an operation as anonymous, send an API access token:

```http
Authorization: Bearer <access-token>
Accept: application/json
```

Remote clients should request the `orchardcore.management` scope, which identifies the `orchardcore` resource. Endpoint authorization evaluates the authenticated principal's permission claims rather than checking the scope directly. The principal must have **Access remote management API** and the operation-specific permission listed on the API page. A missing or invalid token results in `401 Unauthorized`; an authenticated principal without a required permission receives `403 Forbidden`.

See [Authentication](authentication/README.md) for the OpenID Connect endpoints and grants used by remote clients.

## API groups

### Protocol

- [Discovery and manifest](discovery/README.md)
- [Authentication](authentication/README.md)

### Content

- [Content definitions](content-definitions/README.md)
- [Content items](content-items/README.md)
- [Media](media/README.md)
- [Queries](queries/README.md)
- [Workflows](workflows/README.md)

### Administration

- [Features](features/README.md)
- [Recipes](recipes/README.md)
- [Roles](roles/README.md)
- [Users](users/README.md)

### Hosting

- [Tenants](tenants/README.md)
- [Static files](static-files/README.md)

## Request and response conventions

- JSON property names use the casing shown in request and response examples. They are generated from the shipped endpoint contracts.
- Send JSON request bodies with `Content-Type: application/json`.
- Binary upload operations document their required content type on their API page.
- Collection operations that support paging use zero-based `skip` and a bounded `take`; their pages define the exact defaults and envelope.
- Validation failures use the response representation documented for the operation. Do not assume that all modules return one global error shape.
- Content items, query parameters, workflow definitions, and other extensible resources may contain feature-contributed properties. Use the tenant's OpenAPI document and schema operations as the authoritative contract for those dynamic portions.

## OpenAPI and CLI projection

The tenant OpenAPI document is available at the URL returned as `openApiUrl` by the authenticated manifest. Standard OpenAPI operation metadata defines the HTTP contract. The `x-oc-cli` extension projects selected operations into `oc <resource> <verb>` commands; it does not change endpoint authorization or HTTP behavior.

API clients should use OpenAPI operation IDs and routes rather than deriving HTTP behavior from CLI command names.
