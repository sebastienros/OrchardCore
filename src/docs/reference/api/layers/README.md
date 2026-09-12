# Layers API

The Layers API manages tenant-stored layer definitions through `ILayerService`. A layer decides
whether its widgets render on a page. This API replaces definitions and rules; widget placement
is a separate resource. The admin and frontend read the same `LayersDocument` and rule types.

## Availability and authorization

Enable `OrchardCore.Layers` and configure remote management authentication. Each operation requires
an authenticated bearer principal with both `AccessRemoteManagement` and `ManageLayers`.
Application principals and user principals use the same permissions. An admin session cookie
alone does not authorize these endpoints. Requests lacking authentication return 401; callers
without the required permissions receive 403 before a layer document is read or modified.

`OrchardCore.RemoteManagement.Cli` exposes the commands below. The MCP feature independently
exposes eligible tools such as `layers_list`, `layers_create` and `layers_update`; they invoke the
same endpoint handlers and permission checks in process. Disabling Layers removes its API,
capability, commands and tools from discovery. Refresh metadata after changing enabled features.

## Operations

Paths are relative to the tenant URL, including any tenant prefix.

| Method and path | Pomi command | Result |
| --- | --- | --- |
| `GET /api/layers` | `layers list` | Paged definitions with `skip`, `take`, `totalCount`, `items`. |
| `GET /api/layers/{name}` | `layers show <name>` | Complete stored definition; 404 when absent. |
| `GET /api/layer-conditions` | `layers conditions` | Registered condition descriptors and property schemas. |
| `POST /api/layers/validate` | `layers validate` | `isValid` and path-keyed `errors`; never saves or evaluates conditions. |
| `POST /api/layers` | `layers create` | 201 with the saved definition and tenant-relative Location; 409 on a different existing definition. |
| `PUT /api/layers/{name}` | `layers update <name>` | 200 with the replacement; 404 when absent. |
| `DELETE /api/layers/{name}` | `layers delete <name> --force` | 204, including an absent layer; 409 while any latest widget references it. |

List accepts a case-insensitive `search` over names/descriptions, nonnegative `skip` (default 0),
and `take` from 1 to 200 (default 50). Results are sorted by name. Invalid paging returns 400.
Names match case-insensitively. Updates require the body name to match the route exactly and
preserve the stored name's spelling. Renaming is not supported because widgets reference names.

## Definitions and conditions

Create and update accept the complete definition as JSON:

```json
{
  "name": "News visitors",
  "description": "Public news widgets",
  "conditions": [
    {
      "name": "AllConditionGroup",
      "properties": { "displayText": "News page and anonymous visitor" },
      "conditions": [
        {
          "name": "UrlCondition",
          "properties": {
            "value": "/news",
            "operation": "StringStartsWithOperator",
            "caseSensitive": false
          }
        },
        { "name": "IsAnonymousCondition", "properties": {} }
      ]
    }
  ]
}
```

Read the live `layers conditions` result before authoring JSON. Each descriptor contains `name`,
`canWrite`, `supportsChildren` and a JSON Schema for `properties`. The built-in Boolean, Homepage,
authentication, URL, culture, role, content type, JavaScript, All and Any conditions are writable.
Registered extension conditions remain discoverable and readable, but are not writable through
this contract. Their module must supply a supported management implementation before writing them.

The rule requires every root condition to match. All/Any groups use the existing Rules service;
empty rules and empty groups do not match. Only group conditions accept children. Boolean
conditions require a JSON boolean `value`; string comparisons require string `value` and a
registered supported `operation`. `caseSensitive` defaults to false. JavaScript requires a
nonempty `script`; validation parses it without execution. Runtime errors, available scripting
methods, the current URL/culture/principal and content display context still affect evaluation.
Structural validity is not proof that a condition matches a specific page.

Names must contain 1–256 characters, without surrounding whitespace or control characters.
Definitions allow at most 256 conditions and 16 nested groups. The server generates omitted
`conditionId` values. Preserve returned IDs when editing/reordering conditions; supplied IDs
must be unique within the rule and use at most 128 ASCII letters, digits, hyphens or underscores.
Unknown condition names, properties, invalid values and duplicate IDs are rejected before saving.
Mutation validation failures return HTTP 400 with path-keyed validation errors. The validation
operation instead returns HTTP 200 with `isValid: false` and the same errors.

## Replacement, retry and verification

Updates replace the description and the entire conditions array. Omitted description becomes
null, and omitted conditions means an empty rule; read the existing definition before editing.
An identical create retry returns the existing definition, including its generated IDs. A
different definition under the same case-insensitive name returns 409. Identical update retries
preserve generated IDs and avoid another document update. Explicitly supplied IDs are part of
the requested identity and must match for a retry to be equivalent. Omitted optional properties
use the same defaults as their normalized readback.

Concurrent edits have last-writer semantics; there is no conditional ETag update contract.
After an uncertain response, read the layer before retrying. Delete refuses referenced layers
without moving, deleting or unpublishing any widgets. Remove or move those widgets explicitly
before deleting the definition.

```bash
pomi layers conditions --output json
pomi layers schema --operation create
pomi layers validate --body-file news-layer.json --output json
pomi layers create --body-file news-layer.json --output json
pomi layers show "News visitors" --output json
pomi layers update "News visitors" --body-file news-layer.json --output json
```

Verify the saved definition and the rendered pages for the intended URL, culture and visitor
identity. A successful save alone does not establish visibility. See the
[Layers module](../../modules/Layers/README.md) and [Rules module](../../modules/Rules/README.md).
