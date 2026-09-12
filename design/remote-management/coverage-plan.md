# Remote management API and command delivery plan

Planning baseline: `sebros/remote-tenant-cli-plan` at `befe0e93c`, 2026-09-11.
The [feature inventory](coverage-inventory.md) contains the evidence, scope and all 188 feature IDs.
This is a proposed backlog, not a commitment to implement every feature or expose every admin action.

The [task and PR schedule](pr-schedule.md) translates this backlog into reviewable slices,
parallel work lanes, dependency gates and an initial delivery milestone.

## Recommended starting point

Start with a short **B03 shared-content parity audit**, then implement **B01 Layers** and
**B02 Placements / Shortcode templates**. These close concrete gaps between the existing content,
template and media commands and building a complete website. Follow with **B05 typed site settings**.
Keep AdminTemplates as a separate B02 slice rather than making it a dependency of frontend work.

This default assumes the next objective is an agent completing a website after unattended setup.
If the immediate objective is operating many sites/tenants, promote **B06 application credentials
and feature profiles**, then **B11 audit/tasks**, **B07 indexes**, and **B08 deployment**. Existing
installation and automatic client-credentials contexts already work; neither track needs another
interactive-login or username/password-token bootstrap flow.

Do not start by adding CLI metadata to every HTTP endpoint. The five localization string APIs
are intentionally API-only; direct Lucene/Elasticsearch queries overlap the existing Queries
commands; OAuth, GraphQL, Tus and SignalR have protocol-specific behavior.

## How to choose the order

Choose one concrete outcome for the next iteration, such as “create a website with conditional
widgets and placements” or “rotate application credentials across installed tenants.” Select
work packages against that outcome, rather than counting enabled features.

For each proposed slice, record:

| Criterion | Questions to answer |
| --- | --- |
| User value | Which currently blocked automation will become possible? How often will it be used? |
| Reuse | Does it unlock multiple modules, providers or workflows through an existing service? |
| Current workaround | Can existing content/queries/workflows/recipes already do it correctly? Is only a schema/example missing? |
| Scope and effort | Can one useful list/show/update workflow ship independently? Does it require a new persistence or job model? |
| Dependencies | Which contracts and descriptor/schema providers must exist first? |
| Authorization | Does an application principal have meaningful permissions for the operation, or is a human user required? |
| Operational impact | Are writes reversible? Are there secret, tenant-boundary, long-running or external-delivery concerns? |
| Evidence | Has the missing operation been verified against source and a representative tenant? |

Rank ready slices with high user value and reuse first, preferring smaller effort when value is
similar. Dependencies constrain the sequence. High authorization/operational impact requires a
narrower first slice and explicit acceptance cases, not automatically a low priority.

The estimates below are relative **S/M/L**, not calendar estimates. S means a bounded projection
or service adapter; M means a resource contract plus validation/authorization; L means several
resources, providers, credentials or asynchronous operations. Split large packages before assigning
owners or dates. Site/fleet values are provisional H/M/L rankings for the two outcomes above.

## Work packages

Proposed command names in this plan are design candidates, not commands available today.
Each package should reuse the existing module services and permission model and contribute the
same endpoint metadata used by Pomi and eligible MCP tools.

