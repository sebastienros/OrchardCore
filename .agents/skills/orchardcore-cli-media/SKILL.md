---
name: orchardcore-cli-media
description: Manages Orchard Core Media assets through `oc`. Use for image/file folders, uploads, metadata, moves, copies, deletion, CSS/JS/static assets, safe paths, public URLs, and preparing media referenced by content items and Liquid templates.
---

# Orchard Core CLI Media

Use Media for runtime assets, including images, CSS, JavaScript, and SVG. They
share the store and permissions used by the Orchard admin Media library. Tenant
static files are deployment files and have no management API.

## Prepare the tenant

Refresh discovery with `oc api refresh`, then inspect `oc media --help`. If
Media is absent after a successful refresh and feature changes are authorized,
enable `OrchardCore.Media` and run `oc api refresh --force`.
The identity needs `ViewOpenApiContent` for protected OpenAPI discovery,
`AccessRemoteManagement`, `ManageMediaContent`, and permission
to manage the destination folder (`ManageMediaFolder`, or a Secure Media folder
permission). Inspect `oc media constraints show --output json` before upload:
`allowedFileExtensions` includes restricted extensions only when the caller has
`UploadRestrictedMedia`. By default CSS, JavaScript, and SVG require that
additional permission. Do not bypass this policy by switching stores, renaming
files, or granting permissions without authorization.

## Upload image media

Inspect constraints, create a folder, upload, then capture the returned path and
public versioned URL:

```bash
oc media constraints show
oc media folders create --name images
oc media files upload hero.png --path images --file ./hero.png
oc media files show images/hero.png
oc media files list --path images --output table
```

Use returned `filePath` values, such as `images/hero.png`, in `MediaField.Paths`.
Use the matching `MediaTexts` entries for alternative text. Do not invent
`/media/...` field values from public URLs.

Other operations are discoverable per tenant:

```bash
oc media folders list --path ""
oc media items list
oc media files copy --body-file copy.json
oc media files move --body-file move.json
oc media files move-batch --body-file move-batch.json
oc media files delete-batch --body-file delete.json --yes
oc media files delete images/obsolete.png --yes
```

Run the corresponding `schema --operation <verb>` command before sending JSON.
Media uploads do not overwrite an existing destination; choose a new name,
delete intentionally, or use a documented move/copy workflow.

## Upload custom assets

Choose the folder in the client; `assets/styles` below is only a convention.
Inspect existing folders before creating them. The upload argument is a base
filename; `--path` selects the destination folder.

```bash
oc media constraints show --output json
oc media folders create --name assets
oc media folders create --path assets --name styles
oc media files upload site-v1.css --path assets/styles --file ./site.css
oc media files show assets/styles/site-v1.css --output json
oc media files list --path assets/styles --output table
```

Use the returned `url` for public access; resolve relative URLs against the
tenant origin. Do not construct a server filesystem path or assume `/media`:
storage may use a CDN or a remote provider. In Liquid, generate an asset URL
from its returned `filePath`:

```liquid
{% assign stylesheet = "assets/styles/site-v1.css" | asset_url %}
{% style name:"tenant-site", src:stylesheet %}
{% resources type: "Stylesheet" %}
```

Resource tags require `OrchardCore.Resources`. Register styles in the template
and render stylesheet resources in its layout. Use versioned filenames for
updates; uploads reject existing destinations. Only delete/replace an existing
asset when authorized, after checking references. For authorized cleanup:

```bash
oc media files delete assets/styles/site-v1.css --yes
oc media folders delete assets/styles --yes
```

Delete a folder only when its entire contents are authorized for removal.
Verify deletion through Media listing; public/CDN caches may outlive removal.

Media uses extensible `IMediaFileStore`. The default is local disk. Multiple
nodes must use the same shared backend (for example Azure Blob or Amazon S3)
and tenant configuration to share uploads. Uploading alone does not replicate
local storage; do not claim a multi-node check from a single-node fixture.

## Path and upload safety

- Use store-relative forward-slash paths with no leading slash.
- Reject `.`/`..`, empty interior segments, and path separators in filenames.
- Inspect upload extension/size constraints before transferring large assets.
- Use `--file` for binary data; use `--stdin` only when the pipeline preserves
  bytes exactly.
- Never overwrite or delete an asset without checking which content/templates
  reference it.
- Treat SVG and active file formats as untrusted content unless the tenant's
  policy explicitly permits them.

## Verify styling assets

Fetch the returned public URL into a file and compare the raw bytes; do not
round-trip the response through JSON or text commands that can append a newline.
With `asset_url` set to the absolute returned URL and `site.css` the original:

```bash
readback_file=$(mktemp)
curl --fail --silent --show-error "$asset_url" --output "$readback_file"
cmp ./site.css "$readback_file"
rm "$readback_file"
```

Also verify the response content type. When a template references the asset,
verify the rendered page and computed styles. Use returned `filePath` values for
media fields, not public URLs.

Canonical references:
`src/docs/reference/api/media/README.md` and
`src/docs/reference/modules/Media/README.md`.
