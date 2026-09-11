# Install a local CMS

Install the executable first using [CLI installation](cli-installation.md) if
`pomi` is unavailable. Apply the [shared operating rules](shared-rules.md). For a new tenant in an
existing application, use [tenant installation](tenants.md) instead.

Use `pomi install <directory>` to create a new standalone application **or initial
SaaS host** and initialize its Default tenant. This static command needs no tenant context or
login. It embeds this CLI build's `occms` template and uses matching Orchard
dependency versions; no template download or version selection is needed.
Run `pomi doctor` to check for the required stable .NET SDK (currently .NET 10).
Dependencies still need a NuGet feed or populated package cache. Nuget.org is
added by default; parent and user-level NuGet sources remain available. For
this fork's temporary CLI previews, use an inherited Feedz configuration or
pass `--source https://f.feedz.io/sebastienros/orchardcore/nuget/index.json`.
Use `--clear-sources` only when the user wants to exclude inherited package
sources; it leaves nuget.org and an explicit `--source`.

## Choose the site to create

Use the same installer for these two new-host workflows. The password variable
in these examples must already be populated securely as described below.

For a new site without a requested recipe, prefer SQLite and Blank:

```bash
pomi install ./MySite \
  --recipe-name Blank --database-provider Sqlite \
  --site-name "My Site" --user-name admin --email admin@example.com \
  --password-env OC_SITE_PASSWORD --output json
```

For a new SaaS host, create and initialize its **Default tenant** with SaaS:

```bash
pomi install ./MySaaS \
  --recipe-name SaaS --database-provider Sqlite \
  --site-name "My SaaS" --user-name admin --email admin@example.com \
  --password-env OC_SITE_PASSWORD --output json
```

SaaS is the setup recipe, not a different .NET project template. The SaaS
recipe enables tenant management; it does not create the requested child tenants.
The bundled SaaS recipe also enables and configures Pomi Remote Management.
After starting the host, add a context for its root URL and authenticate, then
use [tenant installation](tenants.md) for each requested child:

```bash
pomi context add default https://localhost:5001 --current
pomi login
pomi api refresh
pomi tenants install --help
```

Use the actual host URL. This login is interactive; follow
[authentication](authentication.md) for browser or device approval.
A child tenant can use Blank or Blog independently of the host's SaaS recipe.
Do not attempt `pomi tenants install` before a host exists.

Let Pomi instantiate its embedded template, build the project, and perform Auto
Setup. Do not write the initial `.csproj` or Auto Setup settings yourself, run
`dotnet new occms`, or recreate these steps with shell/HTTP scripts unless the
user explicitly requests manual scaffolding. If prerequisites or feeds fail,
resolve the reported blocker using this guide and the CLI installation reference;
do not silently switch to a manual installation.

## Credentials and options

The password must meet the [setup password policy](setup-password.md). That
reference includes a cryptographic generator guaranteed to meet the standard
policy. The password environment variable must already be provided by the user,
secret manager, or an authorized generation step. Save generated administrator
credentials in a private file accessible to the user before setup, following
[credential handoff](setup-password.md#credential-handoff). The masked interactive prompt, `--password-file`, and
`--password-stdin` are alternatives. Connection strings use the analogous
`--connection-string-*` options. `--connection-string-env` takes a variable
name, not the secret value. There is no inline `--connection-string` option.
See the [safe connection-string examples](tenants.md#schema-properties-and-secret-cli-options)
for the three input forms; local installation uses the same options. Only one
secret may consume stdin.

If no database provider is specified, recommend SQLite and use the default
`Sqlite`. For another requested provider, recommend a unique `--table-prefix`
following the [database choice rules](shared-rules.md#database-choice-for-new-sites-and-tenants).
Do not replace the user's chosen provider. The executable defaults to the SaaS
recipe, administrator `admin`, and UTC, but **pass the recipe explicitly**:
prefer Blank for an unspecified new site, SaaS for a SaaS host, and Blog for a
requested blog. A blog-like directory or site name
does not select the Blog recipe. Consult
`pomi install --help` for other database, recipe, and URL options. `--source`
adds a dependency feed alongside nuget.org. Only an explicitly requested project
template other than `occms` needs direct template tooling. SaaS, Blank, and Blog
are all supported recipes within `pomi install`. Time zones use IANA/TZDB IDs such as `Europe/Paris` and
`America/Los_Angeles`, or `UTC`.

The default listen address is `https://localhost:5001`, which needs a certificate.
Use `dotnet dev-certs https --trust` for local development, or explicitly choose
HTTP with `--urls http://localhost:5000`. Multiple addresses use one quoted
semicolon-separated argument: `--urls "https://localhost:5001;http://localhost:5000"`.

## Start and connect

Without `--run`, installation stops its temporary setup host after completion.
The JSON `tenantState` describes persisted tenant initialization, not a running
server process.
Add `--run` only when the user wants the site left running in the foreground;
Ctrl+C stops it. For an agent-managed development server, install without
`--run`, then use the environment's supported persistent process/session mechanism
with `dotnet run --project ./MySaaS --no-launch-profile --urls https://localhost:5001`
(replace the path and URL as needed). Starting an already installed project with
`dotnet run` is separate from scaffolding it. Do not treat a foreground server
waiting for requests as a failed installation. Existing nonempty destinations are refused. On failure, inspect
the preserved project and output before deciding how to proceed; do not blindly
retry setup against a partly initialized database. Administrator passwords are
not persisted in configuration, but Orchard persists database connection settings.
The bundled SaaS recipe configures Remote Management for Pomi. For other
recipes/builds, check their capabilities and follow the linked setup guide if
Remote Management is absent; do not recreate or reset the installed site.

After installation, see [tenant management and Remote Management setup](tenants.md)
and the versioned [Remote Management reference](https://github.com/sebastienros/OrchardCore/blob/4d4fc0fb66a5d789dff6d8057bbc074107918533/src/docs/reference/modules/RemoteManagement/README.md).
