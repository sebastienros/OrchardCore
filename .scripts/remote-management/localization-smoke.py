#!/usr/bin/env python3
"""Exercise localization OpenAPI projection and authorization on a disposable CLI fixture."""
import json
import os
from pathlib import Path
import subprocess
import sys
import urllib.error
import urllib.request
import urllib.parse

state_path = Path(sys.argv[1])
state = json.loads(state_path.read_text())
assert urllib.parse.urlparse(state['url']).hostname in ('127.0.0.1', 'localhost', '::1')
wrapper = Path(__file__).with_name('pomi-fixture.py')


def pomi(*args, body=None, client=None, status=None):
    env = os.environ.copy()
    if client:
        env['OC_FIXTURE_CLIENT_ID'] = client
    result = subprocess.run([sys.executable, str(wrapper), str(state_path), *args, '--output', 'json'],
                            input=json.dumps(body) if body is not None else None,
                            text=True, capture_output=True, env=env, timeout=90)
    if status:
        assert result.returncode != 0, args
        error = json.loads(result.stderr or result.stdout)
        assert error['error']['status'] == status, (args, error)
        return error
    assert result.returncode == 0, (args, result.stderr, result.stdout)
    return json.loads(result.stdout) if result.stdout.strip() else None


pomi('context', 'add', 'localization-smoke', state['url'], '--current')
pomi('api', 'refresh', '--force')
assert set(pomi('localization', 'translations', 'schema', '--operation', 'set')['required']) == {'culture', 'context', 'key', 'value'}
assert 'supportedCultures' in pomi('localization', 'settings', 'schema', '--operation', 'update')['properties']
settings = pomi('localization', 'settings', 'show')
assert settings['defaultCulture'] in settings['supportedCultures']
updated = {'defaultCulture': 'en', 'supportedCultures': ['en', 'fr'], 'fallBackToParentCulture': True}
assert pomi('localization', 'settings', 'update', '--stdin', body=updated) == updated
assert pomi('localization', 'settings', 'show') == updated
assert pomi('localization', 'settings', 'update', '--stdin', body=updated) == updated
assert pomi('localization', 'cultures', 'list')['totalCount'] == 2
assert pomi('localization', 'cultures', 'available', '--take', '1')['totalCount'] > 2
assert pomi('localization', 'cultures', 'available', '--take', '1') == pomi('localization', 'cultures', 'list', '--include-available', 'true', '--take', '1')
pomi('localization', 'cultures', 'available', client='cli-discovery', status=403)
# Incremental edits preserve settings and repeat safely across tenant releases.
added = {**updated, 'supportedCultures': ['de', 'en', 'fr']}
assert pomi('localization', 'cultures', 'add', 'DE') == added
assert pomi('localization', 'cultures', 'add', 'de') == added
assert pomi('localization', 'cultures', 'remove', 'de', '--force') == updated
assert pomi('localization', 'cultures', 'remove', 'de', '--force') == updated
pomi('localization', 'cultures', 'remove', 'en', '--force', status=400)
pomi('localization', 'cultures', 'add', 'unknown-culture', status=400)
assert pomi('localization', 'settings', 'show') == updated
groups = pomi('localization', 'strings', 'list')
assert {'name': 'media-gallery'} in groups['items']
assert pomi('localization', 'strings', 'list', '--skip', str(groups['totalCount']))['items'] == []
pomi('localization', 'strings', 'list', '--take', '201', status=400)
pomi('localization', 'strings', 'list', client='cli-discovery', status=403)
pomi('localization', 'cultures', 'add', 'de', client='cli-discovery', status=403)
pomi('localization', 'cultures', 'remove', 'fr', '--force', client='cli-discovery', status=403)
labels = pomi('localization', 'strings', 'show', 'media-gallery', '--culture', 'fr', '--take', '200')
assert labels['culture'] == 'fr' and labels['totalCount'] > 0
pomi('localization', 'strings', 'show', 'missing-group', status=404)
pomi('localization', 'strings', 'show', 'media-gallery', '--culture', 'de', status=400)
pomi('localization', 'settings', 'update', '--stdin', body={**updated, 'defaultCulture': 'de'}, status=400)

items = pomi('localization', 'translations', 'list', '--culture', 'fr')['items']
assert items, 'Fixture must register at least one dynamic translation descriptor.'
key = items[0]
translation = {'culture': 'fr', 'context': key['context'], 'key': key['key'], 'value': 'Traduction CLI éàç'}
assert pomi('localization', 'translations', 'set', '--stdin', body=translation)['changed']
assert not pomi('localization', 'translations', 'set', '--stdin', body=translation)['changed']
translated = pomi('localization', 'translations', 'list', '--culture', 'fr')['items']
assert next(item for item in translated if item['context'] == key['context'] and item['key'] == key['key'])['value'] == translation['value']
# A reader cannot edit. A French translator cannot edit English or change tenant culture settings.
pomi('localization', 'translations', 'set', '--stdin', body=translation, client='cli-translation-reader', status=403)
assert pomi('localization', 'translations', 'set', '--stdin', body={**translation, 'value': 'Français'}, client='cli-translator-fr')['changed']
pomi('localization', 'translations', 'set', '--stdin', body={**translation, 'culture': 'en'}, client='cli-translator-fr', status=403)
pomi('localization', 'settings', 'show', client='cli-translator-fr', status=403)
pomi('localization', 'translations', 'list', '--culture', 'fr', client='cli-discovery', status=403)
pomi('localization', 'translations', 'set', '--stdin', body={**translation, 'key': 'Unregistered CLI key'}, status=404)
args = ('localization', 'translations', 'delete', key['context'], key['key'], '--culture', 'fr', '--force')
assert pomi(*args)['changed']
assert not pomi(*args)['changed']
# Anonymous callers cannot reach any of the new read APIs.
for route in ('api/localization/cultures', 'api/localization/cultures/available', 'api/localization/settings', 'api/localization/strings', 'api/localization/strings/media-gallery', 'api/localization/translations?culture=fr'):
    try:
        urllib.request.urlopen(state['url'] + route, timeout=15)
        raise AssertionError('Anonymous access unexpectedly succeeded: ' + route)
    except urllib.error.HTTPError as error:
        assert error.code == 401, (route, error.code)
for method in ('PUT', 'DELETE'):
    request = urllib.request.Request(state['url'] + 'api/localization/cultures/de', method=method)
    try:
        urllib.request.urlopen(request, timeout=15)
        raise AssertionError('Anonymous culture mutation unexpectedly succeeded: ' + method)
    except urllib.error.HTTPError as error:
        assert error.code == 401, (method, error.code)
assert pomi('localization', 'settings', 'show') == updated
print('Localization CLI smoke passed: eleven operations, incremental culture edits, group discovery, paging, retries, and authorization.')
