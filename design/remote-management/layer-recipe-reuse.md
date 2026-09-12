# Layers recipe shared-service audit

The legacy `Layers` recipe calls `ILayerService.LoadLayersAsync` and the document-level
`UpdateAsync`, but bypasses the newer name validation and named mutation methods.
It edits the mutable document while parsing subsequent entries, before rejecting
unknown conditions. The “no changes” error therefore does not prove the loaded
objects remain unchanged. JavaScript parsing already shared by the management API
and editor is also absent from the recipe path.

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
