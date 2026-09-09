#!/usr/bin/env python3
"""Exercise real CLI mutations on a disposable start-fixture.py tenant."""
import json
import os
from pathlib import Path
import secrets
import subprocess
import sys
import urllib.parse

fixture_path = Path(sys.argv[1]).resolve()
state = json.loads(fixture_path.read_text())
assert urllib.parse.urlparse(state['url']).hostname in ('127.0.0.1', 'localhost', '::1')
wrapper = Path(__file__).with_name('oc-fixture.py')
steps = []
prefix = 'Smoke' + secrets.token_hex(4)

def oc(*args, body=None, expected=0, env=None):
    command = [sys.executable, str(wrapper), str(fixture_path), *args, '--output', 'json']
    if body is not None:
        command += ['--stdin']
    result = subprocess.run(command, input=json.dumps(body) if body is not None else None,
                            text=True, capture_output=True, env=env)
    # Only command names/status are recorded, never bodies or token material.
    steps.append({'command': ' '.join(args[:3]), 'exitCode': result.returncode})
    if result.returncode != expected:
        raise RuntimeError(f"{' '.join(args[:3])}: exit {result.returncode}; {result.stderr[:600]}")
    return json.loads(result.stdout) if result.stdout.strip() else None

oc('context', 'add', 'smoke', state['url'], '--current')
oc('api', 'refresh', '--force')
oc('api', 'compatibility')
part = {'name':prefix+'Details','settings':{'ContentPartSettings':{'attachable':True,'reusable':True}},
        'fields':[{'name':'Summary','fieldName':'TextField','settings':{}}]}
oc('content','parts','create',body=part)
ctype = {'name':prefix,'displayName':prefix+' article','settings':{'ContentTypeSettings':{'draftable':True,'versionable':True,'creatable':True,'listable':True}},
         'parts':[{'name':'TitlePart','partName':'TitlePart','settings':{}},{'name':prefix+'Details','partName':prefix+'Details','settings':{}}]}
oc('content','types','create',body=ctype)
oc('content','items','schema',prefix)
payload = {'ContentType':prefix,'TitlePart':{'Title':'CLI smoke draft'},prefix+'Details':{'Summary':{'Text':'Round trip'}}}
assert oc('content','items','validate',body=payload)['isValid']
item = oc('content','items','create-draft',body=payload)
item_id = item['ContentItemId']
assert not oc('content','items','show',item_id,'--version','draft')['Published']
assert oc('content','items','validate-update',item_id,body=payload)['isValid']
assert oc('content','items','list','--content-type',prefix,'--status','published')['totalCount'] == 0
# Destructive input must fail before sending a mutation.
oc('content','items','delete',item_id,expected=1)
assert oc('content','items','show',item_id,'--version','draft')['ContentItemId'] == item_id
oc('content','items','delete',item_id,'--yes')
oc('content','types','delete',prefix,'--yes')
oc('content','parts','delete',prefix+'Details','--yes')
role = oc('roles','create',body={'roleName':prefix,'permissionNames':[]})
role_id = role['roleId']
assert oc('roles','show',role_id)['roleName'] == prefix
env = os.environ.copy()
env['OC_SMOKE_PASSWORD'] = secrets.token_urlsafe(32) + 'aA1!'
user = oc('users','create','--user-name',prefix,'--email',prefix+'@example.test',
          '--is-enabled','true','--role-names',json.dumps([prefix]),'--password-env','OC_SMOKE_PASSWORD',env=env)
assert 'password' not in json.dumps(user).lower()
user_id = user['userId']
assert oc('users','show',user_id)['roleNames'] == [prefix]
oc('users','disable',user_id,'--yes')
oc('users','delete',user_id,'--yes')
oc('roles','delete',role_id,'--yes')
# Static assets have a complete upload/inspect/delete lifecycle.
css = Path(state['root']) / (prefix + '.css')
css.write_text('/* CLI smoke */\nbody { color: #123; }\n')
remote = prefix + '/style.css'
oc('static','files','upload',remote,'--file',str(css))
assert oc('static','files','show',remote)['length'] == css.stat().st_size
oc('static','files','delete',remote,expected=1)
oc('static','files','delete',remote,'--yes')
oc('static','files','delete',remote,'--yes')
oc('static','files','show',remote,expected=4)
assert oc('static','files','list','--path',prefix)['totalCount'] == 0
report = {'passed':True,'steps':steps,'prefix':prefix}
(Path(state['root']) / 'smoke.json').write_text(json.dumps(report,indent=2))
print(json.dumps({'passed':True,'commands':len(steps)}))
