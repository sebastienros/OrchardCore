# Tenant feature-profile definitions

Independent B06 branch `codex/remote-tenant-feature-profiles`, based on merged
application administration `3ca2febc9`. This does not depend on credential PR #22.

## Contract and shared implementation

Expose `tenants feature-profiles list`, `show`, `create`, `update`, `delete` and
rule/schema discovery through the Default tenant's FeatureProfiles feature.
Require `AccessRemoteManagement` and `ManageTenantFeatureProfiles` in routing and
handler invocation, including MCP. Child tenants must not own or mutate profiles.

Use the existing FeatureProfilesManager/document, used by the admin controller,
recipe step and deployment export. Put reusable validation and mutation behavior
there or in its shared editor helper, and call it from admin/recipe/API paths.
Keep transport authorization, paging and response projection in the endpoints.

Retain immutable dictionary IDs and editable display names, including legacy recipes
where ID/name fall back to the dictionary key. Tenant assignment continues to use
those same IDs. Return ordered rules: rule precedence is significant. Discover
registered rule names through FeatureProfilesRuleOptions; do not hardcode only the
two built-in rules or invent an expression grammar for custom delegates.

Create should return 201, equivalent retries 200, differing definitions 409. Update
uses the existing ID (no ID rename), returns 404 when absent, and skips equivalent
saves. Delete returns 204 for an already absent profile. Editing/deleting definitions
does not rewrite tenant assignments or automatically disable installed features;
verify and document the actual next-validation behavior, including missing profiles.

## Baseline observations and gates

The manager currently saves on every equivalent update and absent removal. Tests
will establish these before fixing them in the shared manager. The admin edit action
also removes then reinserts the same profile; replace this with the shared update.
Admin bulk deletion enumerates the mutable dictionary while removing entries; cover
that path before changing it. Recipe JSON accepts legacy missing ID/name fields.

The validator has a comment claiming cross-request caching, but DI registers it as
scoped. Do not claim a cross-request cache bug from that comment alone. Test actual
rule enforcement on an assigned child, changes across fresh requests, rule order,
dependencies and missing profiles before deciding whether runtime changes are needed.

Remaining gates: shared validation/admin/recipe regressions; feature-gated unique
OpenAPI/Pomi/MCP catalog; anonymous/restricted/child-tenant denial; HTTP/Pomi CRUD and
idempotence; live tenant assignment and feature eligibility; strict solution/full
suites; canonical docs/Pomi skills; independent PR with green CI before merge.

Baseline strict server build: zero warnings/errors. The three manager tests ran on
unchanged production code: equivalent update and missing deletion failed because
they persisted again; changed rule order passed. The shared manager now skips those
no-op writes, and the existing admin edit no longer removes before updating.
Passing-after strict server build has zero warnings/errors and all three manager
regressions pass. API request/response models are drafted; endpoints, shared validation
and the remaining integration/live/documentation/CI gates are not complete.

## Endpoint implementation checkpoint

Six endpoints now provide profile CRUD, bounded discovery and the registered rule
schema under the Default tenant. Routing and handlers both require profile-management
and remote-management permissions; handlers reject child tenants before accessing
services. The existing manager validates IDs, legacy name defaults, unique display
names and registered nonempty rules. Admin and recipes use that same validation.

Strict server build: zero warnings/errors. Fourteen focused tests pass across the
manager, CRUD/retries/paging, denied and child calls, admin rejection and recipe legacy
handling/rejection. A prefixed creation-location regression exposed a missing slash;
the production URL is fixed and the regression now passes. Full suite, feature
lifecycle/catalog, live tenant enforcement, Pomi/MCP, skills and CI remain required.
Canonical API/module documentation is drafted, not yet verified by strict docs build.

Full local validation before integrating credential PR #22: strict solution build
zero warnings/errors; server 3,278 passed (one CI-only skip), CLI 291 and MCP 78.
The live Default-tenant fixture passes CRUD/retries, schema discovery, restricted
principals, unknown/null input rejection, omitted-rule clearing, feature removal and
restoration, and MCP with CLI disabled. Generated MCP names retain the hyphen in
`tenants_feature-profiles_*`; the canonical docs and probe use the observed names.
Transport-level MCP denial and framework non-JSON binding errors are tested separately.
Assigned-child runtime enforcement, integrated merged-target validation, skills and
strict docs remain before opening the independent profile PR.

## Integrated verification

Integrated merged credential PR #22 (`b9361a1bb`) without stacking. Strict full solution
build has zero warnings/errors; server 3,281 passed (one CI-only skip), CLI 295 and
MCP 78. Strict MkDocs, plugin links and reproducible skill distribution pass. Pomi
skills are 0.10.23, with the profile workflow and a commit-pinned API manual.

A fresh fixture provisioned a child through `tenants install --enable-remote-management`,
used its saved context without login and saved a private administrator handoff file.
Assignment through the existing tenant update contract preserved the tenant fields.
The child rejected excluded Markdown, accepted it after a later Include rule, rejected
it when order was reversed, and rejected it when its Shortcodes dependency was excluded.
These were fresh requests with no explicit child reload between definition edits.
Deleting the profile preserved its assignment and restored eligibility, matching the
existing missing-profile behavior. Cleanup removed the synthetic assignment/profile.
The first probe needed the CLI's `--force` confirmation flag on feature disable;
production behavior was correct and the completed probe passed after that test fix.

The resource smoke also passes against the fresh integrated fixture after the child
runtime probe. Local verification is complete; independent PR review/CI remain.
