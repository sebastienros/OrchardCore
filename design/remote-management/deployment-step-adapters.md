# Remaining deployment step adapters

This slice starts independently from `sebros/remote-tenant-cli-plan` at
`68bbf5a4d`. It uses the merged plan/step contract from PR #28 and does not depend
on artifact PR #30. Keep its PR based directly on the campaign branch.

## Scope and order

The existing explicit contracts cover the four built-in deployment steps,
content selectors and generic site-settings-property steps. Remaining factories
must keep reporting `canConfigure:false` until an explicit adapter exists; do not
infer write contracts from arbitrary CLR properties.

Start with simple selectors and switches, with no credential-bearing data:

1. AllFeatures `IgnoreDisabledFeatures`, Templates/AdminTemplates `ExportAsFiles`.
2. Content definitions and deletion selectors, validating available type/part names.
3. Media selectors and path normalization, sharing existing driver validation.
4. Queries, translation cultures/categories, custom settings and custom user settings.
5. Remaining no-configuration steps, after auditing their source behavior and feature ownership.

Audit provider-specific index reset/rebuild adapters against the existing provider
demand gates. Credential-bearing provider settings require explicit secret handling
and are not made writable merely because a deployment factory exists.

For each adapter inspect its step, driver, source, startup and existing recipe
callers. Move duplicated validation into shared methods used by both driver and
adapter. Preserve partial-update behavior, feature availability, identities and
write-only fields. Verify safe readback, invalid-patch preservation, legacy admin
behavior and live Pomi/MCP discovery. Update the inventory and package only for
contracts actually implemented and verified.

This is the initial source-audit work order, not proof that every factory has been
audited or that these groups are complete.

## First adapter group: verified implementation

Seven additional factories now have explicit contracts: AllFeatures, Templates,
AdminTemplates, content-definition export/replacement/deletion, and Media. Their
registrations live in the feature startup that owns the corresponding factory.
The content-definition and media adapters validate source references before
mutating the step. Deletion names may exist only on the destination tenant.

Existing callers and reused behavior:

- `ContentDefinitionDeploymentStepDriver.UpdateAsync` and
  `ReplaceContentDefinitionDeploymentStepDriver.UpdateAsync` now call
  `ContentDefinitionSelection.Normalize`, also called by the configuration adapter.
  This retains type-name duplicates, deduplicates part names, and clears both
  selections for `includeAll`. Admin binding clears absent checkbox selections;
  API patches retain omitted properties. Both call paths have regression coverage.
- `DeleteContentDefinitionDeploymentStepDriver.UpdateAsync` parses its text fields
  with the extracted deletion-name parser. The API accepts arrays directly; it
  preserves duplicate names and does not require source definitions to exist.
- `MediaDeploymentStepDriver.UpdateAsync` and its adapter share
  `MediaDeploymentSelection.Normalize`. Admin binding still clears absent selections;
  API omissions retain them. The API additionally validates relative path shape and
  file/directory existence before assignment. Both call paths have regression coverage.
- Feature/template drivers directly bind one Boolean without domain validation;
  their adapters use the explicit Boolean contract. No duplicated service logic
  was introduced. Sources and recipe behavior remain unchanged.

Strict test-project builds pass without warnings. The switch/definition group has
26 passing focused tests; media plus content-definition tests have 16 passing cases.
Strict documentation builds pass. The expanded `deployment-plans-smoke.py` passes
against a disposable tenant: seven contracts discovered and configured through
Pomi, MCP readback, invalid-patch preservation, existing permissions, plan retries,
ordering, and cleanup. Full-solution and CI gates remain before merging this slice.

The remaining groups are separate work, not claimed complete. In particular,
query-based content currently duplicates JSON parameter parsing in its admin
editor and deployment source; the source accepts JSON `null` as empty parameters
while the editor rejects it. Extract shared parsing while preserving that caller
policy, then use it in the query adapter. Translation and custom-settings selectors
and the no-configuration factory audit remain. Provider demand gates still apply.
