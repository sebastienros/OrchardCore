# Frontend search settings contract and caller audit

This independent B07 slice starts from merged `769dd97c9` on
`sebros/remote-tenant-cli-plan`. It has no dependency on unmerged lifecycle PR #26.
The common settings-section API supplies HTTP/Pomi/MCP transport; no new parallel
search-settings endpoint family is needed.

## Contract

Expose a `frontend-search` tenant settings section under `OrchardCore.Search`,
using `SearchPermissions.ManageSearchSettings` for reads and writes in addition to
the management transport permission. Manage three properties:

- `defaultIndexProfileName`: the administrative profile name used by `/search`,
  not its opaque ID or provider resource name. Null/empty clears the default.
- `placeholder`: nullable search form text; null clears the tenant override.
- `pageTitle`: nullable search page title; null clears the tenant override.

Omitted fields preserve existing values. Reject unknown fields and non-string,
non-null values before mutation. A newly selected nonempty default must resolve
through the tenant index profile store. Preserve unchanged stale references when
editing other fields, so a temporarily unavailable provider/profile does not block
unrelated text edits. Do not silently switch providers, enable features, rebuild an
index or grant public query permissions. Equivalent writes must not save settings.
Retain the obsolete provider property and unrelated site settings without exposing
them through this section.

## Existing callers and reuse

| Caller | Current behavior | Required change or intentional boundary |
| --- | --- | --- |
| Search settings admin driver | Binds and directly assigns all three fields, without validating a submitted index name | Use shared validation/application logic with the section provider; retain admin authorization and model-state presentation. |
| Search settings editor view | Renders default-index and placeholder fields but omits PageTitle, although the driver binds/assigns it | Render the existing title field so a later admin edit does not silently clear an API-configured title. |
| Frontend SearchController | Resolves the configured administrative name and applies query permission/provider selection; reads title/placeholder | Preserve runtime behavior; verify default selection and rendered text after remote updates. |
| Search settings deployment source | Exports a generic Settings recipe containing the stored settings | Preserve export shape and imported reference ordering. |
| Generic Settings recipe | Imports settings documents and can precede profile creation in the same recipe | Do not impose interactive selection validation on ordered imports; this transport does not duplicate the new editor validation logic. |

Validation must cover section patch/no-op/error behavior, the actual existing admin
editor, preservation of unrelated/obsolete settings, feature and permission gates,
and frontend runtime behavior with a real Lucene index. The API does not make an
index publicly queryable; retain the existing per-index query permission checks.

## Status

Implementation, caller regression tests, live HTTP/Pomi/MCP/frontend verification,
and Pomi package documentation are complete. Local integration checks pass; independent PR/CI is the remaining gate. The module's existing settings editor and frontend
controller remain authoritative, rather than the deprecated
`SearchSettings.ProviderName` property's stale replacement hint.

## Initial implementation verification

The section provider and existing admin driver now share index selection validation
and settings assignment. The editor renders PageTitle and initializes its bound
model from existing values, preserving fields absent from a partial submission.
The module explicitly references Settings.Core and exposes internals only to the
existing test assembly, following the other settings adapters.

The strict build passes with zero warnings/errors. All nine initial tests pass:
admin/API selection and text equivalence, null clearing, canonical names, no-op
writes, unknown/type-invalid/missing-index rejection without partial mutation,
preservation of stale/empty omitted defaults, and partial admin submissions.
Permission/feature/persistence coverage, real frontend verification, Pomi package
updates, integrated validation and independent PR/CI remain.

## Permission and tenant lifecycle verification

The integrated branch builds with zero warnings/errors. All 12 section/editor
tests pass. Additional coverage exercises actual settings endpoint authorization:
remote access alone, search permission alone and index-management permission with
remote access are each insufficient. Denied admin requests do not bind or mutate.

Real tenant scopes verify persistence, isolation from a second tenant, preservation
of the obsolete provider property without exposing it in readback, section removal
when Search is disabled, and retained settings after re-enabling Search. Frontend
runtime, Pomi/MCP, package/docs and full-suite validation remain.

## Live workflow verification

The committed runtime at `f70b3ed3c` passes HTTP, Pomi and MCP partial updates,
equivalent retries and atomic validation against a fresh tenant. A temporary real
Lucene index is selected as the default. An authenticated browser session verifies
that `/search` renders the configured title and placeholder without an explicit
index in the URL; an anonymous visitor is still challenged for authentication.
The same run verifies permission denials, null clearing, persistence across Search
disable/re-enable and MCP access with the CLI feature disabled. Temporary resources
and original settings are restored by the smoke script.

The first login helper followed the blank fixture's missing homepage after login.
Setting an explicit return URL to `/search` corrected that test setup without
production changes. Pomi package 0.10.26 documents the workflow; the inventory moves
Search from M to D (D26/P24/A1/M28/S36/I70/X3, 188 total). The number of modules
contributing direct WithCliCommand metadata remains unchanged because this adapter
uses the existing common settings operations.

## Integrated release verification

On the integrated target `8bb1ff436`, the full strict solution build passes with zero
warnings/errors. All 3,425 server tests pass with one CI-only skip, all 295 CLI tests
pass, and all 78 authentication/MCP tests pass. The initial no-restore attempt found
missing assets for previously unbuilt projects in this worktree; restoring the full
solution resolved this setup issue without code changes.

Strict MkDocs, plugin links and reproducible Pomi distribution checks pass. All 319
skill examples across 244 generated help pages validate against the fresh tenant.
The live smoke verifies actual frontend behavior as well as management transports.
Independent PR CI, including Linux functional tests and Windows, remains the merge gate.

## Merge result

PR #27 passed all CI checks at exact head `6a2bb476940a59d0377faf94678c6be7f9c7cdcd`
and merged as `2eaa672ce4e988d1802e69984ef213d08fb9d005`. Linux functional tests,
Windows, CLI, documentation, frontend tests and assets all passed. Package/native
publishing remained skipped.