| ID | Package and first useful slice | Site value | Fleet value | Effort | Main dependencies / decisions |
| --- | --- | --- | --- | --- | --- |
| B01 | Layers: list/show/create/update/delete layer definitions; condition descriptors/validation; widget placement/order | H | L | M | Reuse Rules and content; do not duplicate widget content CRUD. |
| B02 | Presentation documents: Placements CRUD, then Shortcode template CRUD, then separate AdminTemplates CRUD | H | L | M per resource | No need to wait for B01; storage ownership and admin-template permissions must stay explicit. |
| B03 | Shared-content parity: exercise menus, lists, taxonomy terms, nested widgets, scheduling and definition-settings discovery | H | M | S audit; fixes sized separately | Baseline existing commands first. Add schemas or service operations only for demonstrated gaps. |
| B04 | Content localization: list localized variants and localize an item through the existing manager; picker settings second | H | M | M | Existing culture and content APIs; preserve per-content and culture permissions. Separate from B14 UI strings. |
| B05 | Typed tenant settings: establish explicit section contracts; first CORS/HTTPS/security headers, then rate limits | H | H | M foundation; M per section | Core Settings and CustomSettings are not arbitrary bags. Decide merge/reset, redaction, validation and shell release behavior. |
| B06 | Fleet identity/tenant policy: list/show applications and scopes, provision/rotate/revoke credentials; feature-profile definitions as a separate slice | M | H | L | Existing bootstrap, OpenID managers, Roles and tenant APIs. User security/settings administration is a later sub-slice. |
| B07 | Index management: providers/index definitions list/show, then create/update/delete and rebuild/status through common Indexing | H | H | L | Common Indexing before backend-specific additions. Define reset/synchronize/rebuild and long-running completion semantics. |
| B08 | Deployment: plan/step type discovery and CRUD, then export artifacts/import execution; remote targets last | M | H | L | Recipe/deployment services; typed step descriptors, artifact transport and job/status decisions. |
| B09 | Media administration: profile CRUD, cache purge, settings; cloud-provider parity and resumable uploads separately | M | M | M; L for transfer/providers | Existing Media API; no second file API. Tus transfer and binary/MCP policy are explicit decisions. |
| B10 | Routing/SEO: URL rewrite rules CRUD/order, robots settings, sitemap/source management and cache controls | H | M | M per resource | Reuse Rules and sitemap services; B05 for settings sections. Public sitemap XML is not management. |
| B11 | Operations: paged audit search/show, task list/schedule/enable/disable; retention/cache/status later | M | H | M per resource | Identity semantics, sanitized operational data, task execution/status contracts. No generic remote log/file browser. |
| B12 | Communications/integrations: SMTP configuration/test first; SMS, notifications and external-login/analytics providers by demand | M | M | L overall; M per adapter | B05 settings conventions; B06 where identity policy is involved. Define human inbox versus application behavior. |
| B13 | Admin experience: AdminMenu node/tree management, then dashboard layout | L | M | M | Tree/node descriptors and Rules where relevant; B03 verifies content-backed dashboard pieces first. |
| B14 | Optional CLI projection of localization strings/translations | M | L | S | Explicitly reverse the existing API-only policy before changing metadata, docs and regression expectations. |

## Slices and completion evidence

### B01 — Layers and widget placement

Reuse [LayerService](../../src/OrchardCore.Modules/OrchardCore.Layers/Services/LayerService.cs),
[admin operations](../../src/OrchardCore.Modules/OrchardCore.Layers/Controllers/AdminController.cs)
and [rule operations](../../src/OrchardCore.Modules/OrchardCore.Layers/Controllers/LayerRuleController.cs).
Proposed groups: `layers` and `layers widgets`.

Ship layer definitions and rule descriptors first, then placement/move/order using content IDs.
Demonstrate creating a conditional layer, attaching an existing widget, moving it between zones,
rendering the expected page, and deleting the layer without unintentionally deleting content.
Test duplicate names, missing widgets, nested conditions and per-content authorization.

### B02 — Presentation documents

Reuse [PlacementsManager](../../src/OrchardCore.Modules/OrchardCore.Placements/Services/PlacementsManager.cs),
[ShortcodeTemplatesManager](../../src/OrchardCore.Modules/OrchardCore.Shortcodes/Services/ShortcodeTemplatesManager.cs)
and the [Templates feature registrations](../../src/OrchardCore.Modules/OrchardCore.Templates/Startup.cs).
Proposed groups: `placements`, `shortcodes templates`, `admin-templates`.

Deliver each resource independently. Verify placement matching keys and ordering, shortcode
round-trip/rendering, and separate admin-template authorization. Database-backed and file-backed
placement storage need distinct ownership checks; do not turn this into arbitrary tenant-file editing.

### B03 — Shared content before specialized commands

Use existing `content items`, `content types`, `content parts`, `content fields`, and
`content settings` operations. Inspect the
[definition service](../../src/OrchardCore.Modules/OrchardCore.ContentTypes/Services/ContentDefinitionApiService.cs)
and [content operations](../../src/OrchardCore.Modules/OrchardCore.Contents/Endpoints/Api/ContentManagementApiEndpoints.cs).

Record a pass/gap result for these workflows:

- Create a frontend menu with nested links; reorder it and preserve per-item permissions.
- Attach/move/order list children and taxonomy terms; preserve references and hierarchy.
- Create Flow/Bag nested content and widget definitions; verify validation and permission boundaries.
- Set and clear publish/archive schedules; verify execution as well as JSON round-trip.
- Discover meaningful field/part settings schemas, including relationship/editor settings.

