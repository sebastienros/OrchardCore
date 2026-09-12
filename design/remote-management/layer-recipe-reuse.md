# Layers recipe shared-service audit

Before PR #25, the legacy `Layers` recipe called `ILayerService.LoadLayersAsync` and the document-level
`UpdateAsync`, but bypassed the newer name validation and named mutation methods.
It edited the mutable document while parsing subsequent entries, before rejecting
unknown conditions. The “no changes” error therefore does not prove the loaded
objects remain unchanged. JavaScript parsing already shared by the management API
and editor was also absent from the recipe path.

This follow-up is independent of the index-definition PR and starts from merged
`7e2a48e89` on `sebros/remote-tenant-cli-plan`.

## Required behavior

- Prepare and validate every recipe entry before changing the layer document.
- Reuse `ILayerService.ValidateName`, named create/update methods, and
  `IRuleManagementService.ValidateCondition` for supported shared validation.
- Preserve legacy recipe input and registered extension condition deserialization;
  do not route recipes through the narrower public API condition allowlist.
- Preserve omitted description/rule values and root/child identities; replace an
  explicitly supplied condition list rather than merging it.
- Cover invalid names, invalid scripts, unknown types after a valid entry, valid
  create/update, and omitted fields in tests of the actual recipe caller.

The individual-condition admin controller supports extension editors and incremental
rule edits. Its traversal/reorder behavior is distinct from replacing a whole rule;
this audit does not claim those methods have already been extracted or validated.

## Evidence

The strict baseline build passed. All three regressions fail on the original
recipe: whitespace names and invalid scripts report no error, and an unknown
condition after a valid entry leaves the loaded document mutated. After refactoring, the strict build passes with zero warnings/errors and all 22
recipe, layer API and rule-service tests pass. Additional successful recipe and
extension compatibility coverage, full-suite validation, docs and PR/CI remain.

The completed full server suite on `c8e574ef3` passed 3,288 tests with one CI-only
skip; strict build and docs passed. Seven recipe cases now include successful
partial updates, root/child identity preservation, empty-list replacement,
registered extension properties, legacy missing rules and immutable root safety.
The branch now integrates merged index PR #24 (`d26b9888b`); combined validation is
running before PR publication. No unmerged PR dependency is introduced.

## Integrated result and caller inventory

[PR #25](https://github.com/sebastienros/OrchardCore/pull/25) merged as
`769dd97c951054650fc5598cc9f3444de1163b99`, with all CI checks passing.
Integrated server validation passed 3,358 tests with one CI-only skip; strict
build, docs and skill distribution checks passed.

| Existing caller | Shared behavior now used | Intentional boundary |
| --- | --- | --- |
| Layers admin create/update/delete | Named `ILayerService` mutations, name checks, reference checks and persistence/cache updates | Model binding, authorization and notifications stay in the controller. |
| JavaScript condition editor | `IRuleManagementService.ValidateCondition` | Editor binding and field error presentation stay in the display driver. |
| Legacy Layers recipe | Name validation and named create/update through `ILayerService`; condition validation through `IRuleManagementService` | Recipe preparation preserves omitted fields, identities and registered extension conditions, and validates all entries before mutation. |
| Individual-condition admin editor | Existing layer document persistence and shared JavaScript validation through the display driver | Incremental condition traversal/reordering and extension editors differ from whole-rule API replacement; they are not claimed as extracted operations. |

Seven recipe regression cases cover invalid names/scripts, unknown conditions
without partial mutation, successful partial updates, identity preservation,
explicit empty lists, extension properties and legacy rule input. Future slices
must record equivalent caller evidence rather than treating interface injection
alone as reuse.
