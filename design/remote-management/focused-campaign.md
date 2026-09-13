# Focused completion phase

The user's latest scope supersedes the earlier whole-backlog completion campaign.
Finish only the following four areas, then **stop the campaign and pause for review**.
Query deployment PR #32 passed all checks and merged as `91f3cf87b`; do
not start further deployment adapters as part of this phase.

| Area | Included completion workflow | Current state |
| --- | --- | --- |
| Rate limits | Tenant policy and limiter discovery, CRUD, enable/disable, existing admin/recipe reuse, runtime 429 and recovery verification | Complete: PR #33, merged `85c42b146`, Linux/Windows CI green |
| Audit/task administration | Bounded paged audit search/show with safe output; task list/show, schedule validation and enable/disable through existing services | Complete: PR #34, merged `9bad63d5b`, Linux/Windows CI green |
| Media administration | Media profile CRUD and output verification; tenant-scoped media cache purge; supported tenant settings and file-policy enforcement | Complete: PR #35, merged `a7638cda9`, Linux/Windows CI green |
| Robots/sitemaps | Typed robots settings; sitemap/source management; public robots/XML output and appropriate cache invalidation | Final PR #36: public text/XML, two-tenant isolation and local regressions verified |

## Completion and pause

This completion record takes effect when final PR #36 merges after green CI.
At that point the narrowed phase is **4 of 4 areas complete (100%)** and the
campaign is **paused**. No further feature slices are scheduled or authorized by
this phase. While #36 remains open, three areas are merged and the final merge
is still required; local validation alone does not satisfy the completion gate.

The four areas have dedicated local regressions and live Pomi/MCP checks. The
final robots/sitemaps workflow covers real text/XML output and two-tenant
isolation; its cached-index regression was reproduced on the unchanged base
manager before fixing the shared manager. CLI 298 and MCP 78 regressions pass.

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
