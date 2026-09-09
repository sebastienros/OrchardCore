#!/usr/bin/env python3
"""Check an actual native executable and package it with completions and a checksum."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import shutil
import statistics
import subprocess
import tarfile
import tempfile
import time
import zipfile

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('directory', type=Path)
parser.add_argument('rid')
args = parser.parse_args()
directory = args.directory.resolve()
exe = directory / ('oc.exe' if args.rid.startswith('win-') else 'oc')
assert exe.is_file(), exe
assert exe.stat().st_size < 30 * 1024 * 1024, 'Native executable exceeds 30 MiB budget'
with tempfile.TemporaryDirectory(prefix='oc-native-smoke-') as config:
    env = os.environ.copy()
    env['OC_CONFIG_HOME'] = config
    env.pop('OC_CLIENT_ID', None)
    env.pop('OC_CLIENT_SECRET', None)
    samples = []
    for _ in range(5):
        started = time.perf_counter()
        result = subprocess.run([str(exe), 'version', '--output', 'json'], env=env, capture_output=True, text=True, check=True)
        samples.append((time.perf_counter() - started) * 1000)
        assert json.loads(result.stdout)['cliVersion']
    assert statistics.median(samples) < 2000, 'Median local startup exceeds 2 s budget'
    for command in [['--help'], ['context','list'], ['doctor']]:
        subprocess.run([str(exe), *command], env=env, stdout=subprocess.DEVNULL, check=True)
    suggestions = subprocess.run([str(exe), '[suggest:5]', 'oc co'], env=env, capture_output=True, text=True, check=True).stdout
    assert 'context' in suggestions.splitlines(), suggestions
    for shell in ('bash', 'zsh', 'fish', 'pwsh'):
        script = subprocess.run([str(exe), 'completion', '--shell', shell], env=env, capture_output=True, text=True, check=True).stdout
        script_path = directory / ('completion.' + shell)
        script_path.write_text(script, newline="\n")
        if os.name != 'nt' and shell in ('bash', 'zsh', 'fish') and shutil.which(shell):
            subprocess.run([shell, '-n', str(script_path)], check=True)
        if shell == 'pwsh' and shutil.which('pwsh'):
            completion_env = env.copy()
            completion_env['OC_COMPLETION_SCRIPT'] = str(script_path)
            completion_env['PATH'] = str(directory) + os.pathsep + env['PATH']
            subprocess.run(['pwsh', '-NoProfile', '-NonInteractive', '-Command',
                "& ([scriptblock]::Create((Get-Content -Raw $env:OC_COMPLETION_SCRIPT))); "
                "$matches = (TabExpansion2 'oc co' 5).CompletionMatches.CompletionText; "
                "if ('context' -notin $matches) { throw 'Native PowerShell completion did not suggest context' }"],
                env=completion_env, check=True)
report = {'rid':args.rid,'binaryBytes':exe.stat().st_size,'startupMilliseconds':samples,
          'medianStartupMilliseconds':statistics.median(samples),'sha256':hashlib.sha256(exe.read_bytes()).hexdigest()}
(directory / 'verification.json').write_text(json.dumps(report,indent=2)+'\n')
package = directory.parent / ('oc-' + args.rid)
files = [exe, directory/'verification.json', *directory.glob('completion.*')]
if args.rid.startswith('win-'):
    archive = package.with_suffix('.zip')
    with zipfile.ZipFile(archive,'w',zipfile.ZIP_DEFLATED) as bundle:
        for file in files:
            bundle.write(file,file.name)
else:
    archive = Path(str(package)+'.tar.gz')
    with tarfile.open(archive,'w:gz') as bundle:
        for file in files:
            bundle.add(file,arcname=file.name)
Path(str(archive)+'.sha256').write_text(hashlib.sha256(archive.read_bytes()).hexdigest()+'  '+archive.name+'\n')
print(json.dumps(report,indent=2))
