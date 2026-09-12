# OpenID application administration slice

This B06 slice starts from merged discovery commit `f28af3782`. It must incorporate the
merged scope slice before final integration checks, without depending on an open PR.

## Existing behavior to retain

`ApplicationController` and `OpenIdApplicationStep` already call
`OpenIdApplicationExtensions.UpdateDescriptorFromSettings`. Extend that shared descriptor
builder and persistence path for the API. The existing OpenID manager remains responsible
for storage, client-secret hashing, registered-application validation and cache updates.

The builder updates the supported grant/endpoint/response-type permissions, PKCE/PAR
requirements, roles, scope permissions and redirect URIs. It copies the existing descriptor
first, preserving custom properties, other permissions and other requirements. An omitted
secret preserves the stored credential; changing to public removes it. Roles, scopes and
redirect URI collections replace their existing values. Admin and recipe contracts retain
these semantics. Validate rejected writes against the real manager, including tracked
readback after an exception, rather than relying only on descriptor mocks.

## API contract

Add create/update/delete under `api/openid/applications`, with query-based natural-key
selection at `by-client-id?clientId=...`. Expose generated `openid applications` commands
and corresponding MCP tools through `OpenId.Management`. Every operation requires both
`AccessRemoteManagement` and `ManageApplications`, including in-process MCP invocation.

Use an explicit, closed JSON contract for application settings. Support client/application
and consent types, display name, roles, scopes, redirect URIs, grant/endpoint flags and
PKCE/PAR requirements. Validate against the same domain rules used by the editor; keep
HTTP binding and UI presentation in their respective callers. Check role/scope references
for API writes so misspellings cannot silently produce an unusable application.

Creation accepts a caller-supplied secret for confidential clients through a protected input
file; responses use the existing redacted application representation. Updates preserve the
secret when omitted. Never echo a credential or stored hash in reads, errors or retry
responses. Equivalent create retries return the existing application, while a different
definition returns conflict. Update keeps the client identifier stable and preserves private
properties. Repeated deletion is a successful no-op. Document manager deletion effects and
verify actual authentication behavior; do not claim all issued tokens are invalidated.

Dedicated generated-secret rotation/revocation and one-time delivery follow as a separate
PR after this application resource contract merges, as required by the schedule.

## Evidence required before publishing

- Existing shared editor/recipe behavior: successful edits preserve private values and
  the current secret, public/confidential transitions follow current rules, invalid edits
  leave the existing application intact, and role/grant replacements have runtime effect.
- HTTP, generated Pomi and MCP create/read/update/delete, semantic retries and conflicts,
  closed-schema validation, missing identifiers and forbidden principals.
- Provision a second least-privilege client from an unattended administrator context;
  authenticate it and prove allowed/denied operations. Keep admin credential handoff intact.
- Feature lifecycle with OpenID Management available independently of the CLI feature,
  and no cross-tenant application access.
- Strict solution build, affected/full test suites, live integration, canonical docs and
  packaged skills; green required CI and reviewed exact head before merging.
