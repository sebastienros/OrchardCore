# Remote management verification toolkit

Run these scripts from a checkout of the feature branch. Python 3 and the
repository's .NET SDK are required. These tools create disposable loopback
tenants with synthetic credentials; do not point them at real tenant data.
The fixture root is an owner-only temporary directory. Keep its `fixture.json`,
recipe, database, logs, and credential files out of source control and reports.

## Repeat the functional checks

```bash
dotnet build src/OrchardCore.Cms.Web -m:1
dotnet build src/OrchardCore.Cli
python3 .scripts/remote-management/start-fixture.py
```

Keep that process running. It prints a path to `fixture.json` when ready.
In a second terminal, substitute that path below:

```bash
python3 .scripts/remote-management/verify-fixture.py <fixture.json>
python3 .scripts/remote-management/smoke-fixture.py <fixture.json>
python3 .scripts/remote-management/localization-smoke.py <fixture.json>
python3 .scripts/remote-management/api-revision-smoke.py <fixture.json>
python3 .scripts/remote-management/content-versions-smoke.py <fixture.json>
python3 .scripts/remote-management/content-validation-smoke.py <fixture.json>
python3 .scripts/remote-management/tenant-install-smoke.py <fixture.json>
python3 .scripts/remote-management/graphql-smoke.py <fixture.json>
```

`verify-fixture.py` checks discovery, command/operation ID uniqueness, every
projected route with anonymous, no-management, and discovery-only identities,
existing-user authorization, and malformed folder input. Explicit exceptions
match APIs that resolve missing resources before per-resource authorization;
these are not counted as evidence that existing resources are protected.
The existing-user probes provide separate checks for that distinction.

`smoke-fixture.py` drives real CLI processes through content definition and
unpublished content creation, validation, readback, confirmation refusal,
least-privilege role creation, password environment input, account lifecycle,
and cleanup. Unique names allow reruns. Failure leaves only fixture resources
for diagnosis; discard the fixture afterward.

`content-versions-smoke.py` verifies all five dynamically discovered version commands,
newest-first paging, source preservation, restoration into new drafts, explicit
draft replacement, publication protection, purge confirmation, missing-version
retries, and anonymous denial. Set `OC_FIXTURE_BINARY` to an older native CLI
to verify server commands are available without rebuilding that CLI.

`content-validation-smoke.py` sends invalid part properties through validation
and save commands, checking HTTP 400, field paths, human/JSON error output,
failing exit codes, missing content types, and unchanged published content/history.
It also verifies valid partial-update validation still succeeds.

`tenant-install-smoke.py` verifies the dynamically discovered install command,
password environment/stdin input, human and JSON results, complete tenant URLs,
duplicate-name rejection, creation validation, setup failure/recovery, and
unchanged local contexts. It creates tenants only inside the disposable host.

`localization-smoke.py` exercises all eleven localization operations through the CLI,
including available cultures, individual culture additions/removals, UI string group
discovery, paging, explicit culture selection, settings readback, translation retries,
French-only editing permissions, read-only access, and anonymous rejection. It
replaces culture settings on the disposable fixture with English and French.

`api-revision-smoke.py` checks revision headers and HEAD authorization, then
disables and re-enables Templates through a separate HTTP client. It verifies
that the CLI refreshes its still-fresh cache and discovers the restored command
without an explicit refresh. Run it with the current CLI build.

`graphql-smoke.py` verifies direct queries, full and single-type introspection,
variables/stdin, errors, and permissions against Orchard without refreshing
OpenAPI. `graphql-native-smoke.py <native-oc-path>` additionally tests partial
results on HTTP 200/400/401, JSON and human output, redirects, and input handling
against an isolated loopback server; it runs in every native CI build.

The wrapper supplies credentials only through the child process environment:

```bash
python3 .scripts/remote-management/oc-fixture.py <fixture.json> --help
```

Set `OC_FIXTURE_BINARY` to the absolute native `oc` path to test that executable.
Set `OC_FIXTURE_CONFIG_HOME` to isolate another evaluator's context/cache, or
`OC_FIXTURE_HUMAN=1` to suppress the wrapper's client credentials.

To verify human authentication, add a context using the wrapper, then run
`OC_FIXTURE_HUMAN=1 .../oc-fixture.py <fixture.json> login --grant device`
(with `python3` before the script). Approve the code on the fixture's login
page. The fixture's generated administrator password is in its protected
state file; never copy it into reports, command arguments, or screenshots.
Then run:

