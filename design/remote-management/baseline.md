# Remote management campaign baseline

Application source: `befe0e93c`; CI-only update merged as `053b0cd2e`.
Host and CLI were built from the application source above. Tests used a disposable SQLite/Blank
loopback fixture with synthetic credentials held in its private temporary directory.

## Completed checks

| Check | Result |
| --- | --- |
| Strict Release solution build with analyzers and warnings as errors | Passed, zero warnings/errors. |
| CLI suite | 291 passed. |
| Authentication/MCP suite | 78 passed. |
| Server suite | 3,092 passed; one CI-only resource-integrity test skipped locally. |
| Discovery and route authorization | Existing `verify-fixture.py` passed. |
| Content definitions/items, users/roles and media | Existing `smoke-fixture.py` passed. |
| Culture/settings/translations | Existing `localization-smoke.py` passed. |
| API revision and dynamic refresh | Existing `api-revision-smoke.py` passed. |
| Version history/restoration | Existing `content-versions-smoke.py` passed. |
| Content validation/no mutation | Existing `content-validation-smoke.py` passed. |
| Tenant install/application provisioning | Existing `tenant-install-smoke.py` passed, including omitted provisioning and context isolation. |
| GraphQL queries/introspection/permissions | Passed after fixing the smoke's shared-cache isolation, described below. |

## Feature/catalog matrix

Run `.scripts/remote-management/catalog-smoke.py <fixture.json>` after building and starting the
existing fixture. These counts belong to this fixture and commit; they are not global feature
coverage percentages or fixed regression expectations for future modules.

| Configuration | OpenAPI operation IDs | Pomi command paths | MCP tools |
| --- | ---: | ---: | ---: |
| CLI and MCP, Templates and Tus enabled | 140 | 128 | 127 |
| Tus disabled | 139 | 127 | 126 |
| Templates disabled, Tus enabled | 135 | 123 | 122 |
| Templates and Tus restored | 140 | 128 | 127 |
| MCP enabled, CLI disabled | 140 | 0 | 127 |

All three name sets were unique. Removing/restoring features removed/restored the corresponding
commands/tools. Disabling CLI metadata did not remove MCP tools. Binary/stream exclusions remain
intentional. This matrix verifies discovery, not tool execution or every permission boundary.

## Shared-content observations

A disposable Pomi authoring probe successfully performed:

- Nested Menu item creation and reorder while preserving embedded IDs and links.
- List membership/order readback through dynamic ContainedPart payloads.
- Nested Flow/Bag updates preserving embedded IDs and the updated leaf value.
- Taxonomy/term hierarchy readback preserving nested term IDs.
- Setting future publish/archive dates through content payloads.

These are positive persistence checks. They do not yet establish equivalent editor-side
permissions, rendering, list query ordering, taxonomy references, or actual scheduled execution.
Keep those distinctions in the feature inventory and in subsequent acceptance scenarios.

Two payload constraints were confirmed: ContainedPart/TermPart are dynamic content parts and
must not be assumed to have attachable stored part definitions; new taxonomy items should obtain
a server ID before a validation/update payload refers to that existing ID. A create-validation
request carrying an unknown root ID returns 404 because it selects existing-item validation.

## Demonstrated defects and disposition

### GraphQL smoke assumes an empty shared cache

Running the documented smoke sequence populated the fixture's CLI cache before GraphQL ran.
GraphQL queries, introspection and permission checks succeeded, but the final assertion inspected
all shared `openapi.json` files and failed because earlier tests created them.

The smoke now allocates its own temporary CLI configuration, passes it through the existing
wrapper, asserts no OpenAPI cache was created there, and cleans it up. It passes against the
same fixture after the preceding tests. This is a test-isolation fix, not a GraphQL product fix.

### Explicit null does not clear content properties

Sending an update-draft payload with `PublishLaterPart.ScheduledPublishUtc: null` and
`ArchiveLaterPart.ScheduledArchiveUtc: null` succeeded, but readback retained the old dates.
The content API's custom update merge settings replace arrays but do not enable null-value
merging; the ordinary content merge defaults explicitly do.

Track this as a separate content-update regression PR. Require a failing-before/passing-after
integration test for explicit nulls, omitted-property preservation, validation and persisted
updates, plus repeat the scheduling probe. Do not mark scheduling management fully covered
until clearing and execution semantics have been verified.

## Evidence handling

Fixture state, tokens, passwords, databases and raw logs remain outside the repository in an
owner-only temporary directory. Reports record command names, counts and outcomes only.
Reproduce with the verification toolkit; do not copy the private fixture state into a PR.

## Homepage assignment verification

`AutoroutePart.SetHomepage` is an editor-only, `[JsonIgnore]` flag. Remote clients
already have `pomi settings set-home-content`, backed by the Home Route API and
its `SetHomeRoute` permission. The earlier claim of a missing command was incorrect;
culture-picker verification now uses this existing command instead of a Settings
recipe. A regression test confirmed that selecting a container already used by a
contained homepage retained the old `JsonPath`. The shared home-route service
compares the complete route so that this selection clears stale route values.

### Application editor tracked-state preservation

On merged discovery baseline `f28af3782`, a real-manager application update preserves
the existing secret and custom properties when the secret is omitted. However, an
invalid redirect URI containing a fragment causes OpenID validation to reject the
update after the tracked application has already been populated.
`OpenIdApplicationEditorTests.SharedEditor_PreservesCredentialsAndPrivateProperties_AndRejectsInvalidChanges`
then reads `Rejected` instead of the previously saved `Updated` display name.
This is an existing shared admin/recipe path defect, reproduced before application API
changes. The application slice restores the original descriptor on validation failure
and exposes the existing settings builder for reuse by the planned API.
The regression passes after the fix, together with all 14 targeted application editor,
admin controller and recipe tests. The test-project build with analyzers and warnings
as errors has zero warnings/errors; broader validation remains part of the API slice.

## Scope recipe update regression

On merged base `4d48d0e6b`, a real-manager recipe update without `Resources`
clears an existing resource list. The recipe copied an empty descriptor into the
stored object instead of copying the stored object into its update descriptor.
The shared scope editor corrects the direction and preserves unedited custom
properties. Admin edits retain their explicit-empty clearing behavior and current
resource restriction. Scope API mutations were delivered in PR #20.

A separate fixture attempt found that nonempty localized scope display-name
maps fail YesSql serialization because `CultureInfo` dictionary keys have no
configured converter. This precedes these changes. The preservation regression
uses supported string-key custom properties and resources; it does not claim
that localized scope metadata persistence is fixed.
