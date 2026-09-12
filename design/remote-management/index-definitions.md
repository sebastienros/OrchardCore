# Common index definitions and Lucene management

Independent B07 branch `codex/remote-index-definitions` starts at merged credential
PR #22 (`b9361a1bb`) and now includes merged feature-profile PR #23
(`7e2a48e89`); no stacked PR dependency.

## Domain audit and planned contract

Use existing IIndexProfileManager, IIndexProfileStore and registered keyed
IIndexManager services. IndexingOptions provides provider/source descriptors.
The Indexing admin controller is the existing create/update/delete boundary;
CreateOrUpdateIndexProfileStep is the general recipe entry point. Lucene also has
legacy recipe wrappers that must continue working.

Expose bounded `indexes list/show` and `indexes providers` discovery with explicit
read DTOs. IndexProfile.Properties can include backend configuration and is not a
safe unrestricted response or write contract. Scope the first editable definition
to Lucene content indexes with an explicit adapter for analyzer, source-storage,
content types and culture fields. Discover registered provider/source capabilities
and report unsupported operations honestly. Preserve index identity and backend
name constraints, unknown extension metadata and existing named-query behavior.

Keep resource authorization in transport handlers (including MCP) and share domain
validation/mutations with the existing admin/recipe paths. Creation currently saves
locally, creates the provider index, compensates locally on provider failure, and
schedules synchronization. Extract shared coordination where appropriate instead
of copying it into endpoints. Do not claim that scheduled synchronization has
finished. Observable rebuild/reset/synchronize is the next independent B07 PR.

## Baseline to verify

CreateOrUpdateIndexProfileStep validates a stored profile before UpdateAsync applies
incoming JSON. DefaultIndexProfileManager.UpdateAsync invokes updating handlers,
saves, and then invokes updated handlers without validating the resulting values.
A regression uses a valid stored profile whose incoming values fail handler validation:
it should reject persistence and restore tracked values, including nested properties.
A companion valid-update test preserves normal handler/store behavior. Run these on
the unchanged baseline before selecting the shared fix and error handling in callers.

Also inspect handler behavior for provider metadata updates, immutable backend names
and existing editor validation. InitializingContext and UpdatingContext normalize null
input to an empty JsonObject, protecting ordinary admin calls; a direct dereference in
the handlers is not evidence of a null-input failure. Provider creation/deletion errors
must not be silently treated as successful API mutations. Test actual Lucene indexing/query results, not just stored descriptors.

## Remaining gates

Implement shared mutation/validation; common discovery and typed Lucene definitions;
admin/recipe regressions; anonymous/denied/cross-tenant isolation; Pomi/MCP catalog
and feature lifecycle; local Lucene content update and named-query execution; full
strict solution/tests; canonical docs/Pomi skills; independent PR and green CI.
Elasticsearch/Azure adapters and observable lifecycle remain later B07 slices under
the campaign's documented provider gates; they are not claimed delivered here.

## Existing editor fields to preserve

The common content editor requires at least one indexed content type and edits
IndexLatest, IndexedContentTypes and Culture. Lucene edits AnalyzerName and
StoreSourceData plus default-query analyzer, query syntax permission, Lucene version
and selected search fields. Discover analyzers from LuceneAnalyzerManager; preserve
provider-owned index mappings rather than exposing an arbitrary properties bag.
The Lucene and content handlers previously applied incoming metadata only while
initializing. Both now use the same population method during updates. Handler contexts
normalize absent data before dispatch; the no-JSON regression preserves metadata
already supplied by the admin editor.

## Shared validation checkpoint

The baseline strict server build passed with zero warnings/errors. Of two manager
regressions, invalid incoming data failed because no validation exception was thrown;
valid incoming data passed. The manager now validates after updating handlers, before
store persistence, and restores every profile field plus a deep copy of properties
on rejection. IndexProfileValidationException preserves validation members/errors.
The general recipe reports those errors after incoming data is applied; the admin
edit action maps post-update validation errors back to ModelState.

The strict server rebuild passes with zero warnings/errors and both manager tests
pass, including nested-property restoration. These are focused manager results,
not proof of full index administration. Existing caller integration and
provider metadata parity, full suites and live Lucene/query checks remain.

## Discovery checkpoint

Bounded index discovery delegates to the existing manager and provider/source options,
returning explicit public fields without private profile properties or backend identity.
The focused suite passes all ten manager, recipe and discovery tests; the strict server
build reports zero warnings/errors. Typed definitions and the remaining gates above
are still in progress; this checkpoint is not a completed B07 slice.

## Metadata update checkpoint

