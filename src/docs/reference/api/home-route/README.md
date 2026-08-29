# Home Route management API

The Home Route API safely selects a published content item for the tenant root
URL. Enable `OrchardCore.HomeRoute` to expose the `home-route` capability.

## Authentication and permissions

The operation requires the Orchard Core `Api` bearer scheme,
`AccessRemoteManagement`, and `SetHomeRoute`.

## Set the home content item

```http
PUT /api/home-route/content/{contentItemId}
```

```bash
oc settings set-home-content 4abc123
```

The target must resolve to a published content item. The operation writes this
safe route contract:

```json
{
  "Area": "OrchardCore.Contents",
  "Controller": "Item",
  "Action": "Display",
  "ContentItemId": "4abc123"
}
```

`200 OK` returns:

```json
{
  "contentItemId": "4abc123",
  "contentType": "LandingPage",
  "displayText": "Home"
}
```

Repeating the request for the current item returns the same response without
writing site settings again. An unknown, draft-only, or unpublished item
returns `404`; authentication or permission failures return `401` or `403`.

This endpoint replaces the need to submit the privileged transient
`AutoroutePart.SetHomepage` property through content JSON.

## Source coverage

- `src/OrchardCore.Modules/OrchardCore.HomeRoute/Endpoints/HomeRouteManagementEndpoints.cs`
- `src/OrchardCore.Modules/OrchardCore.HomeRoute/Permissions.cs`