For each gap, choose one of: improve schema discovery, fix shared lifecycle handling, document an
existing workflow, or add a dedicated service operation. Do not create `html`, `title`, `alias`,
`markdown`, etc. command families merely because those modules lack their own endpoint files.
The audit itself can finish before all resulting fixes; create separately sized follow-up slices.

### B04 — Localized content

Reuse [the existing localization action and manager](../../src/OrchardCore.Modules/OrchardCore.ContentLocalization/Controllers/AdminController.cs).
Proposed extension: `content localizations`.

Demonstrate cloning/localizing an existing item, returning the existing variant on a retry,
listing its localization set, and enforcing content/culture permissions. Do not emulate this by
assigning arbitrary LocalizationPart IDs. Add picker/request-culture settings after the core
workflow. UI translation catalogs and dynamic labels remain the independent B14 decision.

### B05 — Typed settings conventions and first adapters

Use [SiteSettingsManagementService](../../src/OrchardCore.Modules/OrchardCore.Settings/Services/SiteSettingsManagementService.cs)
as the current boundary, not as an assumed generic section API. Existing schema providers only
contribute schema information; new section read/write operations require an explicit contract.
A candidate extension is `settings sections list/show/schema/update` with module-owned providers;
choose that versus distinct groups when the first two real adapters establish the common shape.

Require allowlisted properties, omitted-versus-null semantics, validation matching the admin UI,
feature/section permissions, redacted secrets, and explicit configuration-source/read-only status.
Keep host-owned settings in deployment configuration. Verify a write, safe readback, idempotent
retry and any required shell release. Reuse these conventions for B10 and B12; avoid delaying the
first adapter to design a universal settings framework.

### B06 — Application lifecycle and tenant policy

Start from [ApplicationController](../../src/OrchardCore.Modules/OrchardCore.OpenId/Controllers/ApplicationController.cs),
[ScopeController](../../src/OrchardCore.Modules/OrchardCore.OpenId/Controllers/ScopeController.cs),
[feature-profile administration](../../src/OrchardCore.Modules/OrchardCore.Tenants/Controllers/FeatureProfilesController.cs)
and [existing provisioning](../../src/OrchardCore.Modules/OrchardCore.Tenants/Endpoints/Management/TenantManagementEndpoints.cs).
Proposed groups: `openid applications`, `openid scopes`, and `tenants feature-profiles`.

1. Add redacted application/scope list/show and explicit create/update contracts using existing managers.
2. Add credential rotation/revocation with one-time secret delivery; define rotation overlap and
   access-token/session lifetime behavior. Revoking a secret must not be described as immediately
   invalidating every previously issued token unless that behavior is implemented and tested.
3. Add profile definition discovery/CRUD; preserve existing tenant profile-name assignment.
4. Size user-specific settings, registration/recovery/MFA policy and provider configuration separately.

Verify an unattended install can use its context, provision a second least-privilege application,
rotate that application's credential, authenticate using the replacement, and reject the retired
credential. Preserve the independent administrator account and the private credential-file handoff.
Also verify installing without remote management creates the user account without changing local
contexts. Do not bypass MFA/recovery challenges by projecting existing user actions as app commands.

### B07 — Common index lifecycle

Use [Indexing administration](../../src/OrchardCore.Modules/OrchardCore.Indexing/Controllers/AdminController.cs)
as the shared domain boundary. Proposed groups: `indexes`, `indexes providers`.

Ship descriptors/list/show and a single backend first (Lucene is suitable for a local fixture).
Then add mutation and rebuild/reset/synchronize with observable completion. Extend the same
contract to Elasticsearch and Azure AI where supported, reporting capabilities for differences.
Verify named-query execution still works and content updates appear after indexing. Existing
Lucene/Elasticsearch direct-query APIs need no separate CLI family unless a concrete unsupported
query workflow justifies it. Frontend search default-index/settings follow the lifecycle contract.

### B08 — Deployment plans and artifacts

Reuse [deployment controllers/services](../../src/OrchardCore.Modules/OrchardCore.Deployment)
and [remote deployment](../../src/OrchardCore.Modules/OrchardCore.Deployment.Remote).
Proposed groups: `deployment plans`, `deployment step-types`, `deployment exports/imports`.

Separate plan/step discovery and CRUD from execution. Define typed step descriptors instead of
serializing arbitrary CLR types. Export/import needs artifact ownership, size/type validation,
status/error reporting and cleanup. Decide whether executions require job IDs before building
remote-target support. Verify a representative plan exported from one disposable tenant imports
into another with expected content/settings. Keep private API-key compatibility deliberate;
new management endpoints must follow shared OAuth/permission rules.

