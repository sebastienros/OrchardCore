#!/usr/bin/env python3
"""Validate and stage one complete Orchard Core build before publishing to Feedz."""
import argparse
import json
from pathlib import Path
import shutil
import xml.etree.ElementTree as ET
import zipfile

RIDS = ('linux-x64', 'linux-arm64', 'win-x64', 'win-arm64', 'osx-x64', 'osx-arm64')
REQUIRED = {
    'orchardcore.application.cms.targets', 'orchardcore.application.mvc.targets',
    'orchardcore.module.targets', 'orchardcore.theme.targets',
    'orchardcore.projecttemplates', 'orchardcore.remotemanagement', 'orchardcore.cli',
    *('orchardcore.cli.' + rid for rid in RIDS),
}


def prepare(source, destination, version):
    packages = {}
    dependencies = []
    for path in sorted(source.rglob('*.nupkg')):
        with zipfile.ZipFile(path) as archive:
            nuspecs = [name for name in archive.namelist() if name.endswith('.nuspec')]
            if len(nuspecs) != 1:
                raise ValueError(f'{path.name}: expected one nuspec')
            metadata = ET.fromstring(archive.read(nuspecs[0])).find('./{*}metadata')
            package_id = metadata.findtext('{*}id')
            package_version = metadata.findtext('{*}version')
            if not (package_id == 'OrchardCore' or package_id.startswith('OrchardCore.')):
                raise ValueError(f'Unexpected package ID: {package_id}')
            if package_version != version:
                raise ValueError(f'{package_id}: expected version {version}, got {package_version}')
            key = package_id.lower()
            if key in packages:
                # Every native job independently tests the same RID-neutral installer.
                if key != 'orchardcore.cli':
                    raise ValueError(f'Duplicate package: {package_id}')
                continue
            packages[key] = path
            for dependency in metadata.findall('.//{*}dependency'):
                if dependency.attrib['id'].lower() == 'orchardcore' or dependency.attrib['id'].lower().startswith('orchardcore.'):
                    dependencies.append((package_id, dependency.attrib['id'].lower(), dependency.attrib.get('version')))
            if key == 'orchardcore.projecttemplates':
                configs = [name for name in archive.namelist() if name.endswith('/.template.config/template.json')]
                if len(configs) != 5:
                    raise ValueError(f'Expected all five project templates, got {len(configs)}')
                for name in configs:
                    config = json.loads(archive.read(name))
                    if config['symbols']['OrchardVersion']['defaultValue'] != version:
                        raise ValueError(f'{name}: template uses a different Orchard version')
    missing = REQUIRED - packages.keys()
    if missing:
        raise ValueError(f'Missing required packages: {", ".join(sorted(missing))}')
    for owner, dependency, constraint in dependencies:
        if dependency not in packages:
            raise ValueError(f'{owner}: missing build dependency {dependency}')
        if constraint not in (version, f'[{version}]', f'[{version}, )'):
            raise ValueError(f'{owner}: {dependency} has inconsistent version {constraint}')
    destination.mkdir(parents=True, exist_ok=False)
    # Publish the tool pointer last so it cannot reference unpublished native packages.
    ordered = sorted(packages, key=lambda key: (key == 'orchardcore.cli', key))
    staged = []
    for key in ordered:
        source_path = packages[key]
        target = destination / source_path.name
        shutil.copy2(source_path, target)
        symbols = source_path.with_suffix('.snupkg')
        if symbols.exists():
            shutil.copy2(symbols, destination / symbols.name)
        staged.append(str(target))
    (destination / 'publish-order.txt').write_text('\n'.join(staged) + '\n')
    (destination / 'packages.json').write_text(json.dumps({'version': version, 'packages': ordered}, indent=2))
    print(f'Validated {len(packages)} OrchardCore packages at {version}, including templates and all six native tools.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('source', type=Path)
    parser.add_argument('destination', type=Path, help='New directory for the verified package set')
    parser.add_argument('version')
    args = parser.parse_args()
    prepare(args.source, args.destination, args.version)
