# Shipping the help system with DWSIM

This folder shows how to bundle the built help with DWSIM and host it inside the application via WebView2.

## 1. Build the shippable bundle

From the `helpsystem/` root:

```
python build.py --ship
```

This runs the full pipeline plus two extra steps:

- **`--shrink-images`** — runs `pngquant --quality=65-85` over `docs/images/screens*/`. PNGs typically shrink ~3–5×. Install pngquant via `scoop install pngquant` or download from https://pngquant.org/. Skipped silently if not on PATH.
- **`--portable`** — runs `mkdocs build` with `use_directory_urls: false` and writes the bundle to `dist/dwsim-help/`. Pages become `05-welcome-screen.html` (not `05-welcome-screen/index.html`), so all relative URLs work under `file://` and from a WebView2 virtual host.

You can also run the steps independently:

```
python build.py --shrink-images          # one-time PNG compression
python build.py --portable               # rebuild bundle only
python build.py --skip-convert --portable  # repackage without re-running pandoc
```

## 2. Bundle into DWSIM's installer

Copy the **contents** of `dist/dwsim-help/` into `<DWSIM-install>/Help/` during your installer build. After install, the user's machine should have:

```
<DWSIM>/
├── DWSIM.exe
├── Help/
│   ├── index.html
│   ├── 05-welcome-screen.html
│   ├── ...
│   ├── images/
│   ├── assets/        (mkdocs-material theme JS/CSS)
│   └── search/        (offline search index)
```

## 3. Host it inside DWSIM via WebView2

[HelpWindow.cs](HelpWindow.cs) is a drop-in `Form` that:

- Spins up a WebView2 in DWSIM's user-data folder (`%LOCALAPPDATA%/DWSIM/WebView2Cache`)
- Maps `https://dwsim.help/` → `<DWSIM>/Help/` via `SetVirtualHostNameToFolderMapping` — this gives you a real HTTPS origin without a server, so search index XHRs, MathJax CDN, and anchor scrolling all work.
- Routes external links (e.g. GitHub) out to the user's default browser.

### Requirements

- NuGet package `Microsoft.Web.WebView2` ≥ 1.0.2592
- WebView2 Runtime — preinstalled on Win10 22H2+ and Win11. For older Windows, bundle the [Evergreen Bootstrapper](https://developer.microsoft.com/microsoft-edge/webview2/) (~150 KB stub) in your installer.

### Wiring up

In the main DWSIM form's Help menu handler:

```csharp
// Open at the home page
private void HelpMenuItem_Click(object sender, EventArgs e)
{
    new DWSIM.Help.HelpWindow().Show(this);
}

// Or deep-link to a section anchor (e.g. when the user presses F1 on the TEA panel)
private void TEAPanel_KeyDown(object sender, KeyEventArgs e)
{
    if (e.KeyCode == Keys.F1)
    {
        DWSIM.Help.HelpWindow.OpenAt("sec:tea");
        e.Handled = true;
    }
}
```

`HelpWindow.OpenAt("sec:foo")` deep-links to `https://dwsim.help/index.html#sec:foo`. Anchors come from labels in the original LyX (`\label{sec:tea}`, etc.) and are emitted as HTML `id` attributes during the build.

## 4. AI Assistant knowledge base (BM25 RAG)

The dwsim-assistant Python app **already has BM25 RAG built in** (`server.py` →
`search_knowledge_base()`, exposed to the LLM as the `dwsim_search_knowledge`
tool). It scans `<exe_dir>/knowledge/**/*.md` at startup, splits on H1-H3
headings, ranks via `rank_bm25`. Integration is just **dropping cleaned
markdown files into the right folder** — no code changes.

`build.py --portable` emits the cleaned files under:

```
dist/dwsim-help/assistant-knowledge/
├── 01-license.md
├── 02-contact-information.md
├── 03-introduction.md
├── 05-welcome-screen.md
├── ...   (54 files, ~600 kB total)
```

The cleaning pass strips MathJax `\[…\]`, pandoc `<a id>` anchors, raw HTML
wrappers, image syntax, and `{reference-type=…}` cruft — all noise that
hurts BM25 and bloats the LLM's reading window. Heading levels are remapped
so the assistant's H1-H3 splitter creates one chunk per user-guide *section*
instead of one chunk per *page*:

| Source heading              | Exported heading | Why                                |
|-----------------------------|------------------|------------------------------------|
| H1 page title               | H1               | One per file, splitter sees it     |
| H4 (LyX `\section`)         | **H2**           | Section becomes a chunk boundary   |
| H5 (LyX `\subsection`)      | **H3**           | Sub-section becomes a chunk boundary |
| H6 (LyX `\subsubsection`)   | H4               | Stays inside parent chunk          |

### Integration steps

1. **Build:**
   ```
   python build.py --ship
   ```

2. **Copy the exported markdown into the dwsim-assistant tree:**
   ```
   xcopy /e /y dist\dwsim-help\assistant-knowledge\*.md ^
               C:\path\to\dwsim-assistant\knowledge\user_guide\
   ```
   (Replaces the 9 hand-curated stub files. Keep them if you prefer — the
   splitter happily merges both sets.)

3. **Restart the assistant.** No code changes. On startup, `_kb_chunks()`
   re-walks `knowledge/`, picks up the new files, and the LLM's
   `dwsim_search_knowledge` tool now returns hits from the full user guide.

### Why this approach beats shipping an embedding-model RAG

| Approach              | Build deps            | Runtime deps      | Payload        |
|-----------------------|-----------------------|-------------------|---------------:|
| Embeddings (ONNX)     | sentence-transformers | onnxruntime ~30 MB | ~55 MB        |
| Existing `rank_bm25`  | none beyond requirements.txt | none extra | **~600 kB**    |

Help lookups skew strongly toward exact term matches — users type real class
names, model names, chemistry terms — so BM25 over hand-cleaned prose is
hard to beat for this domain.

### Verifying it worked

After copying the files and restarting the assistant, test in the chat:

> "How do I configure NRTL parameters?"

The LLM should call `dwsim_search_knowledge` with that query (or similar) and
the response should cite content from `40-aqueous-solution-properties.md` /
`37-thermodynamic-properties.md` / etc. If the tool returns "Knowledge base
directory is empty or missing", check that `knowledge/user_guide/` actually
contains the `.md` files and not nested deeper.

## 5. Size & file-count budget

After `--ship`:

| Component        | Files | Approx size |
|------------------|------:|------------:|
| Compressed PNGs  | ~600  | 10–15 MB    |
| HTML pages       |   ~55 | ~1.5 MB     |
| MkDocs theme     |   ~30 | ~1 MB       |
| Search index     |    1  | 0.5–1 MB    |
| **Total**        |       | **~15–20 MB** |

Down from ~64 MB for the unoptimized build.
