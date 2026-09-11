#!/usr/bin/env python3
"""Package canonical CLI skills without maintaining a second copy in source."""
import argparse
import json
from pathlib import Path
import shutil
import subprocess
import sys

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('output', type=Path, help='New parent directory for the plugin and ZIP')
args = parser.parse_args()
repo = Path(__file__).resolve().parents[2]
output = args.output.resolve()
plugin = output / 'orchardcore-cli'
if plugin.exists():
    parser.error(f'{plugin} already exists; choose a new output directory')
(plugin / '.codex-plugin').mkdir(parents=True)
manifest = Path(__file__).with_name('plugin.json')
shutil.copy2(manifest, plugin / '.codex-plugin/plugin.json')
skills = sorted((repo / '.agents/skills').glob('orchardcore-cli*'))
for skill in skills:
    if not (skill / 'SKILL.md').is_file():
        raise SystemExit(f'Missing SKILL.md: {skill.name}')
    shutil.copytree(skill, plugin / 'skills' / skill.name)
shutil.copy2(repo / 'LICENSE', plugin / 'LICENSE')
(plugin / 'README.md').write_text('''# Pomi CLI plugin

Install `pomi` and start with the [workflow router](skills/orchardcore-cli/SKILL.md)
to create a local CMS or connect to an existing tenant. Remote operations require an exact tenant context and
authentication; local `pomi install` requires the matching .NET SDK instead.
Specialists can be selected directly: each links to the same bundled
[context, authentication, and output rules](skills/orchardcore-cli/references/shared-rules.md).
Installation, authentication, and tenant setup procedures are bundled too.
Longer API manuals use commit-pinned links in the skills and require network
access; live tenant schemas take precedence over those reference versions.
The plugin contains instructions only: it installs no binary, MCP server,
credentials, or background process. It uses the CLI's existing authentication.

Sources: https://github.com/sebastienros/OrchardCore/tree/sebros/remote-tenant-cli-plan
Rebuild with `.scripts/remote-management/build-plugin.py <new-output-directory>`.
''')
subprocess.run([sys.executable, str(Path(__file__).with_name('verify-plugin-links.py')), str(plugin)], check=True, stdout=sys.stderr)
archive = shutil.make_archive(str(output / 'orchardcore-cli'), 'zip', root_dir=output, base_dir=plugin.name)
print(json.dumps({'plugin': str(plugin), 'archive': archive, 'skills':len(skills)}))
