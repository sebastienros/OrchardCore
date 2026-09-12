# Remote management task and PR schedule

Based on the [coverage plan](coverage-plan.md) and [inventory](coverage-inventory.md).
This is a proposed schedule. It does not create tasks, issues, branches or pull requests.

## Unit of work

Use one campaign tracker, one task per independently useful PR, and short checklists inside each
task. Keep the B01–B14 work-package IDs as labels or references, not as mandatory PR boundaries.
B06 identity management and B08 deployment, for example, each need several PRs.

An implementation PR should deliver a complete resource or workflow: module service behavior,
permissions, typed API/OpenAPI, Pomi metadata, eligible MCP behavior, relevant tests, documentation
and agent instructions. Keep those changes together so the workflow can be reviewed and tested
end to end. A discovery/read-only workflow can be a useful slice of a larger resource family.

Separate service refactoring only when it is substantial, behavior-preserving, and independently
reviewable. Include a new shared abstraction with its first working consumer; establish its
extension model with a second consumer before treating it as a framework for all modules.

## Initial milestone: compose a website through Pomi

PR labels below are planning IDs, not GitHub PR numbers. This sequence follows the website-building
track. Each row is one proposed task/PR unless the baseline discovers separately scoped defects.

| ID | Task / proposed PR title | Scope and acceptance | Prerequisite |
| --- | --- | --- | --- |
| P00 | Document management coverage and delivery sequence | Commit the inventory, plan and this schedule; agree the first milestone. | None; documents already drafted. |
| P01 | Establish content and discovery compatibility baseline | Run existing disposable-fixture checks; capture manifest/OpenAPI/Pomi/MCP catalogs for relevant feature combinations. Audit menu/list/taxonomy/nested-content/scheduling workflows (B03). Commit useful evidence and targeted regression coverage where needed; file specific gaps. | P00. |
| P02 | Manage layer definitions and conditions remotely | Layer list/show/create/update/delete, supported condition descriptors and validation, OAuth/resource permissions, generated commands and eligible tools (B01). | P01 baseline. |
| P03 | Manage shortcode templates remotely | Template CRUD and descriptor/schema discovery using the existing manager; prove rendering and authorized/denied requests (B02). | P01 baseline; independent of Layers. |
| P04 | Manage shape placements remotely | Placement CRUD and matching/filter semantics; verify rendering and storage ownership (B02). | P01 baseline; independent of P02/P03. |
| P05 | Manage widget placement in layers remotely | Attach/move/order existing widgets, layer/zone validation and content permissions; prove page output (B01). | P02 merged; resolve any B03 defect that blocks this workflow. |

P01 should not grow into a PR fixing every shared-content issue. Produce a pass/gap matrix and
split demonstrated defects into named tasks, such as “Preserve list ordering through content
updates.” Only defects that block the selected milestone become prerequisites. Preserve unrelated
failures as separately tracked findings with before/after evidence.

The milestone gate is a reproducible scenario: install a disposable site with
`--enable-remote-management`, use the returned context without login, create content and a widget,
configure a conditional layer, placement and shortcode template, then verify the rendered result.
Also retain setup-without-remote-management coverage. Each PR supplies its own checks; the
integration task composes them into the milestone scenario instead of deferring testing until the end.

## Parallel schedule

Assume two implementation lanes and one integration/review owner. These are roles; they need not
be three people. With one worker, follow the same order sequentially. Do not launch the entire
backlog at once.

| Window | Implementation lane A | Implementation lane B | Integration / review |
| --- | --- | --- | --- |
| Baseline | P01 source/runtime audit and existing-workflow checks | Refine P02/P03 contracts against current services | Land P00; record baseline failures and shared-file ownership. |
| First increment | P02 layer definitions/conditions | P03 shortcode templates | Review independently; merge each green PR and refresh the other branch. |
| Complete composition | P05 widget placement after P02 merges | P04 shape placements | Run focused checks on each rebased branch and the combined milestone scenario. |
| Next foundation | P06 typed settings with first adapter | P09 content localization | Settle the settings extension contract with its real consumer; review localization independently. |
| Extend configuration | P07 CORS adapter after P06 | P08 security-header adapter after P06 | Verify both adapters against the merged contract and combined feature configuration. |

Keep at most two implementation PRs actively changing code. A separate review task can inspect
their diffs while development continues. Shared CLI parsing, MCP catalog/authentication code,
test-fixture infrastructure, central documentation navigation and plugin version/catalog files
should have one active writer at a time. Module-specific files can be owned by their task.

Schedule by merge gates rather than calendar weeks initially. After the first two implementation
PRs, use observed implementation, review and CI turnaround to estimate dates. A PR waiting for
review should not trigger another dependent branch stack by default.

## Second milestone: configure a usable site

| ID | Task / proposed PR title | Scope | Prerequisite |
| --- | --- | --- | --- |
| P06 | Manage typed tenant settings with an HTTPS adapter | Explicit section discovery/schema/read/update contract, validation, source ownership, redaction conventions and shell-release behavior; HTTPS as the first working adapter (B05). | P01; composition milestone preferred for scheduling, not a code dependency. |
| P07 | Manage CORS policies through typed settings | Full policy administration and validation using P06 conventions; refine the abstraction only as required by this second consumer (B05). | P06 merged. |
| P08 | Manage security headers remotely | Header/CSP settings with validation and expected rendered response headers (B05). | P06 merged; rebase if P07 changes the shared contract. |
| P09 | Localize content through the content localization manager | Discover variants and create/reuse a localized variant with content/culture permissions (B04). | P01; existing culture/content APIs. |
| P10 | Manage URL rewrite rules remotely | Rule CRUD/order and condition validation under shared authorization (B10). | P01; coordinate any changes to common Rules descriptors with P02. |

