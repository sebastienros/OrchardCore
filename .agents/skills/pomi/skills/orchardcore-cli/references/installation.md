# Install a local CMS

Install the executable first using [CLI installation](cli-installation.md) if
`pomi` is unavailable. Apply the [shared operating rules](shared-rules.md). For a new tenant in an
existing application, use [tenant installation](tenants.md) instead.

Use `pomi install <directory>` when the user wants a new local application and
its Default tenant initialized. This static command needs no tenant context or
login. It embeds this CLI build's `occms` template and uses matching Orchard
dependency versions; no template download or version selection is needed.
Run `pomi doctor` to check for the required stable .NET SDK (currently .NET 10).
Dependencies still need a NuGet feed or populated package cache. Nuget.org is
added by default; parent and user-level NuGet sources remain available. For
this fork's temporary CLI previews, use an inherited Feedz configuration or
pass `--source https://f.feedz.io/sebastienros/orchardcore/nuget/index.json`.
Use `--clear-sources` only when the user wants to exclude inherited package
sources; it leaves nuget.org and an explicit `--source`.

```bash
pomi install ./MySite --site-name "My Site" --email admin@example.com --password-env OC_SITE_PASSWORD
```

The password must meet the [setup password policy](setup-password.md). That
reference includes a cryptographic generator guaranteed to meet the standard
policy. The password environment variable must already be provided by the user,
secret manager, or an authorized generation step. The masked interactive prompt, `--password-file`, and
`--password-stdin` are alternatives. Connection strings use the analogous
`--connection-string-*` options. `--connection-string-env` takes a variable
name, not the secret value. There is no inline `--connection-string` option.
See the [safe connection-string examples](tenants.md#schema-properties-and-secret-cli-options)
for the three input forms; local installation uses the same options. Only one
secret may consume stdin.

If no database provider is specified, recommend SQLite and use the default
`Sqlite`. For another requested provider, recommend a unique `--table-prefix`
following the [database choice rules](shared-rules.md#database-choice-for-new-sites-and-tenants).
Do not replace the user's chosen provider. Other defaults are the SaaS recipe,
administrator `admin`, and UTC. Match the recipe to the requested site: use `--recipe-name Blog` for a blog or
`--recipe-name Blank` for a minimal site. A blog-like directory or site name
does not select the Blog recipe. Consult
`pomi install --help` for other database, recipe, and URL options. `--source`
adds a dependency feed alongside nuget.org. Use `dotnet new` directly for other
templates. Time zones use IANA/TZDB IDs such as `Europe/Paris` and
`America/Los_Angeles`, or `UTC`.

The default listen address is `https://localhost:5001`, which needs a certificate.
Use `dotnet dev-certs https --trust` for local development, or explicitly choose
HTTP with `--urls http://localhost:5000`. Multiple addresses use one quoted
semicolon-separated argument: `--urls "https://localhost:5001;http://localhost:5000"`.

Without `--run`, installation stops its temporary setup host after completion.
The JSON `tenantState` describes persisted tenant initialization, not a running
server process.
Add `--run` only when the user wants the site left running in the foreground;
Ctrl+C stops it. Existing nonempty destinations are refused. On failure, inspect
the preserved project and output before deciding how to proceed; do not blindly
retry setup against a partly initialized database. Administrator passwords are
not persisted in configuration, but Orchard persists database connection settings.
Remote Management still needs configuring before using remote commands.

After installation, see [tenant management and Remote Management setup](tenants.md)
and the versioned [Remote Management reference](https://github.com/sebastienros/OrchardCore/blob/4d4fc0fb66a5d789dff6d8057bbc074107918533/src/docs/reference/modules/RemoteManagement/README.md).
