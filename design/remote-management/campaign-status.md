# Remote management campaign status

The campaign is authorized to implement, verify, open and merge independent PRs into the fork's
`sebros/remote-tenant-cli-plan`. **No stacked PRs.** Each branch starts at a merged target commit.
The [schedule](pr-schedule.md) and [plan](coverage-plan.md) define the work and acceptance criteria.

## Campaign decisions

- Regular CI does not build/publish native binaries or packages. Retain managed build/test coverage;
  package publication is manual opt-in. This user-requested CI change precedes P00/P01.

- Deliver the website-building track first, then the non-deferred fleet/operations slices.
- Keep the five localization string APIs intentionally API-only (B14).
- Keep host-owned settings in deployment configuration. Implement tenant settings through typed,
  permission-aware contracts with safe readback.
- Defer additional provider adapters until justified by demand; deliver local/common contracts
  and the specifically selected SMTP integration first. Record provider capabilities explicitly.
- Defer resumable Tus CLI transfer and new binary MCP transport support; preserve existing behavior.
- Preserve optional remote management, unattended application contexts and the separate admin
  credential handoff. Reuse existing APIs wherever the baseline establishes correct coverage.
- Run local before/after checks and required CI. Review authorization, feature gates and combined
  behavior before merging; do not equate a green CLI-only check with a tested server change.

## Delivery ledger

| Slice | State | PR / evidence |
| --- | --- | --- |
| CI: disable automatic package/native publication | Merged | [PR #3](https://github.com/sebastienros/OrchardCore/pull/3), merge `053b0cd2e`; all CI checks passed. |
| P00 Inventory, plan and schedule | Merged | [PR #4](https://github.com/sebastienros/OrchardCore/pull/4), merge `d81badf94`; all CI checks passed. |
| P01 Shared-content/discovery baseline | Merged | [PR #5](https://github.com/sebastienros/OrchardCore/pull/5), merge `6332bce93`; all CI checks passed. See [baseline evidence](baseline.md). |
| B03 Explicit-null content updates | Merged | [PR #6](https://github.com/sebastienros/OrchardCore/pull/6); merged as `f727f9379`; failing-before/passing-after integration and live CLI checks. |
| P02 Layer definitions/conditions | Merged | [PR #7](https://github.com/sebastienros/OrchardCore/pull/7), merge `79edeff6d`; all CI checks passed. Shared admin/API mutations and editor syntax validation verified. Widget placement remains P05. |
| P03 Shortcode templates | Merged | [PR #8](https://github.com/sebastienros/OrchardCore/pull/8), merge `1408ff58f`; all CI checks passed. Shared admin/recipe services, six operations/commands/tools, live rendering and feature gates verified. |
| P04 Shape placements | Merged | [PR #9](https://github.com/sebastienros/OrchardCore/pull/9), merge `541f23b0d`; all CI checks passed. Shared admin validation, seven operations/commands/tools, database/file storage and rendered matching/order verified. |
| P05 Widget placement | Merged | [PR #10](https://github.com/sebastienros/OrchardCore/pull/10), merge `19b7b75e6`; all CI checks passed. Shared admin/editor service, four operations, rendered ordering/moves and content permission/version checks. |
| Website composition gate | Verified locally | `website-composition-smoke.py`: `tenants install --enable-remote-management`, stored application context without login, private administrator files, content/widget/layer/placement/shortcode rendering and setup-only context preservation on the merged presentation baseline. |
| P06 Typed settings + HTTPS | Merged | [PR #11](https://github.com/sebastienros/OrchardCore/pull/11), merge `60236178c`; all CI checks passed. Explicit section contracts, shared HTTPS editor validation, trusted HTTPS/Pomi/MCP checks. Layers zone-settings adapter tracked below. |
| Layer zone settings | Merged | [PR #16](https://github.com/sebastienros/OrchardCore/pull/16), merge `76bf74d92`; all CI checks passed. Shared admin normalization, typed settings, widget readback and feature lifecycle verified. |
| P07 CORS | Merged | [PR #13](https://github.com/sebastienros/OrchardCore/pull/13), merge `fbdb30285`; all CI checks passed. Shared admin/API/runtime validation, typed policies and real preflight checks. |
| P08 Security headers | Merged | [PR #14](https://github.com/sebastienros/OrchardCore/pull/14), merge `5be2b82df`; all CI checks passed. Shared admin/API validation, ownership and live response headers verified. |
| P09 Content localization | Merged | [PR #12](https://github.com/sebastienros/OrchardCore/pull/12), merge `9d990f99d`; all CI checks passed. Shared admin/API workflow, existing localization handlers, draft retries and version-aware permission checks verified. |
| P10 URL rewriting | Merged | [PR #15](https://github.com/sebastienros/OrchardCore/pull/15), merge `e8e6fda18`; all CI checks passed. Shared admin/recipe validation, lifecycle and native runtime rerouting verified. |
| Content culture picker settings | In verification | Independent `codex/remote-culture-picker-settings` from merged target `76bf74d92`; shared existing admin mutations, typed section and runtime cookie/redirect checks. |
| Application/scope discovery and administration | Planned | Separate B06 resource PRs. |
| Application credential lifecycle | Planned | After application administration merges. |
| Tenant feature-profile definitions | Planned | Independent B06 slice. |
| Common index definitions and lifecycle | Planned | B07 with local Lucene verification. |
| Deployment plans, export and import | Planned | Separate B08 resource/execution PRs. |
| Media profiles/cache/settings | Planned | Separate B09 PRs. |
| Robots and sitemap/source management | Planned | Separate B10 PRs. |
| Audit reads and task administration | Planned | Separate B11 PRs. |
| SMTP configuration/test | Planned | B12 following settings conventions. |
| Admin menus and dashboard layout | Planned | B13 after shared-content audit. |
| Admin templates | Planned | Remaining B02 slice. |
| Remaining tenant policy/settings gaps | Refine after baseline | Size demonstrated gaps individually; retain deliberate exclusions. |

Provider-specific integrations, notification principal semantics, user MFA/recovery policy,
remote deployment targets and extended cache/health controls retain the demand/design gates in
the plan. Review and record each disposition before campaign completion; do not silently treat
an unimplemented operation as covered.

## Validation record

- P00: all 188 feature IDs accounted for exactly once; classification totals reconciled;
  Markdown local links/anchors and whitespace checked. Documentation-only changes.
- Initial target: fork has no open PRs; CLI and documentation workflows succeeded on `befe0e93c`.

Update this ledger when slices merge or evidence changes. A package is complete only when its
agreed slices are delivered and any remaining gaps have an explicit disposition.

- Baseline local checks: strict Release solution build, zero warnings/errors; CLI 291 passed;
  authentication/MCP 78 passed; server 3,092 passed with one CI-only skip.
- CI PR local checks: actionlint, publication event matrix, strict documentation build passed.
  GitHub confirms package/native/publication jobs are skipped on push and PR events.

- Shared-service requirement: API service extractions must also replace equivalent logic in
  existing callers. P02 migrates layer admin mutations and JavaScript editor syntax validation;
  regression checks cover metadata edits preserving rule identities and editor evaluation.
