# Deployment plans and typed steps

This independent B08 branch starts at merged `8bb1ff436` and does not depend on
search settings PR #27. Plan and step management precede artifact export/import;
remote target orchestration remains demand-gated. This document records the whole
plan/step slice, not completion of B08 based on discovery alone.

## Contract

Expose `deployment plans` list/show/create/update/delete and ordered `deployment
plans steps` list/show/add/update/delete/order. Expose `deployment step-types`
discovery and explicit schemas. Use the API authentication scheme and require
AccessRemoteManagement plus ManageDeploymentPlan. Plan editing does not execute
an export or import and does not grant those permissions. Preserve the admin list's
additional Export permission where already required by its navigation workflow.

Use existing numeric plan IDs and opaque step IDs. Lists have stable ordering and
bounded paging. Plan names must be nonempty and unique according to the existing
store semantics. Renaming preserves step IDs, order and configuration. Equivalent
writes report unchanged; API deletes support safe retries, while existing admin
missing-resource responses remain unchanged. Validate reorder requests completely
before touching the stored list.

Do not reflect arbitrary CLR types or return raw serialized step properties.
Discovery returns allowlisted identity/type information. Resolve the factory name using
the existing export rule: a registered step.Name takes precedence, otherwise use
the concrete step type name. Generic site-settings factories cannot be identified
by CLR type alone. Explicit per-type adapters
own configuration schemas, validation and safe readback. Registered extension steps
without an adapter remain discoverable and are preserved by plan edits, but are
not silently projected as editable schemas. Preserve unknown step placeholders and
their stored data across unrelated changes. The initial typed adapter set must
support a representative content/settings export plan and the built-in Deployment
steps; review sensitive embedded JSON/file semantics before exposing them.

## Caller audit

| Existing caller | Actual current behavior | Shared boundary / intended migration |
| --- | --- | --- |
| DeploymentPlanController Index | Direct YesSql search/count/order/page; requires ManageDeploymentPlan and Export | Shared plan query, preserving UI authorization and pager presentation. |
| DeploymentPlanController Create/Edit | Local name validation and direct save; edit keeps steps | Shared plan validation/mutation, retaining localized ModelState and notification handling. |
| DeploymentPlanController Delete/Bulk | Direct session deletion | Shared delete operation with existing admin missing-item behavior. |
| StepController Create/Edit | Factory construction, display-driver binding and direct list/save | Retain UI binding; shared step identity/persistence and typed validation for supported steps. |
| StepController Delete/UpdateOrder | Direct list removal/insertion; new order index is not checked before removal | Shared complete validation before mutation; verify invalid order cannot damage tracked state. |
| DeploymentPlansRecipeStep | Resolves factories and deserializes explicit recipe types; aborts unknown types before saving | Preserve recipe shape/extension support, share plan validation and persistence. Recipe replacement is deliberately different from API metadata patch. |
| DeploymentPlanService | Request-cached name dictionary plus recipe create/replace; mutation does not invalidate dictionary | Extend existing domain boundary; regression-test reads after writes instead of adding a competing plan store. |
| DeploymentPlanDeploymentSource | Reads plans through IDeploymentPlanService and emits recipe representation | Keep consuming shared service; preserve export format. |
| Contents AddToDeploymentPlanController single/bulk | Direct DeploymentSteps.Add followed by session save; content/export authorization is separate | Shared append/persistence must preserve content authorization, extension steps and notification/redirect behavior. Artifact execution remains subsequent B08 slices. |

## Required evidence

Failing-before/passing-after existing caller regressions; service/API/admin/recipe
behavior, invalid-write atomicity, no-op retries, extension-step preservation,
permissions, feature lifecycle and tenant isolation. Validate the generated Pomi
commands and MCP tools with real persisted plans. Add canonical docs, Pomi workflow
and package updates, then run full local integration and independent PR CI. Export
and import completion require a later real cross-tenant artifact round trip.

## Status

Source audit and contract are in progress. No discovery or mutation endpoint has
shipped yet. Do not move Deployment out of the inventory's missing category until
the agreed plan/step work is verified, and retain its export/import gaps afterward.

