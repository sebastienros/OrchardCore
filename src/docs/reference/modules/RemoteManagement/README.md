# Remote Management (`OrchardCore.RemoteManagement`)

The Remote Management module exposes a versioned management protocol and tenant-specific OpenAPI document for the Orchard Core command-line interface (`oc`). Enabled Orchard Core features contribute resource commands and JSON Schemas, so the commands available for one tenant may differ from another tenant.

The CLI uses OpenAPI as its management protocol. GraphQL remains available for application queries but is not required by `oc`.

Start with the illustrated [first-tenant walkthrough](../../../guides/remote-management/README.md).

## API reference

- [Management API overview](../../api/README.md)
- [Discovery and manifest](../../api/discovery/README.md)
- [Authentication](../../api/authentication/README.md)

## Enable and configure

Enable **Remote Management** from **Configuration → Features**. This also enables the OpenAPI, OpenID authorization server, OpenID management, and OpenID token validation dependencies.

After enabling the feature, open **Settings → Remote Management**. The page checks every required authentication setting and identifies anything that is missing. Select **Configure Remote Management** (or **Repair configuration**) to configure:

- authorization code with PKCE, device authorization, refresh token, and client credentials grants;
- the `orchardcore.management` scope;
- a public native application with client ID `orchardcore-cli`;
- local API token validation for the current tenant.

The operation uses the current tenant name automatically, can be run repeatedly, and preserves unrelated OpenID Connect endpoints, grants, scopes, applications, roles, and redirect URIs. When the feature is enabled interactively, a one-time notification links administrators to this page.

For automated deployments, the **Orchard Core Remote Management** recipe configures the same requirements. Its `RemoteManagementConfiguration` step resolves the current tenant automatically.

The public bootstrap document is available at `/.well-known/orchardcore-management`. It contains only the protocol version, authentication authority, client ID, and supported grants. The authenticated `/api/management/manifest` endpoint additionally returns tenant identity, compatibility ranges, capabilities, OpenAPI coordinates, and documentation index information.

Access to the authenticated manifest and management APIs requires the **Access remote management API** permission. Each contributed operation also enforces its own Orchard Core permission.

## Install the CLI

The `oc` project can be run as a framework-dependent application during development:

```bash
dotnet run --project src/OrchardCore.Cli -- --help
```

Publish a self-contained executable for the current platform:

```bash
dotnet publish src/OrchardCore.Cli -c Release -r <runtime-identifier>
```

For example, use `osx-arm64`, `osx-x64`, `linux-arm64`, `linux-x64`, `win-arm64`, or `win-x64`.

## Contexts and login

A context identifies one exact tenant URL. Add and select a context before logging in:

```bash
oc context add production https://cms.example.com/tenant-a --current
oc login
```

Browser login uses OAuth authorization code with PKCE and a temporary loopback listener. Credentials are renewed silently with refresh tokens stored in Windows Credential Manager on Windows. On macOS, Linux, and other Unix-like systems, tokens are stored as plaintext owner-only files under `~/.orchardcore/credentials`.

The Unix token directory uses mode `0700` and token files use mode `0600`.
These permissions prevent access by other operating-system users but do not
encrypt tokens at rest. Protect the user account and home directory as you
would for other development credentials.

The CLI does not read or migrate tokens previously written to macOS Keychain or
Linux Secret Service, avoiding an operating-system credential prompt. Sign in
once per context after upgrading; legacy entries can be removed with the
platform's credential-management tools. Tokens from older name-only file
credentials also require one new login because the current key binds the tenant
URL, authority, and client ID. Those obsolete files are not migrated or removed
by a logout of the new login.

For a terminal without a browser, use device authorization:

```bash
oc login --grant device
```

For multiple tenants, run one device flow per named context and approve each
code as the intended tenant-local user:

```bash
oc --context news login --grant device
oc --context marketing login --grant device
```

Tokens are stored separately per context and must not be copied between
tenants. Browser credentials should be entered only in the tenant's HTTPS login
page or by a trusted password manager; never put passwords in command
arguments, logs, screenshots, chat, or browser automation scripts.

