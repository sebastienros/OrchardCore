---
name: orchardcore-cli-automation
description: Automates Orchard Core feature, recipe, query, workflow, user, and role management through `oc`. Use for enabling module capabilities, executing recipes, defining/executing SQL or other queries, managing workflow types and instances, and provisioning tenant-local security principals and permissions.
---

# Orchard Core CLI Automation

Use this skill after selecting the exact tenant context. Refresh OpenAPI after
feature changes because commands and schemas are dynamic.

## Features

```bash
oc features list --search Media --skip 0 --take 200
oc features show OrchardCore.Media
oc features enable OrchardCore.Media
oc features disable OrchardCore.Media --yes
oc api refresh --force
```

Use `--force` only when intentionally overriding dependency/dependent checks.
Re-read the feature state after mutation.

## Recipes

```bash
oc recipes list --search setup --take 200
oc recipes show <recipe-id>
oc recipes schema --operation execute
oc recipes execute <recipe-id> --body-file parameters.json --yes
```

Use the opaque case-sensitive ID returned by list/show. Recipe execution starts
a new execution and is not generally retry-idempotent; do not automatically
repeat it after an ambiguous failure.

## Queries

```bash
oc queries sources list --take 200
oc queries schema --operation create
oc queries validate --body-file query.json
oc queries create --body-file query.json
oc queries show RecentNews
oc queries update RecentNews --body-file query.json
oc queries execute RecentNews --body-file parameters.json
oc queries delete RecentNews --yes
```

Read the selected source's schema before creating a query. Keep paging,
publication state, taxonomy filters, ordering, and projection in the query
rather than duplicating them in Liquid.

Query updates are full semantic replacements. Preserve `returnContentItems`
explicitly: omitting this non-nullable boolean changes it to `false`.

## Workflows

```bash
oc workflow activity-types list --take 200
oc workflow activity-types show <activity-name>
oc workflow types schema --operation create
oc workflow types validate --body-file workflow.json
oc workflow types create --body-file workflow.json
oc workflow types show <workflow-type-id>
oc workflow types enable <workflow-type-id>
oc workflow types execute <workflow-type-id> --body-file input.json
oc workflow instances list --workflow-type-id <workflow-type-id>
oc workflow instances show <workflow-id>
oc workflow instances cancel <workflow-id> --yes
```

Use singular `workflow` in command groups. Discover activity schemas from the
target tenant; enabled modules contribute activity types and properties.
Workflow execution starts a new instance and may not be safe to retry.

## Users and roles

```bash
oc users schema --operation create
oc users create --body-file user.json
oc users list --search editor --role Editor --take 200
oc users show <user-id>
oc users update <user-id> --body-file user.json
oc users disable <user-id> --yes
oc users delete <user-id> --yes

oc roles schema --operation create
oc roles create --body-file role.json
oc roles list --search Content --take 200
oc roles show <role-id>
oc roles update <role-id> --body-file role.json
oc roles delete <role-id> --yes
```

Apply least privilege. Every API still requires `AccessRemoteManagement`; grant
only resource permissions needed by the automation identity. Use stable IDs
and full replacement bodies where the live schema requires them. Never embed
passwords or client secrets in checked-in JSON or command arguments.
User create/update does not yet have the setup command's dedicated secret
options. Supply password-bearing complete JSON through protected
`--body-file` or `--stdin`, never inline `--body`.

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
