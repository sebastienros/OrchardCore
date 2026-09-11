#!/usr/bin/env python3
"""Package canonical CLI skills without maintaining a second copy in source."""
import argparse
import json
from pathlib import Path
import shutil

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

Install `pomi` and start with `orchardcore-cli` to create a local CMS or connect
to an existing tenant. Remote operations require an exact tenant context and
authentication; local `pomi install` requires the matching .NET SDK instead.
The main skill routes to the relevant resource skill.
The plugin contains instructions only: it installs no binary, MCP server,
credentials, or background process. It uses the CLI's existing authentication.

Sources: https://github.com/sebastienros/OrchardCore/tree/sebros/remote-tenant-cli-plan
Guide: src/docs/guides/remote-management/README.md in that branch.
Rebuild with `.scripts/remote-management/build-plugin.py <new-output-directory>`.
''')
archive = shutil.make_archive(str(output / 'orchardcore-cli'), 'zip', root_dir=output, base_dir=plugin.name)
print(json.dumps({'plugin': str(plugin), 'archive': archive, 'skills':len(skills)}))
