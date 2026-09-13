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

Initial strict build and 15 regressions passed, including actual admin editor and
status actions, cron/lock validation, preservation of cached settings and enabled
status, retry behavior, audit payload omission, pagination bounds, and authorization.
Live Pomi/MCP, a real audit-producing action, two-tenant isolation, complete solution
and compatibility checks, canonical docs, and package validation remain in progress.