On the integrated base, two of three real-handler regressions failed: recipe updates
ignored incoming content types, and an empty list escaped validation because it was
never applied. The admin-style update without JSON passed. Content and Lucene handlers
now share population between initialization and updates; duplicate analyzer/source
assignment was removed from the Lucene content handler. A fourth regression verifies
creation still populates metadata and mappings. Strict server build passes with zero
warnings/errors, and all fourteen focused discovery/manager/recipe/metadata tests pass.
These tests do not establish live backend reindexing or complete typed API behavior.

The full server suite also passes: 3,295 passed and one CI-only skip. Strict MkDocs
validation passes on the committed canonical documentation. Shared-handler checkpoint
`cd4ef4cf3` remains local; no index implementation PR has been opened yet.

## Shared provider coordination checkpoint

IIndexProfileManagementService now owns the local/provider create-compensate-schedule
sequence and provider-first deletion. Existing admin creation, single deletion and
bulk deletion, plus recipe creation, use it. The force-delete choice remains explicit;
a missing provider is now handled without awaiting a null task. Uncertain provider
exceptions propagate and do not claim compensation or synchronization succeeded.

Strict server compilation passes with zero warnings/errors. All 28 focused index
tests pass, including 14 new coordination/caller cases covering operation order,
provider rejection, failed compensation, invalid profiles, missing providers,
uncertain exceptions, recipe creation, and admin single/bulk deletion. Typed Lucene
endpoints and full slice validation remain; this is an implementation checkpoint.

Coordinator checkpoint `9d66ee241`: full server suite passes with 3,309 passed and
one CI-only skip; strict documentation validation passes. No index PR is open yet.

## Typed Lucene definition checkpoint

The Lucene/content feature combination registers indexes-lucene, with show/create/
update/delete/analyzer discovery endpoints, explicit public DTOs and API authentication
plus AccessRemoteManagement/ManageIndexes checks. Provider names remain immutable;
equivalent create/update retries avoid mutation. Creation/deletion use the shared
coordinator, and top-level metadata updates use the shared profile handlers.

A live HTTP/Pomi/MCP run found default search fields being concatenated by generic
Properties merging. The Lucene handler now replaces incoming top-level query fields,
including clearing and repeated updates. The first full suite also exposed the shipped
Blog recipe's LUCENE_30 compatibility version; validation now preserves defined legacy
versions, with a dedicated regression. The complete server suite then passed 3,332
with one CI-only skip, and the strict full solution build passed zero warnings/errors.
CLI 295 and MCP/authentication 78 tests pass. Live CRUD reached deletion and identified
missing CLI confirmation metadata. After correction, the live HTTP/Pomi/MCP workflow
passes, including create/show/update/delete, retries, invalid writes, anonymous/restricted
permissions, and MCP access while the CLI feature is disabled.

Shared filename validation covers both definitions and direct Lucene filesystem access.
A real temporary-directory regression verifies a stored ../outside path cannot remove
a sibling directory. Unknown indexing/query analyzers and duplicate provider names
are rejected by a handler used by existing admin and recipe validation.

Remaining before the PR: actual indexed content and
named-query verification (including content updates), feature lifecycle/cross-tenant
checks, finish the legacy recipe/editor caller audit, verify handler failures cannot
produce misleading mutation success, update Pomi skills/inventory and run final checks.
No claim of durable synchronization completion or later provider/lifecycle delivery.

Typed endpoint checkpoint `714ec59bf` also passes strict MkDocs validation.
The worktree is not yet published as an index implementation PR.

## Required handler and indexed-content checkpoint

Four baseline regressions demonstrated that initialization, creation, updating and
validation handler failures were swallowed. Required mutation/validation handlers now
propagate failures; updates restore their pre-handler snapshot on pre-persistence
failure. Three additional regressions distinguish failures after persistence, which
propagate without pretending stored state was rolled back.

The minimal Pomi request exposed required schema fields that already had server
defaults. Defaulted culture/analyzer/search-field properties are now optional in the
schema; explicit null/blank values are still rejected. Contract tests cover defaults
and explicit nulls, and the live runtime check uses the minimal payload successfully.

Strict full solution build passes zero warnings/errors. Server suite: 3,344 passed,
one CI-only skip; CLI 295 and MCP/authentication 78 passed. The real Lucene runtime
scenario creates published content before the index, creates the index through Pomi,
and reads it through a named Lucene term query. After editing and republishing the
content, the normal worker makes the new term match and removes the old term from
query results. The same index and named query definitions remain in place. Cleanup
passes after correcting the script's query-delete success expectation to 200/204.

Live definition verification also passes with Lucene disabled/re-enabled: endpoints
and MCP tools disappear and return, preserving the stored definition. CLI-disabled
MCP access remains verified. Remaining gates are cross-tenant isolation, the legacy
recipe/editor caller audit, skill/inventory updates and final PR review/CI.
