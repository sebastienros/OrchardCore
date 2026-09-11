# Use Pomi with an agent

![Pomi](assets/logo.png){ width="280" }

Give your coding agent the complete Pomi skill package to help it install an
Orchard CMS site, connect to a tenant, and manage content, media, settings, and
other enabled features. The website supplies the downloads; the plugin is the
installable package. Opening a public skill URL does **not** register it with
an agent.

## Choose a download

These downloads belong to the documentation version selected in the website's
version selector. Both ZIPs contain the same ten canonical skill directories
and all their references.

| Download | Purpose |
| --- | --- |
| [Pomi plugin ZIP](../downloads/pomi-plugin.zip) | Complete skills, references, Codex and Claude Code manifests, local marketplaces, license, and Pomi branding |
| [Skills-only ZIP](../downloads/pomi-skills.zip) | The same skill directories for Copilot and agents with other packaging requirements |
| [Browse skills](#browse-skills) | Readable instructions and raw Markdown links, including linked references |

[Package metadata](../downloads/package-metadata.json) and
[SHA-256 checksums](../downloads/SHA256SUMS) identify the exact build. Download
the ZIP to inspect it before installation. The packages do not contain the Pomi
executable, credentials, connectors, or background services.

## Install Pomi and connect

[Install Pomi and authenticate](../guides/remote-management/README.md) on the
machine where the agent executes commands. Native remote management needs no
.NET runtime; local site installation needs the matching .NET SDK. A cloud
agent needs its own executable, network access, and authorized credentials;
installing skills on your laptop does not transfer them to a cloud workspace.

Verify the executable and select the intended tenant before giving the agent
work:

```bash
pomi --version
pomi context list
pomi --context my-site api compatibility
```

Use the [authentication reference](skills/orchardcore-cli/references/authentication.md)
for first-time login, device authorization, or client credentials. Do not paste
tokens or passwords into an agent prompt.

## Install in your agent

Keep **all ten sibling directories** together. Individual specialists link to
shared rules and references in the main skill. Do not copy just `SKILL.md` or
flatten the directory layout. The shell examples below use macOS/Linux paths;
on Windows, use **Extract All** and the equivalent absolute folder paths.

### Codex: install the plugin

1. Download the **Pomi plugin ZIP** and extract it to a stable directory, such as
   `~/agent-packages/pomi-plugin`. Its root contains `.agents/plugins/marketplace.json`
   and `plugins/orchardcore-cli/`.
2. Register that extracted local marketplace and install its plugin:

    ```bash
    codex plugin marketplace add "$HOME/agent-packages/pomi-plugin"
    codex plugin add orchardcore-cli@pomi-download
    ```

3. Start a new Codex task. Ask it to use `orchardcore-cli`, or select a specialist
   such as `orchardcore-cli-content-items`.

Keep the extracted directory so the marketplace source remains available.
When updating, extract the new download there and run the plugin-add command
again, then start a new task. Compare `package-metadata.json` before updating.
If your CLI lacks the plugin commands, update Codex; the command spelling above
uses `plugin add`.

Related skills are distributed as a plugin following
[OpenAI's distribution guidance](https://learn.chatgpt.com/docs/build-skills#distribute-skills-with-plugins).
See [Codex plugins](https://learn.chatgpt.com/docs/plugins) for plugin discovery
and workspace availability. This download is a local marketplace, not a listing
in the OpenAI plugin directory.

### Claude Code: install the same plugin

The plugin ZIP also contains a Claude Code manifest; its skills are the same
files used by Codex. After extraction, load it for one session:

```bash
claude --plugin-dir "$HOME/agent-packages/pomi-plugin/plugins/orchardcore-cli"
```

For persistent installation, run these commands **inside Claude Code**, replacing
the path with your extracted directory:

```text
/plugin marketplace add /absolute/path/to/pomi-plugin
/plugin install orchardcore-cli@pomi-download
```

Invoke `/orchardcore-cli:orchardcore-cli`, or ask Claude to use the relevant Pomi
skill. See the official [plugin installation guide](https://code.claude.com/docs/en/discover-plugins).

If you need repository-scoped skills instead, extract the **Skills-only ZIP**
and copy its ten `skills/orchardcore-cli*` directories into `.claude/skills/`.
For personal Claude Code skills, use `~/.claude/skills/`. These are standalone
skills, invoked as `/orchardcore-cli` without the plugin namespace. Install one
form to avoid duplicate skill listings. See [Claude Code skills](https://code.claude.com/docs/en/skills).

### GitHub Copilot: install the skills-only package

1. Download and extract the **Skills-only ZIP**. The outer directory is
   `pomi-skills`, containing `skills/`, a license, and package metadata.
2. Copy the ten directories under `pomi-skills/skills/` into your repository's
   `.github/skills/`. For example:

    ```text
    .github/skills/orchardcore-cli/SKILL.md
    .github/skills/orchardcore-cli/references/shared-rules.md
    .github/skills/orchardcore-cli-content-items/SKILL.md
    ...
    ```

3. Start an agent session and ask Copilot to use the Pomi skills. Commit the
   directories if a repository-based cloud agent or your team should receive them.

Copilot also supports `.agents/skills/` for project skills. Choose one location
rather than installing duplicates. For personal local skills, use
`~/.copilot/skills/`. The Codex plugin manifest is not needed for this installation.
See [GitHub's skill installation instructions](https://docs.github.com/en/copilot/how-tos/copilot-on-github/customize-copilot/customize-cloud-agent/add-skills).

### Try a bounded task

For example:

> Use the Pomi content-items skill to inspect the Article schema in the `my-site`
> context and validate an unpublished draft. Do not publish it.

The agent should discover the tenant's actual commands, use the shared context,
authentication, and output rules, and stay within your requested scope. A public
Markdown link is useful for reading instructions, but installation is what makes
the complete package discoverable to the agent.

## Browse skills

The rendered pages and raw Markdown are generated from `.agents/skills/` during
the same build as the ZIPs. Each readable page links to its raw Markdown; raw
references retain the original relative links and bytes.

| Workflow | Readable page | Raw instructions |
| --- | --- | --- |
<!-- pomi-skill-list -->

## Version and provenance

<!-- pomi-package-metadata -->

Release documentation builds use the checked-out release branch or tag; `latest`
uses the configured development branch. A release branch can receive fixes, so
a documentation version name alone is not an immutable package identity. Save
the commit-specific ZIP and its checksum for reproducible use. Each build serves
its own commit-specific filenames; it does not retain downloads from every older
build. The docs CI artifact also records the source commit and has GitHub's
artifact retention limits.

Maintainers should activate the desired release tags in Read the Docs and point
`latest` at the development branch. No skill content is fetched from another
branch during a build. These files appear below the selected version, for example
`/en/<version>/downloads/pomi-plugin.zip` and `/en/<version>/agents/`.
Local builds may report uncommitted inputs; those filenames include `working`
and must not be treated as release artifacts.

To reproduce a committed artifact, check out its `sourceCommit` with a clean
working tree and run:

```bash
python3 .scripts/remote-management/build-plugin.py /tmp/pomi-package-rebuild
```

Compare the generated checksums with the downloaded `SHA256SUMS`. ZIP entry order,
timestamps, and permissions are fixed; no build date or local path enters the
archives. All canonical skills remain in `.agents/skills/`; generated website
pages and archives are not maintained as separate source copies.