For unattended jobs, follow [Client credentials for automation](#client-credentials-for-automation). Implicit and password grants are not supported. The CLI uses OAuth access/refresh tokens and does not use or persist ID-token claims as an identity assertion.

Manage multiple tenants with named contexts:

```bash
oc context list
oc context use staging
oc --context production content items list
oc logout production
```

Delete one context with `oc context delete <name> --yes`. To delete every saved context and its stored credentials, use `oc context clear`; confirm the interactive prompt, or pass `--force` for non-interactive use:

```bash
oc context clear --force
```

A context represents and authenticates to exactly one tenant. From a context for the Default tenant, an administrator can prepare another running tenant for remote management:

```bash
oc tenants enable-remote-management Site1
```

The command enables Remote Management and its dependencies, configures the tenant's OpenID server and CLI application, and grants **Access remote management API** to the tenant's Administrator role. Its output includes the tenant URL. Register that URL as a separate context and authenticate directly as a user of the tenant:

```bash
oc context add site1 https://cms.example.com/site1 --current
oc login
oc content items list
```

Direct authentication ensures tenant-local roles and permissions are enforced and newly created content is associated with the authenticated tenant user. Each context stores separate credentials. Discovery caches are keyed by tenant URL, so aliases for the same URL share metadata; `--help` reflects that tenant's enabled features.

## Client credentials for automation

Use client credentials when a CI job, scheduled script, or service needs to run
`oc` without a person opening a browser. The application authenticates as itself;
its tenant-local roles determine what it can do. There is no user consent page
or administrator password in this flow.

This walkthrough creates an application named `orchard-automation` for the tenant
at `https://cms.example.com/tenant-a/`. Replace that URL with your exact tenant
URL, including its path prefix.

### 1. Prepare the tenant and application role

In the target tenant's admin UI, enable and [configure Remote Management](#enable-and-configure).
This enables the token endpoint and client-credentials grant, creates the
`orchardcore.management` scope, and configures local token validation. It does
**not** create a confidential automation application. Keep the public
`orchardcore-cli` application for browser and device login.

Open **Access Control → Roles**, create a role named `Automation`, and grant
**Access remote management API** (`AccessRemoteManagement`). Add the permissions
required by your intended commands. For the `oc features list` example below,
also grant **Manage Features** (`ManageFeatures`). That permission also permits
feature changes; it is not a read-only permission. Save the role.

The scope permits management API access, while role permissions authorize the
individual operations. Assigning the scope alone is not sufficient. The
**Roles** feature must be enabled to create and assign application roles.

In the legacy admin navigation, **Roles** is under **Security**.

### 2. Register a confidential application

Open **Access Control → OpenID Connect → Applications** and create an application.
With legacy navigation, use **Security → OpenID Connect → Management → Applications**.

| Field | Value |
| --- | --- |
| Display Name | `Orchard automation` |
| Application type | **Web application**; this is the application registration type even when the caller is `oc` |
| Client type | **Confidential client** |
| Client Id | `orchard-automation` |
| Client Secret | Generate a secret using the button beside the field and store it in your secret manager |
| Flows | Select **Allow Client Credentials Flow**; leave other flows unchecked for this application |
| Allowed Scopes | Select `orchardcore.management` |
| Client Credentials Roles | Select `Automation` |

Save the application. This flow does not require redirect URIs, a browser
callback, or refresh tokens. If the client-credentials checkbox or management
scope is missing, complete step 1 in this same tenant first.

### 3. Supply credentials and register the context

Configure these environment variables for the process running `oc`:

| Variable | Value |
| --- | --- |
| `OC_CLIENT_ID` | `orchard-automation` |
| `OC_CLIENT_SECRET` | The secret saved in step 2 |

In CI, inject the secret from the CI system's secret store. For a local test in
Bash or Zsh, this prompt reads it without displaying it or putting its literal
value in shell history:

```bash
export OC_CLIENT_ID=orchard-automation
printf 'Client secret: '
IFS= read -r -s OC_CLIENT_SECRET
printf '\n'
export OC_CLIENT_SECRET
```

Add a context for the exact tenant URL:

```bash
oc context add production-automation https://cms.example.com/tenant-a/ --current
```

A context stores the tenant address and discovery metadata, not your automation
secret. Setting these variables before discovery also lets the CLI fetch the
authenticated manifest and tenant-specific commands without browser login.
The same variables apply to every `oc` command in that environment, so use
`--context` explicitly and supply credentials registered in that target tenant.

### 4. Verify authentication and run a command

An optional login check verifies that the server accepts the application:

```bash
oc --context production-automation login --grant client-credentials \
  --client-id orchard-automation \
  --client-secret-env OC_CLIENT_SECRET
```

Success reports the context, grant type, issuer, and token expiry without
printing the access token. This checks authentication; it does not prove that
the application has permission to perform every management operation.

**Client-credentials login does not establish a saved session.** Neither the
secret nor the token is persisted. Keep `OC_CLIENT_ID` and `OC_CLIENT_SECRET`
available for subsequent commands, which obtain tokens automatically. A
preceding `oc login` is optional:

```bash
oc --context production-automation features list --output json
```

The environment credentials take precedence over saved browser/device
credentials. Use `--output json` for scripts that parse results; the default
`--output auto` uses human output in a terminal and JSON when redirected.

For the login check, `--client-secret-env` may name a different variable, or
`--client-secret-stdin` may read the secret from standard input. For example,
with a protected file provided by your secret manager:

```bash
oc --context production-automation login --grant client-credentials \
  --client-id orchard-automation \
  --client-secret-stdin < /path/to/client-secret.txt
```

These explicit secret options are also available on `oc api invoke`. They apply
to that invocation only; dynamic resource commands use `OC_CLIENT_ID` and
`OC_CLIENT_SECRET`.

When finished with the local test, clear the variables:

```bash
unset OC_CLIENT_ID OC_CLIENT_SECRET
```

`oc logout` removes saved human credentials; it does not disable an automation
application or clear environment variables. Change the application's secret in
the admin UI and update your secret store when rotating credentials. Already
issued access tokens may remain valid until expiry.

### Provisioning during tenant setup

`oc tenants setup` does not currently accept a client ID or client secret.
`oc tenants enable-remote-management` configures the server and public CLI
application but does not provision a confidential client.

For repeatable provisioning, a custom setup recipe can enable the required
features, run `RemoteManagementConfiguration`, create the application role, and
include this [OpenID application recipe step](../OpenId/README.md#openid-connect-client-integration-configuration):

```json
{
  "name": "OpenIdApplication",
  "ClientId": "orchard-automation",
  "DisplayName": "Orchard automation",
  "Type": "Confidential",
  "ApplicationType": "web",
  "ClientSecret": "[js: configuration('Automation:ClientSecret')]",
  "AllowClientCredentialsFlow": true,
  "ScopeEntries": [{ "Name": "orchardcore.management" }],
  "RoleEntries": [{ "Name": "Automation" }]
}
```

Create the `Automation` role with the required permissions before this step.
Provide `Automation:ClientSecret` through the target tenant's
[configuration](../Configuration/README.md) using your deployment's secret
configuration provider. The recipe's
[`configuration` function](../Scripting/README.md#recipes-orchardcorerecipes)
reads that value without embedding a literal secret in the recipe. JavaScript
recipe expressions require **JavaScript Scripting** (`OrchardCore.Scripting.JavaScript`).
Configuration is resolved by the **Orchard server process**, not the computer
running `oc`; exporting `OC_CLIENT_SECRET` in your CLI shell does not send it to
a remote server's setup recipe.

Select the recipe with `oc tenants setup Site1 --recipe-name <recipe-name>`
alongside the other required setup arguments. The recipe must already be
available on the server, and a recipe configured when the tenant was created
takes precedence.

### Troubleshooting client credentials

| Symptom | What to check |
| --- | --- |
| `invalid_client` | The application exists in the selected tenant, its type is confidential, and its client ID and secret match. |
| `unauthorized_client` or an unsupported-grant error | Client credentials must be enabled on both the server and the application. |
| `invalid_scope` | The application allows `orchardcore.management`, and that scope exists in the target tenant. Do not request `offline_access` for this flow. |
| `403` when fetching metadata | The application's selected role needs **Access remote management API**. |
| Login succeeds but a command returns `403` | The application role also needs that operation's permissions. |
| A command asks for login after a successful check | The check did not save a session. Supply `OC_CLIENT_ID` and `OC_CLIENT_SECRET` to the command's process. |
| Authentication targets an unexpected tenant or client | Check `oc context show`, the explicit `--context`, and any inherited `OC_CLIENT_ID` / `OC_CLIENT_SECRET` variables. |

See the [client-credentials HTTP contract](../../api/authentication/README.md#client-credentials)
for token endpoint parameters and OAuth error responses.

## Dynamic commands

The CLI downloads the selected tenant's OpenAPI document and maps operations carrying `x-oc-cli` metadata to noun-and-verb commands:

```text
oc <resource> <verb> [arguments] [options]
```

Examples include:

```bash
oc tenants create --name TenantA --request-url-prefix tenant-a --recipe-name SaaS
oc tenants setup TenantA --site-name "Tenant A" --user-name admin --email admin@example.com
oc content items list
oc content items show 4abc...
oc features enable OrchardCore.Media
oc queries execute RecentPosts --body '{ "parameters": {} }'
```

`oc tenants create` creates an uninitialized tenant but no user account. Run
`oc tenants setup` to execute the selected recipe and create the initial
administrator. It securely prompts for the password by default; automation can
use `--password-env`, `--password-stdin`, or `--password-file`. After setup,
enable Remote Management from the Default tenant context, add the initialized
tenant URL as its own context, and authenticate directly:

```bash
oc tenants setup TenantA --site-name "Tenant A" --user-name admin --email admin@example.com
oc tenants enable-remote-management TenantA
oc context add tenant-a https://cms.example.com/tenant-a --current
oc login
```

The default `--output auto` writes human-readable messages in a terminal (tables for lists) and JSON when redirected to a pipe or file. Use `--output human` to keep readable messages even when redirected. Use `--output json` explicitly for automation, or `--output table|csv|tsv|yaml|toml|none` for other representations. Tables may shorten long non-URL cells; HTTP and HTTPS URLs remain complete. JSON preserves the full response. TOML omits properties whose value is `null`; root arrays and scalar values are emitted under `items` and `value`, respectively. JSON request bodies can come from `--body`, `--body-file`, or `--stdin`. Binary request bodies use `--file` or `--stdin`. Discovered option and argument names use lower kebab-case. List commands use zero-based `--skip` and `--take` options for paging.

Every resource that accepts a request body exposes a `schema` verb. When all input operations use the same shape, the schema is returned directly:

```bash
oc content types schema
oc content parts schema
```

Use `--operation` when a resource accepts different request shapes:

```bash
oc users schema --operation create
oc media files schema --operation move-batch
```

The result is a standalone JSON Schema extracted from the tenant's OpenAPI document. Content items use the tenant-aware `oc content items schema <content-type>` command so attached parts and fields reflect the selected content type.

Content-definition schemas include the built-in settings contracts while allowing settings contributed by other features. For example, `oc content parts schema` describes `ContentPartSettings.attachable` and `ContentPartSettings.reusable`, so an attachable reusable part can be submitted without relying on an existing definition as an example:

```json
{
  "name": "ArticleDetails",
  "settings": {
    "ContentPartSettings": {
      "attachable": true,
      "reusable": true
    }
  },
  "fields": []
}
```

Dynamic discovery also requires `ViewOpenApiContent` when the tenant protects
its OpenAPI document. `AccessRemoteManagement` alone does not grant access to
that document or the resource-specific operations it describes.

### Media and custom assets

Use Media for images and custom CSS, JavaScript, or SVG assets. Uploads use the
same store, folder permissions, extension policy, and size limits as the admin
Media library. Inspect the caller's permitted extensions first:

```bash
oc media constraints show --output json
oc media folders create --name assets
oc media folders create --path assets --name styles
oc media files upload site-v1.css --path assets/styles --file ./site.css
oc media files show assets/styles/site-v1.css --output json
oc media files list --path assets/styles --output table
```

Media file commands preserve resource paths and also return direct, absolute
URLs, including configured CDN URLs. File list tables show both **Path** and
**URL**; upload, copy, and move results include the destination URL. See the
[media representations](../../api/media/README.md#file-or-folder).

Choose your own folder convention in the client. Paths are media-store-relative;
the positional upload argument is a base filename. CSS, JavaScript, and SVG
require `UploadRestrictedMedia` by default, in addition to `ManageMediaContent`
and permission for the destination folder. Extensions absent from both configured
extension lists are rejected even with that permission. The constraints response
includes restricted extensions only for callers permitted to upload them.

Use the response's `url` for public access and `filePath` in media fields or the
Liquid `asset_url` filter. Uploads reject existing files, so use versioned names
for updates. Files appear in the admin Media library. See the
[Media API](../../api/media/README.md) for authorization and mutation details.

Media storage is extensible through `IMediaFileStore`; its default is local disk.
To share uploads across nodes, configure the same shared backend, such as
[Azure Blob](../Media.Azure/README.md) or [Amazon S3](../Media.AmazonS3/README.md),
for the tenant on all nodes. Uploading to a local store does not replicate files.
Tenant static files remain deployment assets and have no management API.

Every static and discovered command supports `--help`. OpenAPI metadata is cached by tenant URL and ETag to reduce discovery requests. Help and completion read the cache offline. Successful mutations expire discovery metadata for the next online command. Use `oc api refresh --force` to bypass the cache and `oc api compatibility` for protocol checks.

`oc --version` prints the CLI version number. Use `oc doctor` to inspect the CLI version, platform, local storage, and caches, or `oc doctor --output json` for structured diagnostics. Both commands work offline; `doctor` does not test server connectivity.

The CLI is distributed as standalone native archives and NativeAOT .NET tool
packages for Linux, Windows, and macOS on x64 and Arm64. See
[installation instructions](../../../guides/remote-management/README.md#install-as-a-net-tool)
for installing from the fork's workflow artifacts with the .NET 10 SDK or later.

Set `OC_CONFIG_HOME` to an absolute directory to isolate contexts, caches, and Unix file credentials for testing. The default installation keeps the shared token-file location described above. `oc login --no-browser` prints the PKCE login URL instead of launching a browser; open it on the same computer as the CLI.

Generate shell completion with `oc completion --shell bash|zsh|fish|pwsh`. The generated script uses the CLI's cached command tree; no separate suggestion service is required.

## Raw API and documentation search

`oc api invoke` is the escape hatch for an authenticated endpoint not projected as a dynamic command:

```bash
oc api invoke GET /api/example
```

The CLI maintains a local cache of the search index published by `docs.orchardcore.net`:

```bash
oc docs update
oc docs search "content definition"
oc docs show <result-id>
```

Documentation is treated as untrusted text and is never executed.

## Compatibility

The protocol has an independent semantic version. The CLI rejects unsupported major versions and warns when a server uses a newer compatible minor version. Use the manifest compatibility range rather than the Orchard Core product version when deciding whether a CLI binary can manage a tenant.

Dynamic command names and operation IDs are public compatibility surfaces. Feature authors should preserve them or provide aliases when an HTTP route changes.

## Contribute a management command

Reference `OrchardCore.RemoteManagement.Abstractions` from a feature that owns a management API. Keep the endpoint's standard OpenAPI operation ID, tags, parameters, request schema, responses, summary, and description authoritative, then add only the CLI-specific projection:

```csharp
endpoints.MapGet("/api/example/widgets", ListWidgetsAsync)
    .WithName("ListWidgets")
    .WithSummary("Lists widgets.")
    .WithCliCommand(new CliOperationMetadata(["widget"], "list")
    {
        Capability = "example.widgets",
    });
```

The Remote Management OpenAPI transformer emits this metadata as `x-oc-cli`. Use `Arguments` for positional ordering, `InputMode` for complex bodies, `DefaultJsonBody` when a command should send a default body if none is supplied, `RequiresConfirmation` for destructive operations, aliases for compatibility, and `TableColumns` for optional table output.

Register an `IRemoteManagementCapabilityProvider` when the feature also needs to report a versioned capability in the authenticated manifest. CLI metadata describes discoverability only; endpoint authorization remains mandatory.

## Endpoint and credential boundaries

Use the exact externally reachable tenant URL when adding a context, including
its path prefix. HTTPS is required; HTTP is accepted only for local loopback
development. Embedded URL credentials and fragments are rejected. The
bootstrap manifest must identify that same tenant, and authenticated API and
OpenAPI requests must remain inside its origin and path prefix. OpenID
endpoints must remain on the advertised authority's origin. Automatic HTTP
redirects are disabled, including redirects from token endpoints; fix the
configured public URL when a proxy returns a redirect.

Credentials are keyed by context name, exact tenant URL, authority, and client
ID. Reusing a context name for another tenant is rejected: choose a different
name or explicitly delete the old context. This also separates credentials
when two local configuration directories use the same context name. Older
name-only credential entries are not reused; sign in once after upgrading.
Unix credentials remain shared owner-only files under `~/.orchardcore/credentials`,
so ordinary CLI commands do not prompt for operating-system keychain access.
