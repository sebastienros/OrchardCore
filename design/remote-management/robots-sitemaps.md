# Robots and sitemaps: final focused campaign area

Base: merged media `a7638cda9`, integrated before opening the independent PR. This is the final area, after which the campaign stops for review.

## Existing callers and shared behavior

- SEO's existing robots middleware/provider consumes `RobotsSettings` and defers
  to a physical robots.txt. Expose typed rules with physical-file ownership,
  preserve omission/null semantics, and share field application with the editor.
- `SitemapHelperService.ValidatePathAsync` is already used by regular/index admin
  controllers. Invoke it from the new management service rather than duplicate
  path syntax and collision validation. Metadata/status mutations are shared.
- `SitemapManager` owns documents, routing identifiers and cache filenames. New
  operations call it. Caller audit found child updates/deletion did not invalidate
  referencing sitemap indexes; fix the manager for every existing caller and
  verify cached XML after child path, status and deletion changes.
- Existing custom-path and content-type source editors use the shared built-in
  source validator. API writes deserialize detached candidates, reject unknown
  fields and server-controlled IDs/timestamps, and validate before touching a
  stored source. Unknown third-party source types expose identity only.

## Completion checks

Typed `robots` and `sitemaps-robots` settings; sitemap/index list/show/create/update/
delete/enable/disable; source types/schema/list/create/update/delete; registered
feature and OAuth permission separation; public robots.txt and XML output; child
index invalidation; source retries/invalid preservation; two-tenant isolation;
strict build, CLI/MCP regressions, docs/skills and green independent PR merge.

Server-generated create IDs are not an idempotency guarantee: callers must read
back list/show after an uncertain create result. Update/status and missing delete
retries avoid unnecessary writes. Arbitrary provider configuration is out of scope.

## Before/after evidence

With SEO/Sitemaps enabled, the merged-base fixture returned 404 for the new
management routes. The index cache invalidation regression failed against the
unchanged base SitemapManager (one failing test); the manager was restored and
the initial after-run passed all 12 focused tests with a zero-warning/error full
solution build. Final integrated live and package evidence follows before PR.

Final local evidence: 14 focused tests passed, including both existing source
editors and opaque third-party source readback. CLI 298 and MCP 78 regressions
passed on the integrated media base. The live script passed public robots text,
custom-path and routable-content XML, regenerated cached XML, index changes after
child rename/disable/delete, separate SEO/sitemap permissions, MCP readback and
two-tenant isolation. A live command-path failure exposed a missing second CLI
argument declaration; both source path IDs are now positional and verified.

No third-party source configuration or new sitemap providers are added. Public
create operations return generated IDs; uncertain creates require readback before
retry. The final PR completes the four-area scope only once CI is green and it is
merged. The earlier backlog remains paused; do not start another slice.
