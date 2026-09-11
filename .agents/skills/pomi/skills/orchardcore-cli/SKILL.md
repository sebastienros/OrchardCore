---
name: orchardcore-cli
description: Installs or updates the `pomi` CLI and uses it to initialize local Orchard CMS sites or manage remote tenants. Use for local site creation, contexts, authentication, discovery, tenant setup, enabling Remote Management, compatibility checks, direct GraphQL, dynamic help, and coordinating module-specific management tasks.
---

# Pomi CLI

Use Pomi to create local Orchard CMS applications or manage an existing tenant.
Read the [shared context, authentication, and output rules](references/shared-rules.md)
before issuing commands. Specialists link to the same rules and can be used directly.
Load only the procedure or specialist needed for the user's task.

## Choose the workflow

| Task | Read |
| --- | --- |
| Install/update the Pomi executable or discover its latest package version | [CLI installation](references/cli-installation.md) |
| Create a local application and initialize its Default tenant (`pomi install`) | [Local installation](references/installation.md) |
| Create/setup a tenant in an existing server (`pomi tenants install`, create, setup) | [Tenant installation and Remote Management setup](references/tenants.md) |
| Connect, log in, approve a device code, or manage saved contexts | [Authentication and contexts](references/authentication.md) |
| Diagnose missing commands, cache freshness, permissions, or output | [Shared operating rules](references/shared-rules.md) |
| Define types, parts, fields, and content models | [Content definitions](../orchardcore-cli-content-definitions/SKILL.md) |
| Author, validate, publish, or restore content versions | [Content items](../orchardcore-cli-content-items/SKILL.md) |
| Upload images/files, obtain URLs, or manage custom CSS/JavaScript | [Media](../orchardcore-cli-media/SKILL.md) |
| Create Liquid shape overrides and verify rendering | [Templates](../orchardcore-cli-templates/SKILL.md) |
| Select installed site/admin themes | [Themes](../orchardcore-cli-themes/SKILL.md) |
| Create navigation with official menu content and shapes | [Menus](../orchardcore-cli-menus/SKILL.md) |
| Execute GraphQL documents or inspect its schema | [GraphQL](../orchardcore-cli-graphql/SKILL.md) |
| Manage Site Settings, Custom Settings, and cultures | [Settings](../orchardcore-cli-settings/SKILL.md) |
| Manage features, recipes, queries, workflows, users, and roles | [Administration](../orchardcore-cli-automation/SKILL.md) |

## Coordinate a site build

For a complete site build, create/setup the tenant, enable the required features,
design definitions, upload Media assets, author a draft, create templates, render
and refine, then publish when requested and verify public routes. For an existing
site, start at the relevant step and preserve its content model and configuration.

The [content definitions specialist](../orchardcore-cli-content-definitions/SKILL.md)
covers editable Flow/Bag composition. Keep presentation in templates and Media
assets; theme files are deployed with the application. Pomi has no static-file
upload API. Follow the user's chosen scope rather than treating a single-resource
request as permission to rebuild the site.
