---
name: orchardcore-cli-themes
description: Lists, enables, and selects Orchard Core site and admin themes through `oc`. Use when discovering installed themes, activating a theme and its base themes, setting the current frontend or admin theme, or verifying tenant theme state before applying templates and static styling.
---

# Orchard Core CLI Themes

Manage themes in the target tenant context. The authenticated identity requires
`AccessRemoteManagement` and `ApplyTheme`.

## Workflow

```bash
oc --context site themes list --take 200
oc --context site themes enable TheTheme
oc --context site themes set-current TheTheme
oc --context site themes list --current true
```

`set-current` detects whether the manifest declares a site or admin theme,
selects it in the corresponding setting, and enables it and its base themes
when necessary.

Filter discovery:

```bash
oc themes list --search Agency
oc themes list --admin false --enabled true
oc themes list --admin true
```

The list includes only themes allowed by the active feature profile and omits
hidden, always-enabled, dependency-only, and non-theme features.
Each result includes `id`, `name`, `description`, `isAdmin`, `isEnabled`, and
`isCurrent`.

## Reliability

- Theme IDs match case-insensitively for lookup; use the canonical returned ID
  in automation.
- Repeating `enable` for an enabled theme performs no additional mutation.
- Repeating `set-current` for the selected theme preserves the same setting.
- A missing or non-manageable theme returns `404`.
- Refresh discovery only when theme enablement changes the available API
  surface: `oc api refresh --force`.

After selecting a site theme, use `orchardcore-cli-media` for tenant CSS/assets
and `orchardcore-cli-templates` for custom Liquid shape overrides. Verify the
public route because selecting a theme does not prove its resources or content
templates render correctly.

Canonical references:
`src/docs/reference/api/themes/README.md` and
`src/docs/reference/modules/Themes/README.md`.