## Shared service baseline and correction

Two real-tenant regressions fail on merged `8bb1ff436`: creation after a cached
name lookup remains invisible to the same service, and replacement using a plan
returned by the service clears that tracked plan's own steps. Both fail at their
behavior assertions, after a strict build with zero warnings/errors.

The service now snapshots incoming steps before replacing the tracked list and
invalidates request-local discovery after writes. It also materializes the incoming
plan sequence once, avoiding multiple enumeration. Both regressions pass after the
fix, including persisted step readback in a fresh tenant scope. The strict test
project build passes with zero warnings/errors. Endpoints, admin/recipe mutation
unification, typed step adapters, broader tests, live checks, docs/skills and PR/CI
remain; these two regressions do not establish completion of the plan/step slice.

## Plan metadata implementation

Plan list/show/create/rename/delete now use IDeploymentPlanService. The existing
DeploymentPlanController uses the same query, name validation and mutations,
including bulk deletion. API retries and response projection remain at the endpoint
boundary. Plan responses expose only Id, Name and StepCount. Renaming preserves all
step data; API creation retries preserve an existing plan instead of replacing it.

The strict test-project build passes with zero warnings/errors. Six focused tests
pass: the two failing-before service regressions, actual admin/API mutations with
real persistence, both permission denials before service access, and invalid request
rejection. The workflow verifies conflict rejection, custom file data preservation
without response disclosure, ordering, filtered listing and repeat delete behavior.
Initial test fixture failures were missing MVC/localization dependencies; no runtime
behavior was changed to accommodate them.

Typed step schemas/discovery/mutations, StepController and Contents caller migration,
recipe validation unification, feature/tenant gates, live Pomi/MCP, package docs and
full integration/PR CI remain. Deployment still has the separate artifact execution
gap. The canonical module page documents the implemented metadata commands.

## Shared step mutations and existing callers

StepController now uses the plan service for append, replacement, deletion and
movement. Edits bind a detached document-serialized candidate so validation failures
do not mutate the tracked step. The content-to-plan single/bulk actions use the same
batch append operation, with generated IDs for new steps. Bulk content authorization
finishes before any plan mutation. Content queries and authorization remain in the
content controller.

The service validates all batch identities and complete order requests before
mutation; repeated object references and duplicate IDs are rejected. Replacement
retains type and identity, and equivalent updates/moves/orders report unchanged.
Existing unknown or unaddressable legacy identities are not silently rewritten by
metadata changes; their migration/remote editing contract must be resolved before
closing typed step management.

Two admin reorder regressions fail on integrated `503e9a414`: destinations -1 and
Count remove the first tracked step before throwing. Both now return BadRequest
without changing the list. All 11 focused plan/service/step tests pass, including
accepted/rejected actual admin editors, detached clone preservation, invalid batch
and order atomicity, no-op operations and fresh-scope persistence. Strict test-project
build passes with zero warnings/errors. A remaining direct content query is intentional;
there are no direct plan mutations in either migrated controller.

Typed schemas and remote step operations, recipe validation unification, explicit
legacy step identity handling, content-controller integration tests, live transport
verification and final integration/CI remain. These checkpoints are not a completed
B08 or completed plan/step PR.

## Explicit built-in configuration contracts

IDeploymentStepDefinition now defines factory identity, patch schema, safe readback
and detached-candidate updates. Four explicitly registered built-in adapters cover
RecipeFileDeploymentStep, CustomFileDeploymentStep, JsonRecipeDeploymentStep and
DeploymentPlanDeploymentStep. No arbitrary CLR property serialization supplies a
configuration schema. JSON recipe bodies and custom-file contents are write-only;
metadata remains readable, and omitted values preserve existing configuration.

The JSON-recipe and custom-file admin drivers use the same validation as their
contracts. Invalid editor values leave the supplied step unchanged. Relative package
paths reject traversal and the reserved Recipe.json name; JSON recipe configuration
requires an object with a nonempty name string. Include-all plan selection shares
normalization with its existing admin driver. Recipe metadata uses explicit fields;
its normal model binding remains in the admin driver because it contains no separate
domain validation to extract.

