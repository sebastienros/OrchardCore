# Remote management coverage inventory

Initial source audit of `sebros/remote-tenant-cli-plan` at **`befe0e93c`**, 2026-09-11.
Updated for P02 layer definitions/conditions; the campaign ledger records delivery state.
Implementation order and decision criteria are in the [delivery plan](coverage-plan.md).

P03 adds verified stored shortcode-template coverage after the Layers merge (`79edeff6d`).

## Scope and interpretation

This inventory accounts for **100 module manifests and all 188 declared features** under
`src/OrchardCore.Modules`: **98 production modules / 185 features**, plus two sample modules /
three sample features. The seven bundled themes are listed separately below. External packages,
application-specific modules, framework libraries without module manifests, and test fixtures are
outside this inventory.

The audit resolves both literal and constant feature IDs, including IDs that differ from their
module name. Features are not assumed to inherit their module's API coverage. Obsolete search
feature aliases remain in the inventory but do not create separate backlog items.

**OpenAPI here means a discoverable management contract, not merely an HTTP route.** A controller,
public protocol endpoint, or admin AJAX handler does not establish usable management coverage.
The OpenAPI column explicitly identifies these exceptions. “No dedicated management API” does
not claim the feature has no HTTP endpoints or that no action can appear in a generated document.
The Pomi column lists existing command groups without the `pomi` prefix; proposed groups appear
only in the plan.

This is a source audit of manifests, endpoint registrations, feature gates, controller actions,
CLI metadata, and shared services. It is **not a live all-features OpenAPI snapshot or proof of
end-to-end parity**. Availability depends on the tenant's enabled features, dependencies and
permissions. Shared content coverage establishes an existing transport, not verified parity with
every editor's validation, ordering, permissions or side effects. Those questions are B03 work.
Recipe steps/configuration files can sometimes automate missing areas, but are not counted as
first-class API/command coverage: existing `recipes execute` runs harvested recipes and is not a
generic JSON mutation endpoint.

All features can already participate in shared feature enablement; that alone is not counted as
management of their own resources. Host-owned secrets, middleware and storage providers should
not acquire tenant CRUD APIs solely to eliminate an inventory gap.

## Coverage summary

The categories are mutually exclusive planning classifications, not percentages of functionality.
“Direct” still means only the operations described in that row.

| Code | Classification | Feature count |
| --- | --- | ---: |
| D | Direct management OpenAPI and Pomi commands exist | 15 |
| P | Partial: important operations missing, or only helper/protocol/shared coverage | 27 |
| A | Management OpenAPI exists; Pomi projection intentionally absent | 1 |
| M | Dedicated management API and corresponding commands missing | 36 |
| S | Shared API/commands or a built-in CLI workflow; validate the stated limits | 36 |
| I | Infrastructure, rendering, protocol, provider or alias; no separate API proposed by default | 70 |
| X | Sample; excluded from delivery priorities | 3 |

There are **36 features with neither dedicated management APIs nor commands**, and **27 with
partial coverage requiring a scope decision**. These are not 63 independent implementation tasks:
the plan consolidates them into shared workflows. Another 36 features reuse existing transports
or built-in commands. Eighteen production modules contribute direct `WithCliCommand` operation
registrations; this does not imply all features in those modules are covered.

## Important operation-level distinctions

| Area | Existing coverage | Actual gap or decision | Evidence |
| --- | --- | --- | --- |
| Localization strings | Two UI-string read APIs plus three dynamic-translation APIs | All five intentionally omit CLI metadata; change the product decision and its regression checks before adding commands (B14). | [Culture/string endpoints](../../src/OrchardCore.Modules/OrchardCore.Localization/Endpoints/LocalizationManagementEndpoints.cs), [translation endpoints](../../src/OrchardCore.Modules/OrchardCore.DataLocalization/Endpoints/TranslationManagementEndpoints.cs), [smoke policy](../../.scripts/remote-management/README.md) |
| Lucene / Elasticsearch | Each provides GET/POST content and documents query endpoints; shared named-query management/execution also exists | No direct CLI projection for those query routes; prioritize common index lifecycle rather than another query command family (B07). | [Lucene API](../../src/OrchardCore.Modules/OrchardCore.Lucene/Controllers/LuceneApiController.cs), [Elasticsearch API](../../src/OrchardCore.Modules/OrchardCore.Elasticsearch/Controllers/ElasticsearchApiController.cs), [Queries](../../src/OrchardCore.Modules/OrchardCore.Queries/Endpoints/Api/QueryManagementEndpoints.cs) |
| Site settings | Safe core settings and separate content-type-defined CustomSettings | No generic read/write access to all module settings. The schema contribution interface is not such an API (B05/B12). | [Core service](../../src/OrchardCore.Modules/OrchardCore.Settings/Services/SiteSettingsManagementService.cs), [CustomSettings](../../src/OrchardCore.Modules/OrchardCore.CustomSettings/Endpoints/CustomSettingsManagementEndpoints.cs) |
| Content parts/fields | Generic content JSON and definition CRUD; explicit settings-schema providers in Autoroute, Flows and ContentFields | Validate field/part schemas and lifecycle behavior rather than creating a command family for every part (B03). | [Definition service](../../src/OrchardCore.Modules/OrchardCore.ContentTypes/Services/ContentDefinitionApiService.cs), [ContentPicker settings provider](../../src/OrchardCore.Modules/OrchardCore.ContentFields/Services/ContentFieldsContentDefinitionManagementSchemaProvider.cs) |
| Templates | Frontend TemplatesManager CRUD | AdminTemplates uses a separate manager/document and is not covered (B02). | [Endpoints](../../src/OrchardCore.Modules/OrchardCore.Templates/Endpoints/Management/TemplateManagementEndpoints.cs), [feature startup](../../src/OrchardCore.Modules/OrchardCore.Templates/Startup.cs) |
| Tenants / OpenID | Tenant installation, feature-profile assignment, opt-in application provisioning and automatic context acquisition | Feature-profile definitions and general application/scope/credential lifecycle remain missing (B06). OAuth tokens and MCP public-client registration are not those APIs. | [Tenant endpoints](../../src/OrchardCore.Modules/OrchardCore.Tenants/Endpoints/Management/TenantManagementEndpoints.cs), [OpenID applications](../../src/OrchardCore.Modules/OrchardCore.OpenId/Controllers/ApplicationController.cs), [MCP registration](../../src/OrchardCore.Modules/OrchardCore.OpenId/Controllers/McpClientRegistrationController.cs) |
| Notifications / URL rewriting | Mark-as-read and rule-reorder AJAX endpoints | These are incomplete management surfaces; establish explicit bearer authorization, resource contracts and principal semantics (B12/B10). | [Notification helper](../../src/OrchardCore.Modules/OrchardCore.Notifications/Endpoints/Management/MarkAsReadEndpoints.cs), [reorder helper](../../src/OrchardCore.Modules/OrchardCore.UrlRewriting/Endpoints/Rules/SortRulesEndpoint.cs) |
| GraphQL | Built-in Pomi query/introspection commands | No OpenAPI projection is needed for the existing protocol workflow. | [CLI transport](../../src/OrchardCore.Cli/CliApplication.GraphQL.cs) |
| Media | File/folder CRUD, metadata, constraints, labels and Tus upload-info | Profiles/cache/configuration and a complete resumable-transfer workflow are separate gaps (B09). | [API endpoints](../../src/OrchardCore.Modules/OrchardCore.Media/Endpoints/Api), [Tus registration](../../src/OrchardCore.Modules/OrchardCore.Media/Startup.cs) |
| Deployment | Admin plans/steps/export/import plus a private API-key remote-import protocol | Recipe execution does not replace these management contracts (B08). | [Deployment controllers](../../src/OrchardCore.Modules/OrchardCore.Deployment/Controllers), [remote import](../../src/OrchardCore.Modules/OrchardCore.Deployment.Remote/Controllers/ImportRemoteInstanceController.cs) |

## Complete feature ledger

