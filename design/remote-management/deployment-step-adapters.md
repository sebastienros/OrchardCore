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
