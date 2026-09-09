# Remote management verification record

This record tracks review of `sebros/remote-tenant-cli-plan` in the
`sebastienros/OrchardCore` fork, starting at `4c28c8dc0`. It distinguishes
implemented behavior, measured verification, and outstanding work.

## Agreed constraints

- Commit and push only to the fork; no upstream issues or pull requests.
- Preserve the shared Unix owner-only credential files. This is an intentional
  usability decision: routine commands must not require keychain prompts.
- Fix flaws in the CLI and new management APIs here. Defer existing platform
  vulnerabilities already covered by upstream PRs.

## Upstream dependencies checked on September 8, 2026

| Existing platform change | Tracking PR | Treatment |
| --- | --- | --- |
| Tenant media roots and recipe write containment | [19840](https://github.com/OrchardCMS/OrchardCore/pull/19840) | Defer platform changes; review new media APIs separately |
| Application static file traversal | [19837](https://github.com/OrchardCMS/OrchardCore/pull/19837) | Defer platform changes; review new static-file APIs separately |
| Liquid authoring permissions and rendered HTML | [19820](https://github.com/OrchardCMS/OrchardCore/pull/19820) | Defer shared rendering changes; verify management endpoint authorization |
| Media folder authorization | [19677](https://github.com/OrchardCMS/OrchardCore/pull/19677) | Avoid duplicating shared authorization changes |

## Baseline

- CLI: 57 tests passed on macOS Arm64 with .NET 10 target framework.
- Broader management/schema/content suite: 1,240 passed, three failed.
  Failures: field-schema generation attempted to annotate a boolean JSON
  Schema; legacy content update version count; duplicate autoroute validation.
- Parser: retain System.CommandLine. It already supports the dynamic tree and
  NativeAOT; no evidence so far warrants a dependency migration.

## CLI security changes

Endpoint checks reject remote cleartext transport, embedded credentials,
foreign origins, sibling tenant paths, and ambiguous encoded path segments.
Automatic HTTP redirects are disabled so token requests cannot replay secrets
to an unvalidated location. Context credentials are bound to the tenant,
authority, client ID, and case-insensitive context name. Unit tests inject a
credential store so they never read or delete the developer's OS credentials.

The security review uses [OAuth security BCP](https://www.rfc-editor.org/rfc/rfc9700.html)
and [OAuth for native apps](https://www.rfc-editor.org/rfc/rfc8252.html) as
reference points; it does not claim protocol conformance from a unit-test pass.


## API regression fixes

- Field schemas now preserve boolean schema semantics when adding descriptions.
- Content validation and HTTP responses share one model updater; invalid content
  no longer receives a success response after the database session was cancelled.
- Draft reads/rendering no longer create drafts. Validation uses detached content
  and does not fire update workflows. Route/body ID mismatches are rejected.
- New static-file management reads/listings apply the same symlink boundary as
  writes. These changes are specific to the new APIs, separate from upstream
  application static-file resolution.
- 32 targeted content, schema, and tenant tests passed after these fixes.

## Additional completed changes

- Terminal output defaults to readable tables; redirection defaults to JSON.
  Explicit `--output json` remains the automation contract. Terminal cells
  escape control characters, while TSV and YAML preserve structured values.
- Help and completion use cached discovery offline. Cache writes are atomic,
  and successful mutations expire discovery. The parser handles inherited
  path parameters and nullable/multiple JSON Schema types. Array options accept
  JSON arrays or comma-separated strings; object options accept JSON objects.
- Browser callbacks require the expected route/state and optional issuer before
  any token exchange, bound line/header sizes, and verify S256 PKCE exchange.
  `--no-browser` supports manually opening the URL on the CLI's computer.
- Client credentials omit human identity/refresh scopes, including `roles`.
  User password inputs use environment/file/stdin sources. Complete bodies
  cannot be combined with individual property/secret options.
- The CLI consumes OAuth access/refresh tokens from validated authority endpoints.
  It does not establish identity from ID-token claims and no longer decodes or
  persists unused ID tokens. This is not a general-purpose OpenID relying party.
- Media-folder creation checks management permission before name processing and
  returns a validation problem for empty names. Static files now support
  permission-protected, confirmed deletion of one file, with convergent retries
  and directory/symlink rejection.
- Features, recipes, users, and roles now advertise their existing capabilities
  in the authenticated manifest. Live verification checks every projected
  operation's capability against that manifest.
- Windows CI exposed transient conflicts between concurrent atomic file
  replacements. Bounded Windows retries and delete-sharing readers preserve
  complete files without deleting/truncating the destination first.

## Measured verification

| Area | Actual evidence |
| --- | --- |
| CLI tests | 91 passed on local macOS Arm64 |
| Management/content/schema suite | 1,250 passed; includes new validation, draft read, static deletion, and path regressions |
| Server build | CMS web host built with zero warnings/errors |
| Live authorization | 120 projected operations; 364 checks across anonymous, no-management, and discovery-only identities plus existing-user probes; no unexpected statuses |
| Live native CLI | 30 commands covering definitions, draft validation/readback, confirmation refusal, users/roles, static upload/inspect/delete, and cleanup |
| Interactive terminal | Native upload produced a table; declining deletion preserved the file; accepting deletion removed it |
| Human login | Real device authorization approved in the browser; a new CLI process reused shared-file credentials, refreshed/rotated silently, and logout revoked the refresh token |
| Browser PKCE | Fresh native `login --no-browser` succeeded through the real login/consent UI and loopback callback on September 9; its stored login also passed separate-process reuse, silent refresh rotation, owner-only file checks, and logout revocation. Callback/S256/state/issuer/denial/route/size unit tests passed |
| Documentation | Full MkDocs build passed with `--strict`; login/readiness screenshots captured from the real local tenant; native `docs update`, search, and show succeeded against the published index |
| Skills/plugin | Nine skills validated, portable plugin manifest validated; four fresh blind agent runs across two models recorded below |

Native build results and executable checksums are emitted by
`.scripts/remote-management/native-smoke.py`. The final local macOS Arm64
measurement was 8,551,888 bytes and 6.1 ms median over five `version`
processes (the first process took 184 ms). This is not a cold-machine or network-operation latency claim.
The six-RID workflow verifies both architectures on Linux, Windows, and macOS;
its first run caught the Windows replacement issue described above.
The final [six-platform run](https://github.com/sebastienros/OrchardCore/actions/runs/34314239210)
passed every job at `1f3119012`: 91 CLI tests per platform, warning-free
NativeAOT publishing, native execution, and archive/checksum generation.
PowerShell completion was exercised through `TabExpansion2`; Unix shell scripts
were syntax-checked where the shell was installed.

After the final tenant was stopped, native cached `content items --help` still
returned the discovered commands without contacting it. All disposable host
processes started for this review were stopped.

## Capability coverage

Every enabled group's route authorization and discovery metadata was probed.
Additional behavioral coverage comes from the targeted C# suite and selected
live workflows; route authorization alone is not a full functional test.

| Capability groups | Additional coverage |
| --- | --- |
| Discovery/authentication | Bootstrap, authenticated manifest, compatibility, all three OAuth grant implementations; live device and client credentials, rotation/revocation |
| Content definitions/items | Live custom part/TextField/type creation, schemas, validation, unpublished drafts, readback/rendering, lifecycle and no-mutation regression tests |
| Templates/media/static files | Blind template validation/create/read/delete and CSS upload/public read; media authorization and invalid folder input; static deletion/path/idempotency tests |
| Users/roles | Live least-privilege role creation, password environment input, user create/read/disable/delete; existing-resource denial probes |
| Features/recipes/queries/workflows | Metadata and authorization probes plus C# tests; blind agents discovered query/activity/workflow schemas; live recipe/workflow execution was not part of their tasks |
| Site/custom settings, home route, themes, tenants | Metadata/authorization and C# suite; no claim of a complete multi-tenant site-build or every deployment/provider combination |

## Discovered command inventory

This fixture enabled all 16 documented management resource groups. Counts are
OpenAPI-projected operations, excluding static CLI commands and automatically
synthesized `schema` commands. Feature-specific commands can differ on another
tenant; inspect its help and schemas before constructing requests.

| Capability | Projected operations | HTTP contract |
| --- | ---: | --- |
| `content-definitions` | 19 | [Reference](../../api/content-definitions/README.md) |
| `content-items` | 14 | [Reference](../../api/content-items/README.md) |
| `custom-settings` | 4 | [Reference](../../api/custom-settings/README.md) |
| `features` | 4 | [Reference](../../api/features/README.md) |
| `home-route` | 1 | [Reference](../../api/home-route/README.md) |
| `media` | 18 | [Reference](../../api/media/README.md) |
| `queries` | 8 | [Reference](../../api/queries/README.md) |
| `recipes` | 3 | [Reference](../../api/recipes/README.md) |
| `roles` | 5 | [Reference](../../api/roles/README.md) |
| `settings` | 3 | [Reference](../../api/settings/README.md) |
| `static-files` | 4 | [Reference](../../api/static-files/README.md) |
| `templates` | 5 | [Reference](../../api/templates/README.md) |
| `tenants` | 9 | [Reference](../../api/tenants/README.md) |
| `themes` | 3 | [Reference](../../api/themes/README.md) |
| `users` | 6 | [Reference](../../api/users/README.md) |
| `workflows` | 14 | [Reference](../../api/workflows/README.md) |

## Blind evaluations

Two fresh agents (`gpt-5.6-sol` and `gpt-5.6-luna`) independently created a
schema-driven Article model and one valid unpublished draft. Both verified
zero published items. Feedback clarified workflow casing and draft semantics.

A second fresh pair used only the packaged skills to validate/create/read/delete
a template and upload/inspect CSS. Both found that static deletion was absent;
the operation was subsequently added and verified by the live smoke test.
The original cleanup limitation is retained in the evaluation record instead
of retroactively marking those runs fully successful. These are four bounded
usability observations, not a measured token-efficiency benchmark.

The reproducible prompts, scope, failures, and outcomes are in
[the evaluation record](https://github.com/sebastienros/OrchardCore/blob/sebros/remote-tenant-cli-plan/.scripts/remote-management/evaluations.md).

## Consent page follow-up

The OpenID authorization server now follows `LoginSettings.UseSiteTheme` for
its access pages, matching the sign-in page's admin-theme default and fallback.
Browser and device consent use readable localized scope labels, retain the raw
scope identifiers, and offer explicit **Allow access** and **Cancel** actions.
The login layout can grow beyond the viewport so long permission lists remain
reachable. The OpenID reference documents theme layout and MVC-view overrides.

Verification on September 9 after this change:

- Web/Razor build: zero warnings or errors; all 30 OpenID tests passed,
  including seven new theme-selection cases.
- All 91 CLI tests passed. The cancellation callback regression also asserts
  that a clear browser response is sent without exchanging an authorization code.
- The rebuilt macOS Arm64 native CLI passed live browser approval and denial,
  device code entry and approval, and separate-process reuse, silent refresh
  rotation, owner-only credential permissions, and logout revocation for both
  successful grants.
- Actual browser and device screenshots replaced/extended the getting-started
  guide. The rendered default admin theme was visually checked in dark mode.
- Native help/completion/packaging smoke checks passed after rebuilding the
  local CLI. The earlier six-platform CI run predates the cancellation-response
  change; it is not a verification of this later change on all platforms.

The consent scope list was subsequently converted to the `OpenIdConsentScopes`
shape. A clean Web/Razor build and live browser/device checks verified the
default binding, a dynamic Liquid override created through `oc templates
create`, and restoration of the default after deleting that override.
The documented Liquid example rendered all five requested scope identifiers
on both consent pages while using the admin theme. Browser approval with the
override and device approval with the restored default both completed login.

## Device login QR follow-up

Device login now supports `--qr auto|always|never`. QRCoder generates the
verification URL locally; terminal rendering preserves the quiet zone and
uses two module rows per text row. The URL and user code remain available
when QR rendering is disabled or cannot fit. Redirected/dumb/non-UTF-8
terminals and `NO_COLOR` disable automatic rendering. The selected verification
URL must satisfy the CLI's trusted-origin and transport policy before either
text or QR instructions are displayed.

All 105 CLI tests passed locally, including independent ZXing decoding of the
actual rendered terminal cells, complete/base verification URL selection,
disabled QR output, oversized/narrow-terminal fallback, and untrusted URL
rejection. Native macOS Arm64 publishing and packaging passed with the encoder
license included. The executable grew from 8,551,888 to 8,668,064 bytes.
Live checks covered automatic QR in a compatible terminal, suppression in a
dumb/NO_COLOR terminal, forced QR with redirected output, and explicit disablement.
QR-enabled device login completed and passed token reuse, refresh rotation,
file permission, and logout checks. Phone-camera scanning was not tested;
the automated decoder checks validate the encoded destination and module layout.

The [six-platform CI run](https://github.com/sebastienros/OrchardCore/actions/runs/34373533845)
at `8aebecaf5` passed 105 tests on every platform, native publishing with warnings
treated as errors, and archive/license/completion smoke checks for Windows,
Linux, and macOS on x64 and Arm64.

## Boundaries and follow-up work

- Existing upstream security PRs listed above remain integration dependencies;
  this review does not assert that the entire platform is vulnerability-free.
- Unix token files intentionally remain plaintext and shared by the local user.
  OS account compromise and concurrent processes acting as that same account
  are outside that storage boundary. Context/cache writes are complete-file,
  last-writer-wins replacements, not multi-process merge transactions.
- CLI request prevalidation covers projected top-level constraints. The server
  and its validation handlers remain authoritative; this is not a complete
  implementation of every JSON Schema keyword.
- Native archives are test artifacts, not signed/notarized production releases.
  External identity providers on a different origin are intentionally rejected.
- The illustrated guide and blind tasks cover common paths; they do not certify
  every provider, feature combination, shell integration, or full site build.
  Broader adversarial/multi-tenant deployment exercises remain useful before a
  production release.
