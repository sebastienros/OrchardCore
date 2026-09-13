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
merge. 

## Implementation and local evidence

The admin policy actions and API use `RateLimitPolicyMutations`, including one
shell release per changed status batch. The existing child limiter controller and
API use `RateLimitLimiterMutations`. All four display drivers use the same typed
numeric validators as the remote configuration contracts. Recipe target validation
uses the shared core helper; recipe identity and custom-source semantics remain.

The prior merged query build, with RateLimits enabled, returned 404 for the new
policy API. The final strict full solution build passed with zero warnings/errors. All 43
rate-limit regressions, 298 CLI tests, and 78 MCP tests passed.

The live smoke passed all four built-in configurations through Pomi, MCP reads and
updates, permission separation, identical retries, invalid-update preservation,
active-policy edit rejection, and actual 404 → 429 → 404 enforcement/recovery on a
narrow disposable path. Its private application credential cache exercises the
same token-reuse path as provisioned contexts. Environment credentials intentionally
request fresh tokens, so repeated invocations can encounter the existing OpenID
10/minute per-IP token limit. The independently seeded global policy is temporarily
isolated only in the disposable fixture and restored afterward.

Remote policy responses exclude ownership and arbitrary extension payloads. Unknown
limiter sources remain visible but unconfigurable. Host-contributed route/group
limits remain configured in code. This completes the chosen rate-limit workflow;
it does not introduce arbitrary host configuration or additional limiter sources.

