# OpenID application shared-secret lifecycle

This independent B06 branch starts at merged scope commit `1e1774933`. Application administration is now integrated from merged PR #21 (`3ca2febc9`);
verify the combined workflow before publishing this credential slice.
Application resource management and credential lifecycle remain separate PRs.

## Contract decisions

The current OpenID store supports one shared client secret. Rotation replaces it
immediately, with **zero overlap**. POST `api/openid/applications/credentials:rotate`
selects the application using the `clientId` query parameter, generates 32 random
bytes using the shared credential generator, and updates the existing manager's
stored hash. Its JSON response contains the client ID and new secret exactly once.
Repeating rotation produces another secret and retires the previous one; do not
retry it automatically after an uncertain response. An administrator can explicitly
rotate again if the response is lost. No endpoint recovers a previously issued secret.

POST `api/openid/applications/credentials:revoke` replaces the shared secret with an
undisclosed random value. This preserves the confidential application and its grant,
role and redirect configuration while making its previous shared secret unusable.
It must never turn a confidential client into a public client. Repeating revocation
is semantically successful. A later explicit rotation or editor-supplied credential
can restore shared-secret authentication. Revocation does not disable independent
key/assertion-based authentication mechanisms.

Both operations require `AccessRemoteManagement` and `ManageApplications`, use the
Api bearer scheme, and check the same permissions inside MCP invocation. Public
clients have no shared secret to rotate/revoke and are rejected. Rotation of an
absent application returns 404; revocation of an absent application returns 204.
Neither operation changes administrator credentials or promises immediate revocation
of already-issued tokens or sessions. Existing lifetime/validation configuration applies.

## CLI and MCP

Generate `pomi openid applications credentials rotate <client-id>` and `revoke`.
Both are confirmation-bearing operations (`--force` for automation). Rotation has
`secretResponse: true` metadata, which requires `--secret-output-file <new-path>`.
The CLI must create an owner-only file before sending the mutation, reject existing
paths without changing the server, write the complete response to that file and
print only the output path. This transport metadata can serve other one-time secret
responses. HTTP/MCP callers receive the one-time JSON response and are responsible
for private storage. Rotation responses set `Cache-Control: no-store`.

Update consumers with the replacement credential using existing application
credential sources. Keep administrator handoff files separate. An unattended install
continues to save its original application context without interactive login.

## Implementation and verification gates

- Reuse the OpenID manager's credential hashing/update behavior, the existing tracked
  application lookup pattern and shared validation rollback; do not create a parallel
  credential store or duplicate descriptor-edit logic.
- Reuse the random-secret generator for provisioning and rotation/revocation.
- Test the missing protected-output behavior before modifying the CLI; validate private
  file modes/ACLs, existing-file refusal before mutation and redacted standard output.
- Verify anonymous and restricted callers, missing/public applications, state/property
  preservation, failure rollback and independent CLI/MCP feature lifecycle.
- Provision a second least-privilege application, rotate it, authenticate with the new
  secret and reject the old one; revoke the replacement and prove it is rejected.
  Run HTTP, generated Pomi and eligible in-process MCP checks.
- Integrate the merged application baseline, run strict build/local full suites and
  docs/skills checks, then review and merge only after required CI passes.

## Current state

The `secretResponse` metadata, transformer/parser support and private output writer
are committed. A failing-before baseline reproduces the missing destination gate;
after implementation the strict CLI build is clean and all 295 CLI tests pass.
The writer is exercised for file permissions, preserving the JSON response, returning
only the file path, cleanup of unused reservations, and refusing existing files
before another mutation. Windows ACL assertions await Windows CI.

Next: implement the rotation/revocation endpoints with shared generator and manager
rollback, then run server/CLI/MCP/live credential verification, update the canonical
OpenID docs and skills, and publish an independent PR. This slice is not complete.
