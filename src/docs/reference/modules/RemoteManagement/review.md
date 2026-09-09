# Remote management verification record

This record tracks review of `sebros/remote-tenant-cli-plan` in the
`sebastienros/OrchardCore` fork, starting at `4c28c8dc0`. It distinguishes
implemented behavior, measured verification, and outstanding work.

## Agreed constraints

- Commit and push only to the fork; no upstream issues or pull requests.
- Preserve the shared Unix owner-only credential files. This is an intentional
  usability decision: routine commands must not require keychain prompts.
- Fix flaws in the CLI and new management APIs here. Defer existing platform
  vulnerabilities already covered by upstream PRs.

## Upstream dependencies checked on September 8, 2026

| Existing platform change | Tracking PR | Treatment |
| --- | --- | --- |
| Tenant media roots and recipe write containment | [19840](https://github.com/OrchardCMS/OrchardCore/pull/19840) | Defer platform changes; review new media APIs separately |
| Application static file traversal | [19837](https://github.com/OrchardCMS/OrchardCore/pull/19837) | Defer platform changes; review new static-file APIs separately |
| Liquid authoring permissions and rendered HTML | [19820](https://github.com/OrchardCMS/OrchardCore/pull/19820) | Defer shared rendering changes; verify management endpoint authorization |
| Media folder authorization | [19677](https://github.com/OrchardCMS/OrchardCore/pull/19677) | Avoid duplicating shared authorization changes |

## Baseline

- CLI: 57 tests passed on macOS Arm64 with .NET 10 target framework.
- Broader management/schema/content suite: 1,240 passed, three failed.
  Failures: field-schema generation attempted to annotate a boolean JSON
  Schema; legacy content update version count; duplicate autoroute validation.
- Parser: retain System.CommandLine. It already supports the dynamic tree and
  NativeAOT; no evidence so far warrants a dependency migration.

## CLI security changes

Endpoint checks reject remote cleartext transport, embedded credentials,
foreign origins, sibling tenant paths, and ambiguous encoded path segments.
Automatic HTTP redirects are disabled so token requests cannot replay secrets
to an unvalidated location. Context credentials are bound to the tenant,
authority, client ID, and case-insensitive context name. Unit tests inject a
credential store so they never read or delete the developer's OS credentials.

The security review uses [OAuth security BCP](https://www.rfc-editor.org/rfc/rfc9700.html)
and [OAuth for native apps](https://www.rfc-editor.org/rfc/rfc8252.html) as
reference points; it does not claim protocol conformance from a unit-test pass.

## Remaining verification

Continue with API authorization and mutation behavior, OAuth flows and refresh,
NativeAOT execution, human output, documentation walkthroughs, skill packaging,
and blind evaluations on different models. Record actual outcomes here.
