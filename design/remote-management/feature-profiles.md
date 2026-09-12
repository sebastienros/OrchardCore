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
