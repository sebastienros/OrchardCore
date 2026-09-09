# Orchard Core CLI

`oc` manages Orchard Core tenants through their management APIs. Discover
commands, authenticate interactively, and manage content, media, themes,
templates, and tenant settings from your terminal.

## Install a development build

Download your platform's `oc-tool-<rid>` artifact from the fork's
[Remote management CLI workflow](https://github.com/sebastienros/OrchardCore/actions/workflows/remote_cli.yml).
Extract it into a directory such as `oc-packages`. Keep both `.nupkg` files
together and use the version shown in the job summary:

```sh
dotnet tool install --global OrchardCore.Cli --add-source ./oc-packages --version <version>
oc --version
oc
```

Installation requires the .NET 10 SDK or later. The installer selects the
native executable for your platform. Running that executable does not require
a separately installed .NET runtime. Native packages are provided for Linux,
Windows, and macOS on x64 and Arm64.

Use `dotnet tool update` with the same package, source, and a newer version to
upgrade, or `dotnet tool uninstall --global OrchardCore.Cli` to remove it.

Standalone downloads are also available for computers without the SDK.
These development packages are distributed as workflow artifacts, not through
NuGet.org.

## Get started

Follow the [remote management guide](https://github.com/sebastienros/OrchardCore/blob/sebros/remote-tenant-cli-plan/src/docs/guides/remote-management/README.md)
to prepare a tenant and authenticate. Run `oc --help` to discover commands
available in the local cache, or `oc doctor` to inspect local configuration.
