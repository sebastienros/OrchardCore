---
name: orchardcore-cli
description: Operates Orchard Core remotely with the `oc` CLI. Use for contexts, authentication, discovery, tenant creation and setup, enabling Remote Management, compatibility checks, dynamic help, JSON output, and coordinating module-specific Orchard Core management tasks.
---

# Orchard Core CLI

Use `oc` as a tenant-scoped, OpenAPI-driven management client. A context always
targets one tenant URL and one tenant-local identity.

## Guardrails

1. Run `oc --help` and `oc <group> --help` before assuming a dynamic command is
   available. Enabled features determine each tenant's command tree.
2. Run `oc <group> schema [--operation <verb>]` before constructing JSON.
3. Preserve JSON property casing exactly as emitted by the schema.
4. Use JSON output for automation. Add `--output table` only for human review.
5. Select the intended context explicitly when changing more than one tenant:
   `oc --context <name> ...`.
6. Never put passwords or client secrets directly on a command line.

## Connect to an existing tenant

```bash
oc context add production https://cms.example.com/site-a --current
oc login
oc doctor
oc api refresh
oc --help
```

Use `oc login --grant device` on a headless terminal. Browser login uses
authorization code with PKCE. On Windows, human tokens are encrypted by Windows
Credential Manager. On macOS, Linux, and other Unix-like systems, they are
plaintext owner-only files under `~/.orchardcore/credentials` (`0700`
directory, `0600` files).

For multiple tenants, start one device flow per context and complete each code
as the intended tenant-local user:

```bash
oc --context news login --grant device
oc --context marketing login --grant device
oc --context commerce login --grant device
```

Do not reuse tokens across contexts. Device authorization can automate the CLI
side, but a user must approve each code. For browser login, enter credentials
only in the tenant's HTTPS login page or a trusted password manager. Never pass
browser passwords through CLI arguments, chat, logs, screenshots, or browser
automation scripts.

For automation, use a dedicated confidential OpenID application with minimal
permissions:

```bash
export OC_CLIENT_ID=orchard-automation
export OC_CLIENT_SECRET='<injected-by-secret-store>'
oc --context production content items list
```

Do not use the public `orchardcore-cli` client for client credentials.

## Create and initialize a tenant

Run tenant lifecycle commands from a context targeting the `Default` tenant:

```bash
oc --context default tenants create \
  --name News \
  --request-url-prefix news \
  --database-provider Sqlite \
  --table-prefix News \
  --recipe-name Blank
```

`recipes list` excludes setup recipes. Use a setup recipe known to be installed
by the deployment (for example `Blank` in the standard CMS host), or obtain the
allowed recipe name from the operator/setup UI. Do not infer availability from
ordinary recipe discovery.

Inspect the authoritative inputs when host database presets or patterns differ:

```bash
oc --context default tenants schema --operation create
oc --context default tenants schema --operation setup
```

Set up the uninitialized tenant without opening its setup URL:

```bash
oc --context default tenants setup News \
  --site-name "Contoso News" \
  --user-name admin \
  --email admin@example.com
```

The default password prompt is masked. For automation, use exactly one of:

```bash
oc --context default tenants setup News ... --password-env OC_TENANT_ADMIN_PASSWORD
printf '%s' "$OC_TENANT_ADMIN_PASSWORD" | oc --context default tenants setup News ... --password-stdin
oc --context default tenants setup News ... --password-file /run/secrets/news-admin-password
```

Prefer the prompt for interactive work, CI-injected environment variables for
automation, and owner-readable short-lived files for mounted secrets. The
secret-bearing setup command intentionally has no inline `--password` or
`--body` option.

After setup, configure direct management and authenticate to the child tenant:

```bash
oc --context default tenants enable-remote-management News
oc context add news <exact-url-returned-by-enable-remote-management> --current
oc login
oc api compatibility
oc api refresh
```

Do not proxy a Default-tenant identity into a child tenant. Direct
authentication preserves tenant-local roles, ownership, and authorship.

## Manage contexts

```bash
oc context list
oc context use news
oc --context production content items list
oc logout news
oc context delete news --yes
oc context clear --force
```

Refresh discovery after enabling or disabling features:

```bash
oc api refresh
oc --help
```

Use `oc api compatibility` to diagnose protocol or version mismatches, and
`oc api invoke <METHOD> <PATH>` only when no projected resource command exists.

## Route module work

Load the narrowest relevant skill:

- **Content model design**: `orchardcore-cli-content-definitions`
- **Content authoring and lifecycle**: `orchardcore-cli-content-items`
- **Images, files, and tenant CSS/static assets**: `orchardcore-cli-media`
- **Custom Liquid templates and rendering**: `orchardcore-cli-templates`
- **Installed site/admin theme selection**: `orchardcore-cli-themes`
- **Menu content and official menu-shape rendering**: `orchardcore-cli-menus`
- **Site Settings and Custom Settings**: `orchardcore-cli-settings`
- **Features, recipes, queries, workflows, users, and roles**:
  `orchardcore-cli-automation`

For a complete site build, apply them in this order: create/setup tenant,
enable required features, design definitions, upload media/static assets,
create a draft fixture, create templates, render and refine the draft, publish
content, then verify public routes.

Use a content-driven model by default: page types compose ordered section
widgets with `FlowPart`; collection-section widgets own semantic named
`BagPart` attachments containing inner blocks/widgets. Keep design markup,
classes, responsive behavior, and scripts in Liquid/templates and static
assets—not in content fields.

## Failure handling

- Treat `401` as missing/invalid authentication and `403` as insufficient
  tenant-local permissions.
- Treat `404` as either an unavailable feature/command or an unknown resource;
  refresh discovery before concluding.
- Inspect Validation Problem Details and correct the named property.
- Retry only according to the operation's documented idempotency contract.
- Never print setup passwords, access tokens, refresh tokens, client secrets,
  or unredacted connection strings.

Canonical references:
`src/docs/reference/modules/RemoteManagement/README.md`,
`src/docs/reference/api/discovery/README.md`,
`src/docs/reference/api/authentication/README.md`, and
`src/docs/reference/api/tenants/README.md`.
