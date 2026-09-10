#!/usr/bin/env python3
"""Exercise embedded CMS installation against published packages, without a tenant fixture."""
import argparse
import json
import os
from pathlib import Path
import secrets
import signal
import socket
import subprocess
import tempfile
import time
import urllib.error
import urllib.request

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('oc', type=Path, help='Native CLI from the published build to test')
parser.add_argument('--source', help='Explicit NuGet feed for preview dependencies')
args = parser.parse_args()
oc = str(args.oc.resolve())
source_args = ['--source', args.source] if args.source else []


def free_port():
    with socket.socket() as listener:
        listener.bind(('127.0.0.1', 0))
        return listener.getsockname()[1]


def responds(url):
    try:
        with urllib.request.urlopen(url, timeout=1) as response:
            return response.status == 200
    except (OSError, urllib.error.URLError):
        return False


with tempfile.TemporaryDirectory(prefix='oc-install-smoke-') as scratch:
    root = Path(scratch)
    env = os.environ.copy()
    env['OC_CONFIG_HOME'] = str(root / 'config')
    password = 'Aa1!' + secrets.token_hex(20)
    env['OC_INSTALL_PASSWORD'] = password
    # A new site must not inherit an unrelated tenant's configuration.
    env['OrchardCore__OrchardCore_AutoSetup__Tenants__1__ShellName'] = 'Unexpected'
    no_sdk = env.copy()
    no_sdk['PATH'] = str(root / 'empty-path')
    diagnostics = subprocess.run([oc, 'doctor', '--output', 'json'], env=no_sdk, capture_output=True, text=True, check=True, timeout=15)
    assert json.loads(diagnostics.stdout)['localInstallWarning'], diagnostics.stdout
    assert not diagnostics.stderr, diagnostics.stderr
    missing = subprocess.run([oc, 'install', str(root / 'missing-sdk'), '--site-name', 'Missing SDK', '--email', 'admin@example.com', '--password-env', 'OC_INSTALL_PASSWORD'], env=no_sdk, capture_output=True, text=True, timeout=15)
    assert missing.returncode != 0 and '.NET' in missing.stderr, missing
    assert not (root / 'missing-sdk').exists()

    site = root / 'CMS with spaces'
    base = [oc, 'install', str(site), '--site-name', 'Embedded CMS', '--email', 'admin@example.com', '--recipe-name', 'SaaS', '--output', 'json', *source_args]
    completed = subprocess.run([*base, '--password-env', 'OC_INSTALL_PASSWORD'], env=env, capture_output=True, text=True, timeout=600)
    assert completed.returncode == 0, completed.stderr
    assert 'Now listening on:' not in completed.stderr, completed.stderr
    assert '127.0.0.1' not in completed.stderr, completed.stderr
    assert 'Failed to determine the https port' not in completed.stderr, completed.stderr
    assert 'Build succeeded.' not in completed.stderr, completed.stderr
    result = json.loads(completed.stdout)
    assert result['tenantState'] == 'Running' and result['tenant'] == 'Default', result
    assert json.loads((site / 'App_Data/tenants.json').read_text())['Default']['State'] == 'Running'
    if os.name != 'nt':
        assert (site / 'App_Data').stat().st_mode & 0o777 == 0o700
    assert not (site / 'App_Data/Sites/Unexpected').exists()
    for path in site.rglob('*'):
        if path.is_file() and path.stat().st_size < 20_000_000:
            assert password.encode() not in path.read_bytes(), f'Plaintext password in {path.relative_to(site)}'
    assert password not in completed.stdout + completed.stderr
    refused = subprocess.run([*base, '--password-env', 'OC_INSTALL_PASSWORD'], env=env, capture_output=True, text=True, timeout=15)
    assert refused.returncode != 0 and 'overwrite' in refused.stderr, refused

    # A setup failure must be reported without claiming a ready site.
    failure = subprocess.run([oc, 'install', str(root / 'bad-recipe'), '--site-name', 'Invalid recipe', '--email', 'admin@example.com', '--recipe-name', 'RecipeThatDoesNotExist', '--password-env', 'OC_INSTALL_PASSWORD', '--output', 'json', *source_args], env=env, capture_output=True, text=True, timeout=600)
    assert failure.returncode != 0 and not failure.stdout.strip(), failure
    assert password not in failure.stderr
    assert 'Installation diagnostics:' in failure.stderr, failure.stderr
    assert 'The AutoSetup failed installing the site' in failure.stderr, failure.stderr
    assert (root / 'bad-recipe/Program.cs').exists()

    # Check --run in the foreground, stdin input, path prefix, and cancellation.
    port = free_port()
    run_url = f'http://127.0.0.1:{port}'
    second_port = free_port()
    while second_port == port:
        second_port = free_port()
    second_url = f'http://127.0.0.1:{second_port}'
    output_path = root / 'run.json'
    error_path = root / 'run.log'
    with output_path.open('w') as output, error_path.open('w') as errors:
        process = subprocess.Popen([oc, 'install', str(root / 'running-site'), '--site-name', 'Running CMS', '--email', 'admin@example.com', '--recipe-name', 'SaaS', '--request-url-prefix', 'news', '--password-stdin', '--run', '--urls', run_url + ';' + second_url, '--output', 'json', *source_args], env=env, stdin=subprocess.PIPE, stdout=output, stderr=errors, text=True, start_new_session=os.name != 'nt', creationflags=subprocess.CREATE_NEW_PROCESS_GROUP if os.name == 'nt' else 0)
        try:
            process.stdin.write(password + '\n')
            process.stdin.close()
            deadline = time.monotonic() + 600
            while not (responds(run_url + '/news/') and responds(second_url + '/news/')):
                assert process.poll() is None, error_path.read_text()
                assert time.monotonic() < deadline, error_path.read_text()
                time.sleep(0.25)
            assert json.loads(output_path.read_text())['url'].rstrip('/') == run_url + '/news'
            run_logs = error_path.read_text()
            assert run_logs.count('Now listening on:') == 2, run_logs
            assert run_logs.count('Application started.') == 1, run_logs
            assert f'Now listening on: {run_url}' in run_logs, run_logs
            assert f'Now listening on: {second_url}' in run_logs, run_logs
            if os.name == 'nt':
                process.send_signal(signal.CTRL_BREAK_EVENT)
            else:
                process.send_signal(signal.SIGINT)
            process.wait(timeout=20)
            deadline = time.monotonic() + 5
            while responds(run_url + '/news/') and time.monotonic() < deadline:
                time.sleep(0.25)
            assert not responds(run_url + '/news/'), 'Foreground server survived CLI cancellation'
            assert not responds(second_url + '/news/'), 'Second listener survived CLI cancellation'
            assert password not in output_path.read_text() + error_path.read_text()
        finally:
            if os.name != 'nt':
                try:
                    os.killpg(process.pid, signal.SIGKILL)
                except ProcessLookupError:
                    pass
            elif process.poll() is None:
                subprocess.run(['taskkill', '/PID', str(process.pid), '/T', '/F'], check=False, capture_output=True)
            process.wait()

    print(json.dumps({'version': result['packageVersion'], 'embeddedTemplate': True, 'autoSetup': True,
                      'foregroundRun': True, 'cancellationStopsServer': True, 'missingSdkWarning': True,
                      'existingFilesProtected': True, 'setupFailureReported': True, 'noPlaintextAdminPassword': True}))
