# Common index definitions and Lucene management

Independent B07 branch `codex/remote-index-definitions` starts at merged credential
PR #22 (`b9361a1bb`). Feature-profile PR #23 is independent and must merge before
its changes are integrated here; no stacked PR dependency.

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

Also inspect handler behavior for null data, provider metadata updates, immutable
backend names and existing editor validation. Null-data handler failures and
provider creation/deletion errors must not be silently treated as successful API
mutations. Test actual Lucene indexing/query results, not just stored descriptors.

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
The Lucene initialization handler currently applies analyzer/source settings only
when initializing, so recipe update parity needs explicit verification. The common
handler dereferences optional data without a null guard; inspect the resulting
logged failures in admin New/Update paths rather than assuming they are harmless.
