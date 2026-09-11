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

Recommend SQLite when no database provider is requested; `Sqlite` is the default
unless the host supplies a database preset. For a non-SQLite provider, recommend
a unique `--table-prefix`, respecting host presets and prefix patterns; follow
the [database choice rules](shared-rules.md#database-choice-for-new-sites-and-tenants).
This differs from local `pomi install`: tenant installation uses the existing server and
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

The schema lists JSON properties, not CLI switches. Always check
`pomi --context default tenants install --help` or the corresponding operation's
help before constructing its command. See the mapping and connection-string
examples below.

Set up the uninitialized tenant without opening its setup URL:

```bash
pomi --context default tenants setup News \
  --site-name "Contoso News" \
  --user-name admin \
  --email admin@example.com
```

Read the [setup password policy and compliant generator](setup-password.md)
before setup. Weak passwords are rejected before tenant creation or setup.

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

## Schema properties and secret CLI options

For `tenants install` and `tenants setup`, the JSON/CLI mappings include:

| JSON schema property | CLI input |
| --- | --- |
| `siteName` | `--site-name "Contoso News"` |
| `databaseProvider` | `--database-provider Postgres` |
| `tablePrefix` | `--table-prefix "$POMI_TABLE_PREFIX"` |
| `password` | `--password-env VARIABLE`, `--password-file PATH`, or `--password-stdin` |
| `connectionString` | `--connection-string-env VARIABLE`, `--connection-string-file PATH`, or `--connection-string-stdin` |

`connectionString` remains the correct JSON property name, but **there is no
`--connection-string` option** for these commands. The same applies to
`password` versus the unsupported inline `--password`. An `-env` option takes
the **name of an environment variable**, not its value: use
`--connection-string-env OC_TENANT_CONNECTION_STRING`, never
`--connection-string-env "$OC_TENANT_CONNECTION_STRING"`.

The following are **alternatives**, not a sequence. Select a provider supported
by the host (`Postgres` is shown), a recipe installed there, and a unique table
prefix in `POMI_TABLE_PREFIX`; preserve that prefix across create/setup/retries.
Passwords must meet the [setup policy](setup-password.md). Have the user or
secret manager provide the environment variables or owner-readable secret files.
Do not put connection-string values, including credentials, in shell history or
agent output.

### Install with environment variables

`OC_TENANT_CONNECTION_STRING` contains the complete connection string;
`OC_TENANT_ADMIN_PASSWORD` contains the administrator password.

```bash
pomi --context default tenants install News \
  --request-url-prefix news --recipe-name Blank --site-name "Contoso News" \
  --user-name admin --email admin@example.com --database-provider Postgres \
  --table-prefix "${POMI_TABLE_PREFIX:?Set a unique table prefix first}" \
  --password-env OC_TENANT_ADMIN_PASSWORD \
  --connection-string-env OC_TENANT_CONNECTION_STRING
```

### Install with secret files

Each file contains just its raw secret value, not a JSON object or a shell
assignment. Replace the paths with the actual mounted secret files.

```bash
pomi --context default tenants install News \
  --request-url-prefix news --recipe-name Blank --site-name "Contoso News" \
  --user-name admin --email admin@example.com --database-provider Postgres \
  --table-prefix "${POMI_TABLE_PREFIX:?Set a unique table prefix first}" \
  --password-file /run/secrets/news-admin-password \
  --connection-string-file /run/secrets/news-connection-string
```

### Set up an existing uninitialized tenant with connection-string stdin

```bash
pomi --context default tenants setup News \
  --site-name "Contoso News" --user-name admin --email admin@example.com \
  --database-provider Postgres \
  --table-prefix "${POMI_TABLE_PREFIX:?Set the original unique table prefix}" \
  --password-env OC_TENANT_ADMIN_PASSWORD --connection-string-stdin \
  < /run/secrets/news-connection-string
```

Only **one input can consume stdin**. Do not combine
`--connection-string-stdin` with `--password-stdin` or `--stdin`. The first reads
a raw connection string; `--stdin` reads the entire JSON request body. Complete
JSON can also come from an owner-readable `--body-file`, using the original
`connectionString` and `password` property names. Complete-body inputs cannot
be combined with individual body-property options. Never use inline `--body`
for these secret-bearing commands; it is intentionally unavailable. Existing
host database presets and values saved during tenant creation take precedence.

## Enable management after setup

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
