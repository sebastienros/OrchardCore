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
also tried the exact-route DELETE fallback, which returned 404. The media
skill now tells agents to report an unavailable operation on older servers
instead of substituting another storage API or local server-file deletion.
A confirmed, permission-protected `static files delete` operation was added
in response, with retry and path-boundary tests. The live functional check
verifies its final behavior; the original blind results above are retained
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
