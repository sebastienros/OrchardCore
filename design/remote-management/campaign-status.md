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
| P00 Inventory, plan and schedule | In review | [PR #4](https://github.com/sebastienros/OrchardCore/pull/4), rebased onto merged target `6332bce93`. |
| P01 Shared-content/discovery baseline | Merged | [PR #5](https://github.com/sebastienros/OrchardCore/pull/5), merge `6332bce93`; all CI checks passed. See [baseline evidence](baseline.md). |
| B03 Explicit-null content updates | In review | [PR #6](https://github.com/sebastienros/OrchardCore/pull/6); failing-before/passing-after integration and live CLI checks. |
| P02 Layer definitions/conditions | In progress | B01; branch starts at merged baseline `6332bce93`. |
| P03 Shortcode templates | Planned | B02. |
| P04 Shape placements | Planned | B02. |
| P05 Widget placement | Planned | After P02 merges. |
| P06 Typed settings + HTTPS | Planned | B05. |
| P07 CORS | Planned | After P06 merges. |
| P08 Security headers | Planned | After P06 merges. |
| P09 Content localization | Planned | B04. |
| P10 URL rewriting | Planned | B10. |
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
