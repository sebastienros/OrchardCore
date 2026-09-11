#!/usr/bin/env python3
"""Verify dynamic tenant installation with a disposable Default-tenant fixture."""
import json
import os
from pathlib import Path
import secrets
import subprocess
import sys
import urllib.parse

state_path = Path(sys.argv[1])
state = json.loads(state_path.read_text())
assert urllib.parse.urlparse(state['url']).hostname in ('127.0.0.1', 'localhost', '::1')
wrapper = Path(__file__).with_name('oc-fixture.py')
password = secrets.token_urlsafe(32) + 'aA1!'
env = {**os.environ, 'OC_INSTALL_TEST_PASSWORD': password}


def oc(*args, stdin=None, status=None, output='json', client=None):
    command = [sys.executable, str(wrapper), str(state_path), *args, '--output', output]
    run_env = {**env, **({'OC_FIXTURE_CLIENT_ID': client} if client else {})}
    result = subprocess.run(command, input=stdin, text=True, capture_output=True, timeout=180, env=run_env)
    assert password not in result.stdout + result.stderr, 'Password leaked into command output'
    if status is not None:
        assert result.returncode != 0, args
        error = json.loads(result.stderr or result.stdout)['error']
        assert error['status'] == status, (args, error)
        return error
    assert result.returncode == 0, (args, result.stderr)
    return json.loads(result.stdout) if output == 'json' and result.stdout.strip() else result.stdout


oc('context', 'add', 'tenant-install-smoke', state['url'], '--current')
oc('api', 'refresh', '--force')
help_result = subprocess.run([sys.executable, str(wrapper), str(state_path), 'tenants', 'install', '--help'],
                             text=True, capture_output=True, env=env, timeout=60)
assert help_result.returncode == 0 and '--password-env' in help_result.stdout and '--connection-string-env' in help_result.stdout
assert '--create-context' not in help_result.stdout
before = oc('context', 'list')
prefix = 'Install' + secrets.token_hex(4)
for mode in ('env', 'stdin'):
    name = prefix + mode
    args = ['tenants', 'install', name, '--request-url-prefix', name.lower(), '--recipe-name', 'Blog' if mode == 'env' else 'Blank',
            '--site-name', 'Installed by CLI', '--user-name', 'admin',
            '--email', 'admin@example.com', '--site-time-zone', 'Europe/Paris']
    args += ['--password-env', 'OC_INSTALL_TEST_PASSWORD'] if mode == 'env' else ['--password-stdin']
    if mode == 'env':
        human = oc(*args, output='human')
        assert 'Tenant created and initialized successfully.' in human, human
        assert 'tenants enable-remote-management ' + name in human, human
        assert state['url'].rstrip('/') + '/' + name.lower() in human, human
        installed = oc('tenants', 'show', name)
    else:
        installed = oc(*args, stdin=password)
    assert installed['state'] == 'Running' and installed['setupUrl'] is None, installed
    assert installed['databaseProvider'] == 'Sqlite', installed
    assert installed['primaryUrl'].rstrip('/') == state['url'].rstrip('/') + '/' + name.lower(), installed
    oc(*args, stdin=password if mode == 'stdin' else None, status=409)
    assert oc('tenants', 'show', name)['tenantId'] == installed['tenantId']

# Creation failure must not leave a shell; setup failure must preserve it with recovery details.
bad = prefix + 'Invalid'
args = ['tenants', 'install', bad, '--request-url-prefix', 'invalid/path', '--recipe-name', 'Blank',
        '--database-provider', 'Sqlite', '--site-name', 'Test', '--user-name', 'admin',
        '--email', 'admin@example.com', '--password-env', 'OC_INSTALL_TEST_PASSWORD']
oc(*args, status=400)
oc('tenants', 'show', bad, status=404)
args[args.index('invalid/path')] = bad.lower()
args[args.index('Blank')] = 'MissingRecipe'
error = oc(*args, status=400)
assert error['details']['stage'] == 'setup' and error['details']['tenantName'] == bad, error
assert oc('tenants', 'show', bad)['state'] == 'Uninitialized'
oc(*args, status=409)
# The uninitialized tenant can be repaired using the existing commands.
oc('tenants', 'update', bad, '--stdin', stdin=json.dumps({'requestUrlPrefix': bad.lower(), 'databaseProvider': 'Sqlite', 'recipeName': 'Blank'}))
human = oc('tenants', 'setup', bad, '--site-name', 'Recovered', '--user-name', 'admin', '--email', 'admin@example.com',
           '--password-env', 'OC_INSTALL_TEST_PASSWORD', output='human')
assert 'successfully' in human
assert oc('context', 'list') == before, 'Installation unexpectedly changed local contexts'
print('Tenant install smoke passed: dynamic discovery, secret inputs, running tenant URLs, duplicate rejection, validation, partial failure/recovery, unchanged contexts.')