### B09 — Media beyond file CRUD

Use [media profile administration](../../src/OrchardCore.Modules/OrchardCore.Media/Controllers/MediaProfilesController.cs)
and [media cache administration](../../src/OrchardCore.Modules/OrchardCore.Media/Controllers/MediaCacheController.cs).
Proposed groups: `media profiles`, `media cache`.

Ship profiles independently, then tenant-scoped cache invalidation and settings adapters. Verify
profile output, file-policy enforcement and no cross-tenant cache effects. Cloud storage tests
are a separate requirement from local IMediaFileStore tests. Add resumable Tus CLI transfer only
when large-upload automation is a selected outcome; `media uploads show` is only upload-info.
MCP binary/multipart/stream exclusions must be documented rather than counted as accidental gaps.

### B10 — Routes, robots and sitemaps

Reuse [URL rewriting](../../src/OrchardCore.Modules/OrchardCore.UrlRewriting),
[SEO drivers](../../src/OrchardCore.Modules/OrchardCore.Seo/Drivers)
and [SitemapManager](../../src/OrchardCore.Modules/OrchardCore.Sitemaps/Services/SitemapManager.cs).
Proposed groups: `url-rewriting rules`, `sitemaps`, `sitemaps sources`.

Create a full rule contract rather than exposing only reorder. Verify redirect matching, order,
invalid rule rejection and removal. Manage robots settings through B05 conventions. Verify a
sitemap/source configuration change affects the public XML and invalidates the appropriate cache.
Content SeoMetaPart editing remains shared content work.

### B11 — Tenant operations

Start with [audit reads](../../src/OrchardCore.Modules/OrchardCore.AuditTrail/Controllers/AdminController.cs)
and [background task administration](../../src/OrchardCore.Modules/OrchardCore.BackgroundTasks/Controllers/BackgroundTaskController.cs).
Proposed groups: `audit events`, `background-tasks`.

Ship bounded paged audit reads and task list/show first, then task scheduling/enablement.
Explicitly decide which retention/pruning/cache actions and sanitized health summaries are needed.
Only offer run-now/cancel when an underlying service supports the required lifecycle. Verify
an application acting on one tenant cannot inspect or mutate another tenant's task/audit data.
Test schedule validation, disabled-task behavior and safe audit output with sensitive fields.

### B12 — Communications and provider adapters

Use the module-owned settings/drivers and services in
[Email.Smtp](../../src/OrchardCore.Modules/OrchardCore.Email.Smtp),
[Sms](../../src/OrchardCore.Modules/OrchardCore.Sms),
[Notifications](../../src/OrchardCore.Modules/OrchardCore.Notifications), and external-authentication modules.

Start with a redacted SMTP adapter and controlled test-send through a local fake transport.
Prioritize other providers by actual usage; do not implement every Azure/SMS/social adapter at once.
Separate public analytics identifiers from secrets. Notifications first need a decision about
whether operations target a human's inbox, an explicitly authorized user, or application delivery;
the current mark-as-read helper's ambient user ID does not answer that question. Test recipient
permissions, secret readback, provider errors and deterministic behavior without real deliveries.

### B13 — Admin menu and dashboard layout

Reuse [AdminMenuService](../../src/OrchardCore.Modules/OrchardCore.AdminMenu/Services/AdminMenuService.cs)
and [DashboardController](../../src/OrchardCore.Modules/OrchardCore.AdminDashboard/Controllers/DashboardController.cs).
Proposed groups: `admin-menus`, `dashboard`.

Ship node descriptors and menu-tree CRUD/order, then assess what dashboard layout actually needs
beyond B03 content editing. Verify hierarchy, permissions and the resulting admin UI. This work
can move earlier for white-label/back-office setup but is not a prerequisite for a public website.

### B14 — Intentional API-only localization

The [localization smoke policy](../../.scripts/remote-management/README.md) currently requires all
five UI-string/dynamic-translation operations to remain in OpenAPI without CLI metadata.

Choose one of two outcomes: keep the API-only policy and mark the omission intentional, or add
command metadata for UI-string groups/read and dynamic-translation list/set/delete. If changing
policy, update the smoke expectations, command docs and agent skills together. Verify paging,
retry semantics and culture-specific permissions. Do not promise general PO-file editing from
the existing dynamic-data translation contract.