Each feature appears exactly once. Module links point to the manifest defining the feature;
operation evidence is above and the plan links to the main implementation entry points.
Backlog IDs refer to [the delivery plan](coverage-plan.md#work-packages).

### D — Direct management OpenAPI and Pomi commands exist

| Module / feature ID | OpenAPI or HTTP surface | Existing Pomi coverage | Scope, gap and next action | Plan |
| --- | --- | --- | --- | --- |
| [ContentTypes](../../src/OrchardCore.Modules/OrchardCore.ContentTypes/Manifest.cs) / `OrchardCore.ContentTypes` | Yes | `content types`, `parts`, `fields`, `part-types`, `field-types`, `settings` | Definition CRUD/discovery exists; extension settings-schema completeness belongs to B03. | — |
| [Contents](../../src/OrchardCore.Modules/OrchardCore.Contents/Manifest.cs) / `OrchardCore.Contents` | Yes | `content items`; `content versions` | CRUD, drafts, validation/schema, rendering and version operations exist; extension lifecycle parity belongs to B03. | — |
| [CustomSettings](../../src/OrchardCore.Modules/OrchardCore.CustomSettings/Manifest.cs) / `OrchardCore.CustomSettings` | Yes | `custom-settings` | Content-type-defined custom settings are supported. This does not expose arbitrary module site-settings sections. | — |
| [Features](../../src/OrchardCore.Modules/OrchardCore.Features/Manifest.cs) / `OrchardCore.Features` | Yes | `features` | Existing management surface; preserve regression coverage. | — |
| [HomeRoute](../../src/OrchardCore.Modules/OrchardCore.HomeRoute/Manifest.cs) / `OrchardCore.HomeRoute` | Yes | `settings set-home-content` | Existing management surface; preserve regression coverage. | — |
| [Queries](../../src/OrchardCore.Modules/OrchardCore.Queries/Manifest.cs) / `OrchardCore.Queries` | Yes | `queries`; `queries sources` | Definitions, validation, source discovery and execution exist. Backends require their feature to be enabled. | — |
| [Recipes](../../src/OrchardCore.Modules/OrchardCore.Recipes/Manifest.cs) / `OrchardCore.Recipes` | Yes | `recipes` | List/show/execute existing non-setup recipes. Not arbitrary recipe upload, plan editing or deployment import. | — |
| [Roles](../../src/OrchardCore.Modules/OrchardCore.Roles/Manifest.cs) / `OrchardCore.Roles` | Yes | `roles` | Existing management surface; preserve regression coverage. | — |
| [Settings](../../src/OrchardCore.Modules/OrchardCore.Settings/Manifest.cs) / `OrchardCore.Settings` | Yes | `settings show`, `update`, `schema` | Safe core properties only. Schema contributions currently include CustomSettings; there is no arbitrary module-section read/write endpoint. | — |
| [Shortcodes](../../src/OrchardCore.Modules/OrchardCore.Shortcodes/Manifest.cs) / `OrchardCore.Shortcodes.Templates` | Yes | `shortcodes templates` | P03: stored template CRUD/validation and live schemas, shared admin/recipe services, normalized retries, sanitized usage, rendered output and feature gates verified. Code-defined providers remain separate. | B02 |
| [Templates](../../src/OrchardCore.Modules/OrchardCore.Templates/Manifest.cs) / `OrchardCore.Templates` | Yes | `templates` | Frontend template document CRUD exists. It does not address the separate AdminTemplatesManager. | — |
| [Tenants](../../src/OrchardCore.Modules/OrchardCore.Tenants/Manifest.cs) / `OrchardCore.Tenants` | Yes | `tenants` | Lifecycle/install and application provisioning exist; feature-profile definitions remain a separate gap. | — |
| [Themes](../../src/OrchardCore.Modules/OrchardCore.Themes/Manifest.cs) / `OrchardCore.Themes` | Yes | `themes` | Theme discovery and activation exist; source-code/theme asset editing is not theme activation. | — |
| [Users](../../src/OrchardCore.Modules/OrchardCore.Users/Manifest.cs) / `OrchardCore.Users` | Yes | `users` | Basic account lifecycle, roles and password fields exist. Custom-user settings and MFA administration are separate gaps. | — |
| [Workflows](../../src/OrchardCore.Modules/OrchardCore.Workflows/Manifest.cs) / `OrchardCore.Workflows` | Yes | `workflow types`, `activity-types`, `instances` | Definition CRUD/validation, activity schemas, execution and instance list/show/cancel exist; extensions share these operations. | — |

### P — Partial: important operations missing, or only helper/protocol/shared coverage

| Module / feature ID | OpenAPI or HTTP surface | Existing Pomi coverage | Scope, gap and next action | Plan |
| --- | --- | --- | --- | --- |
| [ContentLocalization](../../src/OrchardCore.Modules/OrchardCore.ContentLocalization/Manifest.cs) / `OrchardCore.ContentLocalization` | Shared content payloads | Shared content commands | No first-class localize/clone-to-culture or localization-set management operation; assigning raw part JSON is not equivalent to LocalizeAsync. | B04 |
| [Deployment.Remote](../../src/OrchardCore.Modules/OrchardCore.Deployment.Remote/Manifest.cs) / `OrchardCore.Deployment.Remote` | Private API-key import protocol | None | Remote clients/instances/targets remain admin-only; existing import authentication is not the shared OAuth management contract. | B08 |
| [Elasticsearch](../../src/OrchardCore.Modules/OrchardCore.Elasticsearch/Manifest.cs) / `OrchardCore.Elasticsearch` | Content/documents query API | Shared `queries` | Direct query endpoints lack CLI metadata; named-query execution is shared. Index lifecycle remains missing; avoid duplicating query transports. | B07 |
| [Facebook](../../src/OrchardCore.Modules/OrchardCore.Facebook/Manifest.cs) / `OrchardCore.Facebook` | SDK helper; no management contract | None | Provider/widget/pixel settings are not managed through OpenAPI/Pomi. Authentication callbacks are not administration APIs. | B12 |
| [Layers](../../src/OrchardCore.Modules/OrchardCore.Layers/Manifest.cs) / `OrchardCore.Layers` | Layer CRUD and condition descriptor/validation APIs | `layers` | P02 provides definitions, normalized retries, restricted OAuth principals and eligible MCP tools; live rendering and feature gates verified. Widget attachment/movement/order remains P05. | B01 |
| [Localization](../../src/OrchardCore.Modules/OrchardCore.Localization/Manifest.cs) / `OrchardCore.Localization` | Culture/settings APIs plus 2 string APIs | `localization cultures`; `localization settings` | Culture management has parity. String-group discovery and translated strings intentionally lack CLI metadata. | B14 |
| [Lucene](../../src/OrchardCore.Modules/OrchardCore.Lucene/Manifest.cs) / `OrchardCore.Lucene` | Content/documents query API | Shared `queries` | Direct query endpoints lack CLI metadata; named-query execution is shared. Index lifecycle remains missing; avoid duplicating query transports. | B07 |
| [Media](../../src/OrchardCore.Modules/OrchardCore.Media/Manifest.cs) / `OrchardCore.Media` | File/folder/metadata management APIs | `media` groups | Files/folders, metadata, constraints and UI labels have commands. Media profile CRUD and media settings remain gaps. | B09 |
| [Media](../../src/OrchardCore.Modules/OrchardCore.Media/Manifest.cs) / `OrchardCore.Media.Tus` | Tus upload protocol plus info API | `media uploads show` | Upload-info has a command. A resumable Tus transfer workflow is not supplied by that command; prioritize only for large-upload demand. | B09 |
| [Notifications](../../src/OrchardCore.Modules/OrchardCore.Notifications/Manifest.cs) / `OrchardCore.Notifications` | Mark-as-read AJAX helper | None | Missing supported list/show/manage-inbox contract. Current helper relies on a user identity; define human inbox versus application-principal behavior first. | B12 |
| [OpenId](../../src/OrchardCore.Modules/OrchardCore.OpenId/Manifest.cs) / `OrchardCore.OpenId.Server` | OAuth/OIDC protocol | Built-in login/token use | Server configuration is admin-only; token issuance does not provide application/scope administration. | B06 |
| [OpenId](../../src/OrchardCore.Modules/OrchardCore.OpenId/Manifest.cs) / `OrchardCore.OpenId.RemoteManagement.Mcp` | MCP OAuth discovery/dynamic registration | No general security-admin commands | Security configuration UI/protocol exists; public MCP-client registration does not replace confidential application lifecycle management. | B06 |
| [Seo](../../src/OrchardCore.Modules/OrchardCore.Seo/Manifest.cs) / `OrchardCore.Seo` | Shared SEO content payload | Shared content commands | SeoMetaPart travels with content; robots/site-level SEO settings lack management operations. | B10 |
| [Sitemaps](../../src/OrchardCore.Modules/OrchardCore.Sitemaps/Manifest.cs) / `OrchardCore.Sitemaps` | Public sitemap XML | None | Sitemap/source definitions, enablement and cache controls lack management APIs. | B10 |
| [Tenants](../../src/OrchardCore.Modules/OrchardCore.Tenants/Manifest.cs) / `OrchardCore.Tenants.FeatureProfiles` | Profile assignment through tenant APIs | Shared tenant create/update/install | Tenant requests accept profile names, but profile definition list/show/CRUD is admin-only. | B06 |
| [Twitter](../../src/OrchardCore.Modules/OrchardCore.Twitter/Manifest.cs) / `OrchardCore.Twitter` | Shared workflow activity contracts | Shared `workflow` commands | Integration activities use Workflows when enabled; Twitter client/provider settings lack management. | B12 |
| [UrlRewriting](../../src/OrchardCore.Modules/OrchardCore.UrlRewriting/Manifest.cs) / `OrchardCore.UrlRewriting` | Rule-reordering AJAX helper | None | Missing rule list/show/CRUD and condition/ordering validation under shared OAuth authorization. | B10 |
| [Users](../../src/OrchardCore.Modules/OrchardCore.Users/Manifest.cs) / `OrchardCore.Users.ExternalAuthentication` | User web/authentication flows | Base `users` only | Feature-specific settings/security administration lacks a management contract. Preserve user-consent/challenge semantics; do not project send-code or recovery endpoints blindly. | B06 |
| [Users](../../src/OrchardCore.Modules/OrchardCore.Users/Manifest.cs) / `OrchardCore.Users.ChangeEmail` | User web/authentication flows | Base `users` only | Feature-specific settings/security administration lacks a management contract. Preserve user-consent/challenge semantics; do not project send-code or recovery endpoints blindly. | B06 |
| [Users](../../src/OrchardCore.Modules/OrchardCore.Users/Manifest.cs) / `OrchardCore.Users.Registration` | User web/authentication flows | Base `users` only | Feature-specific settings/security administration lacks a management contract. Preserve user-consent/challenge semantics; do not project send-code or recovery endpoints blindly. | B06 |
| [Users](../../src/OrchardCore.Modules/OrchardCore.Users/Manifest.cs) / `OrchardCore.Users.ResetPassword` | User web/authentication flows | Base `users` only | Feature-specific settings/security administration lacks a management contract. Preserve user-consent/challenge semantics; do not project send-code or recovery endpoints blindly. | B06 |
| [Users](../../src/OrchardCore.Modules/OrchardCore.Users/Manifest.cs) / `OrchardCore.Users.TimeZone` | User web/authentication flows | Base `users` only | Feature-specific settings/security administration lacks a management contract. Preserve user-consent/challenge semantics; do not project send-code or recovery endpoints blindly. | B06 |
| [Users](../../src/OrchardCore.Modules/OrchardCore.Users/Manifest.cs) / `OrchardCore.Users.Localization` | User web/authentication flows | Base `users` only | Feature-specific settings/security administration lacks a management contract. Preserve user-consent/challenge semantics; do not project send-code or recovery endpoints blindly. | B06 |
| [Users](../../src/OrchardCore.Modules/OrchardCore.Users/Manifest.cs) / `OrchardCore.Users.2FA` | User web/authentication flows | Base `users` only | Feature-specific settings/security administration lacks a management contract. Preserve user-consent/challenge semantics; do not project send-code or recovery endpoints blindly. | B06 |
| [Users](../../src/OrchardCore.Modules/OrchardCore.Users/Manifest.cs) / `OrchardCore.Users.2FA.AuthenticatorApp` | User web/authentication flows | Base `users` only | Feature-specific settings/security administration lacks a management contract. Preserve user-consent/challenge semantics; do not project send-code or recovery endpoints blindly. | B06 |
| [Users](../../src/OrchardCore.Modules/OrchardCore.Users/Manifest.cs) / `OrchardCore.Users.2FA.Email` | User web/authentication flows | Base `users` only | Feature-specific settings/security administration lacks a management contract. Preserve user-consent/challenge semantics; do not project send-code or recovery endpoints blindly. | B06 |
| [Users](../../src/OrchardCore.Modules/OrchardCore.Users/Manifest.cs) / `OrchardCore.Users.2FA.Sms` | User web/authentication flows | Base `users` only | Feature-specific settings/security administration lacks a management contract. Preserve user-consent/challenge semantics; do not project send-code or recovery endpoints blindly. | B06 |

### A — Management OpenAPI exists; Pomi projection intentionally absent

| Module / feature ID | OpenAPI or HTTP surface | Existing Pomi coverage | Scope, gap and next action | Plan |
| --- | --- | --- | --- | --- |
| [DataLocalization](../../src/OrchardCore.Modules/OrchardCore.DataLocalization/Manifest.cs) / `OrchardCore.DataLocalization` | Yes: 3 translation operations | None by design | Dynamic data translations list/set/delete deliberately lack CLI metadata. UI PO catalogs are outside this contract. | B14 |

### M — Dedicated management API and corresponding commands missing

| Module / feature ID | OpenAPI or HTTP surface | Existing Pomi coverage | Scope, gap and next action | Plan |
| --- | --- | --- | --- | --- |
| [AdminMenu](../../src/OrchardCore.Modules/OrchardCore.AdminMenu/Manifest.cs) / `OrchardCore.AdminMenu` | No dedicated management API | None | Admin menu documents, node types, hierarchy and ordering have admin UI/services, but no management contract. These are separate from frontend Menu content. | B13 |
| [AuditTrail](../../src/OrchardCore.Modules/OrchardCore.AuditTrail/Manifest.cs) / `OrchardCore.AuditTrail` | No dedicated management API | None | Missing paged event search/show/export and retention settings; reuse the audit query/filter model. | B11 |
| [AzureAI](../../src/OrchardCore.Modules/OrchardCore.AzureAI/Manifest.cs) / `OrchardCore.AzureAI` | No dedicated management API | None | Index lifecycle/configuration is missing from management APIs. Start with the common Indexing abstraction, then provider-specific settings. | B07 |
| [BackgroundTasks](../../src/OrchardCore.Modules/OrchardCore.BackgroundTasks/Manifest.cs) / `OrchardCore.BackgroundTasks` | No dedicated management API | None | Task listing, schedule/configuration and enable/disable are admin-only. Decide whether run-now has a supported service contract. | B11 |
| [ContentLocalization](../../src/OrchardCore.Modules/OrchardCore.ContentLocalization/Manifest.cs) / `OrchardCore.ContentLocalization.ContentCulturePicker` | No dedicated management API | None | Culture picker/request-culture settings lack management; this is separate from culture list/settings APIs. | B04 |
| [Contents](../../src/OrchardCore.Modules/OrchardCore.Contents/Manifest.cs) / `OrchardCore.Contents.VersionPruning` | No dedicated management API | None | Version list/delete exists in base Contents, but automated pruning policy/settings lack management. | B11 |
| [Contents](../../src/OrchardCore.Modules/OrchardCore.Contents/Manifest.cs) / `OrchardCore.Contents.Deployment.ExportContentToDeploymentTarget` | No dedicated management API | None | Admin export/add-to-plan/download action; define shared deployment operations and artifact handling. | B08 |
| [Contents](../../src/OrchardCore.Modules/OrchardCore.Contents/Manifest.cs) / `OrchardCore.Contents.Deployment.AddToDeploymentPlan` | No dedicated management API | None | Admin export/add-to-plan/download action; define shared deployment operations and artifact handling. | B08 |
| [Contents](../../src/OrchardCore.Modules/OrchardCore.Contents/Manifest.cs) / `OrchardCore.Contents.Deployment.Download` | No dedicated management API | None | Admin export/add-to-plan/download action; define shared deployment operations and artifact handling. | B08 |
| [Cors](../../src/OrchardCore.Modules/OrchardCore.Cors/Manifest.cs) / `OrchardCore.Cors` | No dedicated management API | None | Policy listing/CRUD/validation is admin-only; use a typed section and existing permissions. | B05 |
| [Deployment](../../src/OrchardCore.Modules/OrchardCore.Deployment/Manifest.cs) / `OrchardCore.Deployment` | No dedicated management API | None | Plan/step CRUD, step schemas, export artifacts and import execution lack a management contract. Existing recipe execution does not cover plan editing or arbitrary archive import. | B08 |
| [Email](../../src/OrchardCore.Modules/OrchardCore.Email/Manifest.cs) / `OrchardCore.Email` | No dedicated management API | None | Delivery configuration and test-send are admin-only; define redacted settings and bounded delivery checks. | B12 |
| [Email.Smtp](../../src/OrchardCore.Modules/OrchardCore.Email.Smtp/Manifest.cs) / `OrchardCore.Email.Smtp` | No dedicated management API | None | Tenant SMTP settings need an explicit redacted read/write contract; core settings commands do not cover them. | B12 |
| [Facebook](../../src/OrchardCore.Modules/OrchardCore.Facebook/Manifest.cs) / `OrchardCore.Facebook.Login` | No dedicated management API | None | External-login configuration lacks a management contract; OAuth callbacks and the core SDK helper do not administer it. | B12 |
| [Facebook](../../src/OrchardCore.Modules/OrchardCore.Facebook/Manifest.cs) / `OrchardCore.Facebook.Pixel` | No dedicated management API | None | Pixel tracking settings lack a management contract. This feature does not depend on Facebook core SDK endpoints. | B12 |
| [GitHub](../../src/OrchardCore.Modules/OrchardCore.GitHub/Manifest.cs) / `OrchardCore.GitHub.Authentication` | No dedicated management API | None | External login provider settings need a redacted administration contract; OAuth login callbacks are separate. | B12 |
| [Google](../../src/OrchardCore.Modules/OrchardCore.Google/Manifest.cs) / `OrchardCore.Google.GoogleAuthentication` | No dedicated management API | None | Authentication/analytics/tag-manager settings lack management contracts; handle secrets separately from public identifiers. | B12 |
| [Google](../../src/OrchardCore.Modules/OrchardCore.Google/Manifest.cs) / `OrchardCore.Google.Analytics` | No dedicated management API | None | Authentication/analytics/tag-manager settings lack management contracts; handle secrets separately from public identifiers. | B12 |
| [Google](../../src/OrchardCore.Modules/OrchardCore.Google/Manifest.cs) / `OrchardCore.Google.TagManager` | No dedicated management API | None | Authentication/analytics/tag-manager settings lack management contracts; handle secrets separately from public identifiers. | B12 |
| [Https](../../src/OrchardCore.Modules/OrchardCore.Https/Manifest.cs) / `OrchardCore.Https` | No dedicated management API | None | HTTPS/HSTS tenant settings lack a management section; validate transport and restart effects. | B05 |
| [Indexing](../../src/OrchardCore.Modules/OrchardCore.Indexing/Manifest.cs) / `OrchardCore.Indexing` | No dedicated management API | None | Missing provider/index list, definitions, reset/synchronize/rebuild and status. Reuse common index management; do not implement one complete API per backend. | B07 |
| [Media](../../src/OrchardCore.Modules/OrchardCore.Media/Manifest.cs) / `OrchardCore.Media.Cache` | No dedicated management API | None | Media cache purge/settings are admin-only; define a tenant-scoped operation. | B09 |
| [Microsoft.Authentication](../../src/OrchardCore.Modules/OrchardCore.Microsoft.Authentication/Manifest.cs) / `OrchardCore.Microsoft.Authentication.MicrosoftAccount` | No dedicated management API | None | Microsoft Account/Azure AD provider configuration lacks a redacted management contract. | B12 |
| [Microsoft.Authentication](../../src/OrchardCore.Modules/OrchardCore.Microsoft.Authentication/Manifest.cs) / `OrchardCore.Microsoft.Authentication.AzureAD` | No dedicated management API | None | Microsoft Account/Azure AD provider configuration lacks a redacted management contract. | B12 |
| [OpenId](../../src/OrchardCore.Modules/OrchardCore.OpenId/Manifest.cs) / `OrchardCore.OpenId.Client` | No dedicated management API | None | Client/provider settings lack a management contract; authentication callbacks remain protocol endpoints. | B06 |
| [OpenId](../../src/OrchardCore.Modules/OrchardCore.OpenId/Manifest.cs) / `OrchardCore.OpenId.Management` | No dedicated management API | None | Application/scope CRUD, role/grant assignment, credential rotation/revocation and redacted status lack general management APIs. Bootstrap provisioning is narrower. | B06 |
| [OpenId](../../src/OrchardCore.Modules/OrchardCore.OpenId/Manifest.cs) / `OrchardCore.OpenId.Validation` | No dedicated management API | None | Validation/authority configuration lacks management; may remain deployment-owned depending on scenario. | B06 |
| [Placements](../../src/OrchardCore.Modules/OrchardCore.Placements/Manifest.cs) / `OrchardCore.Placements` | No dedicated management API | None | Shape placement CRUD/filtering lacks a management API; preserve storage and matching semantics. | B02 |
| [RateLimits](../../src/OrchardCore.Modules/OrchardCore.RateLimits/Manifest.cs) / `OrchardCore.RateLimits` | No dedicated management API | None | Limiter definitions, ordering/validation and tenant settings are admin-only. | B05 |
| [ReCaptcha](../../src/OrchardCore.Modules/OrchardCore.ReCaptcha/Manifest.cs) / `OrchardCore.ReCaptcha` | No dedicated management API | None | Tenant validation settings lack a redacted section; do not expose challenge bypass/validation as an admin command. | B12 |
| [Search](../../src/OrchardCore.Modules/OrchardCore.Search/Manifest.cs) / `OrchardCore.Search` | No dedicated management API | None | Frontend search settings/default index selection lack typed management; query execution does not manage indexes. | B07 |
| [Security](../../src/OrchardCore.Modules/OrchardCore.Security/Manifest.cs) / `OrchardCore.Security` | No dedicated management API | None | Security header/CSP settings lack a typed management section. | B05 |
| [Sms](../../src/OrchardCore.Modules/OrchardCore.Sms/Manifest.cs) / `OrchardCore.Sms` | No dedicated management API | None | SMS configuration and test-send lack a management contract; use redacted settings and bounded checks. | B12 |
| [Templates](../../src/OrchardCore.Modules/OrchardCore.Templates/Manifest.cs) / `OrchardCore.AdminTemplates` | No dedicated management API | None | Separate admin template document is not exposed by the frontend templates API. | B02 |
| [Twitter](../../src/OrchardCore.Modules/OrchardCore.Twitter/Manifest.cs) / `OrchardCore.Twitter.Signin` | No dedicated management API | None | Provider settings lack a redacted management contract; sign-in is an authentication protocol. | B12 |
| [Users](../../src/OrchardCore.Modules/OrchardCore.Users/Manifest.cs) / `OrchardCore.Users.CustomUserSettings` | No dedicated management API | None | User-specific content settings lack management/schema operations; site CustomSettings does not cover them. | B06 |

### S — Shared API/commands or a built-in CLI workflow; validate the stated limits

| Module / feature ID | OpenAPI or HTTP surface | Existing Pomi coverage | Scope, gap and next action | Plan |
| --- | --- | --- | --- | --- |
| [AdminDashboard](../../src/OrchardCore.Modules/OrchardCore.AdminDashboard/Manifest.cs) / `OrchardCore.AdminDashboard` | Shared content API | Shared content commands | Dashboard widgets are content. Dashboard layout/position update remains an admin action; assess shared-content parity before adding layout commands. | B13 |
| [Alias](../../src/OrchardCore.Modules/OrchardCore.Alias/Manifest.cs) / `OrchardCore.Alias` | Shared content API | Shared content commands | AliasPart travels with content; verify alias uniqueness and definition-setting discovery. | B03 |
| [Apis.GraphQL](../../src/OrchardCore.Modules/OrchardCore.Apis.GraphQL/Manifest.cs) / `OrchardCore.Apis.GraphQL` | GraphQL protocol; no OpenAPI projection | Built-in `graphql` | Queries and introspection already have a dedicated CLI transport. GraphQL configuration is a separate settings candidate. | B05 |
| [ArchiveLater](../../src/OrchardCore.Modules/OrchardCore.ArchiveLater/Manifest.cs) / `OrchardCore.ArchiveLater` | Shared content API | Shared content commands | ArchiveLaterPart is content; verify schedule round-trip and execution semantics before deciding on a convenience command. | B03 |
| [AutoSetup](../../src/OrchardCore.Modules/OrchardCore.AutoSetup/Manifest.cs) / `OrchardCore.AutoSetup` | Setup infrastructure, not a management API | Built-in `install`; shared tenant install | Unattended setup and optional application-context provisioning already exist. Do not add another password/token setup API. | — |
| [Autoroute](../../src/OrchardCore.Modules/OrchardCore.Autoroute/Manifest.cs) / `OrchardCore.Autoroute` | Shared content API | Shared content commands | AutoroutePart is content and contributes definition-settings schema; no separate route CRUD needed for ordinary content URLs. | B03 |
| [ContentFields](../../src/OrchardCore.Modules/OrchardCore.ContentFields/Manifest.cs) / `OrchardCore.ContentFields` | Shared content API | Shared content commands | Field definitions/values use shared APIs. Settings-schema providers cover selected settings, not every editor or picker; verify relationship fields. | B03 |
| [ContentPreview](../../src/OrchardCore.Modules/OrchardCore.ContentPreview/Manifest.cs) / `OrchardCore.ContentPreview` | Preview web flow; shared render API | `content items render`; `content versions render` | Rendering exists. Browser preview tokens and unsaved editor payloads are separate UX; defer unless a workflow needs them. | — |
| [Elasticsearch](../../src/OrchardCore.Modules/OrchardCore.Elasticsearch/Manifest.cs) / `OrchardCore.Search.Elasticsearch.ContentPicker` | Shared content API | Shared content commands | Content picker editor extension; shared field values/settings, no dedicated picker CLI. | B03 |
| [Facebook](../../src/OrchardCore.Modules/OrchardCore.Facebook/Manifest.cs) / `OrchardCore.Facebook.Widgets` | Shared content API | Shared content commands | Social plugin widgets use shared content; SDK delivery comes from the Facebook core dependency. Provider settings remain B12. | B03 |
| [Flows](../../src/OrchardCore.Modules/OrchardCore.Flows/Manifest.cs) / `OrchardCore.Flows` | Shared content API | Shared content commands | FlowPart/BagPart use content JSON; a definition-settings provider exists. Verify nested content permissions and ordering. | B03 |
| [Forms](../../src/OrchardCore.Modules/OrchardCore.Forms/Manifest.cs) / `OrchardCore.Forms` | Shared content API | Shared content commands | Form widgets use content and workflow definitions; submission is a public workflow, not a management CRUD resource. | B03 |
| [Html](../../src/OrchardCore.Modules/OrchardCore.Html/Manifest.cs) / `OrchardCore.Html` | Shared content API | Shared content commands | HtmlBodyPart values use content API; no standalone HTML-resource endpoint needed. | B03 |
| [Liquid](../../src/OrchardCore.Modules/OrchardCore.Liquid/Manifest.cs) / `OrchardCore.Liquid` | Editor intellisense helper; shared content API | Shared content/templates commands | Liquid content values are managed through content. Editor intellisense is not a missing CLI operation. | — |
| [Lists](../../src/OrchardCore.Modules/OrchardCore.Lists/Manifest.cs) / `OrchardCore.Lists` | Shared content API | Shared content commands | ListPart/ContainedPart use content. Verify attach/detach/move/order invariants; admin ordering and remote publishing are not dedicated management APIs. | B03 |
| [Lucene](../../src/OrchardCore.Modules/OrchardCore.Lucene/Manifest.cs) / `OrchardCore.Search.Lucene.ContentPicker` | Shared content API | Shared content commands | Content picker editor extension; shared field values/settings, no dedicated picker CLI. | B03 |
| [Markdown](../../src/OrchardCore.Modules/OrchardCore.Markdown/Manifest.cs) / `OrchardCore.Markdown` | Shared content API | Shared content commands | MarkdownBodyPart uses content; no standalone body endpoint needed. | B03 |
| [Media](../../src/OrchardCore.Modules/OrchardCore.Media/Manifest.cs) / `OrchardCore.Media.Security` | Shared media authorization | Shared `media` commands | User-media permissions are enforced by shared operations; application identities do not automatically have a user folder. | B09 |
| [Media.AmazonS3](../../src/OrchardCore.Modules/OrchardCore.Media.AmazonS3/Manifest.cs) / `OrchardCore.Media.AmazonS3` | Shared media file API | Shared `media` commands | File operations use IMediaFileStore. Provider configuration and cache administration are distinct; cloud-provider parity needs its own verification. | B09 |
| [Media.Azure](../../src/OrchardCore.Modules/OrchardCore.Media.Azure/Manifest.cs) / `OrchardCore.Media.Azure.Storage` | Shared media file API | Shared `media` commands | File operations use IMediaFileStore. Provider configuration and cache administration are distinct; cloud-provider parity needs its own verification. | B09 |
| [Menu](../../src/OrchardCore.Modules/OrchardCore.Menu/Manifest.cs) / `OrchardCore.Menu` | Shared content API | Shared content commands | Frontend menus and nested items are content. Verify hierarchy/reorder/link/permission handling before adding convenience commands; different from AdminMenu. | B03 |
| [OpenId](../../src/OrchardCore.Modules/OrchardCore.OpenId/Manifest.cs) / `OrchardCore.OpenId.RemoteManagement` | Shared tenant provisioning | `tenants enable-remote-management`; install opt-in | Shared security bootstrap exists; application inventory/rotation remains B06. | B06 |
| [OpenId](../../src/OrchardCore.Modules/OrchardCore.OpenId/Manifest.cs) / `OrchardCore.OpenId.RemoteManagement.Cli` | Shared application provisioning | Install/provision context acquisition | Unattended application contexts already avoid interactive login. Extend lifecycle via B06, not a second setup flow. | B06 |
| [PublishLater](../../src/OrchardCore.Modules/OrchardCore.PublishLater/Manifest.cs) / `OrchardCore.PublishLater` | Shared content API | Shared content commands | PublishLaterPart is content; verify schedule round-trip and execution semantics before deciding on a convenience command. | B03 |
| [Queries](../../src/OrchardCore.Modules/OrchardCore.Queries/Manifest.cs) / `OrchardCore.Queries.Sql` | Shared query API | Shared `queries` | SQL source integrates into query definitions/execution; no direct database administration API proposed. | — |
| [RemoteManagement](../../src/OrchardCore.Modules/OrchardCore.RemoteManagement/Manifest.cs) / `OrchardCore.RemoteManagement` | Bootstrap/manifest endpoints | Built-in context/discovery | Already supplies shared permissions, OAuth discovery and the operation catalog. | — |
| [RemoteManagement](../../src/OrchardCore.Modules/OrchardCore.RemoteManagement/Manifest.cs) / `OrchardCore.RemoteManagement.Mcp` | MCP protocol/tools | Not a Pomi command group | Tool catalog uses eligible endpoint metadata and in-process delegates. Binary/multipart/stream operations have deliberate exclusions. | — |
| [RemoteManagement](../../src/OrchardCore.Modules/OrchardCore.RemoteManagement/Manifest.cs) / `OrchardCore.RemoteManagement.Cli` | OpenAPI CLI metadata transformer | Dynamic Pomi commands | Projection infrastructure exists; new operations should contribute metadata rather than hardcoded CLI parsers. | — |
| [Setup](../../src/OrchardCore.Modules/OrchardCore.Setup/Manifest.cs) / `OrchardCore.Setup` | Setup web flow; shared tenant setup API | Built-in `install`; `tenants setup/install` | Setup already supports creating the administrator and opt-in application context. Installing without remote management remains supported. | — |
| [Spatial](../../src/OrchardCore.Modules/OrchardCore.Spatial/Manifest.cs) / `OrchardCore.Spatial` | Shared content API | Shared content commands | Geo point fields use shared content/definition APIs; verify coordinate validation/schema before any specialized command. | B03 |
| [Taxonomies](../../src/OrchardCore.Modules/OrchardCore.Taxonomies/Manifest.cs) / `OrchardCore.Taxonomies` | Shared content API | Shared content commands | Taxonomy/term/field content uses shared APIs. Verify term hierarchy, references and tag creation; do not count admin picker actions as management coverage. | B03 |
| [Title](../../src/OrchardCore.Modules/OrchardCore.Title/Manifest.cs) / `OrchardCore.Title` | Shared content API | Shared content commands | TitlePart uses shared content; no title-specific command needed. | B03 |
| [Widgets](../../src/OrchardCore.Modules/OrchardCore.Widgets/Manifest.cs) / `OrchardCore.Widgets` | Shared content API | Shared content commands | Widgets are content and definitions; placement in layers is the B01 gap. | B01, B03 |
| [Workflows](../../src/OrchardCore.Modules/OrchardCore.Workflows/Manifest.cs) / `OrchardCore.Workflows.Http` | Shared workflow definition/execution API | Shared `workflow` commands | Activities integrate with workflow types/instances. HTTP trigger/session behavior is runtime protocol, not a separate CRUD resource. | — |
| [Workflows](../../src/OrchardCore.Modules/OrchardCore.Workflows/Manifest.cs) / `OrchardCore.Workflows.Timers` | Shared workflow definition/execution API | Shared `workflow` commands | Activities integrate with workflow types/instances. HTTP trigger/session behavior is runtime protocol, not a separate CRUD resource. | — |
| [Workflows](../../src/OrchardCore.Modules/OrchardCore.Workflows/Manifest.cs) / `OrchardCore.Workflows.Session` | Shared workflow definition/execution API | Shared `workflow` commands | Activities integrate with workflow types/instances. HTTP trigger/session behavior is runtime protocol, not a separate CRUD resource. | — |

### I — Infrastructure, rendering, protocol, provider or alias; no separate API proposed by default

| Module / feature ID | OpenAPI or HTTP surface | Existing Pomi coverage | Scope, gap and next action | Plan |
| --- | --- | --- | --- | --- |
| [Admin](../../src/OrchardCore.Modules/OrchardCore.Admin/Manifest.cs) / `OrchardCore.Admin` | No dedicated management API | No dedicated command | Admin shell and navigation infrastructure; feature enablement is already shared. | — |
| [Antivirus](../../src/OrchardCore.Modules/OrchardCore.Antivirus/Manifest.cs) / `OrchardCore.Antivirus.ClamAV` | No dedicated management API | No dedicated command | ClamAV integration configured by the host; retain host configuration unless tenant administration is explicitly needed. | — |
| [AzureAI](../../src/OrchardCore.Modules/OrchardCore.AzureAI/Manifest.cs) / `OrchardCore.Search.AzureAI` | No dedicated management API | No dedicated command | Obsolete compatibility feature ID; enables its replacement automatically. Do not create a parallel API/CLI. | — |
| [ContentFields](../../src/OrchardCore.Modules/OrchardCore.ContentFields/Manifest.cs) / `OrchardCore.ContentFields.Indexing.SQL` | No dedicated management API | No dedicated command | SQL indexing extension; no separate managed resource. | — |
| [ContentFields](../../src/OrchardCore.Modules/OrchardCore.ContentFields/Manifest.cs) / `OrchardCore.ContentFields.Indexing.SQL.UserPicker` | No dedicated management API | No dedicated command | SQL indexing extension; no separate managed resource. | — |
| [ContentLocalization](../../src/OrchardCore.Modules/OrchardCore.ContentLocalization/Manifest.cs) / `OrchardCore.ContentLocalization.Sitemaps` | No dedicated management API | No dedicated command | Sitemap provider extension; source administration belongs to B10. | — |
| [Contents](../../src/OrchardCore.Modules/OrchardCore.Contents/Manifest.cs) / `OrchardCore.Contents.FileContentDefinition` | No dedicated management API | No dedicated command | Content-definition storage implementation; shared definition operations, no file-store API. | — |
| [DataProtection.Azure](../../src/OrchardCore.Modules/OrchardCore.DataProtection.Azure/Manifest.cs) / `OrchardCore.DataProtection.Azure` | No dedicated management API | No dedicated command | Key persistence provider; host credentials/configuration, not tenant resource CRUD. | — |
| [Diagnostics](../../src/OrchardCore.Modules/OrchardCore.Diagnostics/Manifest.cs) / `OrchardCore.Diagnostics` | No dedicated management API | No dedicated command | Error-page handling; do not expose exception internals as a new management API. Operational status belongs to B11. | — |
| [DynamicCache](../../src/OrchardCore.Modules/OrchardCore.DynamicCache/Manifest.cs) / `OrchardCore.DynamicCache` | No dedicated management API | No dedicated command | Caching implementation/content behavior. Add scoped inspection/invalidation only if an operations use case warrants B11. | — |
| [Elasticsearch](../../src/OrchardCore.Modules/OrchardCore.Elasticsearch/Manifest.cs) / `OrchardCore.Search.Elasticsearch` | No dedicated management API | No dedicated command | Obsolete compatibility feature ID; enables its replacement automatically. Do not create a parallel API/CLI. | — |
| [Elasticsearch](../../src/OrchardCore.Modules/OrchardCore.Elasticsearch/Manifest.cs) / `OrchardCore.Search.Elasticsearch.Worker` | No dedicated management API | No dedicated command | Indexing/background extension; index administration belongs to B07 and task controls to B11. | — |
| [Email.Azure](../../src/OrchardCore.Modules/OrchardCore.Email.Azure/Manifest.cs) / `OrchardCore.Email.Azure` | No dedicated management API | No dedicated command | Provider setup/credentials: B12 only for settings owned by the tenant; retain deployment-owned configuration. | — |
| [Feeds](../../src/OrchardCore.Modules/OrchardCore.Feeds/Manifest.cs) / `OrchardCore.Feeds` | Feed delivery protocol | No dedicated command | Content syndication output; manage underlying content using shared APIs. No feed-download management endpoint proposed. | — |
| [HealthChecks](../../src/OrchardCore.Modules/OrchardCore.HealthChecks/Manifest.cs) / `OrchardCore.HealthChecks` | Health-check protocol | No dedicated command | Retain liveness/readiness protocol. Consider a sanitized aggregate in B11 rather than reproducing every probe. | — |
| [Indexing](../../src/OrchardCore.Modules/OrchardCore.Indexing/Manifest.cs) / `OrchardCore.Indexing.Worker` | No dedicated management API | No dedicated command | Indexing/background extension; index administration belongs to B07 and task controls to B11. | — |
| [Liquid](../../src/OrchardCore.Modules/OrchardCore.Liquid/Manifest.cs) / `OrchardCore.Liquid.Core` | No dedicated management API | No dedicated command | Liquid rendering dependency; no independent management resource. | — |
| [Localization](../../src/OrchardCore.Modules/OrchardCore.Localization/Manifest.cs) / `OrchardCore.Localization.ContentLanguageHeader` | No dedicated management API | No dedicated command | Culture selection/header behavior; use shared culture configuration where applicable, no standalone command. | — |
| [Localization](../../src/OrchardCore.Modules/OrchardCore.Localization/Manifest.cs) / `OrchardCore.Localization.AdminCulturePicker` | No dedicated management API | No dedicated command | Culture selection/header behavior; use shared culture configuration where applicable, no standalone command. | — |
| [Lucene](../../src/OrchardCore.Modules/OrchardCore.Lucene/Manifest.cs) / `OrchardCore.Search.Lucene` | No dedicated management API | No dedicated command | Obsolete compatibility feature ID; enables its replacement automatically. Do not create a parallel API/CLI. | — |
| [Lucene](../../src/OrchardCore.Modules/OrchardCore.Lucene/Manifest.cs) / `OrchardCore.Search.Lucene.Worker` | No dedicated management API | No dedicated command | Indexing/background extension; index administration belongs to B07 and task controls to B11. | — |
| [Media](../../src/OrchardCore.Modules/OrchardCore.Media/Manifest.cs) / `OrchardCore.Media.Indexing` | No dedicated management API | No dedicated command | Indexing/background extension; index administration belongs to B07 and task controls to B11. | — |
| [Media](../../src/OrchardCore.Modules/OrchardCore.Media/Manifest.cs) / `OrchardCore.Media.Indexing.Text` | No dedicated management API | No dedicated command | Indexing/background extension; index administration belongs to B07 and task controls to B11. | — |
| [Media](../../src/OrchardCore.Modules/OrchardCore.Media/Manifest.cs) / `OrchardCore.Media.Slugify` | No dedicated management API | No dedicated command | Filename normalization behavior; existing upload/move APIs apply the configured policy. | — |
| [Media](../../src/OrchardCore.Modules/OrchardCore.Media/Manifest.cs) / `OrchardCore.Media.SignalR` | Media hub protocol | No dedicated command | Realtime notification extension; existing media CRUD is separate. | — |
| [Media.AmazonS3](../../src/OrchardCore.Modules/OrchardCore.Media.AmazonS3/Manifest.cs) / `OrchardCore.Media.AmazonS3.ImageCache` | No dedicated management API | No dedicated command | Image-cache storage provider. Scoped purge/provider configuration belongs to B09 only if needed; do not duplicate file APIs. | — |
| [Media.AmazonS3](../../src/OrchardCore.Modules/OrchardCore.Media.AmazonS3/Manifest.cs) / `OrchardCore.Media.AmazonS3.ImageSharpImageCache` | No dedicated management API | No dedicated command | Image-cache storage provider. Scoped purge/provider configuration belongs to B09 only if needed; do not duplicate file APIs. | — |
| [Media.Azure](../../src/OrchardCore.Modules/OrchardCore.Media.Azure/Manifest.cs) / `OrchardCore.Media.Azure.ImageCache` | No dedicated management API | No dedicated command | Image-cache storage provider. Scoped purge/provider configuration belongs to B09 only if needed; do not duplicate file APIs. | — |
| [Media.Azure](../../src/OrchardCore.Modules/OrchardCore.Media.Azure/Manifest.cs) / `OrchardCore.Media.Azure.ImageSharpImageCache` | No dedicated management API | No dedicated command | Image-cache storage provider. Scoped purge/provider configuration belongs to B09 only if needed; do not duplicate file APIs. | — |
| [Media.ImageSharpV3](../../src/OrchardCore.Modules/OrchardCore.Media.ImageSharpV3/Manifest.cs) / `OrchardCore.Media.ImageSharpV3` | No dedicated management API | No dedicated command | Image processing implementation; shared media operations, no separate management resource. | — |
| [Media.Indexing.OpenXML](../../src/OrchardCore.Modules/OrchardCore.Media.Indexing.OpenXML/Manifest.cs) / `OrchardCore.Media.Indexing.OpenXML` | No dedicated management API | No dedicated command | Text extraction extension; index lifecycle belongs to B07. | — |
| [Media.Indexing.Pdf](../../src/OrchardCore.Modules/OrchardCore.Media.Indexing.Pdf/Manifest.cs) / `OrchardCore.Media.Indexing.Pdf` | No dedicated management API | No dedicated command | Text extraction extension; index lifecycle belongs to B07. | — |
| [MiniProfiler](../../src/OrchardCore.Modules/OrchardCore.MiniProfiler/Manifest.cs) / `OrchardCore.MiniProfiler` | No dedicated management API | No dedicated command | Developer profiling UI; no general remote management API proposed. | — |
| [Navigation](../../src/OrchardCore.Modules/OrchardCore.Navigation/Manifest.cs) / `OrchardCore.Navigation` | No dedicated management API | No dedicated command | Navigation infrastructure; menu documents/content are tracked under AdminMenu/Menu. | — |
| [Notifications](../../src/OrchardCore.Modules/OrchardCore.Notifications/Manifest.cs) / `OrchardCore.Notifications.Email` | No dedicated management API | No dedicated command | Delivery-channel extension; notification contracts belong to B12. | — |
| [OpenApi](../../src/OrchardCore.Modules/OrchardCore.OpenApi/Manifest.cs) / `OrchardCore.OpenApi` | OpenAPI discovery/UI | Built-in `api` discovery | Discovery infrastructure already used by Pomi; no additional per-UI administration command. | — |
| [OpenApi](../../src/OrchardCore.Modules/OrchardCore.OpenApi/Manifest.cs) / `OrchardCore.OpenApi.SwaggerUI` | No dedicated management API | No dedicated command | Documentation UI; shared OpenAPI document, no new command needed. | — |
| [OpenApi](../../src/OrchardCore.Modules/OrchardCore.OpenApi/Manifest.cs) / `OrchardCore.OpenApi.ReDocUI` | No dedicated management API | No dedicated command | Documentation UI; shared OpenAPI document, no new command needed. | — |
| [OpenApi](../../src/OrchardCore.Modules/OrchardCore.OpenApi/Manifest.cs) / `OrchardCore.OpenApi.ScalarUI` | No dedicated management API | No dedicated command | Documentation UI; shared OpenAPI document, no new command needed. | — |
| [OpenId](../../src/OrchardCore.Modules/OrchardCore.OpenId/Manifest.cs) / `OrchardCore.OpenId` | No dedicated management API | No dedicated command | OpenId core is dependency infrastructure; application/scope management is a separate feature below. | — |
| [Placements](../../src/OrchardCore.Modules/OrchardCore.Placements/Manifest.cs) / `OrchardCore.Placements.FileStorage` | No dedicated management API | No dedicated command | Placement storage provider; share B02 semantics and honor deployment-owned files. | — |
| [Queries](../../src/OrchardCore.Modules/OrchardCore.Queries/Manifest.cs) / `OrchardCore.Queries.Core` | No dedicated management API | No dedicated command | Query service dependency. Enable Queries for its management surface; do not duplicate endpoints here. | — |
| [ReCaptcha](../../src/OrchardCore.Modules/OrchardCore.ReCaptcha/Manifest.cs) / `OrchardCore.ReCaptcha.Users` | No dedicated management API | No dedicated command | User-flow integration; ReCaptcha settings contract is tracked in B12. | — |
| [Recipes](../../src/OrchardCore.Modules/OrchardCore.Recipes/Manifest.cs) / `OrchardCore.Recipes.Core` | No dedicated management API | No dedicated command | Recipe execution infrastructure; management operations belong to Recipes. | — |
| [Redis](../../src/OrchardCore.Modules/OrchardCore.Redis/Manifest.cs) / `OrchardCore.Redis` | No dedicated management API | No dedicated command | Distributed cache/bus/lock/key providers; deployment configuration, not independent tenant CRUD. | — |
| [Redis](../../src/OrchardCore.Modules/OrchardCore.Redis/Manifest.cs) / `OrchardCore.Redis.Cache` | No dedicated management API | No dedicated command | Distributed cache/bus/lock/key providers; deployment configuration, not independent tenant CRUD. | — |
| [Redis](../../src/OrchardCore.Modules/OrchardCore.Redis/Manifest.cs) / `OrchardCore.Redis.Bus` | No dedicated management API | No dedicated command | Distributed cache/bus/lock/key providers; deployment configuration, not independent tenant CRUD. | — |
| [Redis](../../src/OrchardCore.Modules/OrchardCore.Redis/Manifest.cs) / `OrchardCore.Redis.Lock` | No dedicated management API | No dedicated command | Distributed cache/bus/lock/key providers; deployment configuration, not independent tenant CRUD. | — |
| [Redis](../../src/OrchardCore.Modules/OrchardCore.Redis/Manifest.cs) / `OrchardCore.Redis.DataProtection` | No dedicated management API | No dedicated command | Distributed cache/bus/lock/key providers; deployment configuration, not independent tenant CRUD. | — |
| [Resources](../../src/OrchardCore.Modules/OrchardCore.Resources/Manifest.cs) / `OrchardCore.Resources` | No dedicated management API | No dedicated command | Resource registration/rendering. Safe global resource settings are partly covered by core settings; no resource-registry CRUD proposed. | — |
| [ResponseCompression](../../src/OrchardCore.Modules/OrchardCore.ResponseCompression/Manifest.cs) / `OrchardCore.ResponseCompression` | No dedicated management API | No dedicated command | Host/tenant pipeline option; no dedicated resource API proposed. | — |
| [ReverseProxy](../../src/OrchardCore.Modules/OrchardCore.ReverseProxy/Manifest.cs) / `OrchardCore.ReverseProxy` | No dedicated management API | No dedicated command | Proxy/pipeline configuration; preserve deployment ownership unless an explicit tenant scenario justifies an adapter. | — |
| [Roles](../../src/OrchardCore.Modules/OrchardCore.Roles/Manifest.cs) / `OrchardCore.Roles.Core` | No dedicated management API | No dedicated command | Role/permission service dependency; full management endpoints belong to Roles. | — |
| [Rules](../../src/OrchardCore.Modules/OrchardCore.Rules/Manifest.cs) / `OrchardCore.Rules` | No dedicated management API | No dedicated command | Reusable condition engine. Layers exposes built-in condition descriptors/validation through IRuleManagementService; JavaScript validation parses without executing. Extend through other owning features (B01/B05). | — |
| [Scripting](../../src/OrchardCore.Modules/OrchardCore.Scripting/Manifest.cs) / `OrchardCore.Scripting` | No dedicated management API | No dedicated command | Script engine dependency; existing recipes/workflows own execution. No generic remote eval command proposed. | — |
| [Shortcodes](../../src/OrchardCore.Modules/OrchardCore.Shortcodes/Manifest.cs) / `OrchardCore.Shortcodes` | No dedicated management API | No dedicated command | Shortcode parser/providers. Content/template values can contain shortcodes; database templates are the separate feature below. | — |
| [SignalR](../../src/OrchardCore.Modules/OrchardCore.SignalR/Manifest.cs) / `OrchardCore.SignalR` | Realtime protocol | No dedicated command | Messaging infrastructure; do not project hubs into CRUD commands. | — |
| [SignalR.Azure](../../src/OrchardCore.Modules/OrchardCore.SignalR.Azure/Manifest.cs) / `OrchardCore.SignalR.Azure` | No dedicated management API | No dedicated command | Realtime transport/provider configuration; deployment concern by default. | — |
| [SignalR.Redis](../../src/OrchardCore.Modules/OrchardCore.SignalR.Redis/Manifest.cs) / `OrchardCore.SignalR.Redis` | No dedicated management API | No dedicated command | Realtime backplane/provider configuration; deployment concern by default. | — |
| [Sitemaps](../../src/OrchardCore.Modules/OrchardCore.Sitemaps/Manifest.cs) / `OrchardCore.Sitemaps.RazorPages` | No dedicated management API | No dedicated command | Sitemap source/background extension; use B10 sitemap administration and B11 task controls. | — |
| [Sitemaps](../../src/OrchardCore.Modules/OrchardCore.Sitemaps/Manifest.cs) / `OrchardCore.Sitemaps.Cleanup` | No dedicated management API | No dedicated command | Sitemap source/background extension; use B10 sitemap administration and B11 task controls. | — |
| [Sms](../../src/OrchardCore.Modules/OrchardCore.Sms/Manifest.cs) / `OrchardCore.Notifications.Sms` | No dedicated management API | No dedicated command | Delivery-channel extension in the Sms module; notification contracts belong to B12. | — |
| [Sms.Azure](../../src/OrchardCore.Modules/OrchardCore.Sms.Azure/Manifest.cs) / `OrchardCore.Sms.Azure` | No dedicated management API | No dedicated command | Provider setup/credentials: B12 only for settings owned by the tenant; retain deployment-owned configuration. | — |
| [Taxonomies](../../src/OrchardCore.Modules/OrchardCore.Taxonomies/Manifest.cs) / `OrchardCore.Taxonomies.ContentsAdminList` | No dedicated management API | No dedicated command | Admin content-list filtering extension; underlying content/taxonomy data uses shared APIs. | — |
| [Tenants](../../src/OrchardCore.Modules/OrchardCore.Tenants/Manifest.cs) / `OrchardCore.Tenants.FileProvider` | No dedicated management API | No dedicated command | Tenant storage/distribution behavior; lifecycle uses shared tenant APIs. No independent provider command proposed. | — |
| [Tenants](../../src/OrchardCore.Modules/OrchardCore.Tenants/Manifest.cs) / `OrchardCore.Tenants.Distributed` | No dedicated management API | No dedicated command | Tenant storage/distribution behavior; lifecycle uses shared tenant APIs. No independent provider command proposed. | — |
| [Users](../../src/OrchardCore.Modules/OrchardCore.Users/Manifest.cs) / `OrchardCore.Users.AuditTrail` | No dedicated management API | No dedicated command | Audit event contributor; read/export belongs to B11. | — |
| [Users](../../src/OrchardCore.Modules/OrchardCore.Users/Manifest.cs) / `OrchardCore.Users.Authentication.CacheTicketStore` | No dedicated management API | No dedicated command | Authentication ticket storage implementation; no general ticket enumeration API proposed. | — |
| [XmlRpc](../../src/OrchardCore.Modules/OrchardCore.XmlRpc/Manifest.cs) / `OrchardCore.XmlRpc` | XML-RPC protocol | Shared content commands | Legacy publishing protocol; prefer existing content APIs for automation rather than a new CLI transport. | — |
| [XmlRpc](../../src/OrchardCore.Modules/OrchardCore.XmlRpc/Manifest.cs) / `OrchardCore.RemotePublishing` | XML-RPC protocol | Shared content commands | Legacy publishing protocol; prefer existing content APIs for automation rather than a new CLI transport. | — |

### X — Sample; excluded from delivery priorities

| Module / feature ID | OpenAPI or HTTP surface | Existing Pomi coverage | Scope, gap and next action | Plan |
| --- | --- | --- | --- | --- |
| [Demo](../../src/OrchardCore.Modules/OrchardCore.Demo/Manifest.cs) / `OrchardCore.Demo` | Sample endpoints | None | Sample module; excluded from delivery priorities. | — |
| [Demo](../../src/OrchardCore.Modules/OrchardCore.Demo/Manifest.cs) / `OrchardCore.Demo.Foo` | Sample endpoints | None | Sample module; excluded from delivery priorities. | — |
| [Mvc.HelloWorld](../../src/OrchardCore.Modules/OrchardCore.Mvc.HelloWorld/Manifest.cs) / `OrchardCore.Mvc.HelloWorld` | Sample MVC routes | None | Sample module; excluded from delivery priorities. | — |

## Bundled themes

Theme discovery, enablement and selecting the current theme already use `themes` commands.
Theme source editing/building is repository work; theme-specific settings require their own
review and are not assumed covered by activation.

| Theme | Coverage |
| --- | --- |
| [SafeMode](../../src/OrchardCore.Themes/SafeMode/Manifest.cs) | Shared Themes API/Pomi; no independent command family proposed. |
| [TheAdmin](../../src/OrchardCore.Themes/TheAdmin/Manifest.cs) | Shared Themes API/Pomi; no independent command family proposed. |
| [TheAgencyTheme](../../src/OrchardCore.Themes/TheAgencyTheme/Manifest.cs) | Shared Themes API/Pomi; no independent command family proposed. |
| [TheBlogTheme](../../src/OrchardCore.Themes/TheBlogTheme/Manifest.cs) | Shared Themes API/Pomi; no independent command family proposed. |
| [TheComingSoonTheme](../../src/OrchardCore.Themes/TheComingSoonTheme/Manifest.cs) | Shared Themes API/Pomi; no independent command family proposed. |
| [TheSaaSTheme](../../src/OrchardCore.Themes/TheSaaSTheme/Manifest.cs) | Shared Themes API/Pomi; no independent command family proposed. |
| [TheTheme](../../src/OrchardCore.Themes/TheTheme/Manifest.cs) | Shared Themes API/Pomi; no independent command family proposed. |

## Refreshing this audit

1. Record the new source commit and enumerate every `Module`/`Feature` assembly attribute in
   `src/OrchardCore.Modules/*/Manifest.cs`. Resolve constant IDs; use the implicit module feature
   only where there are no explicit Feature attributes. Keep obsolete IDs and samples explicit.
2. Compare `Endpoints`, API controllers, middleware/protocol registration and feature-specific
   Startup classes. Inspect `WithCliCommand`/`CliOperationMetadata` registrations separately from
   the mere existence of an HTTP verb or OpenAPI security declaration.
3. Review shared content, query, workflow, settings and storage contracts before assigning gaps.
   Classify each feature once and reconcile the ledger/counts. Add new gaps to a work package.
4. For a selected package, capture the tenant manifest, OpenAPI, Pomi help and MCP tool list in
   disposable feature configurations before and after the change. Reconcile operation IDs,
   command paths, schemas, feature gates and deliberate transport exclusions.
5. Update this inventory and the plan when a package ships. “Done” requires the contract and
   relevant acceptance checks in the plan; an endpoint count alone is insufficient.

Useful starting points are the [OpenAPI configuration](../../src/OrchardCore.Modules/OrchardCore.OpenApi/Startup.cs),
[CLI metadata transformer](../../src/OrchardCore.Modules/OrchardCore.RemoteManagement/CliOperationTransformer.cs),
[Pomi parser](../../src/OrchardCore.Cli/OpenApiCliParser.cs),
[MCP catalog](../../src/OrchardCore.Modules/OrchardCore.RemoteManagement/Mcp/McpToolCatalog.cs),
and the [existing verification toolkit](../../.scripts/remote-management/README.md).
