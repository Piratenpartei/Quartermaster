# Vendored files for the collaborative editor

All files in this directory are vendored (committed verbatim) third-party JavaScript, pulled from public CDNs as pre-built bundles. No npm, no build step — they drop straight into `wwwroot` and are served as static assets.

The loading strategy is:

- **UMD**: `cm5.js` + `cm5-markdown.js` + `cm5.css` are loaded via regular `<script>` / `<link>` tags in `index.html` to set `window.CodeMirror` globally.
- **ESM**: `yjs.js` + `y-awareness.js` + `y-cm5.js` are loaded via dynamic `import()` from `codemirror-editor.js`. An import map in `index.html` redirects the esm.sh absolute paths they reference (`/yjs@...`, `/codemirror@...`) to the local files.
- **Shims**: `cm5-esm.js` re-exports `window.CodeMirror` as an ESM default so `y-cm5.js` can `import CodeMirror from "codemirror"`. `process.js` provides a minimal `process.env` polyfill for Yjs.

## Sources and versions

| File | Source | Version |
|---|---|---|
| `cm5.js` | https://cdnjs.cloudflare.com/ajax/libs/codemirror/5.65.16/codemirror.js | codemirror@5.65.16 (UMD) |
| `cm5.css` | https://cdnjs.cloudflare.com/ajax/libs/codemirror/5.65.16/codemirror.css | codemirror@5.65.16 |
| `cm5-theme-dark.css` | https://cdnjs.cloudflare.com/ajax/libs/codemirror/5.65.16/theme/material-darker.min.css | codemirror@5.65.16 material-darker theme |
| `cm5-markdown.js` | https://cdnjs.cloudflare.com/ajax/libs/codemirror/5.65.16/mode/markdown/markdown.min.js | codemirror@5.65.16 markdown mode addon |
| `cm5-esm.js` | hand-written 20-line ESM wrapper around `window.CodeMirror` | — |
| `yjs.js` | https://esm.sh/yjs@13.6.27?target=es2022 (bundled) | yjs@13.6.27 |
| `y-awareness.js` | https://esm.sh/y-protocols@1.0.6/awareness?target=es2022&bundle&external=yjs | y-protocols@1.0.6 |
| `y-cm5.js` | https://esm.sh/y-codemirror@3.0.1?target=es2022&bundle&external=yjs,codemirror | y-codemirror@3.0.1 (CM5 binding) |
| `process.js` | hand-written minimal Node `process.env` polyfill | — |

## Why CodeMirror 5 (not 6)

CodeMirror 6 has a modular architecture (`@codemirror/state`, `@codemirror/view`, `@codemirror/language`, etc.) where each package inlines its own copy of shared dependencies when fetched as individual ESM bundles. Combining them at runtime fails with "Unrecognized extension value in extension set" — the `instanceof` checks break across duplicated `@codemirror/state` copies. Solving this would require either a bundler (no npm toolchain, per user preference) or vendoring ~20 individual transitive files with a fragile import map.

CM5 predates this architecture and ships as single-file UMD, making vendoring trivial. The trade-off is that CM5 is in maintenance mode (no new features, bug fixes only) — but our needs (markdown editor + collaborative cursors + per-character background colors via `markText`) have been stable in CM5 for years.

## How to refresh a vendored file

1. Fetch the new version from the source URL above.
2. Replace the file in this directory.
3. Update the version cell in the table.
4. Re-run the end-to-end tests in `MeetingHubTests` (SignalR pathway) and smoke-test the meeting live page in Chrome.
5. Check the console for import/loading errors — dependencies between these files are tightly coupled via the import map.
