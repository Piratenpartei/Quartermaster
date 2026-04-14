// CodeMirror 5 editor wrapper for Blazor interop.
//
// Lazy-loads the vendored CM5 UMD bundle (~440KB combined with markdown mode)
// on first use — meeting live pages are the only consumer, no reason to pay
// that cost on the initial Blazor boot.
//
// Exposes a small imperative API under `window.cmEditor` so Blazor interop
// can call it via `IJSRuntime.InvokeAsync("cmEditor.createEditor", ...)`.
// We avoid the `import()` dynamic module path because Blazor's asset
// pipeline rewrites import URLs to fingerprinted names that the server's
// plain `UseStaticFiles` middleware doesn't serve.
//
// Handles are string ids stored in the `editors` map. Blazor holds onto the
// id and passes it back on subsequent calls — it never sees the CM instance
// directly (can't be marshaled across JS interop).

(function () {
const COLLAB_EDITOR_BASE = "/js/collab-editor/";

// Name of the CM5 theme we use when the site is in dark mode. "default"
// is the built-in light theme that ships with cm5.css; "material-darker"
// comes from cm5-theme-dark.css. When the site's `data-bs-theme` attribute
// changes, every live collab editor swaps its `theme` option accordingly.
const DARK_THEME = "material-darker";
const LIGHT_THEME = "default";

function currentTheme() {
    return document.documentElement.getAttribute("data-bs-theme") === "dark"
        ? DARK_THEME : LIGHT_THEME;
}

// One MutationObserver watches the <html> data-bs-theme attribute for any
// toggle from the existing Blazor theme switcher and broadcasts the new
// theme name to every live CM5 editor via `entry.cm.setOption("theme")`.
let themeObserver = null;
function ensureThemeObserver() {
    if (themeObserver)
        return;
    themeObserver = new MutationObserver(() => {
        const theme = currentTheme();
        for (const entry of editors.values()) {
            if (entry.cm) {
                try { entry.cm.setOption("theme", theme); } catch (_) {}
            }
        }
    });
    themeObserver.observe(document.documentElement, {
        attributes: true,
        attributeFilter: ["data-bs-theme"]
    });
}

let loadPromise = null;

function loadCm5Once() {
    if (loadPromise)
        return loadPromise;

    loadPromise = (async () => {
        if (typeof globalThis.CodeMirror === "function")
            return;

        // Inject the CM5 stylesheet.
        if (!document.querySelector('link[data-cm5="core"]')) {
            const link = document.createElement("link");
            link.rel = "stylesheet";
            link.href = COLLAB_EDITOR_BASE + "cm5.css";
            link.dataset.cm5 = "core";
            document.head.appendChild(link);
        }

        // Also inject the dark theme CSS so switching is instant (no late
        // stylesheet fetch when the user toggles dark mode for the first time).
        if (!document.querySelector('link[data-cm5="theme-dark"]')) {
            const dark = document.createElement("link");
            dark.rel = "stylesheet";
            dark.href = COLLAB_EDITOR_BASE + "cm5-theme-dark.css";
            dark.dataset.cm5 = "theme-dark";
            document.head.appendChild(dark);
        }

        // Load the CM5 UMD script, then the markdown mode addon (which
        // depends on window.CodeMirror already being set).
        await loadScript(COLLAB_EDITOR_BASE + "cm5.js", "cm5-core");
        await loadScript(COLLAB_EDITOR_BASE + "cm5-markdown.js", "cm5-markdown");

        if (typeof globalThis.CodeMirror !== "function") {
            throw new Error("codemirror-editor.js: window.CodeMirror was not set after loading cm5.js");
        }
    })();

    return loadPromise;
}

function loadScript(src, id) {
    return new Promise((resolve, reject) => {
        const existing = document.querySelector(`script[data-cm5-id="${id}"]`);
        if (existing) {
            resolve();
            return;
        }
        const script = document.createElement("script");
        script.src = src;
        script.dataset.cm5Id = id;
        script.onload = () => resolve();
        script.onerror = () => reject(new Error(`Failed to load ${src}`));
        document.head.appendChild(script);
    });
}

const editors = new Map();
let nextHandleId = 1;

async function createEditor(element, initialText, isReadOnly, dotnetRef) {
    await loadCm5Once();
    ensureThemeObserver();

    if (!element) {
        throw new Error("codemirror-editor.js: createEditor called with null element");
    }

    // Clear any placeholder content from the host div (Blazor renders the
    // div empty but defensive).
    element.innerHTML = "";

    const cm = globalThis.CodeMirror(element, {
        value: initialText || "",
        mode: "markdown",
        lineNumbers: true,
        lineWrapping: true,
        readOnly: isReadOnly ? "nocursor" : false,
        theme: currentTheme()
    });

    const handle = "cm5-" + (nextHandleId++);
    const entry = { cm, dotnetRef };
    editors.set(handle, entry);

    // Forward change events to Blazor via the DotNetObjectReference. Read
    // through `entry.dotnetRef` so `setText` can temporarily null it to
    // suppress echo-back during programmatic updates.
    cm.on("change", () => {
        const ref = entry.dotnetRef;
        if (!ref)
            return;
        try {
            ref.invokeMethodAsync("OnJsValueChanged", cm.getValue());
        } catch (_) {
            // Component may have been disposed; ignore.
        }
    });

    return handle;
}

function setText(handle, text) {
    const entry = editors.get(handle);
    if (!entry)
        return;
    // Avoid echoing back via the change handler by temporarily clearing the ref.
    const saved = entry.dotnetRef;
    entry.dotnetRef = null;
    try {
        if (entry.cm.getValue() !== text)
            entry.cm.setValue(text || "");
    } finally {
        entry.dotnetRef = saved;
    }
}

function getText(handle) {
    const entry = editors.get(handle);
    return entry ? entry.cm.getValue() : "";
}

function setReadOnly(handle, readOnly) {
    const entry = editors.get(handle);
    if (!entry)
        return;
    entry.cm.setOption("readOnly", readOnly ? "nocursor" : false);
}

function dispose(handle) {
    const entry = editors.get(handle);
    if (!entry)
        return;
    // If presence is subscribed, detach the awareness listener.
    if (entry._presenceFire && entry.awareness) {
        try { entry.awareness.off("change", entry._presenceFire); } catch (_) {}
    }
    entry._presenceDotnetRef = null;
    entry._presenceFire = null;
    // If collab is attached, tear it down first.
    if (entry.binding) {
        try { entry.binding.destroy(); } catch (_) {}
    }
    // CM5 doesn't have an explicit destroy — just detach from the DOM and
    // drop the reference.
    const wrapper = entry.cm.getWrapperElement();
    if (wrapper && wrapper.parentNode)
        wrapper.parentNode.removeChild(wrapper);
    entry.dotnetRef = null;
    editors.delete(handle);
}

// -----------------------------------------------------------------------
// Collaborative editing (Yjs) layer
// -----------------------------------------------------------------------

let collabLibsPromise = null;

function loadCollabLibsOnce() {
    if (collabLibsPromise)
        return collabLibsPromise;
    collabLibsPromise = (async () => {
        await loadCm5Once();
        const Y = await import(COLLAB_EDITOR_BASE + "yjs.js");
        const awarenessMod = await import(COLLAB_EDITOR_BASE + "y-awareness.js");
        const yCmMod = await import(COLLAB_EDITOR_BASE + "y-cm5.js");
        return {
            Y,
            awarenessMod,
            Awareness: awarenessMod.Awareness,
            CodemirrorBinding: yCmMod.CodemirrorBinding
        };
    })();
    return collabLibsPromise;
}

function base64ToBytes(b64) {
    if (!b64)
        return new Uint8Array(0);
    const bin = atob(b64);
    const len = bin.length;
    const out = new Uint8Array(len);
    for (let i = 0; i < len; i++)
        out[i] = bin.charCodeAt(i);
    return out;
}

function bytesToBase64(bytes) {
    if (!bytes || bytes.length === 0)
        return "";
    let bin = "";
    const chunk = 0x8000;
    for (let i = 0; i < bytes.length; i += chunk) {
        bin += String.fromCharCode.apply(null, bytes.subarray(i, i + chunk));
    }
    return btoa(bin);
}

// Create a collaborative CodeMirror 5 editor bound to a Y.Doc.
// `initialStateBase64` is the Yjs snapshot from the server (empty for a
// fresh document). `initialText` is only used as a legacy plain-text seed
// if there's no prior snapshot.
async function createCollabEditor(element, initialText, initialStateBase64, canEdit, dotnetRef) {
    const libs = await loadCollabLibsOnce();
    ensureThemeObserver();

    if (!element)
        throw new Error("createCollabEditor called with null element");
    element.innerHTML = "";

    const doc = new libs.Y.Doc();
    const ytext = doc.getText("content");

    // Apply the server's existing snapshot (if any) BEFORE constructing the
    // binding so the editor renders the final text in one step.
    const initialBytes = base64ToBytes(initialStateBase64);
    if (initialBytes.length > 0) {
        libs.Y.applyUpdate(doc, initialBytes);
    } else if (initialText) {
        ytext.insert(0, initialText);
    }

    const cm = globalThis.CodeMirror(element, {
        value: "",
        mode: "markdown",
        lineNumbers: true,
        lineWrapping: true,
        readOnly: canEdit ? false : "nocursor",
        theme: currentTheme()
    });

    const awareness = new libs.Awareness(doc);
    const binding = new libs.CodemirrorBinding(ytext, cm, awareness);

    const handle = "cm5-collab-" + (nextHandleId++);
    const entry = {
        cm, doc, ytext, awareness, binding, libs, dotnetRef, canEdit,
        authorMarkers: [],     // CM5 TextMarker objects for other authors' runs
        localUserId: null,     // set later via setCollabUser — needed for the known-authors cache
        // Persistent map of userId → {name, color}. Accumulates every user
        // we've ever seen (via snapshot load, awareness updates, or
        // setCollabUser) so historical characters stay correctly colored
        // even after the author disconnects or is deleted. Seeded from the
        // server snapshot via applyKnownAuthors().
        knownAuthors: new Map()
    };
    editors.set(handle, entry);

    // Per-character authorship layer:
    //
    // 1) After every LOCAL insertion we annotate the inserted range with an
    //    {author: userId} attribute so the attribution travels with the text
    //    through CRDT merges and survives toDelta() re-serialization.
    //
    // 2) On every document change (local OR remote) we rebuild the CM5
    //    TextMarker layer from the attributed delta, colored per-author.
    //
    // Both steps run inside the same Y.Text observer. The author-tagging
    // pass uses a distinct transaction origin ("author-mark") so its own
    // follow-up observe event skips step 1 and only rebuilds markers.
    const AUTHOR_ORIGIN = "author-mark";
    ytext.observe((event, transaction) => {
        // Step 1: attribute newly-inserted local text with the author id.
        const isLocalInsert = transaction.local && transaction.origin !== AUTHOR_ORIGIN;
        if (isLocalInsert && entry.localUserId) {
            const inserts = [];
            let pos = 0;
            for (const op of event.delta || event.changes?.delta || []) {
                if (op.retain != null) {
                    pos += op.retain;
                } else if (op.insert != null) {
                    if (typeof op.insert === "string") {
                        inserts.push({ pos, length: op.insert.length });
                        pos += op.insert.length;
                    }
                }
                // `delete` ops don't advance position in the new state.
            }
            if (inserts.length > 0) {
                doc.transact(() => {
                    for (const ins of inserts) {
                        ytext.format(ins.pos, ins.length, { author: entry.localUserId });
                    }
                }, AUTHOR_ORIGIN);
            }
        }
        // Step 2: rebuild the marker layer (may be called twice per edit in
        // the local case — once for the insert, once for the format — but
        // rebuild is idempotent and cheap for meeting-note sizes).
        rebuildAuthorMarkers(entry);
    });

    // Forward local Yjs updates to the hub. Yjs emits "update" for both
    // local and remote changes — we mark remote applies via the origin
    // parameter so we don't echo them back.
    doc.on("update", (update, origin) => {
        if (origin === "remote")
            return;
        const ref = entry.dotnetRef;
        if (!ref)
            return;
        try {
            ref.invokeMethodAsync("OnJsCollabUpdate", bytesToBase64(update));
        } catch (_) {
            // Component disposed.
        }
    });

    awareness.on("update", (changes, origin) => {
        // A new peer joined (or changed their color) — the marker layer
        // might need to recolor runs for authors who weren't yet in the
        // awareness map when they were emitted. Cheap to run either way.
        rebuildAuthorMarkers(entry);

        if (origin === "remote") {
            // Bootstrap case: a new peer just appeared in our awareness
            // map. In a purely-relay hub the peer doesn't know about us
            // yet (we haven't sent anything since they joined). Two
            // things need to happen to bring the new peer in sync:
            //
            // 1) Re-emit our local awareness state so they pick up our
            //    name + color (otherwise their knownAuthors cache lacks us).
            // 2) Send a full Yjs state-as-update so they get any document
            //    content we've typed since their LoadDocument snapshot —
            //    the snapshot timer may not have fired yet, and the server
            //    doesn't buffer past updates. Yjs CRDTs are idempotent on
            //    update apply, so the new peer just merges it cleanly.
            //
            // Skip entirely for read-only clients: they have nothing to
            // contribute (no local awareness, no edit rights), and trying
            // to push a state update would be rejected by the hub's
            // HasEditPermission check and surface as "save failed" in
            // the UI.
            if (changes.added && changes.added.length > 0 && entry.canEdit) {
                const localState = awareness.getLocalState();
                if (localState) {
                    awareness.setLocalState(localState);  // triggers outbound awareness via the default branch
                }
                // Fire a fresh doc update so late-joining peers catch up.
                // We read our current full state and emit it through the
                // same channel that relays normal edits. Origin is left
                // undefined so the doc.on("update") handler treats it as
                // a local broadcast (NOT "remote").
                const ref = entry.dotnetRef;
                if (ref && entry.doc) {
                    try {
                        const stateUpdate = libs.Y.encodeStateAsUpdate(entry.doc);
                        ref.invokeMethodAsync("OnJsCollabUpdate", bytesToBase64(stateUpdate));
                    } catch (_) {
                        // Ignore — best-effort bootstrap.
                    }
                }
            }
            return;
        }

        const ref = entry.dotnetRef;
        if (!ref)
            return;
        const changed = changes.added.concat(changes.updated).concat(changes.removed);
        if (changed.length === 0)
            return;
        try {
            const bytes = libs.awarenessMod.encodeAwarenessUpdate(awareness, changed);
            ref.invokeMethodAsync("OnJsAwarenessUpdate", bytesToBase64(bytes));
        } catch (_) {
            // Ignore.
        }
    });

    return handle;
}

function applyRemoteUpdate(handle, updateBase64) {
    const entry = editors.get(handle);
    if (!entry || !entry.doc)
        return;
    const bytes = base64ToBytes(updateBase64);
    if (bytes.length === 0)
        return;
    entry.libs.Y.applyUpdate(entry.doc, bytes, "remote");
}

function applyRemoteAwareness(handle, awarenessBase64) {
    const entry = editors.get(handle);
    if (!entry || !entry.awareness)
        return;
    try {
        entry.libs.awarenessMod.applyAwarenessUpdate(
            entry.awareness, base64ToBytes(awarenessBase64), "remote");
    } catch (_) {
        // Malformed payload — ignore.
    }
}

// Build a SaveSnapshot payload: the full Yjs state + current plain text.
// The server persists both.
function getCollabSnapshot(handle) {
    const entry = editors.get(handle);
    if (!entry || !entry.doc)
        return null;
    const state = entry.libs.Y.encodeStateAsUpdate(entry.doc);
    return {
        documentStateBase64: bytesToBase64(state),
        plainText: entry.ytext.toString()
    };
}

function setCollabUser(handle, userId, userName, color) {
    const entry = editors.get(handle);
    if (!entry || !entry.awareness)
        return;
    entry.localUserId = userId;
    // Keep the known-authors cache in sync with the current local user so
    // the local user's own runs get colored correctly on the very first
    // rebuild — even before any awareness round-trip happens.
    entry.knownAuthors.set(userId, { name: userName, color: color });
    entry.awareness.setLocalStateField("user", {
        id: userId,
        name: userName,
        color: color
    });
    rebuildAuthorMarkers(entry);
}

// Seed the known-authors cache from a server-provided map. Used on
// LoadDocument so authors from previous sessions (or deleted users)
// already have their name + color before any rebuild happens.
function applyKnownAuthors(handle, knownAuthors) {
    const entry = editors.get(handle);
    if (!entry || !knownAuthors)
        return;
    for (const [userId, info] of Object.entries(knownAuthors)) {
        if (!entry.knownAuthors.has(userId)) {
            entry.knownAuthors.set(userId, {
                name: (info && info.name) || "",
                color: (info && info.color) || "#1e88e5"
            });
        }
    }
    rebuildAuthorMarkers(entry);
}

// Serialize the current known-authors cache for inclusion in a
// SaveSnapshot payload. Returns a plain object keyed by userId so it
// survives the JSON round-trip to the server.
function getKnownAuthors(handle) {
    const entry = editors.get(handle);
    if (!entry)
        return {};
    const result = {};
    for (const [userId, info] of entry.knownAuthors) {
        result[userId] = { name: info.name, color: info.color };
    }
    return result;
}

// Convert a hex color like "#EE7733" to an rgba() string with the given
// alpha. Used for the subtle background tint that's applied to text
// written by other authors.
function hexToRgba(hex, alpha) {
    if (!hex || hex[0] !== "#" || hex.length !== 7)
        return `rgba(30, 136, 229, ${alpha})`;
    const r = parseInt(hex.slice(1, 3), 16);
    const g = parseInt(hex.slice(3, 5), 16);
    const b = parseInt(hex.slice(5, 7), 16);
    return `rgba(${r}, ${g}, ${b}, ${alpha})`;
}

// Capture any users currently in the awareness map into the entry's
// persistent knownAuthors cache. Called after every awareness change so
// that users' display info survives disconnect — once we've seen a user,
// we remember their name and color even after they leave.
function captureKnownAuthorsFromAwareness(entry) {
    if (!entry || !entry.awareness)
        return;
    for (const [, state] of entry.awareness.getStates()) {
        const u = state?.user;
        if (!u || !u.id)
            continue;
        if (!entry.knownAuthors.has(u.id)) {
            entry.knownAuthors.set(u.id, {
                name: u.name || "",
                color: u.color || "#1e88e5"
            });
        }
    }
}

// Look up a user's display info for the marker layer. Checks the
// persistent cache first (survives disconnects), falls back to null for
// truly-unknown authors (legacy data, author deleted before we ever saw
// them) — the caller renders those with a neutral "Unbekannt" fallback.
function lookupKnownAuthor(entry, userId) {
    if (!entry || !userId)
        return null;
    return entry.knownAuthors.get(userId) || null;
}

// Rebuild the per-character authorship marker layer on a collab editor.
// Walks ytext.toDelta() (runs of text grouped by attributes), emits one
// CM5 TextMarker per attributed run with a subtle background tint and a
// native-tooltip `title`. Everyone — including the local user — sees the
// same thing: every character is colored by its author, so all peers
// share one consistent view. Runs without an author attribute (e.g.,
// legacy Notes seed text imported before Phase 5) are left uncolored.
function rebuildAuthorMarkers(entry) {
    if (!entry || !entry.cm || !entry.ytext)
        return;
    // Clear the previous generation of markers.
    for (const m of entry.authorMarkers) {
        try { m.clear(); } catch (_) {}
    }
    entry.authorMarkers = [];

    // Refresh the persistent cache from any currently-connected peers
    // before walking the delta.
    captureKnownAuthorsFromAwareness(entry);

    const delta = entry.ytext.toDelta();
    const cmDoc = entry.cm.getDoc();
    let index = 0; // absolute char offset in the Y.Text
    for (const op of delta) {
        if (typeof op.insert !== "string")
            continue;
        const length = op.insert.length;
        const author = op.attributes && op.attributes.author;
        // Unattributed seed text stays uncolored.
        if (!author) {
            index += length;
            continue;
        }
        const user = lookupKnownAuthor(entry, author);
        const color = user ? user.color : "#1e88e5";
        const name = user ? user.name : "Unbekannt";
        const from = cmDoc.posFromIndex(index);
        const to = cmDoc.posFromIndex(index + length);
        const marker = cmDoc.markText(from, to, {
            css: "background-color: " + hexToRgba(color, 0.18),
            title: "Geschrieben von " + name,
            inclusiveLeft: false,
            inclusiveRight: true
        });
        entry.authorMarkers.push(marker);
        index += length;
    }
}

// Returns the current list of {clientId, userId, name, color} for every
// peer currently in the document's awareness map, including the local
// user. Used by the presence pills UI. Deduplicated by userId so the same
// user in two tabs shows up once.
function getPresenceList(handle) {
    const entry = editors.get(handle);
    if (!entry || !entry.awareness)
        return [];
    const seen = new Map();
    for (const [clientId, state] of entry.awareness.getStates()) {
        if (!state || !state.user || !state.user.id)
            continue;
        if (!seen.has(state.user.id)) {
            seen.set(state.user.id, {
                clientId,
                userId: state.user.id,
                name: state.user.name || "",
                color: state.user.color || "#1e88e5"
            });
        }
    }
    return Array.from(seen.values());
}

// Register a Blazor callback to be invoked whenever the awareness state
// changes (someone joins, leaves, or updates their user info). The callback
// receives the refreshed presence list from getPresenceList.
function subscribePresence(handle, dotnetRef) {
    const entry = editors.get(handle);
    if (!entry || !entry.awareness)
        return;
    const fire = () => {
        try {
            dotnetRef.invokeMethodAsync("OnJsPresenceChanged", getPresenceList(handle));
        } catch (_) { /* disposed */ }
    };
    entry.awareness.on("change", fire);
    entry._presenceDotnetRef = dotnetRef;
    entry._presenceFire = fire;
    // Fire immediately so the component gets the initial list.
    fire();
}

// Debug/test helper: manually inject a cursor state into local awareness
// to bypass the y-codemirror `document.hasFocus()` guard, which blocks
// auto cursor sync in remote-controlled browser tabs. In real user usage
// the guard passes, so the cursor field appears automatically on cursorActivity.
function _debugSetCursor(handle, fromLine, fromCh, toLine, toCh) {
    const entry = editors.get(handle);
    if (!entry || !entry.awareness || !entry.cm)
        return "no entry";
    const cm = entry.cm;
    const Y = entry.libs.Y;
    const ytext = entry.ytext;
    const anchorIdx = cm.indexFromPos({ line: fromLine, ch: fromCh });
    const headIdx = cm.indexFromPos({ line: toLine, ch: toCh });
    const anchor = Y.createRelativePositionFromTypeIndex(ytext, anchorIdx);
    const head = Y.createRelativePositionFromTypeIndex(ytext, headIdx);
    entry.awareness.setLocalStateField("cursor", {
        anchor: JSON.stringify(anchor),
        head: JSON.stringify(head)
    });
    return "ok";
}

// Debug-only: returns a snapshot of the internal state of a collab editor
// for troubleshooting awareness / cursor sync in Chrome DevTools.
function _debugState(handle) {
    const entry = editors.get(handle);
    if (!entry)
        return { error: "no such handle", keys: Array.from(editors.keys()) };
    const awarenessStates = entry.awareness
        ? Array.from(entry.awareness.getStates().entries()).map(([k, v]) => ({ clientId: k, state: v }))
        : null;
    return {
        hasBinding: !!entry.binding,
        hasAwareness: !!entry.awareness,
        clientId: entry.doc ? entry.doc.clientID : null,
        localAwarenessState: entry.awareness ? entry.awareness.getLocalState() : null,
        allAwarenessStates: awarenessStates
    };
}

function _debugFirstHandle() {
    const keys = Array.from(editors.keys());
    return keys.length > 0 ? keys[0] : null;
}

function _debugDelta(handle) {
    const entry = editors.get(handle);
    if (!entry || !entry.ytext) return null;
    return entry.ytext.toDelta();
}

function _debugRebuild(handle) {
    const entry = editors.get(handle);
    if (!entry) return "no entry";
    rebuildAuthorMarkers(entry);
    return "rebuilt";
}

function _debugYtextInsert(handle, pos, text) {
    const entry = editors.get(handle);
    if (!entry || !entry.ytext) return "no entry";
    entry.ytext.insert(pos, text);
    return entry.ytext.toString().substring(0, 80);
}

function _debugKnownAuthors(handle) {
    const entry = editors.get(handle);
    if (!entry) return null;
    const out = {};
    for (const [k, v] of entry.knownAuthors) out[k] = v;
    return out;
}

window.cmEditor = {
    createEditor,
    setText,
    getText,
    setReadOnly,
    dispose,
    createCollabEditor,
    applyRemoteUpdate,
    applyRemoteAwareness,
    getCollabSnapshot,
    setCollabUser,
    applyKnownAuthors,
    getKnownAuthors,
    getPresenceList,
    subscribePresence,
    _debugState,
    _debugFirstHandle,
    _debugSetCursor,
    _debugDelta,
    _debugRebuild,
    _debugYtextInsert,
    _debugKnownAuthors
};
})();
