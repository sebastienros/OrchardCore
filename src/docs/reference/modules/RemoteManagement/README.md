# Remote Management (`OrchardCore.RemoteManagement`)

The Remote Management module exposes a versioned management protocol and tenant-specific OpenAPI document for the Orchard Core command-line interface (`oc`). Enabled Orchard Core features contribute resource commands and JSON Schemas, so the commands available for one tenant may differ from another tenant.

The CLI uses OpenAPI as its management protocol. GraphQL remains available for application queries but is not required by `oc`.

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

For automated deployments, the **Orchard Core Remote Management** recipe configures the same requirements. When applying the recipe to a tenant not named `Default`, set the `Tenant` value in its `OpenIdValidationSettings` step to the target tenant name.

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

Browser login uses OAuth authorization code with PKCE and a temporary loopback listener. Credentials are renewed silently with refresh tokens stored in Windows Credential Manager, macOS Keychain, or Linux Secret Service.

For a terminal without a browser, use device authorization:

```bash
oc login --grant device
```

For automation, register a confidential OpenID application with the minimum required roles and scopes. Set `OC_CLIENT_ID` and `OC_CLIENT_SECRET` for dynamic commands and initial API discovery:

```bash
export OC_CLIENT_ID=orchard-automation
export OC_CLIENT_SECRET='<secret>'
oc content-item list
```

For `oc login` and `oc api invoke`, the secret can instead be named with `--client-secret-env` or read from standard input with `--client-secret-stdin`. Client-credential tokens and secrets are not persisted. Implicit and password grants are not supported.

Manage multiple tenants with named contexts:

```bash
oc context list
oc context use staging
oc --context production content-item list
oc logout production
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

Direct authentication ensures tenant-local roles and permissions are enforced and newly created content is associated with the authenticated tenant user. Each context stores separate credentials, manifests, and OpenAPI metadata, so `--help` reflects that tenant's enabled features.

## Dynamic commands

The CLI downloads the selected tenant's OpenAPI document and maps operations carrying `x-oc-cli` metadata to noun-and-verb commands:

```text
oc <resource> <verb> [arguments] [options]
```

Examples include:

```bash
oc content items list
oc content items show 4abc...
oc features enable OrchardCore.Media
oc queries execute RecentPosts --body '{ "parameters": {} }'
```

JSON is written to standard output by default. Use `--output table|csv|tsv|yaml|toml|none` when another representation is needed. TOML omits properties whose value is `null`; root arrays and scalar values are emitted under `items` and `value`, respectively. JSON request bodies can come from `--body`, `--body-file`, or `--stdin`. Binary request bodies use `--file` or `--stdin`. Discovered option and argument names use lower kebab-case. List commands use zero-based `--skip` and `--take` options for paging.

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

### Media and static files

Use `media files` for files managed by Orchard's media store. Uploads enforce the media configuration, including allowed extensions and maximum file size:

```bash
oc media folders create --name articles
oc media files upload header.png --path articles --file ./header.png
```

Use `static files` for tenant-owned web assets such as CSS or JavaScript. These commands are available when the `OrchardCore.Tenants.FileProvider` feature is enabled and operate on the tenant's static-file root:

```bash
oc static files upload styles/site.css --file ./site.css
oc static files list --path styles
oc static files show styles/site.css
```

Paths are relative to their respective roots. Use `--stdin` instead of `--file` to stream either kind of upload from standard input.

Every static and discovered command supports `--help`. OpenAPI metadata is cached by context and ETag to reduce discovery requests. Use `oc api refresh --force` to bypass the cache, and `oc api compatibility` or `oc doctor` to diagnose protocol and local-environment problems.

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
