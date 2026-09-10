#!/usr/bin/env python3
"""Verify typed payload rejection and CLI error output on a disposable tenant."""
import json
from pathlib import Path
import secrets
import subprocess
import sys
import urllib.parse

state_path = Path(sys.argv[1])
state = json.loads(state_path.read_text())
assert urllib.parse.urlparse(state['url']).hostname in ('127.0.0.1', 'localhost', '::1')
wrapper = Path(__file__).with_name('oc-fixture.py')


def oc(*args, body=None, error_path=None, output='json'):
    command = [sys.executable, str(wrapper), str(state_path), *args, '--output', output]
    if body is not None:
        command += ['--stdin']
    result = subprocess.run(command, input=json.dumps(body) if body is not None else None,
                            text=True, capture_output=True, timeout=90)
    if error_path:
        assert result.returncode != 0, (args, result.stdout)
        if output == 'json':
            error = json.loads(result.stderr or result.stdout)['error']
            assert error['status'] == 400 and error_path in error['details']['errors'], error
        else:
            text = result.stderr + result.stdout
            assert 'An error occurred.' in text and error_path in text, text
        return
    assert result.returncode == 0, (args, result.stderr)
    return json.loads(result.stdout) if result.stdout.strip() else None


oc('context', 'add', 'content-validation-smoke', state['url'], '--current')
oc('api', 'refresh', '--force')
name = 'ValidationSmoke' + secrets.token_hex(4)
oc('content', 'types', 'create', body={
    'name': name, 'displayName': name,
    'settings': {'ContentTypeSettings': {'draftable': True, 'versionable': True, 'creatable': True}},
    'parts': [{'name': 'TitlePart', 'partName': 'TitlePart', 'settings': {}}],
})
item = oc('content', 'items', 'save', body={'ContentType': name, 'TitlePart': {'Title': 'Original'}})
item_id = item['ContentItemId']
bad = {'TitlePart': {'Title': {'a': 'b'}}}
for output in ('human', 'json'):
    oc('content', 'items', 'validate-update', item_id, body=bad, error_path='TitlePart.Title', output=output)
oc('content', 'items', 'update-draft', item_id, body=bad, error_path='TitlePart.Title')
oc('content', 'items', 'update', item_id, body=bad, error_path='TitlePart.Title')
oc('content', 'items', 'validate', body=bad, error_path='ContentType')
oc('content', 'items', 'create-draft', body={**bad, 'ContentType': name}, error_path='TitlePart.Title')
assert oc('content', 'items', 'validate-update', item_id, body={'TitlePart': {'Title': 'Valid revision'}})['isValid']
assert oc('content', 'versions', 'list', item_id)['totalCount'] == 1
assert oc('content', 'items', 'show', item_id) == item
oc('content', 'items', 'delete', item_id, '--yes')
oc('content', 'types', 'delete', name, '--yes')
print('Content validation CLI smoke passed: field-specific HTTP 400, human/JSON errors, nonzero exit, missing type, unchanged published item and history, valid partial update.')