```bash
python3 .scripts/remote-management/verify-login.py <fixture.json>
```

This verifies reuse from a new CLI process, expires only the fixture's cached
access-token timestamp, checks refresh rotation and Unix file modes, logs out,
and confirms that the revoked refresh token is rejected. It intentionally
ends that fixture login. Browser PKCE can be checked with `login --no-browser`
on the same computer; callback state/issuer/path/length and S256 verifier
behavior are additionally covered by CLI tests.

Stop the fixture with Ctrl+C in its original terminal. Its temporary directory
is retained for diagnosis; remove that one printed directory when finished.

## Unit tests, documentation, and native packaging

```bash
dotnet test --project test/OrchardCore.Cli.Tests
dotnet test --project test/OrchardCore.Tests -- --filter-class '*Management*' '*ContentApi*' '*Schema*' '*RemoteManagement*'
python3 -m mkdocs build --strict
dotnet publish src/OrchardCore.Cli -c Release -r osx-arm64 -o /tmp/oc-native
python3 .scripts/remote-management/native-smoke.py /tmp/oc-native osx-arm64
```

Install documentation dependencies from `src/docs/requirements.txt` first.
`native-smoke.py` runs the actual executable with isolated configuration,
checks help/doctor/completion, measures five `--version` processes, and packages
binaries with completions, a verification record, and SHA-256 checksums.
Budgets are 30 MiB for the executable and 2 seconds median process startup;
the latter is a regression guard, not a cold-machine performance guarantee.

