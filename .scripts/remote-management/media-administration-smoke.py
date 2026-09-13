#!/usr/bin/env python3
"""Verify media administration through Pomi/MCP and tenant isolation."""
import json
import os
from pathlib import Path
import secrets
import subprocess
import sys
import tempfile
import time
import urllib.error
import urllib.parse
import urllib.request

state_path = Path(sys.argv[1])
state = json.loads(state_path.read_text())
base = state['url']
assert urllib.parse.urlparse(base).hostname in ('127.0.0.1', 'localhost', '::1')
wrapper = Path(__file__).with_name('pomi-fixture.py')
name = 'MediaSmoke' + secrets.token_hex(4)
tokens = {}


def token(client):
    if client not in tokens:
        body = urllib.parse.urlencode(dict(grant_type='client_credentials', client_id=client,
            client_secret=state['OC_CLIENT_SECRET'], scope='orchardcore.management')).encode()
        with urllib.request.urlopen(urllib.request.Request(base + 'connect/token', data=body,
                headers={'Content-Type': 'application/x-www-form-urlencoded'}), timeout=30) as response:
            tokens[client] = json.load(response)['access_token']
    return tokens[client]


def request(path, method='GET', body=None, client='cli-fixture', status=200, raw=False):
    url = urllib.parse.urljoin(base, path)
    assert urllib.parse.urlparse(url).netloc == urllib.parse.urlparse(base).netloc
    headers = {'Content-Type': 'application/json', 'Accept': 'application/json, text/event-stream'}
    if client:
        headers['Authorization'] = 'Bearer ' + token(client)
    req = urllib.request.Request(url, method=method, headers=headers,
        data=json.dumps(body).encode() if body is not None else None)
    try:
        response = urllib.request.urlopen(req, timeout=60)
    except urllib.error.HTTPError as error:
        response = error
    with response:
        text = response.read().decode()
        assert (response.status in status if isinstance(status, tuple) else response.status == status), (method, path, response.status, status)
        if raw:
            return text, response.headers
        if response.headers.get('Content-Type', '').startswith('text/event-stream'):
            return next(json.loads(line[6:]) for line in text.splitlines()
                if line.startswith('data: ') and json.loads(line[6:]).get('id') == 1)
        return json.loads(text) if text and 'json' in response.headers.get('Content-Type', '') else None


def pomi(*args, body=None, client='cli-fixture', status=None, failure=False):
    env = os.environ.copy()
    env.update(OC_FIXTURE_CONFIG_HOME=config_home, OC_FIXTURE_CLIENT_ID=client)
    selected_state = state_path
    command = [sys.executable, str(wrapper), str(selected_state), *args, '--output', 'json']
    if body is not None:
        command.append('--stdin')
    result = subprocess.run(command, input=json.dumps(body) if body is not None else None,
        capture_output=True, text=True, env=env, timeout=90)
    if failure:
        assert result.returncode != 0, args
        return
    if status:
        assert result.returncode != 0, args
        assert json.loads(result.stderr or result.stdout)['error']['status'] == status, 'unexpected CLI error status'
        return
    if result.returncode != 0:
        try:
            error = json.loads(result.stderr or result.stdout).get('error', {})
            print('CLI failure:', args[:3], error.get('code'), error.get('status'), error.get('message'), flush=True)
        except (ValueError, AttributeError):
            message = result.stderr or result.stdout
            for value in [state.get('OC_CLIENT_SECRET', ''), *tokens.values()]:
                if value:
                    message = message.replace(value, '[redacted]')
            print('CLI diagnostic:', message[:1600], flush=True)
    assert result.returncode == 0, (args[:3], 'CLI request failed')
    return json.loads(result.stdout) if result.stdout.strip() else None


