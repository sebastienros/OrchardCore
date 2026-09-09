# Manage your first tenant with `oc`

The Orchard Core CLI brings tenant management to your terminal. Sign in once,
select a tenant, and discover the commands that its enabled features provide.
This walkthrough starts with a read-only content listing, then creates and
validates an unpublished article.

## 1. Build the CLI

From a checkout containing `src/OrchardCore.Cli`, with the .NET SDK selected by
`global.json`, publish a native executable:

```bash
dotnet publish src/OrchardCore.Cli -c Release -r osx-arm64 -o artifacts/oc
./artifacts/oc/oc version
```

Replace `osx-arm64` with your runtime identifier: `osx-x64`, `linux-x64`,
`linux-arm64`, `win-x64`, or `win-arm64`. Native publishing also requires the
platform's native compiler toolchain. Windows produces `oc.exe`.
Add the output directory to your `PATH`; the examples below use `oc`.

For development without native compilation, use
`dotnet run --project src/OrchardCore.Cli -- <command>`.
These instructions build from source; they do not assume a published tool or
binary release is available.

## 2. Prepare the tenant

Sign into the tenant's administration dashboard as an administrator. Enable
**Remote Management** under **Configuration → Features**, then open
**Settings → Remote Management**. Select **Configure Remote Management** or
**Repair configuration**. The page should report that the tenant is ready.

![Remote Management settings showing all nine requirements configured](images/readiness.png)

This configures the management scope, the public `orchardcore-cli` application,
PKCE, device authorization, refresh tokens, and local token validation.
The action preserves unrelated OpenID settings. For automation, the Remote
Management recipe performs the same configuration for the current tenant.

Your identity needs **Access remote management API** and the permissions for
the resources you will use. A production automation account should receive
only the permissions its job requires.

## 3. Save the exact tenant URL

```bash
oc context add tutorial https://cms.example.com/news --current
oc context list
```

Replace the URL with your tenant's public base URL, including its path prefix.
Do not use the admin or login URL. A context named `tutorial` now selects that
one tenant. Commands use it until you select another context.

Remote connections require HTTPS. HTTP is accepted for loopback development,
for example `http://127.0.0.1:5000/news`. To switch to another tenant URL, create
a new context name; existing credentials cannot follow a changed tenant URL.

## 4. Sign in once

```bash
oc login
```

The CLI opens the tenant's login page in your browser. Sign in as the intended
tenant-local user and approve the request. Return to the terminal to verify
that login completed.

![Tenant login form used for browser and device authorization](images/login.png)

