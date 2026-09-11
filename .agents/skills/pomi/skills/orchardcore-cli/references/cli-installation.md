# Install or update Pomi

Apply the [shared operating rules](shared-rules.md). The NuGet package ID is
`OrchardCore.Cli`; the installed command is `pomi`. Check `pomi --version` first
and reuse a suitable installation unless installation or an update is needed.
Installing this native .NET tool requires the .NET 10 SDK or later; check
`dotnet --list-sdks`. Running the installed native executable needs no separate
.NET runtime. Installing the agent plugin does not install the executable.

## Find the latest published version

Use the user's registered NuGet sources, including a globally configured feed:

```bash
dotnet package search OrchardCore.Cli --prerelease --format json
```

Check `problems` for feed errors, then read `latestVersion` from the intended
`sourceName` and package whose `id` is exactly `OrchardCore.Cli`. Ignore platform-suffixed IDs such as
`OrchardCore.Cli.osx-arm64`: the tool installer selects the native package.
If there is no matching package, report that instead of inventing a version.
Add `--exact-match` when you need the list of available versions for a specific
build; that changes the response to individual versions rather than a single
`latestVersion`. Do not sort version strings lexicographically.

`dotnet tool search OrchardCore.Cli --prerelease --detail` is suitable for
NuGet.org packages. It does **not** search other registered feeds; use
`dotnet package search` for them. This fork's previews currently use
`https://f.feedz.io/sebastienros/orchardcore/nuget/index.json`. If Feedz is not
registered, append `--source` with that URL to the package query. An explicit
`--source` restricts that query to the selected feed without changing NuGet
configuration. Feedz is temporary, not the permanent default for Orchard Core.
Do not infer a version from an old example or the newest workflow run, whose
packages may not have finished publishing. Respect a requested exact version.

## Install or update a preview

For the latest available version, including prereleases, from registered feeds:

```bash
dotnet tool install --global OrchardCore.Cli --prerelease
pomi --version
```

If the global tool is already installed and the user wants an update:

```bash
dotnet tool update --global OrchardCore.Cli --prerelease
```

If the required feed is not registered, append
`--add-source https://f.feedz.io/sebastienros/orchardcore/nuget/index.json` for a
fork preview. This adds a source for that command without registering it globally.
No extra source argument is needed when the feed is already configured.

Without `--prerelease`, an unpinned installation selects a stable version and
can miss this fork's previews. For reproducibility or to match a server/build,
replace `--prerelease` with `--version <version>` using the exact discovered or
requested version. Latest can change between discovery and installation;
check `pomi --version` afterward and report the version actually installed.
All configured feeds may participate in version selection. Keep
`--prerelease` only when previews are wanted.

If `pomi` is not found after installation, use the tool directory reported by
the installer (normally `$HOME/.dotnet/tools` on macOS/Linux) in `PATH`, or invoke
its executable directly. Preserve an existing project-local tool installation;
use `--local` instead of `--global` and `dotnet tool run pomi -- <arguments>`.

Continue with [authentication and contexts](authentication.md), or
[local CMS installation](installation.md) to create a new site. The CLI embeds
its matching site template; a newer CLI does not update an existing server.
