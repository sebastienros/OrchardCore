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
the SaaS setup recipe, then starts the site at `http://localhost:5000`.
Omit `--run` to stop after setup. Local installation requires the matching
.NET SDK (currently .NET 10); `oc doctor` reports whether it is available.
The template is embedded; restoring the site's dependencies still requires
NuGet access or a populated package cache. Run `oc install --help` for database,
recipe, URL, and secret-input options.

Follow the [remote management guide](https://github.com/sebastienros/OrchardCore/blob/sebros/remote-tenant-cli-plan/src/docs/guides/remote-management/README.md)
to prepare a tenant and authenticate. Run `oc --help` to discover commands
available in the local cache, or `oc doctor` to inspect local configuration.
