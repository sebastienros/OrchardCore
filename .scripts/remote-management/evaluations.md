# Blind skill evaluations

Conducted September 8–9, 2026 against disposable local SQLite tenants. Each
agent started without conversation history, code-review findings, expected
command sequences, or previous reports. Models were explicitly selected to
exercise the user's requested comparison: `gpt-5.6-sol` and `gpt-5.6-luna`.
Agents could read the supplied CLI skills, discover live help/schemas, and
operate only on their assigned resource prefixes. Credentials were injected
by the fixture wrapper; agents were prohibited from reading credential files.

## Round 1: content model and unpublished authoring

Identical task for both models: create a prefixed Article type with TitlePart
and a Summary TextField, validate and create one draft, inspect it, leave it
unpublished, and explain workflow discovery and permission failures.

| Model | Outcome | Friction observed |
| --- | --- | --- |
| gpt-5.6-sol | Part/type created; draft and validate-update valid; exactly one draft and zero published items | One local zsh scalar-command composition failure, corrected using a function; help did not enumerate content versions |
| gpt-5.6-luna | Part/type created; draft valid; title/summary verified through render; zero published items | Expected 404 when requesting schema before type creation; ambiguity about workflow DTO casing |

Both used live schemas and preserved publication state. No mutation was
blindly retried. The root/automation skills now distinguish camelCase workflow
DTOs from PascalCase content-item properties; group descriptions and draft
read guidance were improved. This was a small usability sample, not a
statistical efficiency benchmark. Token consumption and wall-clock latency
were not measured and are not inferred from success.

## Round 2: packaged skills, templates, and static files

Fresh agents used only skills copied into the generated plugin. Both received
the same task with a different prefix: validate/create/read a harmless Liquid
template, upload/list/read a CSS file, delete their own resources, and inspect
query/workflow schemas without executing them.

| Model | Template | Static file | Cleanup |
| --- | --- | --- | --- |
| gpt-5.6-sol | Validated, created, read back | Upload, metadata, list, and public bytes verified | Template deleted and 404 verified; static delete unavailable |
| gpt-5.6-luna | Validated, created, read back | Upload, metadata, list, and public bytes verified | Template deleted; static delete unavailable |

Both independently found the missing static-file deletion capability. Sol
also tried the exact-route DELETE fallback, which returned 404. At the time, the media
skill was updated to tell agents to report an unavailable operation on older servers
instead of substituting another storage API or local server-file deletion.
A confirmed, permission-protected `static files delete` operation was added
in response, with retry and path-boundary tests. The live functional check at that time
verified its behavior; the original blind results above are retained
rather than relabeled as complete successes.

## Repeatable protocol

1. Build and start the disposable fixture using the toolkit README.
2. Package the current skills into a new directory.
3. Start fresh agents with no history, specifying the two models above.
4. Give each a separate copied fixture CLI config directory and unique prefix.
5. Supply only the task, wrapper invocation, skill location, resource boundary,
   and prohibition on credential/source/prior-report access. Do not provide
   the expected command sequence or seed failed commands.
6. Capture command outcomes, schema/help consultations, retries, publication
   state, cleanup state, and any authorization or secret-handling violation.
7. Read back resources independently and record failures without hiding them.

These runs cover core authoring and template/static-file workflows. They do
not establish skill efficiency for a full site build, tenant lifecycle,
all field/settings combinations, live recipe/workflow executions, every
media provider, or all supported shells and operating systems.

The static-file workflow evaluated above has been retired. Custom assets now use
Media, its folder authorization, and its restricted-extension permission. Those
historical runs do not validate the replacement workflow.

## Round 3: custom assets through Media

Task: use the packaged skills and CLI to upload harmless CSS beneath an assigned
prefix, choose the subfolder convention, inspect metadata/listing, verify public
bytes, demonstrate the file-type permission boundary, and remove created assets
and folders. Explain whether a single fixture proves multi-node availability.
Each model used an isolated CLI context and two injected client identities, one
with `UploadRestrictedMedia` and one without it. Both otherwise had the same
Media permissions. No source code, state files, credentials, prior reports, or
conversation history were supplied.

The first fresh pair was blocked because the fixture omitted `ViewOpenApiContent`
from its limited roles. Both could read the manifest but received 403 when
fetching OpenAPI. Luna made no remote changes. Sol made 25 wrapper calls, read
the constraints for both identities, and created two empty folders through raw
API calls; guessed cleanup routes returned 404, leaving the folders for the
fixture owner to remove. The owner verified and removed those empty folders.
Neither uploaded a file or completed the task. These runs remain recorded as
blocked, not successful asset evaluations.

The fixture now grants the existing OpenAPI viewing permission and verifies
document access for both limited identities. The setup docs and skills explain
that a readable manifest followed by a refresh 403 can mean missing document
permission; repeated login does not grant permission. A second fresh pair used
new contexts and the rebuilt plugin after that correction.

