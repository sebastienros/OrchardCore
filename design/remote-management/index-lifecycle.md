# Observable index lifecycle

This independent branch starts at merged index-definition PR #24 (`d26b9888b`).
PR #25 is unrelated and is not a dependency. The scope is the remaining common
reset/rebuild/synchronize contract with observable completion, validated on Lucene.
Other provider adapters retain their demand gates.

## Existing callers and behavior

- `DefaultIndexProfileManager.SynchronizeAsync` queues a post-request background
  callback. Handler exceptions are logged, and the caller receives no operation
  result. `ResetAsync` uses the same exception-swallowing handler invocation helper.
- `ContentIndexProfileHandler.ResetAsync` resets the provider cursor;
  `SynchronizedAsync` invokes `ContentIndexingService` / `NamedIndexingService`.
- Indexing admin single and bulk actions and reset/rebuild recipes separately
  coordinate reset, provider rebuild, profile update and scheduled synchronization.
  These paths must use the shared lifecycle coordinator when it is introduced.
- `NamedIndexingService` acquires per-index distributed locks, processes batches,
  and persists provider cursors. Currently a rejected or failed batch can be skipped
  before a later successful batch advances the cursor. Document-handler exceptions
  are swallowed by the invocation helper. Batches with no selected documents do
  not persist their progress. Regression tests have been added before production
  changes. The strict baseline build passed; all three regressions failed on the
  unchanged worker (provider rejection, handler failure, and filtered progress).
- Immediate content indexing separately handles deletes in `IndexingContentHandler`.
  The queued service's handling of filtered/deleted records must be verified together
  with that path; absence of a delete call in one class alone does not prove the
  whole content-deletion workflow is broken.

## Required implementation and evidence

1. Share truthful per-index processing outcomes and preserve retryable progress.
   A failure must not be hidden by later cursor advancement. Filtered records may
   advance progress only after successful processing of the batch.
2. Coordinate reset/rebuild/synchronize under the existing per-index lock, including
   existing admin and recipe callers. Avoid nested lock acquisition and distinguish
   busy/unavailable/rejected/failed work from successful completion.
3. Expose tenant-local operations and status under API authentication,
   AccessRemoteManagement and ManageIndexes. Record the completion boundary and
   distinguish accepted work from completion; define restart and retry behavior.
4. Generate Pomi/MCP operations through the shared OpenAPI metadata, including
   destructive confirmations and bounded status observation. Do not fabricate
   completion from scheduling or a successful HTTP response alone.
5. Verify failure/cursor behavior, contention, provider failures, tenant isolation,
   existing callers and actual Lucene query results after reset/rebuild/sync.
   Update canonical docs, Pomi instructions, inventory and campaign ledger.

The precise operation persistence and completion-boundary contract remains under
implementation review. No lifecycle endpoint is implemented or claimed complete at
this checkpoint.

## Processing checkpoint

The shared worker now stops an individual index on a failed document build,
provider rejection or cursor-write failure, while other indexes may continue.
A failure preparing/loading a whole batch stops the run. Successful filtered batches
advance progress without a provider write, and indexes already ahead are not moved
backwards. Required document-build handlers are awaited directly so failure is not
silently treated as a complete document. The strict build and all six focused progress tests pass. A new lock-ordering
regression failed before moving provider existence/cursor reads under the per-index
lock and passes afterward. Provider rejection/exception tests also prove that other
indexes continue. Observable operation results and lifecycle endpoints remain pending.

## Processing results

`ProcessRecordsWithResultsAsync` uses the same worker as existing methods and
returns a tenant-local index ID, outcome and last confirmed cursor. Completed means
no further tasks were observed at the final read, not a guarantee about subsequent
content changes. Missing profiles/providers and contention have explicit outcomes;
failed indexes cannot inherit another index's success. Provider exception details
are not part of this result contract. The strict build and all ten progress/outcome tests pass.

Merged Layers PR #25 (`769dd97c9`) is integrated; all its CI passed. The lifecycle
branch still has no unmerged dependencies. Operation persistence, reset/rebuild
coordination, endpoint/command exposure and live lifecycle verification remain.

## Lifecycle coordinator checkpoint

`IIndexLifecycleService.ExecuteAsync` dispatches to a keyed indexing source and
uses the worker's single per-index lock for preparation and processing. Reset
resets/persists the profile before processing; rebuild first recreates the provider
index, then resets/persists and processes. A rejected provider rebuild does not
advance into reset or processing. Synchronization performs no reset. Required reset
handler failures now propagate. This executor does not itself schedule a job.