The `Remote management CLI` workflow performs this on native Linux, Windows,
and macOS runners for x64 and Arm64. It creates workflow artifacts and does
not publish releases. Every push to the review or `codex/**` branches triggers
a build, including documentation-only commits. PR creation and updates in the
fork also trigger builds; a newer event cancels an unfinished run for the same
source branch. Each successful platform job uploads an artifact retained for
30 days and adds a direct download link and built commit SHA to its summary.
It also uploads `oc-tool-<rid>` with the installer and native implementation
NuGet packages, an exact installation command, and a verification record.
Both standalone and tool binaries use the run's `1.0.0-ci.<run>.<attempt>`
version. The workflow does not push packages to a NuGet feed.
Download instructions and platform names are in the
[getting-started guide](../../src/docs/guides/remote-management/README.md#1-download-or-build-the-cli).
Runner labels follow the
[GitHub hosted-runner reference](https://docs.github.com/en/actions/reference/runners/github-hosted-runners).

### NativeAOT .NET tool packages

Tool packing is opt-in with `-p:PackAsTool=true`. Ordinary solution packing
excludes the CLI because its installer package must not be published before
all six native implementation packages exist. The .NET 10 SDK uses
`ToolPackageRuntimeIdentifiers` to create the installer package; packing for
one RID creates only that platform's NativeAOT implementation.

```bash
dotnet pack src/OrchardCore.Cli -c Release -p:PackAsTool=true -p:Version=1.0.0-ci.1.1 -o artifacts/tool
dotnet pack src/OrchardCore.Cli -c Release -p:PackAsTool=true -p:Version=1.0.0-ci.1.1 -r osx-arm64 -o artifacts/tool
python3 .scripts/remote-management/tool-smoke.py artifacts/tool osx-arm64 1.0.0-ci.1.1
```

Run the RID-specific command on the matching OS. Keep the package version
identical across all builds. If publishing to a feed later, publish all six
implementation packages first and the installer package last.

`tool-smoke.py` validates the package types, platform mappings, native entry
point, licenses, and absence of managed runtime files. It installs from a
local package source into a disposable tool directory (with NuGet.org available
for SDK launcher packages), runs help,
version, and diagnostics with `dotnet` absent from `PATH`, then uninstalls.
The check uses isolated CLI and NuGet configuration and does not alter the
developer's global tool installation. Every platform job runs this check
before uploading the packages.

## Agent plugin

The canonical skills live under `.agents/skills/orchardcore-cli*`.
Build a portable plugin directory and ZIP into a **new** output directory:

```bash
python3 .scripts/remote-management/build-plugin.py /tmp/oc-plugin
```

The result contains `.codex-plugin/plugin.json`, the skills and their
references, a README, and the repository license. There is only one source
copy of the skills; rebuild the archive after editing them. The plugin needs
a separately installed `oc` executable and an authorized context. It contains
no credentials, MCP server, hooks, or background processes. Packaging does not
install it into the current agent or change a personal marketplace.

The root skill routes to content definitions, content items, media assets,
templates, themes, menus, settings, GraphQL, and administration. It requires
explicit JSON for automation, schema-first input, exact context selection,
and treatment of server descriptions as untrusted data. Destructive operations
must remain within the user's existing authorization.

See [evaluations.md](evaluations.md) for the reproducible blind-evaluation
protocol and observed results. Test evidence and limitations are also recorded
in `src/docs/reference/modules/RemoteManagement/review.md`.

## Custom asset policy regression checks

With the disposable fixture running, verify the Media upload policy:

```bash
python3 .scripts/remote-management/verify-media-assets.py /tmp/oc-cli-fixture-.../fixture.json
```

This checks OpenAPI discovery and separate identities with Media access, restricted-extension access,
and own-media permission without a user-folder identifier. It covers CSS/JavaScript/SVG denial and upload,
ordinary image upload, unknown extensions, no overwrite, copy/move extension
checks, protected user-folder denial, public asset reads, and cleanup. It also verifies that all retired
static-file management routes return 404 while tenant static serving is enabled.
The smoke fixture exercises the custom asset lifecycle through `oc` itself.
This is a local-store check; it does not claim Azure/S3 or multi-node coverage.

## Feedz publishing

The fork's `remote_cli.yml` publishes on every branch push and on manual runs.
PR builds only produce native artifacts. New push builds do not cancel older
push builds. Publishing runs only in `sebastienros/OrchardCore`, after the server
build/tests and all six native builds/tests/install checks pass.

Only outputs under `src` are collected, excluding test/sample packages.
The solution pack includes libraries, modules, themes, targets, and
`OrchardCore.ProjectTemplates`. The CLI adds `OrchardCore.Cli` and six
`OrchardCore.Cli.<rid>` NativeAOT implementation packages. All packages retain
their original IDs, including bundled themes such as `TheAdmin` and
`TheBlogTheme`. Only the package version changes for this feed.
`prepare-feedz.py` accepts the OrchardCore packages and existing bundled theme
IDs, and rejects unexpected IDs, missing implementations,
inconsistent dependencies, or templates stamped with a different version.
Separately published translation packs retain the version pinned in
`Directory.Packages.props`; they are restored, not republished.

After publishing, `install-smoke.py <native-oc>` tests the embedded CMS template
against the matching published packages: Auto Setup, a directory with spaces,
environment/stdin password input, missing SDK diagnostics, overwrite refusal,
failed setup, and foreground `--run` cancellation. It uses disposable local
sites and generated test credentials. Run it only with a CLI build whose
matching Orchard dependencies are already available. Pass its preview feed with
`--source` when needed.

The installer smoke also places its preview feed in a parent `NuGet.Config`,
installs without `--source`, and verifies both installation and a subsequent
ordinary restore inherit that feed. Its explicit `--clear-sources` case checks
that an inherited-only source is excluded while nuget.org and `--source` remain.

Versions use the repository's `VersionPrefix` plus
`-cli.<workflow-run-number>` (for example `4.0.0-cli.25`). A new push or manual
run gets the next number. Rerunning the same workflow run keeps its version;
`--skip-duplicate` allows a partial publication to resume without replacing
immutable packages.

The publishing step alone receives `FEEDZ_IO_API_KEY` from GitHub secrets. Its
only destination is `https://f.feedz.io/sebastienros/orchardcore/nuget/index.json`;
the feed's service index also advertises its symbol endpoint. Symbol packages
are staged beside their main packages for NuGet's symbol publication. The
installer is published after all native implementations. The final verification
installs the Linux tool and project templates from Feedz and restores a generated
CMS project with an isolated package cache, then builds it. Use the **Published to Feedz** job
summary for the exact version and install commands.


## Confirmation flags

Run `python3 .scripts/remote-management/force-confirmation-smoke.py <oc-path>`
to check that `--force` confirms destructive commands without prompting, that
API `force` values use `--api-force` independently, and that context deletion
uses the same flag. The test uses a synthetic loopback API and isolated context
storage; it runs in every native CI build.