On the authorization page, verify that the application is **Orchard Core CLI**
and select **Allow access**. The requested scopes include management access and
`offline_access`, which allows later commands to refresh your login silently.
Choose **Cancel** if you did not start this request. The page follows the
tenant's login theme; this example uses the default admin theme.
See [customizing authorization pages](../../reference/modules/OpenId/README.md#customizing-authorization-pages)
to apply your site's branding.

![Browser authorization page asking for consent to Orchard Core CLI](images/consent.png)

The callback page says authorization was received. The terminal then reports
`grantType: browser` and the tenant issuer when the token exchange succeeds.

If the terminal is on a remote computer or has no browser, use:

```bash
oc login --grant device
```

Open the displayed verification URL in your browser, check the tenant and
application, and approve the displayed code. The waiting CLI completes login.

![Device authorization page showing the matching user code and requested permissions](images/device-consent.png)
Use `oc login --no-browser` to copy a browser-flow URL manually **on the same
computer**; its callback listens only on that computer's loopback interface.

Later commands reuse the login and refresh expiring tokens automatically.
On macOS and Linux, tokens remain in shared, owner-only files under
`~/.orchardcore/credentials`: directory mode `0700`, file mode `0600`.
They are plaintext and protected by your operating-system account permissions.
Routine commands do not open the OS secret store or prompt for authentication.
Windows uses Windows Credential Manager.

## 5. Run your first command

```bash
oc content items list --take 10
```

In a terminal, the result is a table. An empty result means there are no items
matching the default published-content filter; it does not mean login failed.
To see drafts:

```bash
oc content items list --status draft --take 10
```

`--skip` is a zero-based offset. For the next page, use `--skip 10 --take 10`.
Capture a complete, stable response for scripts with explicit JSON output:

```bash
oc content items list --status draft --output json > drafts.json
```

The default `auto` output selects tables in a terminal and JSON when redirected.
Tables shorten long cells for readability; use JSON when every value matters.
Other formats include `csv`, `tsv`, `yaml`, `toml`, and `none`.

## 6. Discover before writing

```bash
oc --help
oc content items --help
oc content types schema --output json
oc content items schema Article --output json
```

The last command requires an existing `Article` content type. If it is absent,
create the simple type below. If it already exists, inspect its schema and
adapt the article payload to that model instead of replacing the definition.
This example needs the Contents, Content Types, and Title features.

Save this as `article-type.json`:

```json
{
  "name": "Article",
  "displayName": "Article",
  "settings": {
    "ContentTypeSettings": {
      "creatable": true,
      "listable": true,
      "draftable": true,
      "versionable": true
    }
  },
  "parts": [
    { "name": "TitlePart", "partName": "TitlePart", "settings": {} }
  ]
}
```

```bash
oc content types create --body-file article-type.json
oc content items schema Article --output json
```

Definition DTOs use camelCase. Content-item parts and properties preserve their
schema casing, including `ContentType`, `TitlePart`, and `Title`.

## 7. Create and check a draft

Save this as `article.json`:

```json
{
  "ContentType": "Article",
  "TitlePart": { "Title": "My first CLI article" }
}
```

```bash
oc content items validate --body-file article.json
oc content items create-draft --body-file article.json --output json
```

Validation should return `isValid: true`. Copy the returned `ContentItemId`
into the commands below:

```bash
oc content items show <content-item-id> --version draft --output json
oc content items render <content-item-id> --version draft
oc content items validate-update <content-item-id> --body-file article.json
```

The item remains unpublished. `validate-update` validates a detached candidate;
it does not save it or run update workflows. Reading `--version draft` never
creates a draft; it returns `404` when no draft exists. To publish intentionally:

```bash
oc content items publish <content-item-id>
```

Do not retry creation blindly after a connection failure: a request may have
succeeded before its response was lost. Read back the state first. Consult the
[content API retry contract](../../reference/api/content-items/README.md) for
updates, lifecycle operations, and deletes.

## 8. Use contexts and secrets deliberately

Use `oc --context production <command>` to make the destination explicit.
Destructive commands marked for confirmation ask in an interactive terminal;
noninteractive callers must supply `--yes`. `oc context clear --force` removes
all saved contexts and their credentials, so use it only for an intentional reset.

For unattended jobs, register a separate **confidential** OpenID application
with client-credentials flow, the `orchardcore.management` scope, and minimal
roles. Inject `OC_CLIENT_ID` and `OC_CLIENT_SECRET` through your CI secret
facility, then run ordinary commands:

```bash
oc --context production content items list --output json
```

Do not use the public `orchardcore-cli` application for this grant.
Client credentials and their tokens are not persisted. For user provisioning,
use `--password-env`, `--password-file`, or `--password-stdin` with individual
property options; alternatively supply the entire request in a protected
`--body-file` or `--stdin`. Do not combine a complete body with property options.

## Troubleshooting and next steps

| Symptom | Next step |
| --- | --- |
| Command missing from help | Check the selected context and enabled features; run `oc api refresh --force` |
| `401` | Verify the context and sign in again, or check injected client credentials |
| `403` | Check both management access and the resource-specific tenant permissions |
| `404` for a draft | Verify the ID and whether a draft exists; inspect the latest version |
| Validation failure | Correct the named property using the live schema and exact casing |
| Local storage or cache problem | Run `oc doctor` to inspect paths and storage availability |
| Protocol mismatch | Run `oc api compatibility` and use a compatible CLI version |

Help and shell completion use cached metadata without contacting the server.
After feature changes, successful mutations expire discovery caches so the next
online command can refresh them. `doctor` checks local state, not connectivity.
For Bash completion, run `oc completion --shell bash > oc-completion.bash` and
source the file. Other supported shells are `zsh`, `fish`, and `pwsh`.

Sign out of the current context with `oc logout`. It attempts token revocation
and removes stored human credentials on success. Disconnecting a terminal or
closing a shell does not log you out.

Continue with the [complete management API reference](../../reference/api/README.md),
[CLI reference](../../reference/modules/RemoteManagement/README.md), and
[verification record](../../reference/modules/RemoteManagement/review.md).
Agent skills and plugin packaging are documented in
[the repository test toolkit](https://github.com/sebastienros/OrchardCore/tree/sebros/remote-tenant-cli-plan/.scripts/remote-management).
