---
name: orchardcore-cli-media
description: Manages Orchard Core Media and tenant static files through `oc`. Use for image/file folders, uploads, metadata, moves, copies, deletion, CSS/JS/static assets, safe paths, public URLs, and preparing media referenced by content items and Liquid templates.
---

# Orchard Core CLI Media and Static Files

Use Media for editor-managed assets referenced by content fields. Use the
tenant File Provider for site-owned CSS, JavaScript, fonts, and other static
files. Both are tenant-scoped but have different roots and public URLs.

## Prepare the tenant

```bash
oc features enable OrchardCore.Media
oc features enable OrchardCore.Tenants.FileProvider
oc features enable OrchardCore.Resources
oc api refresh --force
oc media --help
oc static files --help
```

The identity needs `AccessRemoteManagement` plus the Media or tenant-static-file
permission required by each operation.

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

## Upload tenant CSS and static assets

```bash
oc static files upload styles/site.css --file ./site.css
oc static files upload styles/site.css --overwrite true --file ./site.css
oc static files list --path styles --output table
oc static files show styles/site.css
```

The response URL is the public tenant URL, for example
`/tenant-a/styles/site.css`, not an API route. Static responses are aggressively
cached. Version file names or template query strings when replacing assets:

```liquid
{% style name:"tenant-site", src:"~/styles/site.css?v=20260829-1" %}
```

The active layout must render stylesheet resources:

```liquid
{% resources type: "Stylesheet" %}
```

## Path and upload safety

- Use store-relative forward-slash paths with no leading slash.
- Reject `.`/`..`, empty interior segments, and path separators in filenames.
- Keep media and static paths separate.
- Inspect upload extension/size constraints before transferring large assets.
- Use `--file` for binary data; use `--stdin` only when the pipeline preserves
  bytes exactly.
- Never overwrite or delete an asset without checking which content/templates
  reference it.
- Treat SVG and active file formats as untrusted content unless the tenant's
  policy explicitly permits them.

## Verify styling assets

```bash
curl -fsSI 'https://cms.example.com/tenant-a/styles/site.css?v=20260829-1'
oc content items render <id> --version published --display-type Detail
```

Confirm the public response content type, template stylesheet registration,
media URLs, image alternative text, and browser-computed styles.

Canonical references:
`src/docs/reference/api/media/README.md`,
`src/docs/reference/api/static-files/README.md`, and
`src/docs/reference/modules/Media/README.md`.
