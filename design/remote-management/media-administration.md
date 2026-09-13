# Media administration

Third of the four final campaign areas; stop after robots/sitemaps also completes.
This branch starts independently at merged `85c42b146`, without stacking on the
audit/task PR.

## Caller audit and intended workflow

- `MediaProfilesController` repeats profile conversion and can overwrite another
  profile while renaming. `MediaProfilesManager` owns the document, while
  `MediaProfileService` already turns definitions into actual image commands.
  Share validation/conversion and atomic profile mutations between existing admin
  actions and the API; verify processed image dimensions after profile changes.
- `MediaCacheController` calls `IMediaFileStoreCache.PurgeAsync`; its unavailable
  service branch misses a return and can dereference null. Reuse a cache operation
  service, retain clear unavailable/error reporting, and include the existing
  tenant-scoped `IResizedImageCache` for local image cache purging.
- Existing gallery, upload/Tus handlers, constraints discovery and request-size
  filters consume `MediaOptions`. Typed tenant upload policy must affect those
  actual paths through the same options, without duplicating upload validation or
  permitting tenant settings to expand host limits. Keep host storage/provider
  configuration out of the API.
- Existing `MediaApiSettings` controls cookie/bearer authentication. Preserve that
  behavior and its admin driver when exposing a typed settings contract.

Acceptance: profile CRUD/retries/rename conflicts and old admin paths; real image
output; cache invalidation limited to the selected tenant; settings readback and
actual upload-policy enforcement; authenticated Pomi/MCP; two-tenant isolation;
strict builds, useful regressions, docs and package checks; independent green CI
and merge. No new cloud providers or binary MCP workflow.
