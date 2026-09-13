# Focused completion phase

The user's latest scope supersedes the earlier whole-backlog completion campaign.
Finish only the following four areas, then **stop the campaign and pause for review**.
Query deployment PR #32 passed all checks and merged as `91f3cf87b`; do
not start further deployment adapters as part of this phase.

| Area | Included completion workflow | Current state |
| --- | --- | --- |
| Rate limits | Tenant policy and limiter discovery, CRUD, enable/disable, existing admin/recipe reuse, runtime 429 and recovery verification | Complete: PR #33, merged `85c42b146`, Linux/Windows CI green |
| Audit/task administration | Bounded paged audit search/show with safe output; task list/show, schedule validation and enable/disable through existing services | Complete: PR #34, merged `9bad63d5b`, Linux/Windows CI green |
| Media administration | Media profile CRUD and output verification; tenant-scoped media cache purge; supported tenant settings and file-policy enforcement | Implementation and local validation in progress |
| Robots/sitemaps | Typed robots settings; sitemap/source management; public robots/XML output and appropriate cache invalidation | Pending |

Progress is **2 of 4 areas complete (50%)** for this narrowed phase. Report regular
updates with completed areas and approximate partial progress, making clear that
this is a new denominator rather than the original campaign's approximately 55%.
Count an area complete only after local validation, CI and merge. Do not count
queued checks as additional completion.

Keep existing demand gates: no new cloud providers, resumable/binary MCP transfer,
generic remote logs, or speculative task run/cancel lifecycle. Retention/pruning
and extended health/cache administration beyond the named workflows remain out of
this focused phase unless required to make the selected workflow correct.

SMTP, AdminTemplates, admin menus/dashboard, remaining deployment selectors and
other earlier backlog items stay paused. The selector checkpoint is branch
`codex/deployment-selection-adapters` at `3d1b48f8f`; its unresolved queued custom
settings execution-principal finding is documented there and is not claimed fixed.

After all four areas are verified and merged, provide a review summary covering
what shipped, validation, limitations, and the paused backlog. Do not automatically
resume the original campaign or start another tranche.
