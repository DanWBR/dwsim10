# Building the documentation

This page explains how to (re)generate the Fluent API documentation site
locally — the hand-written guides, the auto-generated API reference and
the static HTML output.

All commands assume you are at the root of `DWSIM.FluentAPI/`.

## 1. Prerequisites

- **Python 3.9+** on the `PATH`.
- **MkDocs + Material theme + extensions**:

  ```bash
  pip install -r requirements-docs.txt
  ```

  (Pulls `mkdocs`, `mkdocs-material`, `pymdown-extensions`.)

- The compiled assembly **`DWSIM.Automation.FluentAPI.dll`** with its XML
  doc file at `bin/Debug/DWSIM.Automation.FluentAPI.xml`. Build the
  project once with MSBuild (the `.csproj` already has
  `<DocumentationFile>` enabled) so the XML is fresh.

  ```bash
  msbuild DWSIM.Automation.FluentAPI.csproj /p:Configuration=Debug
  ```

## 2. Regenerate the auto-built API Reference

Whenever you change XML doc comments in the C# / VB.NET sources, re-run:

```bash
python scripts/generate_api_reference.py
```

The script reads `bin/Debug/DWSIM.Automation.FluentAPI.xml` and emits
one Markdown page per public type into `docs/api-reference/`, plus an
index page organized by namespace. Existing files in that folder are
purged before each run, so the output stays in sync with the build.

You don't need to commit `docs/api-reference/` if you'd rather treat it
as a build artifact — `.gitignore` it and run the script in CI before
publishing.

## 3. Live preview

```bash
mkdocs serve
```

Opens `http://127.0.0.1:8000` with hot reload — edits to any `.md` or to
`mkdocs.yml` re-render the page in the browser.

## 4. Static HTML build

```bash
mkdocs build
```

Writes the entire site to `site/`. The output is fully self-contained
(HTML + CSS + JS + client-side search via lunr) — open
`site/index.html` directly in a browser, or publish the folder on any
static host (GitHub Pages, S3, Netlify, …).

The site uses `use_directory_urls: false` so links resolve to `.html`
files instead of folders, which means the site works correctly when
served from the local filesystem (`file://`) without an HTTP server.

For a stricter build that fails on broken links, missing files or other
problems:

```bash
mkdocs build --strict
```

## 5. Full rebuild from scratch

```bash
pip install -r requirements-docs.txt
python scripts/generate_api_reference.py
mkdocs build --strict
```

That's the canonical sequence. Drop it into a CI step (GitHub Actions /
Azure DevOps / GitLab CI) and publish `site/` as the deployable
artifact.

## 6. File layout

```
DWSIM.FluentAPI/
├── mkdocs.yml                    # MkDocs config (theme, nav, extensions)
├── requirements-docs.txt         # Python deps for building the docs
├── scripts/
│   └── generate_api_reference.py # XML doc -> Markdown generator
├── docs/                         # Markdown sources
│   ├── index.md
│   ├── BUILDING.md               # this file
│   ├── getting-started/
│   ├── api/                      # hand-written task-oriented reference
│   ├── api-reference/            # auto-generated from XML doc (purged each run)
│   ├── examples/
│   └── python-guide.md
├── site/                         # static HTML output (mkdocs build)
└── bin/Debug/
    └── DWSIM.Automation.FluentAPI.xml   # source for the auto reference
```

## 7. Troubleshooting

| Symptom | Cause / fix |
|---|---|
| `XML not found at bin/Debug/...` from the generator | Build the project first so MSBuild emits the XML doc file. |
| Auto reference looks stale after a code change | Re-run `python scripts/generate_api_reference.py`, then `mkdocs build`. |
| `mkdocs build --strict` fails on a broken cref link | A `<see cref="...">` in an XML doc points to a member or type that no longer exists. Fix the source XML comment and regenerate. |
| Pages render but the static site shows folder listings when browsed locally | `use_directory_urls: false` must be set in `mkdocs.yml` (already configured here). |
| Search doesn't return results | Hard-refresh the page — the lunr index is loaded once per session and may be cached. |
