# Tenant rate-limit management

Priority: return to the higher-value B05 gap after core deployment export/import.
This branch starts at merged `83bdcb470`, independently of query PR #32. Preserve
all remaining deployment work in its checkpoint rather than treating it as done.

## First complete workflow

Discover limiter types and their explicit schemas; list/show/create/update/delete
policies; configure fixed-window, sliding-window, concurrency and token-bucket
limiters; enable/disable policies and observe runtime HTTP 429 behavior. Target and child-limiter
edits remain limited to disabled policies; existing enabled-policy metadata edits
remain supported, matching the administration workflow. Keep application-principal and per-tenant permission checks explicit.

## Existing caller audit

- `IRateLimitPolicyStore` already owns persisted cloning/current/enabled snapshots.
  Reuse it; do not introduce another document store.
- `AdminController` duplicates enable/disable/delete and shell-release decisions
  across individual and bulk actions. Extract shared mutations used by those
  actual actions and the endpoints.
- `LimiterController` owns child limiter mutations and rejects edits to enabled
  policies. Reuse that rule in the shared service and migrate the existing methods.
- The four limiter display drivers validate numeric parameters before storing
  typed data. Extract explicit validators used by their UpdateAsync methods and
  remote contracts; reject invalid API patches before mutation.
- `CreateOrUpdateRateLimitPoliciesStep` owns recipe identity/name matching and
  scope/path/group validation. Share matching domain rules while preserving recipe
  replacement behavior and custom-source extension compatibility.
- `RateLimiterOptionsConfigurations` loads enabled policies when building the
  tenant pipeline. Status/mutation changes affecting active policies must request
  the existing shell-release mechanism; prove runtime behavior after the release.

## Acceptance

Record missing baseline discovery, strict builds and focused legacy-caller/API
regressions. Verify Pomi/MCP discovery, denied mutations, retries, tenant isolation,
invalid-patch preservation and enabled-policy editing rules. Use a narrowly scoped
fixture path for the real 429/recovery check. Keep host-owned built-in route
configuration separate from tenant policy administration. Update canonical docs,
Pomi skills, inventory, progress and CI evidence before an independent exact-green
merge. No implementation or validation is claimed by this initial audit.
