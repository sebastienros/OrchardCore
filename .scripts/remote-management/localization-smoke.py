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
wrapper = Path(__file__).with_name('oc-fixture.py')


def oc(*args, body=None, client=None, status=None):
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


oc('context', 'add', 'localization-smoke', state['url'], '--current')
oc('api', 'refresh', '--force')
assert set(oc('localization', 'translations', 'schema', '--operation', 'set')['required']) == {'culture', 'context', 'key', 'value'}
assert 'supportedCultures' in oc('localization', 'settings', 'schema', '--operation', 'update')['properties']
settings = oc('localization', 'settings', 'show')
assert settings['defaultCulture'] in settings['supportedCultures']
updated = {'defaultCulture': 'en', 'supportedCultures': ['en', 'fr'], 'fallBackToParentCulture': True}
assert oc('localization', 'settings', 'update', '--stdin', body=updated) == updated
assert oc('localization', 'settings', 'show') == updated
assert oc('localization', 'settings', 'update', '--stdin', body=updated) == updated
assert oc('localization', 'cultures', 'list')['totalCount'] == 2
assert oc('localization', 'cultures', 'list', '--include-available', 'true', '--take', '1')['totalCount'] > 2
labels = oc('localization', 'strings', 'show', 'media-gallery', '--culture', 'fr', '--take', '200')
assert labels['culture'] == 'fr' and labels['totalCount'] > 0
oc('localization', 'strings', 'show', 'missing-group', status=404)
oc('localization', 'strings', 'show', 'media-gallery', '--culture', 'de', status=400)
oc('localization', 'settings', 'update', '--stdin', body={**updated, 'defaultCulture': 'de'}, status=400)

items = oc('localization', 'translations', 'list', '--culture', 'fr')['items']
assert items, 'Fixture must register at least one dynamic translation descriptor.'
key = items[0]
translation = {'culture': 'fr', 'context': key['context'], 'key': key['key'], 'value': 'Traduction CLI éàç'}
assert oc('localization', 'translations', 'set', '--stdin', body=translation)['changed']
assert not oc('localization', 'translations', 'set', '--stdin', body=translation)['changed']
translated = oc('localization', 'translations', 'list', '--culture', 'fr')['items']
assert next(item for item in translated if item['context'] == key['context'] and item['key'] == key['key'])['value'] == translation['value']
# A reader cannot edit. A French translator cannot edit English or change tenant culture settings.
oc('localization', 'translations', 'set', '--stdin', body=translation, client='cli-translation-reader', status=403)
assert oc('localization', 'translations', 'set', '--stdin', body={**translation, 'value': 'Français'}, client='cli-translator-fr')['changed']
oc('localization', 'translations', 'set', '--stdin', body={**translation, 'culture': 'en'}, client='cli-translator-fr', status=403)
oc('localization', 'settings', 'show', client='cli-translator-fr', status=403)
oc('localization', 'translations', 'list', '--culture', 'fr', client='cli-discovery', status=403)
oc('localization', 'translations', 'set', '--stdin', body={**translation, 'key': 'Unregistered CLI key'}, status=404)
args = ('localization', 'translations', 'delete', key['context'], key['key'], '--culture', 'fr', '--yes')
assert oc(*args)['changed']
assert not oc(*args)['changed']
# Anonymous callers cannot reach any of the new read APIs.
for route in ('api/localization/cultures', 'api/localization/settings', 'api/localization/strings/media-gallery', 'api/localization/translations?culture=fr'):
    try:
        urllib.request.urlopen(state['url'] + route, timeout=15)
        raise AssertionError('Anonymous access unexpectedly succeeded: ' + route)
    except urllib.error.HTTPError as error:
        assert error.code == 401, (route, error.code)
print('Localization CLI smoke passed: seven operations, paging, culture selection, retries, and authorization.')
