#!/usr/bin/env python3
"""Exercise native CLI output against a disposable loopback API, without credentials."""
import argparse
import hashlib
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
import json
import os
from pathlib import Path
import subprocess
import tempfile
import threading

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('oc', type=Path)
args = parser.parse_args()
oc = str(args.oc.resolve())


class Handler(BaseHTTPRequestHandler):
    def log_message(self, *_):
        pass

    def do_POST(self):
        self.rfile.read(int(self.headers.get('Content-Length', 0)))
        self.send_response(response_status)
        self.send_header('Content-Type', 'application/json')
        self.end_headers()
        self.wfile.write(json.dumps(result).encode())

    def do_DELETE(self):
        self.send_response(204)
        self.end_headers()


server = ThreadingHTTPServer(('127.0.0.1', 0), Handler)
thread = threading.Thread(target=server.serve_forever, daemon=True)
thread.start()
tenant = f'http://127.0.0.1:{server.server_port}/'
setup_url = tenant + 'Demo/setup?token=' + 'sample-token-' * 32
result = {'name': 'Demo', 'state': 'Uninitialized', 'setupUrl': setup_url,
          'primaryUrl': tenant + 'Demo/', 'canDelete': False, 'featureProfiles': []}
response_status = 201
try:
    with tempfile.TemporaryDirectory(prefix='oc-human-output-') as directory:
        config = Path(directory)
        env = os.environ.copy()
        env['OC_CONFIG_HOME'] = directory
        env.pop('OC_CLIENT_ID', None)
        env.pop('OC_CLIENT_SECRET', None)
        (config / 'contexts.json').write_text(json.dumps({
            'currentContext': 'test', 'contexts': [{'name': 'test', 'tenantUrl': tenant}],
        }))
        cache = config / 'cache' / hashlib.sha256(tenant.encode()).hexdigest()[:16]
        cache.mkdir(parents=True)

        def invoke(*options, verb='create'):
            # Mutations invalidate metadata; reseed this isolated fixture for each call.
            (cache / 'openapi.json').write_text(json.dumps({
                'expiresAt': '9999-01-01T00:00:00Z',
                'content': json.dumps({'paths': {'/api/tenants': {'post': {
                    'operationId': 'CreateTenant',
                    'x-oc-cli': {'commandGroup': ['tenants'], 'verb': verb if verb != 'delete' else 'create'},
                }, 'delete': {
                    'operationId': 'DeleteTenant',
                    'x-oc-cli': {'commandGroup': ['tenants'], 'verb': 'delete'},
                }}}}),
            }))
            return subprocess.run([oc, 'tenants', verb, *options], env=env,
                                  capture_output=True, text=True, check=True, timeout=15).stdout

        human = invoke()
        assert human.startswith("Tenant 'Demo' created successfully."), human
        assert 'Setup URL: ' + setup_url in human, human
        assert ' | ' not in human and 'Can delete' not in human, human
        assert 'oc --context=test tenants setup Demo' in human, human
        assert "--email '<admin-email>'" in human, human
        assert '--password' not in human, human
        assert json.loads(invoke('--output', 'json')) == result
        assert json.loads(invoke('--output', 'auto')) == result  # Explicit auto follows redirection.
        assert setup_url in invoke('--output', 'table')
        assert not invoke('--output', 'none')
        assert invoke('--output', 'human') == human
        assert invoke(verb='delete').strip() == 'Tenant removed successfully.'
        response_status = 202
        result = {'operationId': 'job-42', 'status': 'pending'}
        pending = invoke()
        assert pending.startswith('Request accepted.') and 'job-42' in pending, pending
        response_status = 200
        result = {'name': 'Demo', 'state': 'Running', 'primaryUrl': tenant + 'Demo/'}
        running = invoke()
        assert 'oc --context=test tenants enable-remote-management Demo' in running, running
        setup = invoke(verb='setup')
        assert "Tenant 'Demo' set up successfully." in setup, setup
        assert 'oc --context=test tenants enable-remote-management Demo' in setup, setup
        result = {'name': 'Demo', 'state': 'Running', 'url': tenant + 'Demo/'}
        enabled = invoke(verb='enable-remote-management')
        assert "Tenant 'Demo' configured for remote management successfully." in enabled, enabled
        assert f'oc context add Demo {tenant}Demo/ --current' in enabled, enabled
        result = {'success': False, 'message': 'The request could not be completed'}
        failed = invoke()
        assert 'unsuccessful result' in failed and result['message'] in failed, failed
        response_status = 400
        try:
            invoke()
            raise AssertionError('HTTP failure reported success')
        except subprocess.CalledProcessError as error:
            assert not error.stdout.strip() and 'api_error' in error.stderr, error
        # The spelling is deliberately --output; --format is not an alias.
        help_result = subprocess.run([oc, '--help'], env=env, capture_output=True, text=True, check=True)
        assert '--output' in help_result.stdout and '--format' not in help_result.stdout
        print(human.strip())
        print('Verified native human output, complete URLs, and explicit JSON/table/none/auto modes.')
finally:
    server.shutdown()
    server.server_close()
    thread.join(timeout=5)
