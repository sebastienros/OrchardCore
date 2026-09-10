# Orchard Core CLI

`oc` creates local Orchard Core sites and manages tenants through their APIs. Discover
commands, authenticate interactively, and manage content, media, themes,
templates, and tenant settings from your terminal.

## Install a development build

Each successful push build in the fork publishes to
[the Orchard Core Feedz feed](https://f.feedz.io/sebastienros/orchardcore/nuget/index.json).
Use the exact version from the workflow's **Published to Feedz** summary:

```sh
dotnet tool install --global OrchardCore.Cli --add-source https://f.feedz.io/sebastienros/orchardcore/nuget/index.json --version <version>
oc --version
oc
```

The [workflow](https://github.com/sebastienros/OrchardCore/actions/workflows/remote_cli.yml)
also provides `oc-tool-<rid>` artifacts. To install a downloaded artifact, keep
both `.nupkg` files together and replace the source above with `./oc-packages`.

Installation requires the .NET 10 SDK or later. The installer selects the
native executable for your platform. Running that executable does not require
a separately installed .NET runtime. Native packages are provided for Linux,
Windows, and macOS on x64 and Arm64.

Use `dotnet tool update` with the same package, source, and a newer version to
upgrade, or `dotnet tool uninstall --global OrchardCore.Cli` to remove it.

Standalone downloads are also available for computers without the SDK.
These development packages are available on Feedz and as workflow artifacts;
they are not published to NuGet.org.

## Get started

Create and initialize a local CMS with the template embedded in this CLI build:

```sh
oc install ./MyOrchardSite --site-name "My Orchard Site" --email admin@example.com --run
```

Enter the administrator password at the masked prompt. This uses SQLite and
the SaaS setup recipe, then starts the site at `https://localhost:5001`.
For local HTTPS, prepare the certificate with `dotnet dev-certs https --trust`.
Multiple listen addresses use a quoted list, for example
`--urls "https://localhost:5001;http://localhost:5000"`.
Omit `--run` to stop after setup. Local installation requires the matching
.NET SDK (currently .NET 10); `oc doctor` reports whether it is available.
The template is embedded; restoring the site's dependencies still requires
NuGet access or a populated package cache. Nuget.org is added by default, and
parent/user NuGet sources remain available. For this fork's temporary previews,
configure Feedz in an inherited NuGet configuration or add
`--source https://f.feedz.io/sebastienros/orchardcore/nuget/index.json`.
Use `--clear-sources` to ignore inherited package sources and retain only
nuget.org plus any explicit `--source`.
`--site-time-zone` takes an IANA/TZDB ID such as `Europe/Paris` or
`America/Los_Angeles` (default `UTC`). Run `oc install --help` for database,
recipe, URL, and secret-input options.

Follow the [remote management guide](https://github.com/sebastienros/OrchardCore/blob/sebros/remote-tenant-cli-plan/src/docs/guides/remote-management/README.md)
to prepare a tenant and authenticate. Run `oc --help` to discover commands
available in the local cache, or `oc doctor` to inspect local configuration.

The default `--output auto` uses human-readable messages in a terminal and JSON
when redirected to a pipe or file. In a terminal, changes report completion and
useful details with full setup URLs, and lists remain tables. Use `--output json`
explicitly in scripts, or `--output human` to keep readable messages when
redirecting. Other formats are `table`, `csv`, `tsv`, `yaml`, `toml`, and `none`.

For device authorization across separate processes, use `oc login device start`,
`oc login device show <session-id>`, and `oc login device wait <session-id>`.
Each returns one JSON result with `--output json`; `start` and `show` support
opt-in `--qr always` PNG output. See the
[device login guide](https://github.com/sebastienros/OrchardCore/blob/sebros/remote-tenant-cli-plan/src/docs/guides/remote-management/README.md#start-and-complete-device-login-separately).

## GraphQL

With GraphQL enabled on the selected tenant:

```sh
oc graphql execute --query '{ __typename }'
oc graphql execute --file query.graphql --variables-file variables.json
oc graphql schema --output json > graphql-schema.json
```

These built-in GraphQL commands call GraphQL directly, reuse the current context/login,
and need no OpenAPI refresh. GraphQL errors return exit code 4 while preserving
partial data and errors in the JSON response. Human output shows readable errors
on stderr and any partial data on stdout. See the
[GraphQL CLI reference](https://github.com/sebastienros/OrchardCore/blob/sebros/remote-tenant-cli-plan/src/docs/reference/modules/Apis.GraphQL/README.md#use-graphql-from-the-cli)
for permissions, stdin, operation names, custom endpoint paths, and mutations.
