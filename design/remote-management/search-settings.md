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

Source audit complete. Implementation, tests, live verification, Pomi documentation,
package update and independent PR/CI remain. The module's existing settings editor
and frontend controller are the authoritative behavior, not the deprecated
`SearchSettings.ProviderName` property's stale replacement hint.
