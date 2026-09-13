# Audit and task administration

This is the second of the four final areas selected by the user. Finish rate
limits, audit/task administration, media administration, and robots/sitemaps, then
stop for reflection. This branch starts independently from merged `91f3cf87b`; it
is not stacked on the rate-limit PR.

## Existing paths and shared implementation

Audit search reuses `IAuditTrailAdminListFilterParser` and
`IAuditTrailAdminListQueryService`, the exact parser/query service used by the admin
list. Show uses `IAuditTrailManager.GetEventAsync`, as the existing detail action
does. The response is an explicit metadata projection; stored snapshots/properties
and IP addresses are not exposed. Pagination bounds prevent unbounded or
overflowing requests while preserving the existing filter syntax.

`BackgroundTaskManager` remains the document and deferred-signal owner. The new
management service resolves registered task defaults/overrides, clones shared
settings before mutation, validates using the scheduler's exact NCrontab parser,
and avoids redundant writes. The existing list, editor, enable/disable, and bulk
actions use it. Settings updates preserve enabled status; enabling validates
existing settings, and disabling remains possible when persisted settings are bad.
The API exposes registered tasks, validation, complete configuration updates, and
status changes. It does not invent run/cancel or execution-history lifecycle.

## Verification

The baseline host had both features enabled and returned 404 for both management
routes. The final integrated strict solution build passed with zero warnings and
errors, followed by 18 focused regressions, 298 CLI tests and 78 MCP tests.

The live Pomi/MCP smoke passed with actual User.Created audit production, filtered
search/paging/show, metadata-only output, denied access and separate resource
permissions, task schedule validation, configuration/status retries, and two tenants
on one host. The second tenant could not retrieve the first event or affect its
task settings. Original task configuration/status were restored; the disposable
user was deleted and the secondary tenant stopped.

The live test found an update-response issue: rereading immutable settings before
deferred cache invalidation returned old values after a successful write. The API
now returns the settings it wrote, with a regression using separate immutable and
mutable documents. The rebuilt host passed the complete live workflow afterward.

Canonical docs and Pomi 0.10.32 describe the workflow. CI/package validation and
merge evidence are tracked with the independent PR.
