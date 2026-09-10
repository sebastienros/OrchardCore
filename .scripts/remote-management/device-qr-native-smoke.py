#!/usr/bin/env python3
"""Verify opt-in device QR output and PNG delivery before user authorization."""
import base64
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
import json
import os
from pathlib import Path
import queue
import struct
import subprocess
import sys
import tempfile
import threading
import zlib

binary = str(Path(sys.argv[1]).resolve())
challenge_read = threading.Event()


class Handler(BaseHTTPRequestHandler):
    def log_message(self, *_):
        pass

    def respond(self, status, value):
        self.send_response(status)
        self.send_header('Content-Type', 'application/json')
        self.end_headers()
        self.wfile.write(json.dumps(value).encode())

    def do_GET(self):
        assert self.path == '/blog/.well-known/openid-configuration'
        self.respond(200, {'issuer': tenant, 'token_endpoint': tenant + 'connect/token',
                           'device_authorization_endpoint': tenant + 'connect/device'})

    def do_POST(self):
        self.rfile.read(int(self.headers.get('Content-Length', 0)))
        if self.path == '/blog/connect/device':
            self.respond(200, {'device_code': 'private-device-code', 'user_code': '1234-5678',
                               'verification_uri': tenant + 'connect/verify',
                               'verification_uri_complete': verification_url,
                               'expires_in': 60, 'interval': 1})
        else:
            assert self.path == '/blog/connect/token'
            challenge_read.wait(10)
            self.respond(400, {'error': 'access_denied'})


server = ThreadingHTTPServer(('127.0.0.1', 0), Handler)
tenant = f'http://127.0.0.1:{server.server_port}/blog/'
verification_url = tenant + 'connect/verify?user_code=1234-5678'
threading.Thread(target=server.serve_forever, daemon=True).start()
try:
    with tempfile.TemporaryDirectory(prefix='oc-device-qr-') as directory:
        root = Path(directory)
        (root / 'contexts.json').write_text(json.dumps({'currentContext': 'test', 'contexts': [{
            'name': 'test', 'tenantUrl': tenant, 'authority': tenant, 'clientId': 'orchardcore-cli',
            'grantTypes': ['urn:ietf:params:oauth:grant-type:device_code']}]}))
        env = dict(os.environ, OC_CONFIG_HOME=directory, NO_COLOR='1', TERM='dumb')
        env.pop('OC_CLIENT_ID', None)
        env.pop('OC_CLIENT_SECRET', None)
        command = [binary, 'login', '--grant', 'device']
        challenge_read.set()
        for options in ([], ['--qr', 'never'], ['--output', 'human']):
            result = subprocess.run(command + options, env=env, capture_output=True, text=True, timeout=15)
            assert result.returncode == 1 and not result.stdout.strip(), result
            assert verification_url in result.stderr and '\x1b' not in result.stderr, result.stderr

        for options in (['--qr', 'always', '--output', 'json'], ['--qr', 'auto']):
            challenge_read.clear()
            process = subprocess.Popen(command + options, env=env, stdout=subprocess.PIPE, stderr=subprocess.PIPE, text=True)
            try:
                lines = queue.Queue()
                threading.Thread(target=lambda: lines.put(process.stdout.readline()), daemon=True).start()
                pending_text = lines.get(timeout=8)
                pending = json.loads(pending_text)
                assert process.poll() is None  # Still waiting for the user's decision.
                assert pending['status'] == 'authorization_pending'
                assert pending['verificationUri'] == verification_url and pending['userCode'] == '1234-5678'
                assert 'private-device-code' not in pending_text and 'access_token' not in pending_text
                assert pending['qrCode']['mediaType'] == 'image/png'
                png = base64.b64decode(pending['qrCode']['base64'], validate=True)
                assert png[:8] == b'\x89PNG\r\n\x1a\n'
                width, height = struct.unpack('>II', png[16:24])
                assert 100 < width == height < 4096
                offset = 8
                compressed = bytearray()
                while offset < len(png):
                    length = struct.unpack('>I', png[offset:offset + 4])[0]
                    chunk = png[offset + 4:offset + 8 + length]
                    assert zlib.crc32(chunk) == struct.unpack('>I', png[offset + 8 + length:offset + 12 + length])[0]
                    if chunk[:4] == b'IDAT':
                        compressed.extend(chunk[4:])
                    offset += length + 12
                assert zlib.decompress(compressed)
                challenge_read.set()
                stdout, stderr = process.communicate(timeout=10)
                assert process.returncode == 1 and not stdout.strip()
                assert 'denied' in stderr and '\x1b' not in stderr
            finally:
                if process.poll() is None:
                    process.kill()
                    process.wait()
        print('Native device QR smoke passed: opt-in defaults, flushed PNG JSON, and pending authorization is not success.')
finally:
    challenge_read.set()
    server.shutdown()
    server.server_close()