def tool(name, arguments):
    result = request('mcp', 'POST', {'jsonrpc': '2.0', 'id': 1, 'method': 'tools/call',
        'params': {'name': name, 'arguments': arguments}})['result']
    content = json.loads(result['content'][0]['text'])
    assert content['statusCode'] == 200, (name, content['statusCode'])
    return json.loads(content['body']) if content['body'] else None


with tempfile.TemporaryDirectory(prefix='media-admin-cli-', dir=state_path.parent) as config_home:
    pomi('context', 'add', name, base, '--current')
    for feature in ['OrchardCore.Media', 'OrchardCore.Media.Cache', 'OrchardCore.RemoteManagement.Mcp']:
        request('api/features/' + feature + ':enable?force=true', 'POST')
    pomi('api', 'refresh', '--force')
    for path, client in [('api/media/profiles', 'cli-media-profiles'), ('api/media/cache', 'cli-media-cache')]:
        request(path, client=None, status=401)
        request(path, client='cli-discovery', status=403)
        request(path, client=client)
    request('api/media/profiles', client='cli-media-cache', status=403)
    request('api/media/cache', client='cli-media-profiles', status=403)
    profile = {'name': name.lower(), 'width': 100, 'height': 50, 'mode': 'Crop', 'format': 'Png',
        'quality': 90, 'backgroundColor': '#fff', 'autoOrient': True, 'hint': 'live smoke'}
    upload_before = pomi('settings', 'sections', 'show', 'media-upload-policy')['values']
    api_before = pomi('settings', 'sections', 'show', 'media-api')['values']
    try:
        created = pomi('media', 'profiles', 'create', body=profile)
        assert created == profile
        assert pomi('media', 'profiles', 'create', body=profile) == created
        assert pomi('media', 'profiles', 'show', profile['name']) == created
        for change in [{'width': -1}, {'quality': 101}, {'backgroundColor': '#zzzzzz'}]:
            pomi('media', 'profiles', 'update', profile['name'], body={**profile, **change}, status=400)
            assert pomi('media', 'profiles', 'show', profile['name']) == created
        changed = {**profile, 'width': 80}
        assert tool('media_profiles_update', {'query': {'name': profile['name']}, 'body': changed}) == changed
        assert pomi('media', 'profiles', 'show', profile['name']) == changed
        cache = pomi('media', 'cache', 'show')
        assert cache['resizedConfigured'] and not cache['remoteConfigured']
        pomi('media', 'cache', 'purge', 'remote', '--force', status=503)
        pomi('media', 'cache', 'purge', 'resized', '--force')
        patch = {'maxFileSize': 100, 'allowedFileExtensions': ['.png']}
        update = pomi('settings', 'sections', 'update', 'media-upload-policy', body=patch)
        assert update['changed'] and update['reloadRequested']
        assert not pomi('settings', 'sections', 'update', 'media-upload-policy', body=patch)['changed']
        readback = pomi('settings', 'sections', 'show', 'media-upload-policy')['values']
        assert readback['effectiveMaxFileSize'] == 100 and readback['effectiveAllowedFileExtensions'] == ['.png']
        pomi('settings', 'sections', 'update', 'media-upload-policy', body={'maxFileSize': readback['hostMaxFileSize'] + 1}, status=400)
        pomi('settings', 'sections', 'update', 'media-api', body={'authenticationScheme': 'Bearer'})
        assert pomi('settings', 'sections', 'show', 'media-api')['values']['authenticationScheme'] == 'Bearer'
        pomi('settings', 'sections', 'update', 'media-api', body={'authenticationScheme': 'invalid'}, status=400)
        print('PASS: media profile CRUD/retries/validation, MCP update, cache availability/purge, permissions, settings limits/retries/authentication.', flush=True)
    finally:
        pomi('settings', 'sections', 'update', 'media-upload-policy', body={key: upload_before[key] for key in ['maxFileSize', 'allowedFileExtensions']})
        pomi('settings', 'sections', 'update', 'media-api', body=api_before)
        pomi('media', 'profiles', 'delete', profile['name'], '--force')