Strict test-project build passes with zero warnings/errors. All 27 focused tests
pass, including actual admin-driver equivalence, write-only readback, omitted-value
preservation, explicit clearing, incorrect types/unknown fields, identity preservation
and the prior plan/step regressions. The contracts are a foundation; remote type/schema
and step endpoints, generic settings/content adapters, recipe import validation,
legacy identities, live Pomi/MCP and final integration/CI remain.

## Step type discovery and schemas

The deployment capability now includes `deployment step-types list` and `schema`.
Both routes use the same API authentication and permission requirements as plan
CRUD. Enabled factories are listed in stable ordinal order, with explicit
CanConfigure support. A known factory without a definition returns 501 for schema;
an unavailable factory returns 404. Listing does not call factories to create steps
or read stored configuration.

The existing DeploymentPlanDeploymentSource now calls the shared
DeploymentStepTypeResolver. Generic settings factories retain their registered name;
other steps retain the existing concrete type-name behavior. Schema descriptors use
registered contracts, never reflected CLR property lists.

Strict test-project build passes with zero warnings/errors. All 29 focused tests
pass, including actual tenant registrations, schema write-only metadata, denied
permissions, unsupported/unavailable distinctions and generic factory identity.
Persisted step list/show/mutations, remaining adapters, recipe/legacy identity work,
content-caller integration, live Pomi/MCP and full-suite/CI validation remain.

## Persisted step endpoints

The shared deployment capability now maps `deployment plans steps` list/show/add/
update/delete/order to the existing plan service and explicit configuration contracts.
Adds require a caller-selected nonempty ID of at most 128 characters. Matching
retries report unchanged; conflicting type or requested configuration returns 409.
Updates apply only to detached candidates, with identity and order preserved. Complete
reorder requests validate before mutation. Unsupported configuration remains unreadable
and uneditable through these contracts while its step identity can be listed/deleted.

All 31 focused tests pass with a strict zero-warning/error build. The real persisted
workflow verifies retry/conflict behavior, invalid patch atomicity, write-only data
preservation and clearing, ordering, safe responses, fresh-scope persistence and
repeated deletion. Every new handler denies missing remote permission before accessing
the plan service.

The first factory-created retry incorrectly returned conflict because immutable
LocalizedString category metadata lost constructor state during document cloning.
Preserving that metadata on the detached copy corrects the retry and passes the full
focused suite. The shared service also uses the same step comparison for normal
updates.

Remaining release gates include generic settings/content contracts, recipe validation
and legacy step identities, content-controller integration and tenant/feature tests,
live HTTP/Pomi/MCP, Pomi skills/package, full integration and independent PR CI. No
artifact execution or remote target support is claimed by these step operations.

## Content and settings adapters

Generic settings factories now register an explicit empty configuration contract
alongside the existing factory/source. The contract exposes no settings values.
Content adapters cover all published content, selected types with setup-recipe
options, and one existing content item. The definition update interface is asynchronous
so reference validation uses the tenant's real content manager.

The single-item admin driver shares its existence check and assignment with the
adapter; invalid selections preserve its original ID. The selected-content driver
and adapter share assignment, preserving the editor's empty-selection semantics and
API omitted-field semantics. Existing export sources and permissions are unchanged.

Strict build passes with zero warnings/errors. All 36 focused tests pass. Tests
exercise actual driver equivalence, rejected references, invalid patch atomicity,
generic settings discovery without value disclosure and a real tenant plan whose
content/settings steps produce recipe data through the existing export sources.

Recipe validation/legacy identities, content-to-plan controller integration,
tenant/feature gates, live HTTP/Pomi/MCP, package updates and full integration/CI
remain. Additional registered factories remain explicitly discoverable with their
configuration support reported; review the remaining built-in adapter coverage in
the B08 completion audit instead of equating these initial adapters with every
possible extension contract.

