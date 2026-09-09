#!/usr/bin/env python3
"""Start an isolated loopback Orchard tenant for CLI tests. Never use production data."""
import json
import os
from pathlib import Path
import secrets
import socket
import subprocess
import tempfile
import time
import urllib.request

repo = Path(__file__).resolve().parents[2]
root = Path(tempfile.mkdtemp(prefix="oc-cli-fixture-"))
os.chmod(root, 0o700)
(root / "Recipes").mkdir()
(root / "wwwroot").mkdir()
# Reuse the shipped Blank recipe while removing its full-line JSON comments.
blank = (repo / "src/OrchardCore.Themes/TheAdmin/Recipes/blank.recipe.json").read_text()
recipe = json.loads("\n".join(line for line in blank.splitlines() if not line.lstrip().startswith("//")))
recipe["name"] = "CliFixture"
# Keep tenant static serving enabled to prove it exposes no management API.
recipe["steps"][0]["enable"] += ["OrchardCore.RemoteManagement", "OrchardCore.Tenants", "OrchardCore.Tenants.FileProvider", "OrchardCore.Workflows", "OrchardCore.Queries.Sql"]
secret = secrets.token_urlsafe(32)
recipe["steps"] += [
    {"name": "RemoteManagementConfiguration"},
    {"name": "Roles", "Roles": [{"Name": "CliDiscovery", "Permissions": ["AccessRemoteManagement"]}]},
    {"name": "OpenIdApplication", "ClientId": "cli-discovery", "ClientSecret": secret,
     "DisplayName": "Disposable discovery-only client", "Type": "confidential", "ConsentType": "implicit",
     "AllowClientCredentialsFlow": True, "RoleEntries": [{"Name": "CliDiscovery"}],
     "ScopeEntries": [{"Name": "orchardcore.management"}]},
    {"name": "OpenIdApplication", "ClientId": "cli-fixture", "ClientSecret": secret,
     "DisplayName": "Disposable CLI fixture", "Type": "confidential", "ConsentType": "implicit",
     "AllowClientCredentialsFlow": True, "RoleEntries": [{"Name": "Administrator"}],
     "ScopeEntries": [{"Name": "orchardcore.management"}]},
    {"name": "OpenIdApplication", "ClientId": "cli-denied", "ClientSecret": secret,
     "DisplayName": "Disposable denied client", "Type": "confidential", "ConsentType": "implicit",
     "AllowClientCredentialsFlow": True, "ScopeEntries": [{"Name": "orchardcore.management"}]},
]
# Separate identities verify existing Media permissions without the admin wildcard.
for suffix, permissions in [
    ("media", ["ManageMediaContent", "ManageMediaFolder"]),
    ("media-restricted", ["ManageMediaContent", "ManageMediaFolder", "UploadRestrictedMedia"]),
    ("media-no-folder", ["ManageOwnMediaContent"]),
]:
    role = "Cli-" + suffix
    recipe["steps"] += [
        {"name": "Roles", "Roles": [{"Name": role, "Permissions": ["AccessRemoteManagement", "ViewOpenApiContent", *permissions]}]},
        {"name": "OpenIdApplication", "ClientId": "cli-" + suffix, "ClientSecret": secret,
         "DisplayName": "Disposable " + suffix + " client", "Type": "confidential", "ConsentType": "implicit",
         "AllowClientCredentialsFlow": True, "RoleEntries": [{"Name": role}],
         "ScopeEntries": [{"Name": "orchardcore.management"}]},
    ]
(root / "Recipes/cli-fixture.recipe.json").write_text(json.dumps(recipe))
with socket.socket() as sock:
    sock.bind(("127.0.0.1", 0))
    port = sock.getsockname()[1]
url = f"http://127.0.0.1:{port}/"
env = os.environ.copy()
env.update({"ASPNETCORE_ENVIRONMENT": "Development", "ORCHARD_APP_DATA": str(root / "App_Data")})
setup = {"ShellName": "Default", "SiteName": "CLI review", "SiteTimeZone": "UTC",
         "AdminUsername": "admin", "AdminEmail": "admin@example.test", "AdminPassword": secrets.token_urlsafe(32) + "aA1!",
         "DatabaseProvider": "Sqlite", "RecipeName": "CliFixture"}
for key, value in setup.items():
    env["OrchardCore__OrchardCore_AutoSetup__Tenants__0__" + key] = value
log = (root / "host.log").open("w")
host = subprocess.Popen(["dotnet", str(repo / "src/OrchardCore.Cms.Web/bin/Debug/net10.0/OrchardCore.Cms.Web.dll"), "--contentRoot", str(root), "--urls", url], env=env, stdout=log, stderr=log)
state = {"url": url, "pid": host.pid, "root": str(root), "OC_CLIENT_ID": "cli-fixture", "OC_CLIENT_SECRET": secret,
         "OC_CONFIG_HOME": str(root / "cli"), "adminPassword": setup["AdminPassword"]}
(root / "fixture.json").write_text(json.dumps(state))
for attempt in range(90):
    if host.poll() is not None:
        raise SystemExit(f"Fixture host exited. Inspect {root / 'host.log'}")
    try:
        with urllib.request.urlopen(url + ".well-known/orchardcore-management", timeout=20) as response:
            if response.status == 200:
                print(root / "fixture.json", flush=True)
                break
    except Exception:
        time.sleep(1)
else:
    host.terminate()
    raise SystemExit(f"Fixture did not become ready. Inspect {root / 'host.log'}")

try:
    host.wait()
except KeyboardInterrupt:
    host.terminate()
    host.wait(timeout=15)