## Suggested sequencing and dependency boundaries

| Stage | Website-building track (default) | Fleet-management track |
| --- | --- | --- |
| Baseline | Source-to-runtime catalog capture and B03 shared workflow audit | Same baseline; preserve unattended install/context tests |
| First useful increment | B01 Layers; B02 Placements and Shortcode templates | B06 application/scope lifecycle; feature profiles separately |
| Next | B05 typed settings; B04 localized content; B10 routing/SEO | B11 audit/tasks; B07 common index lifecycle |
| Broaden | B07 indexing; B06 credential lifecycle; B09 media administration | B08 deployment; B05 remaining tenant configuration; B09 media |
| Demand-driven | B08/B11 operations, B12 providers, B13 admin UI, B02 AdminTemplates | B12 providers, B13 admin UI, public-site composition as needed |
| Explicit policy decision | B14; direct search-query projections; Tus transfer | Same |

B01 and B02 can be separate implementation slices. B05 conventions should precede new module
settings adapters. B07's common contract should precede provider-specific APIs. B08's artifact
and execution contracts should precede remote-target orchestration. B06's application lifecycle
should reuse the existing install/provision path rather than replace it. Long-running work in
B07/B08/B11 should share conventions where their actual service semantics align.

## Definition of done for an API/command slice

A slice is complete when it delivers the selected workflow through the existing architecture:

1. **Contract and domain behavior:** typed requests/responses and schemas, bounded paging where
   applicable, stable operation IDs, useful validation errors, retries/concurrency semantics,
   and reuse of module services/events rather than calling admin controllers or self-HTTP.
2. **Authorization:** shared remote-management bearer authentication plus existing feature/resource
   permissions. Test anonymous, no-management, discovery-only, read-only, appropriately authorized
   human and application principals, and cross-tenant/resource denial relevant to the operation.
3. **Discovery and projection:** feature-gated endpoints, OpenAPI, CLI metadata, sensible generated
   help/output/confirmation, and eligible MCP tool metadata. Verify global endpoint/operation/command
   uniqueness with the feature and relevant subfeatures enabled, disabled and re-enabled. This is
   especially relevant to the previous duplicate Tus endpoint regression.
4. **Transport scope:** eligible MCP tools invoke the same in-process endpoint behavior. The current
   catalog excludes hidden/stream operations and unsupported body/response shapes, including
   binary/multipart cases; specify and test any intended HTTP/Pomi-only operation rather than
   promising automatic MCP parity. See the [catalog](../../src/OrchardCore.Modules/OrchardCore.RemoteManagement/Mcp/McpToolCatalog.cs).
5. **Before/after evidence:** capture the failing or missing workflow before the change, then run
   focused service/endpoint tests and a disposable-tenant Pomi smoke that proves the new workflow.
   Exercise both successful and denied operations. For applicable tools, compare MCP outcomes and
   permissions with HTTP/Pomi. Preserve existing Pomi commands and dynamic cache/revision refresh.
6. **Setup and feature boundaries:** installing with remote management creates a working application
   context without login; omitting it remains supported. Enabling an ordinary feature must not
   silently enable remote management/OpenID. CLI and MCP remain independently selectable projections.
7. **Documentation:** update canonical module/CLI documentation, relevant agent skills and private
   credential handoff instructions when the workflow changes; update the inventory row and record
   intentional exclusions. Run the relevant existing toolkit checks and required repository CI.

Use the [remote-management verification toolkit](../../.scripts/remote-management/README.md) as the
starting point. Do not claim a full functional regression pass based solely on unit tests, a
successful build, or an OpenAPI document that contains the new route.

## Decisions to record before implementation

The plan can proceed without resolving every item at once. For the first selected slice, record
its outcome, included operations, exclusions, prerequisites, owner, acceptance fixture and relative
size. Then record these campaign-level choices as they become relevant:

- Website-building versus fleet-management priority; the default track above is an assumption.
- B14 API-only policy; keep it unless the string-editing automation use case warrants changing it.
- Settings owned by tenants versus configuration owned by deployment; no unrestricted property bag.
- Whether long-running index/deployment operations need a shared job/status contract.
- Whether resumable CLI uploads and additional MCP transfer support justify their scope.
- Which provider integrations have actual users and suitable test environments.

Infrastructure, obsolete aliases, themes and samples should remain explicit “shared / no dedicated
API needed / excluded” entries unless a concrete management workflow changes that decision.
