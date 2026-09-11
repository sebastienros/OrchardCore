---
name: orchardcore-cli-automation
description: Automates Orchard Core feature, recipe, query, workflow, user, and role management through `pomi`. Use for enabling module capabilities, executing recipes, defining/executing SQL or other queries, managing workflow types and instances, and provisioning tenant-local security principals and permissions.
---

# Pomi CLI Automation

Use this skill after selecting the exact tenant context. Refresh OpenAPI after
feature changes because commands and schemas are dynamic.

## Features

```bash
pomi features list --search Media --skip 0 --take 200
pomi features show OrchardCore.Media
pomi features enable OrchardCore.Media
pomi features disable OrchardCore.Media --force
pomi api refresh --force
```

`--force` skips the CLI confirmation only. The feature API's separate
`--api-force true` option includes missing dependencies (enable) or enabled
dependents (disable); use it only when those additional changes are authorized.
For example, `pomi features disable OrchardCore.Media --force` keeps dependency
checks enabled. Never add `--api-force true` merely to avoid a prompt.
Re-read the feature state after mutation.

## Recipes

```bash
pomi recipes list --search setup --take 200
pomi recipes show <recipe-id>
pomi recipes schema --operation execute
pomi recipes execute <recipe-id> --body-file parameters.json --force
```

Use the opaque case-sensitive ID returned by list/show. Recipe execution starts
a new execution and is not generally retry-idempotent; do not automatically
repeat it after an ambiguous failure.

## Queries

```bash
pomi queries sources list --take 200
pomi queries schema --operation create
pomi queries validate --body-file query.json
pomi queries create --body-file query.json
pomi queries show RecentNews
pomi queries update RecentNews --body-file query.json
pomi queries execute RecentNews --body-file parameters.json
pomi queries delete RecentNews --force
```

Read the selected source's schema before creating a query. Keep paging,
publication state, taxonomy filters, ordering, and projection in the query
rather than duplicating them in Liquid.

Query updates are full semantic replacements. Preserve `returnContentItems`
explicitly: omitting this non-nullable boolean changes it to `false`.

## Workflows

```bash
pomi workflow activity-types list --take 200
pomi workflow activity-types show <activity-name>
pomi workflow types schema --operation create
pomi workflow types validate --body-file workflow.json
pomi workflow types create --body-file workflow.json
pomi workflow types show <workflow-type-id>
pomi workflow types enable <workflow-type-id>
pomi workflow types execute <workflow-type-id> --body-file input.json
pomi workflow instances list --workflow-type-id <workflow-type-id>
pomi workflow instances show <workflow-id>
pomi workflow instances cancel <workflow-id> --force
```

Use singular `workflow` in command groups. Discover activity schemas from the
target tenant; enabled modules contribute activity types and properties.
Workflow execution starts a new instance and may not be safe to retry.
Workflow DTOs generally use camelCase, unlike PascalCase content-item
properties. Preserve the casing in the live operation/activity schemas; do
not copy content-item casing into workflow payloads.

## Users and roles

```bash
pomi users schema --operation create
pomi users create --body-file user.json
pomi users list --search editor --role Editor --take 200
pomi users show <user-id>
pomi users update <user-id> --body-file user.json
pomi users disable <user-id> --force
pomi users delete <user-id> --force

pomi roles schema --operation create
pomi roles create --body-file role.json
pomi roles list --search Content --take 200
pomi roles show <role-id>
pomi roles update <role-id> --body-file role.json
pomi roles delete <role-id> --force
```

Apply least privilege. Every API still requires `AccessRemoteManagement`; grant
only resource permissions needed by the automation identity. Use stable IDs
and full replacement bodies where the live schema requires them. Never embed
passwords or client secrets in checked-in JSON or command arguments.
User create/update exposes `--password-env`, `--password-file`, and
`--password-stdin`. Supply exactly one source, or put the complete payload
in a protected `--body-file`/`--stdin`. Inline `--body` and `--password` are
not exposed for secret-bearing operations. Inspect live help on older servers.

## Automation sequence

1. Select an explicit context.
2. Check compatibility and refresh discovery.
3. Enable required features.
4. Refresh discovery again.
5. Inspect the operation schema.
6. Validate definitions where supported.
7. Execute mutations in dependency order.
8. Read back resources and capture stable IDs.
9. Treat destructive actions and execution-style commands as non-retryable
   unless their API reference explicitly says otherwise.

Canonical references are under `src/docs/reference/api/` in `features`,
`recipes`, `queries`, `workflows`, `users`, and `roles`.