| Model | Corrected-fixture result | Observed limitations |
| --- | --- | --- |
| gpt-5.6-sol | 28 CLI calls and two public HTTP requests; uploaded 54-byte CSS, metadata/list matched, raw bytes and SHA-256 matched, other identity's upload rejected with HTTP 400, file and all three folders deleted | Eight help calls, three schema calls, one refresh; attempted a delete schema even though that operation has no input schema |
| gpt-5.6-luna | 41 CLI calls; uploaded two CSS versions, metadata/list and public response inspected, both files and all three folders deleted | Compared constraints rather than attempting a denied upload. Its byte comparison used `jq -r` on a JSON-wrapped response, adding an LF locally; the agent corrected its initial unsupported attribution to the server. This run did not establish exact raw byte equality. |

Both agents correctly limited the storage conclusion to one tenant origin, not
multiple nodes. The fixture owner independently checked that both prefixes were
absent after cleanup. The Media skill now demonstrates a raw download to a
temporary file followed by `cmp`, avoiding text-output transformations. These
are bounded usability observations and command counts, not a statistical or
token-efficiency benchmark. Neither model accessed fixture credentials or
changed permissions; the fixture owner made the discovery-permission correction.

## Round 4: embedded local CMS installation

Fresh `gpt-5.6-sol` and `gpt-5.6-luna` agents received the canonical CLI skill,
the native executable, an isolated destination and CLI configuration directory,
and an owner-readable test password file. They could create local sites and
restore dependencies, but could not inspect implementation, earlier results,
other evaluations, or live tenants. No expected command sequence was supplied.
This used the new installer built locally with version `4.0.0-cli.19` against
the already published matching server packages; the published CLI package at
that version predates the installer.

| Model / task | Outcome | Observed limitations |
| --- | --- | --- |
| gpt-5.6-sol: create a local blog, use a password file, leave stopped | Created and initialized a site; verified administrator/site data and no remaining host | Omitted `--recipe-name Blog`, so it created a SaaS site. This did **not** fully satisfy the blog request. Reported ambiguity between persisted tenant state and server process state. |
| gpt-5.6-luna: create a SaaS site, supply password through stdin, leave stopped | Used the requested recipe, SQLite and UTC; verified persisted administrator/site data and no remaining host | First restore was blocked by sandbox DNS. Preserved the failed project and reran with authorized network access. |

The skill and command help now explain that a blog requires the Blog recipe;
a blog-like folder or site name does not select it. Installer output now calls
the persisted state `tenantState`, and the skill explicitly distinguishes it
from an active server process.

A fresh `gpt-5.6-sol` agent then received the original blog task, the revised
skill and CLI, and a new isolated directory without the earlier findings.
It explicitly selected the Blog recipe, supplied the password by file, and
verified Blog media, initialized SQLite state, the closed temporary listener,
and no process holding site files. A sandbox DNS failure required inspecting
and removing the incomplete generated project before retrying with network
access. It completed the blog task. Neither the first incorrect recipe choice
nor the environmental failures are counted as clean first-attempt successes.

Agents did not print passwords or use inline secret arguments, and left no
site processes running. They left pre-existing services on port 5000 untouched.
The scripted native installer check separately covers foreground `--run`,
cancellation, overwrite refusal, missing SDK, setup failure, and plaintext
password absence. These are small behavioral samples, not statistical model
or token-efficiency measurements, and do not validate remote databases or every
operating system.

## Round 5: direct GraphQL

Fresh `gpt-5.6-sol` and `gpt-5.6-luna` agents received only the GraphQL skill,
a native CLI wrapper with a preselected disposable context, and the task to
inspect `SiteCulture`, retrieve configured cultures, and explain partial errors
and OpenAPI refresh requirements. They could use read-only GraphQL operations
and help, but not implementation sources, fixture state, credentials, or other
agent reports. The injected identity had only `ExecuteGraphQL`, without
`AccessRemoteManagement` or `ViewOpenApiContent`. No OpenAPI cache was supplied.

| Model | Outcome | Observed limitations |
| --- | --- | --- |
| gpt-5.6-sol | Five CLI attempts: help, single-type inspection (retried once), query-root introspection, and the culture query. Correctly returned `en-US` as default and explained exit 4 with preserved partial data/errors and no required OpenAPI refresh. | Initial schema request was blocked by sandbox loopback access; identical request succeeded with escalation. Used explicit query-root introspection to discover `siteCultures`. |
| gpt-5.6-luna | Six CLI attempts: help, single-type inspection (retried once), `Query` type inspection, culture query, and an additional missing-content-item query. Correctly returned the same culture and explained error/refresh behavior. | Same sandbox retry. Assumed the conventional `Query` type name, which existed in this fixture. Its additional missing-item probe returned `null` without errors, so it did not demonstrate runtime partial-error handling. |

Both completed the bounded task without changing server data or permissions.
These observations do not establish statistical model/token efficiency or
coverage of arbitrary GraphQL schemas and mutations. The deterministic native
smoke separately verifies preservation of partial data with errors on HTTP
200/400/401, failing exit codes, and no automatic retries or redirects. Real
Orchard smoke tests verify full/type introspection, variables/stdin, server
validation errors, and the GraphQL permission boundary.
