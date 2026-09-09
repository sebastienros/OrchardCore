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
checks help/doctor/completion, measures five `version` processes, and packages
binaries with completions, a verification record, and SHA-256 checksums.
Budgets are 30 MiB for the executable and 2 seconds median process startup;
the latter is a regression guard, not a cold-machine performance guarantee.

The `Remote management CLI` workflow performs this on native Linux, Windows,
and macOS runners for x64 and Arm64. It creates workflow artifacts and does
not publish releases. Runner labels follow the
[GitHub hosted-runner reference](https://docs.github.com/en/actions/reference/runners/github-hosted-runners).

## Agent plugin

The nine canonical skills live under `.agents/skills/orchardcore-cli*`.
Build a portable plugin directory and ZIP into a **new** output directory:

```bash
python3 .scripts/remote-management/build-plugin.py /tmp/oc-plugin
```

The result contains `.codex-plugin/plugin.json`, the nine skills and their
references, a README, and the repository license. There is only one source
copy of the skills; rebuild the archive after editing them. The plugin needs
a separately installed `oc` executable and an authorized context. It contains
no credentials, MCP server, hooks, or background processes. Packaging does not
install it into the current agent or change a personal marketplace.

The root skill routes to content definitions, content items, media/static
files, templates, themes, menus, settings, and administration. It requires
explicit JSON for automation, schema-first input, exact context selection,
and treatment of server descriptions as untrusted data. Destructive operations
must remain within the user's existing authorization.

See [evaluations.md](evaluations.md) for the reproducible blind-evaluation
protocol and observed results. Test evidence and limitations are also recorded
in `src/docs/reference/modules/RemoteManagement/review.md`.