## Recipe replacement preflight

The existing recipe handler now delegates batch validation to the deployment plan
service before invoking replacement. Direct replacement callers have the same
preflight guard. Invalid names, duplicate names/step IDs, malformed steps and the
shared built-in JSON/file-path rules reject the whole batch before mutation. The
handler reports recipe errors and preserves canonical factory names on deserialized
steps. Content reference validation remains outside replacement preflight because
recipe ordering can create those references later.

A real-tenant regression test failed against the preceding implementation because
a later empty plan name was accepted. The corrected strict build has zero warnings
and errors; all 45 focused tests pass. Recipe handler tests verify persisted plans
remain unchanged after malformed structures, unavailable factories, unsafe paths,
duplicate names and duplicate IDs, and verify a valid configuration round-trip.

Legacy step identities, content-to-plan controller integration, tenant/feature gates,
live HTTP/Pomi/MCP, package updates and final integration/CI remain release gates.

## Legacy step identities and recipe replay

Deployment schema version 2 migrates missing and case-insensitive duplicate step
IDs. Existing valid IDs and the first occurrence of a duplicate are retained; other
steps receive unique IDs. Disabled-feature placeholders update only the identity
field in their preserved JSON so the repair survives document serialization without
losing unknown configuration. Migration is explicit, rather than a side effect of
metadata reads.

The same identity helper assigns IDs during recipe replacement. Recipes without
explicit IDs reuse the same type/name at the same position when possible; explicit
IDs are reserved first. Authors who reorder steps should supply IDs when they need
identity continuity. Invalid duplicate IDs in new recipe batches remain rejected.

Strict build: zero warnings/errors. All 47 focused tests pass, including persisted
legacy repair, migration replay, unknown nested-payload preservation, and recipe
replay across tenant scopes. The full server suite passes: 3,472 tests succeeded, zero failed, and one expected CI-only test skipped. Strict documentation build also passes.
Content-to-plan controller integration, tenant/feature lifecycle, live HTTP/Pomi/MCP,
package updates and final integration/CI remain release gates.

## Existing content actions and disabled-feature regression

Real-tenant controller tests exercise single/bulk content additions using published
content, the actual plan service and fresh-scope readback. Accepted additions receive
unique persisted IDs. Denial of the second bulk item leaves the existing plan intact
both in the tracked object and in storage.

A feature-lifecycle test fails before the serializer fix: disabling the single-item
deployment feature makes plan loading throw while reading LocalizedString category
metadata. The generic unknown-type fallback discarded the document serializer
options. Passing those options to fallback deserialization preserves the registered
converters. The lifecycle test then checks rename while disabled and restoration of
the same typed step, ID and reference when the feature is re-enabled.

Validation: strict build has zero warnings/errors; all 12 targeted content-action,
feature-lifecycle and resilient serializer tests pass. The full server suite passes
with 3,475 successes, zero failures and one expected CI-only skip. Tenant isolation,
live HTTP/Pomi/MCP, Pomi packaging and final solution/CLI/MCP/CI remain.

## Tenant isolation and live transports

Two real tenants verify that plan reads, name lookup/listing, rename/delete and
step add/update/delete/reorder cannot affect the other tenant's plan. The owner
plan retains its original name and step after all foreign operations. All 14 plan
service/recipe/migration/isolation tests pass. The full solution strict build has
zero warnings/errors; the full CLI and MCP suites pass (295 and 78 respectively).

`deployment-plans-smoke.py` passes on an isolated loopback tenant using the current
Debug build. It exercises unauthenticated/discovery-only/denied HTTP callers,
generated Pomi type discovery and explicit write-only schema, plan and step CRUD,
retry behavior, invalid patch rejection without mutation, MCP step add and order,
safe readback, filtered listing, and cleanup/repeated deletion. The MCP tools list
contains the expected deployment operations. The smoke removes its temporary plan.

Pomi skills/package, distribution/help checks, inventory reconciliation, final
review and independent PR CI remain before the plan/step slice is ready to merge.
Export/import artifacts remain separate B08 work.
