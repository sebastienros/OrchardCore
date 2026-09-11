# Create and initialize tenants

Apply the [shared operating rules](shared-rules.md). Use
[authentication and contexts](authentication.md) if the Default tenant context
is not ready. To create a local application, use [local installation](installation.md).

Use `pomi tenants install <name>` to create and set up a tenant in an existing
Orchard application. Run it from an authenticated context targeting the
`Default` tenant; it requires tenant-management permissions and no local SDK:

```bash
pomi --context default tenants install News \
  --request-url-prefix news \
  --recipe-name Blank \
  --site-name "Contoso News" \
  --user-name admin \
  --email admin@example.com \
  --password-env OC_TENANT_ADMIN_PASSWORD \
  --output json
```

SQLite is the default unless the host supplies a database preset. This differs
from local `pomi install`: tenant installation uses the existing server and
creates no local application. It creates neither a context nor a login token.
After success, follow the Remote Management and child-tenant login steps below.
If creation succeeds but setup fails, inspect `pomi tenants show News`. For an
uninitialized tenant, correct the setup inputs and use `tenants setup News`;
do not blindly repeat installation or delete the partially created tenant.

For separate creation and setup (for example, to hand off an uninitialized
tenant), use:

```bash
pomi --context default tenants create \
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
pomi --context default tenants schema --operation install
pomi --context default tenants schema --operation create
pomi --context default tenants schema --operation setup
```

Set up the uninitialized tenant without opening its setup URL:

```bash
pomi --context default tenants setup News \
  --site-name "Contoso News" \
  --user-name admin \
  --email admin@example.com
```

Both `tenants install` and `tenants setup` use a masked password prompt by
default. For automation, use exactly one of:

```bash
pomi --context default tenants setup News ... --password-env OC_TENANT_ADMIN_PASSWORD
printf '%s' "$OC_TENANT_ADMIN_PASSWORD" | pomi --context default tenants setup News ... --password-stdin
pomi --context default tenants setup News ... --password-file /run/secrets/news-admin-password
```

Prefer the prompt for interactive work, CI-injected environment variables for
automation, and owner-readable short-lived files for mounted secrets. The
secret-bearing install/setup commands intentionally have no inline `--password` or
`--body` option.

After setup, configure direct management and authenticate to the child tenant:

```bash
pomi --context default tenants enable-remote-management News
pomi context add news <exact-url-returned-by-enable-remote-management> --current
pomi login
pomi api compatibility
pomi api refresh
```

Do not proxy a Default-tenant identity into a child tenant. Direct
authentication preserves tenant-local roles, ownership, and authorship.

For host presets, validation, permissions, and complete DTOs, read the versioned
[tenant API reference](https://github.com/sebastienros/OrchardCore/blob/4d4fc0fb66a5d789dff6d8057bbc074107918533/src/docs/reference/api/tenants/README.md).