The content indexing source is registered with the shared coordinator. The strict build passes with zero warnings/errors; all 21 focused processing,
coordinator and handler tests pass, including operation ordering, single lock
acquisition and reset-handler propagation. Existing admin/recipe migration, persistent status and endpoints remain.

## Persistent operation records

The tenant Indexing feature now registers a separate operation-record map index and
migration. `IndexOperationStore` uses independent YesSql sessions and commits each
creation/transition, rather than enlisting status updates in the indexing work's
transaction. IDs are opaque GUIDs; lookup uses the tenant store and its index.

Pending/running/terminal transitions are guarded by expected state and optimistic
concurrency. Completion requires a completed processing result for the same index;
terminal records cannot be restarted through a transition. Records contain action,
state, timestamps, outcome and confirmed cursor, not provider exception text.
The strict build and both real-tenant persistence/isolation tests pass. Scheduling, interruption detection,
existing caller migration and endpoints remain incomplete.

## Background execution and uncertainty

The runner records pending work before scheduling an HTTP background job, claims
pending state with optimistic concurrency, and executes the shared coordinator in
a child shell scope. Completion is recorded after that scope returns, including
transaction disposal; exceptions become failed outcomes with details logged only
on the server. A repeated invocation does not restart running or terminal work.

Status observation marks pending/running records uncertain after 30 minutes without
a transition. This is not cancellation and does not prove provider work stopped.
The original execution may still record its eventual result; uncertain work is never
automatically restarted. A lost post-request callback or host restart therefore
cannot be reported as successful. The strict build and all seven runner/database tests pass, covering confirmed
outcomes, exceptions, repeated invocation, overdue pending work, eventual completion
after uncertainty, persistence and tenant isolation.

## Caller and route checkpoint

Admin single/bulk reset/rebuild/synchronize and both lifecycle recipes now use the
shared operation runner. Duplicate provider/reset orchestration was removed;
notifications acknowledge queueing rather than claiming completion. The obsolete
admin service-provider dependency was removed and its test fixture updated.

Remote routes request each action and read operation status, using existing API
scheme and both management/index permissions. Lucene content startup opts into the
remote provider gate; discovery lists source lifecycle actions. Other remote
provider/source pairs return 501 until verified. The strict build and all 23 focused discovery, permission/provider-gate and
existing management tests pass. Live execution, caller regressions,
interruption/feature lifecycle checks and final packaging/PR remain.

## Live lifecycle evidence

On `4b63d2619`, the strict full solution build passed with zero warnings/errors and
the full server suite passed 3,381 tests with one CI-only skip. The committed
`index-lifecycle-smoke.py` ran successfully against a fresh tenant fixture using
that runtime. HTTP synchronization and Pomi reset reached completed state and
preserved a real Lucene query result. Changing the selected content type followed
by an MCP rebuild removed the old query result; restoring the definition and
rebuilding through Pomi restored it. All operations were polled to their persisted
terminal outcome rather than treating 202 as completion.

The same run verified anonymous and insufficient-permission denials, lifecycle
rejection while the Lucene provider feature was disabled, and continued access to
the persisted operation status while that provider was disabled. The first MCP
smoke attempt used a flat argument instead of the documented `query` object; the
corrected test passed without changing production code.

Existing admin/recipe invocation regressions, final packaging and independent
PR/CI validation remain outstanding; these live checks do not substitute for them.

## Preparation failures and legacy recipe execution

The strict build passes with zero warnings/errors. All six lifecycle coordinator
cases pass, including rejected provider rebuild, provider exception and reset
exception: failed preparation releases the lock without reading the cursor,
processing tasks or updating the profile after failure.

Six real-tenant recipe integration cases pass for `ResetIndex` and `RebuildIndex`:
case-insensitive selected names, include-all and empty selection. They invoke the
actual recipe handlers, persist pending records through the shared runner, then
allow the real HTTP background callback to execute. Deliberately absent stored
profiles produce persisted `Failed`/`NotFound` outcomes, not successful completion.
These tests also verify that recipes do not directly call reset/synchronize logic.

Current CLI and MCP suites pass all 295 and 78 tests respectively. Admin-action
regressions, final docs/package validation and PR/CI review remain before merge.
