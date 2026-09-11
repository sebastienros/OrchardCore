# Pomi agent plugin

Start with the [workflow router](skills/orchardcore-cli/SKILL.md), or select a
specialist directly. Each links to bundled context, authentication, and output
rules. Longer manuals use commit-pinned links and require network access.
Live tenant schemas take precedence. Install Pomi separately; this package
contains instructions and branding, not a CLI executable or credentials.

## Install from the Orchard Core marketplace

```bash
codex plugin marketplace add sebastienros/OrchardCore --ref sebros/remote-tenant-cli-plan
codex plugin add pomi@orchardcore
```

Start a new task and ask for the `orchardcore-cli` skill. Claude Code uses the
same skill files; run these commands inside Claude Code:

```text
/plugin marketplace add sebastienros/OrchardCore@sebros/remote-tenant-cli-plan
/plugin install pomi@orchardcore
```

See [compatibility.json](compatibility.json) for supported Pomi/server versions.
The manifests hold the package version. Git marketplace installs select the
source revision; generated ZIPs also include `package-metadata.json` with the
exact source commit and checksums.

## Maintaining the package

This directory is the complete, directly installable plugin and the canonical
source of all ten skills. Keep every sibling directory and reference together.
Bump the version in both manifests when changing shipped skills or assets, so
marketplace clients can detect updates. The documentation builder packages this
directory; it does not generate an independent copy of the instructions.
