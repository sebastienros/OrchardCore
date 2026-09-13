# Deployment export/import contract

Independent work starts from merged campaign head `2eaa672ce`, not PR #28.
Plan/step management is a separate PR. Integrate it only after its merge; do not
publish this branch as a stacked PR.

## Existing callers and transport evidence

- `Deployment/Controllers/ExportFileController.Execute` loads a plan, maps its
  recipe metadata, executes `IDeploymentManager`, zips temporary files and relies
  on a result filter to remove the archive after download.
- `Deployment.Remote/Controllers/ExportRemoteInstanceController` separately builds
  and zips a deployment result. Preserve that protocol's current behavior; remote
  targets remain demand-gated. Share archive mechanics where semantics match.
- `Deployment/Controllers/ImportController.Import` stages file uploads through
  `FileCreationService`, extracts ZIPs or stages recipe JSON, invokes
  `IDeploymentManager.ImportDeploymentPackageAsync`, then cleans temporary paths.
  Its JSON action also imports through the same manager.
- `DeploymentManager` already owns ordered source execution and target-handler
  import. New orchestration must call it, not recreate its loops.
- `TemporaryFileBuilder` owns an export directory but not the sibling ZIP. Failure
  during archive creation needs explicit archive cleanup.
- Pomi supports binary request bodies through `--file`. Dynamic responses currently
  use `ReadAsStringAsync`; ZIP download requires an explicit file-output path.
  Never decode an archive as text or place archive bytes in JSON output.

## Chosen execution model

Use tenant-local operation IDs for exports and imports. Persist accepted/running/
succeeded/failed state and an uncertain state for interrupted work. An uncertain
import must not restart automatically: recipe execution can partially commit and
is not generally idempotent. Client request IDs deduplicate acceptance, not replay
external side effects. A repeated ID with a different request conflicts.

Start operations only through the existing authenticated API scheme, requiring
`AccessRemoteManagement` plus `Export` or `Import` as appropriate. Managing a plan
alone grants neither export nor import. Reauthorize status/download/removal; bind
operations and artifacts to the tenant and initiating principal (user subject or
client application identity). Do not return arbitrary server paths or exception
text. Return stable error codes and bounded, sanitized diagnostics.

A durable tenant worker claims accepted work; persisted state and a claim prevent
concurrent execution. Follow index-operation conventions where their semantics
match, without coupling deployment to an indexing feature. Execution records must
commit before the worker begins. Host restart must expose interruption rather
than report success from a stale lease.

## Artifact model

Artifacts use opaque IDs and private tenant storage, never public media URLs.
Metadata includes purpose, filename, media type, byte length, checksum, creation
and expiry times. Default expiry is 24 hours, configurable by the host. Cleanup
must exclude active operations/streams and handle failed or abandoned staging.
Deletion is repeatable and rejected while an operation actively uses the artifact.

Upload accepts ZIP and recipe JSON through the existing file-creation pipeline.
Use host-configurable limits for uploaded size, expanded bytes and entry count.
Default limits: 100 MiB uploaded, 500 MiB expanded, 10,000 archive entries. Validate
while streaming, not solely through request headers. Reject absolute/traversal
paths, symlinks, duplicate normalized paths and unsupported file types. Require a
root Recipe.json and validate recipe structure before accepting execution. Archive
validation cannot promise every recipe step will succeed or that import is atomic.

Export saves a completed ZIP atomically before reporting success. Download requires
an authorized explicit request and streams bytes; no signed public links or generic
filesystem browser. Pomi exposes an explicit output-file option with no accidental
overwrite. MCP exposes JSON operation/artifact metadata and execution commands;
new binary MCP transfer stays within its existing demand gate.

## Delivery and release gates

1. Extract archive generation/staging helpers and migrate existing local admin
   callers. Verify metadata, payload, cleanup on failure and existing permissions.
2. Add tenant artifact ownership, limits, validation and cleanup, with binary HTTP
   upload/download and explicit Pomi file output. Existing JSON responses retain
   their output behavior.
3. Add export execution/status endpoints and shared worker orchestration. Verify
   acceptance retries, feature/tenant/principal boundaries and interrupted work.
4. Add import execution/status using the same artifact infrastructure. Preserve
   existing admin validation and providers; do not rerun uncertain imports.
5. Export a representative plan from one disposable tenant, import into another,
   and verify content/settings and file bytes rather than merely HTTP success.
6. Update skills/docs/inventory and run local suites/live smoke, independent PR CI,
   review and exact-green merge. Separate export/import PRs if review scope grows.

Plan configuration support does not imply every feature's export step has a typed
remote editor. Audit remaining built-in adapters separately. Private remote target
and API-key protocols retain their documented demand gate.

## Shared archive extraction in progress

`IDeploymentArchiveService` now owns source execution, ZIP creation and temporary
staging cleanup. Its returned stream deletes the private ZIP when disposed. Local
admin download and the existing remote multipart export both use it. Local recipe
metadata mapping and the remote protocol's empty descriptor remain unchanged.
Each export gets an independent random archive path, avoiding collisions between
concurrent exports of plans with the same name. No new remote target API is added.

Two archive tests pass, covering recipe metadata/file bytes with two open exports
and cleanup after source failure. The strict test-project build has zero warnings
and errors; strict documentation build also passes. Actual controller transport
checks, full integration and the artifact/operation gates above remain before
release. This checkpoint extracts existing archive mechanics and introduces no
remote artifact management API yet.

## Existing export controller verification

All six archive/controller tests pass with a strict zero-warning/error build.
The actual local admin action streams a ZIP through MVC, preserves recipe metadata
and nested file bytes, and disposes its archive stream. Denied authorization stops
before plan access or archive generation. The existing remote action is exercised
with an in-memory HTTP handler: it sends the ZIP multipart part with its existing
empty recipe metadata and closes the archive after successful delivery and after a
simulated send exception. No external remote target is contacted by these tests.

Private artifact storage, input validation/limits, operation persistence/workers,
HTTP/Pomi download transport and cross-tenant export/import remain to implement.

## Shared import staging

DeploymentPackageService stages ZIP/JSON input and validates paths, unique archive
entries, upload/expanded sizes, entry count and named recipe-step structure. It
returns an owned file provider with deterministic cleanup. Input stream ownership
stays with the caller. The existing local upload and JSON import actions now call
this service before DeploymentManager; upload file-event handlers are preserved.

PR #28 merged as `c037e583568fcf87155e6c984329e66919f2f1dc` after all required CI
passed at exact head `fbcc29684c0254587a5f41d818a14723a7bb3ef9`. Integrate that
merged target after this staging checkpoint; the artifact branch began independently.

Staging checkpoint validation: strict build and documentation pass; all 19
archive/staging tests pass. Remaining gates include actual import-controller
pipeline/cleanup tests, cancellation/symlink cases, durable artifacts and execution,
Pomi binary transport and the cross-tenant round-trip.

## Integrated import action and edge-case validation

After integrating merged PR #28, strict build and the full server suite pass:
3,495 successes, zero failures and one expected CI-only skip. Additional tests
then cover the actual upload and JSON import actions, preserving file-pipeline
rejection, denied permissions, malformed recipe rejection before execution and
cleanup after execution failure. Cancellation leaves the caller's stream open and
removes staging; ZIP symlink entries are rejected. The final focused run passes
all 28 archive/staging/controller tests with zero build warnings/errors.

The upload fixture initially lacked FormFile headers and failed before reaching
its event pipeline; correcting the fixture verifies the intended controller paths.
No production changes were needed for these additional scenarios. Durable owned
artifact storage and operation orchestration are the next implementation gates.