P06 should remain a small module-owned contract, not a universal arbitrary-settings API. If HTTPS
reveals substantial unrelated service refactoring, extract that refactoring before expanding the
adapter. Rate limits, robots settings, localization picker settings and SMTP become separate
consumers after the contract is demonstrated by P06/P07.

## Later queue and PR boundaries

Refine only the next selected milestone into fully specified tasks. The following are recommended
boundaries, not authorization to launch all of them or a promise that every row will be needed.

| Work package | Split into separate PRs | Dependency / merge order |
| --- | --- | --- |
| B06 identity and tenant policy | Redacted application/scope discovery; scope CRUD; application CRUD; credential rotation/revocation; feature-profile definitions; user settings/security policy by feature | Discovery first; application CRUD uses supported scope contracts; credential lifecycle follows application management. Feature profiles are independent. User/MFA policy is a later design decision. |
| B07 indexing | Common provider/index discovery plus Lucene definitions; rebuild/reset/synchronize with observable completion; Elasticsearch adapter; Azure AI adapter; frontend search settings | Establish the common contract with Lucene, then lifecycle/status. Backend adapters can proceed independently once the shared contract settles. |
| B08 deployment | Plan/step discovery and CRUD; export artifacts; import execution; remote target/client management | Plans first; agree artifact and execution/status contracts before export/import. Remote orchestration follows successful local round-trip. |
| B09 media | Profile CRUD; scoped cache administration; tenant media settings; cloud-provider parity fixes; resumable Tus CLI transfer if selected | Profiles/cache are independent of each other. Settings use P06 conventions. Tus is an explicit transport project. |
| B10 routing/SEO | URL rewrite rules (P10); robots settings; sitemap/source CRUD; sitemap cache controls | Robots settings use P06. Sitemap cache behavior follows the sitemap resource contract. |
| B11 operations | Paged audit read/search; task discovery/configuration/enablement; retention/pruning; selected cache/health controls | Reads and task administration can run independently. Add run-now/cancel only after supported lifecycle semantics are established. |
| B12 communications/providers | SMTP adapter and controlled test-send; one additional provider per demonstrated need; notification contract, then inbox/delivery operations | Settings adapters follow P06/P07. Decide principal/recipient semantics before notification endpoints. |
| B13 admin experience | Admin menu/node descriptor and tree management; dashboard layout | Separate resources. Reuse B03 evidence for content-backed dashboard parts. |
| B02 remaining presentation | AdminTemplates CRUD | Independent of frontend templates but must preserve its separate permission/document model. |
| B14 localization strings | CLI metadata, regression-policy update and docs for the five existing APIs | Only if the intentional API-only policy is changed. |

If fleet management becomes the next objective, keep P00/P01 and replace the composition lanes
with B06 application discovery/scope management in one lane and feature-profile definitions in
the other. Follow with application CRUD/credential lifecycle and B11 audit/task administration.
Then schedule B07 indexing and B08 deployment. This changes priority, not the PR boundaries or
the existing unattended installation workflow.

## Branches, ownership and merging

- Base independent PRs on the latest `sebros/remote-tenant-cli-plan` in the fork. Use a dedicated
  branch and worktree per implementation task, such as `codex/remote-layers` or
  `codex/remote-shortcode-templates`.
- Keep the integration branch green and merge PRs individually. Once a prerequisite merges,
  create its dependent task branch from the updated base. Use stacked PRs only when work on a
  concrete prerequisite cannot reasonably wait; document the parent and retarget after it merges.
- Give each task ownership of its module, tests and corresponding documentation. Coordinate
  shared-file edits with the integration owner instead of duplicating changes across branches.
- Rebase or merge the latest base before final verification. Run checks against the actual
  candidate commit; resolve integration failures in the owning PR before merging.
- Have an independent review cover permission/resource boundaries and feature combinations,
  alongside normal behavior review. A human and an application principal must both have the
  semantics claimed by the new operation.
- Update the inventory and task state when the PR merges. Mark a work package complete only
  when its agreed slices have shipped; keep deferred operations visible.

## Task template

Use this information when creating a task or issue:

```text
Outcome: One concrete workflow this PR makes possible.
Package / PR ID: Bxx / Pxx or a descriptive later-queue ID.
Base and prerequisites: Target branch, required merged PRs.
Owned files: Module, tests and docs; list coordinated shared files.
Scope: Resource operations and transport coverage included in this PR.
Exclusions: Closely related operations explicitly deferred.
Reuse: Existing services, permission providers and schema contracts.
Before evidence: Missing/failing workflow on the recorded base commit.
Acceptance: Successful workflow, denied operations, feature gates and retry behavior.
Compatibility: Existing Pomi commands, optional setup, OpenAPI/command uniqueness,
               cache refresh and eligible MCP equivalence.
Documentation: Canonical docs, relevant agent skills, inventory update.
Validation: Focused checks plus required CI, with tested commit recorded.
```

Use the [coverage plan's definition of done](coverage-plan.md#definition-of-done-for-an-apicommand-slice)
and the [verification toolkit](../../.scripts/remote-management/README.md) for the detailed checks.
Every implementation task includes verification. The integration task owns cross-PR scenarios
and review/merge coordination; it is not a final testing phase that all earlier work waits for.
