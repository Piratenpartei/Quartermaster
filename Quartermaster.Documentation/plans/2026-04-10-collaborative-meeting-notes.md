# Collaborative Meeting Notes

Real-time collaborative editing of agenda item notes during a meeting, plus live meeting page updates (votes, status changes, agenda completions) for all participants.

## Goals (in priority order)

1. **Critical: Collaborative editing of agenda item notes.** Multiple officers can type into the same notes field simultaneously without losing each other's changes. Concurrent edits merge correctly with guaranteed convergence (CRDT).
2. **Critical: Live cursor position sync.** Each user sees where the other users' cursors are inside the editor.
3. **Nice-to-have: Per-character background color.** Each user gets a color and the characters they wrote get a faint tint of that color. Hover on any colored character shows the author's name. *Per-character authorship is a hard requirement — per-line is not acceptable.*
4. **Nice-to-have: Line numbers in the editor.**

Beyond these four, we also want the live meeting page to auto-refresh when:
- Someone casts a vote on an agenda item
- An agenda item gets started/completed/reopened
- The meeting status changes
- Presence is updated

## User decisions (from plan review)

| # | Decision |
|---|---|
| 1 | Vendor all JS dependencies, no npm/build pipeline |
| 2 | Save interval is a configurable option, default **10s** (via the Options admin page) |
| 3 | Keep the markdown preview pane alongside the editor |
| 4 | Subtle background tint + hover tooltip showing the author's display name. No specific palette preference. |
| 5 | Protocol PDF can lag behind live state (reads from the last persisted snapshot) |

**Hard constraint**: per-character authorship tracking. This ruled out the naive OT approach originally proposed — see the architecture discussion below.

## Scope and non-goals

**In scope:**
- Real-time edit propagation between connected meeting participants via SignalR
- CRDT-based merge for concurrent edits (via Yjs)
- Per-character authorship tracking with live rendering (hover tooltips)
- Awareness (presence, cursor positions, user colors)
- Persistence: latest merged document saved to a new `CollabDocuments` table periodically and on the last-user-disconnect
- Reconnection: client that reloads or loses its connection re-syncs from the server-stored snapshot
- Permission gating: only users with `EditMeetings` on the meeting's chapter can edit. Users with `ViewMeetings` can see the live updates and other users' cursors in read-only mode.
- Live page updates for votes/agenda transitions/status changes via the same SignalR hub

**Explicitly out of scope:**
- Multi-document collaboration (each agenda item is its own document)
- Rich text formatting beyond Markdown
- Offline editing (must be online to edit)
- Full-text search of live documents on the server side (PDF-like readers use the periodically-saved plain text)
- Replay / time-travel through edit history (the audit log still records author-attributed save events)
- Mobile-optimized cursor visualizations

## Architecture

### Editor: CodeMirror 6

Switch the agenda item notes editor from a plain `<textarea>` to **CodeMirror 6** wrapped in a Blazor component via JS interop. CodeMirror 6 gives us:

- **Line numbers for free** (Goal #4)
- **Markdown syntax highlighting** via `@codemirror/lang-markdown`
- **Decoration API** for remote cursors and per-character background colors (Goals #2 and #3)
- **Read-only mode** for viewers without edit permissions
- Mature, well-maintained, MIT-licensed, ~50KB minified+gzipped
- Vendored as ESM bundles in `wwwroot/js/codemirror/` (matches existing no-build-step pattern)

### Collaboration: Yjs (CRDT)

Instead of implementing Operational Transforms on the server, we use **Yjs** — a production-grade CRDT library — on the client side. The server is a dumb relay plus periodic snapshot persistence.

**Why Yjs instead of OT**:

The original OT plan was discarded once per-character authorship became a hard requirement. With OT we'd need to maintain a parallel author array and make every server-side transform correct for both text and authors — doubling the bug surface in the most error-prone part of the system. With Yjs, per-character authorship is intrinsic to the data model: every character carries the `clientID` of its inserter, and we map clientID→userId via the awareness protocol.

**Yjs properties relevant to our design:**
- **Mathematically guaranteed convergence**: clients that receive the same set of operations in any order converge to the same state. There's no "merge conflict" concept — conflicts don't exist.
- **Operation-based, not state-based**: small update packets, not full-document transmits
- **Battle-tested**: used by Linear, JupyterLab, Replit, Notion, and many others
- **Built-in awareness API**: ephemeral presence data (cursor positions, user colors) propagated alongside the document
- **Binary wire format**: compact, suitable for high-frequency small updates

**Client-side stack (all vendored)**:
- `yjs` — the CRDT library (~100KB minified+gzipped)
- `y-codemirror.next` — binding to CodeMirror 6 (~10KB)
- `y-protocols` — sync and awareness message encoding (~5KB)
- ESM bundles dropped into `Quartermaster.Blazor/wwwroot/js/yjs/`

Total added JS: ~115KB gzipped on top of CodeMirror's ~50KB = ~165KB for the whole editor feature. Loaded lazily, only on the meeting live page.

### Server architecture: dumb relay + periodic snapshots

The server does NOT run Yjs. It's a pure message relay via SignalR + a periodic persistence mechanism.

**Why relay instead of server-side Y.Doc**:
- Keeps the server code small — no need to port Yjs operations to C#
- No native dependency on a .NET y-crdt binding (the maturity of which I'd need to verify before committing)
- CRDT convergence means any client can produce a canonical snapshot at any time — we don't need server authority over the document state
- The protocol PDF and audit log read from a denormalized `PlainText` column, not the CRDT state, so the server never needs to parse Yjs binary

**How sync works**:
1. Client connects to `MeetingHub` via SignalR, joins the group for the meeting
2. Client requests the initial state for an agenda item: server returns the latest `DocumentState` blob from the DB
3. Client constructs its local Yjs doc from the blob
4. Client sends/receives Yjs sync messages through the hub; the hub broadcasts them to other group members (opaque byte arrays, server doesn't inspect)
5. Awareness messages (cursors, presence) flow through the same relay channel
6. Periodically (every `save_interval_seconds`, default 10), the currently-active clients each try to POST a snapshot via a dedicated hub method; the first one wins and persists, the others become no-ops within that interval

**On the last-client-disconnect**: the server triggers an immediate snapshot request to the leaving client before cleanup, so the final state is captured even if the snapshot timer hasn't fired.

**On server restart**: in-memory presence state is lost (clients reconnect and re-advertise), but document state is preserved in the DB. Worst-case edit loss is bounded by the save interval (≤10s with default config, configurable lower).

### Database schema: `CollabDocuments` table

A new generic table for live-edited documents. Polymorphic on `EntityType` so future live-edited entities can reuse it.

```sql
CREATE TABLE CollabDocuments (
    Id CHAR(36) PRIMARY KEY,                   -- Guid
    EntityType VARCHAR(64) NOT NULL,           -- "AgendaItem" today
    EntityId CHAR(36) NOT NULL,                -- FK target (no DB-level constraint, soft ref)
    DocumentState LONGBLOB NOT NULL,           -- Yjs binary state
    PlainText LONGTEXT NOT NULL,               -- denormalized plain text for PDF/audit/fallback
    ClientUserMap LONGTEXT NOT NULL,           -- JSON: {clientId: userId} for authorship resolution
    LastUpdatedAt DATETIME NOT NULL,
    LastUpdatedByUserId CHAR(36),
    CreatedAt DATETIME NOT NULL,
    UNIQUE KEY ux_entity (EntityType, EntityId),
    INDEX ix_last_updated (LastUpdatedAt)
);
```

**Fields in detail**:
- `DocumentState`: the full Yjs binary snapshot. Clients load this on connect and apply any missing updates from the relay. Typical size: a few KB even for documents with long edit histories because Yjs garbage-collects tombstones.
- `PlainText`: extracted plain text from the Yjs doc at snapshot time. Read by the protocol PDF renderer, audit log, and any future non-collab consumer. Writing this keeps the existing read paths unchanged.
- `ClientUserMap`: JSON dictionary mapping Yjs client IDs to Quartermaster user IDs. Needed because Yjs operations embed a numeric client ID, not the user ID. Built up as users connect. Never shrinks (old entries identify past authors of still-existing characters).
- `LastUpdatedByUserId`: audit convenience — who triggered the most recent snapshot save

**Migration**: new FluentMigrator migration creates the table. No changes to existing tables.

**AgendaItem.Notes relationship**: the existing `AgendaItem.Notes` column becomes a fallback/legacy field. For agenda items that have a `CollabDocuments` row, readers should prefer `CollabDocuments.PlainText`. For backward compatibility during migration, on first collaborative edit of an existing item we'll seed the `CollabDocuments` row with the current `Notes` value. The `Notes` column stays populated (we'll write to both during the transition period for safety), but is deprecated for new reads in the collaborative flow.

### Per-character authorship rendering

Yjs `Y.Text` tracks the originating clientID of every character intrinsically. But we need to attach our own `author` attribute to survive CRDT merges cleanly, so we use the `Y.Text.format(from, to, {author: userId})` API on every local insertion:

```javascript
ytext.applyDelta([
    {retain: cursorPos},
    {insert: typedText, attributes: {author: myUserId}}
]);
```

Reading authorship for rendering:

```javascript
const delta = ytext.toDelta();
// delta is a list of {insert, attributes}
// Each entry is a run of characters with the same attributes (including author)
```

CodeMirror decorations are generated from the delta runs. Each run gets a background-tint decoration keyed by `author`, plus a `title` attribute with the author's display name (from the awareness map) so hover shows the user name.

**Color assignment**: when a user joins a document, the server assigns them the next color from an 8-color palette. The user↔color mapping is part of the awareness state so all clients agree on which color each user has. Palette: 8 colorblind-safe hues from the Tol Vibrant scheme, applied as `rgba(r, g, b, 0.12)` for the subtle tint effect.

**Own writing is not colored**: lines/characters written by the current user themselves get no background tint. Only other users' writing is colored. This keeps the editor visually calm when you're typing in isolation.

### SignalR hub shape

Single `MeetingHub` for all meeting-related real-time messaging:

```csharp
public class MeetingHub : Hub {
    // Lifecycle
    public override Task OnConnectedAsync();       // parse user from auth
    public override Task OnDisconnectedAsync();    // cleanup group memberships

    // Joining a meeting's real-time channel
    public Task JoinMeeting(Guid meetingId);      // permission-checked, adds to group
    public Task LeaveMeeting(Guid meetingId);

    // Collaborative editing (one document per agenda item)
    public Task<byte[]> LoadDocument(Guid agendaItemId);              // returns snapshot + awareness bootstrap
    public Task SendUpdate(Guid agendaItemId, byte[] update);         // opaque Yjs update, broadcast to group
    public Task SendAwareness(Guid agendaItemId, byte[] awareness);   // opaque awareness update, broadcast to group
    public Task SaveSnapshot(Guid agendaItemId, byte[] snapshot,
                              string plainText,
                              Dictionary<uint, Guid> clientUserMap);  // persistence trigger

    // Live page updates (not collaborative — just broadcasts)
    // (Sent by the server to clients; not invoked directly by clients)
    // - "AgendaItemChanged" (itemId, reason)
    // - "MeetingStatusChanged" (meetingId, newStatus)
    // - "PresenceChanged" (meetingId, itemId, userId, isPresent)
}
```

Message broadcasts use `Clients.OthersInGroup(...)` so senders don't echo back.

Permission check on `JoinMeeting`: user must have `ViewMeetings` on the meeting's chapter. Edit mutations (`SendUpdate`) additionally require `EditMeetings`. Viewers can see the live content and other users' cursors but can't send edits.

### Client-side state management in Blazor

New Blazor component: `CollaborativeEditor.razor` + `.razor.cs` in `Components/Inputs/`.

```razor
<div id="@_editorId"></div>

@code { /* in .razor.cs per project convention */ }
```

Parameters:
- `AgendaItemId` — the document to edit
- `HubConnection` — injected `MeetingHubClient` wrapping the SignalR connection
- `ReadOnly` — whether the user has edit permission
- `CurrentUserId` / `CurrentUserName` — for awareness
- `ValueChanged` — fired when the document changes locally (for the markdown preview pane)

The component's code-behind uses JS interop to:
1. Initialize the Yjs doc and CodeMirror editor
2. Connect them via `y-codemirror.next`
3. Wire sync and awareness messages to/from the hub
4. Invoke `LoadDocument` on init, `SaveSnapshot` on the timer and on disposal
5. Clean up on dispose

`MeetingLive.razor` replaces the existing notes editor with `CollaborativeEditor`. The markdown preview pane stays, fed by the `ValueChanged` callback.

## Implementation phases

Each phase is independently shippable and produces visible progress.

### Phase 0: Yjs vendoring prototype

**Goal**: confirm we can vendor `yjs`, `y-codemirror.next`, and `y-protocols` as pre-built ESM bundles and load them into a Blazor WASM page without npm or a build step. **This is a prerequisite for Phase 3; if it fails, we need to rethink the dependency strategy before committing to the rest of the plan.**

**Effort estimate**: 1–2 hours

Steps:
1. Download pre-built ESM bundles from a reputable CDN (candidates: `https://esm.sh/yjs`, `https://cdn.jsdelivr.net/npm/yjs/+esm`, or `https://unpkg.com/yjs`). Pin to a specific version.
2. Place the bundles in a scratch directory (e.g., `Quartermaster.Blazor/wwwroot/js/yjs-proto/`) with a `VERSIONS.md` noting the source URL and version.
3. Create a minimal throwaway test page (or use the browser devtools console on the existing dev server) that:
   - Loads the `yjs` ESM bundle via `<script type="module">` or a dynamic `import()`
   - Instantiates a `Y.Doc`, creates a `Y.Text`, applies an insertion, reads it back
   - Loads `y-protocols/awareness` and instantiates an `Awareness` object
   - Loads `y-codemirror.next` and wires it to a minimal CodeMirror 6 editor (also vendored for this prototype)
4. If any bundle has unresolved `import` statements at runtime, set up an **import map** in `index.html`:
   ```html
   <script type="importmap">
   { "imports": { "yjs": "/js/yjs-proto/yjs.js", "y-protocols/awareness": "/js/yjs-proto/y-protocols-awareness.js" } }
   </script>
   ```
5. Verify it works in Chrome. Verify the total download size of the bundles matches expectations (~115KB gzipped).
6. Document the vendoring recipe in `VERSIONS.md` so Phase 3 can follow it directly.

**Exit criteria**:
- A `Y.Doc` can be instantiated and manipulated from the Blazor wwwroot JS
- `y-codemirror.next` successfully binds a Yjs doc to a CodeMirror editor in a browser page
- Total bundle size is within ~150KB gzipped
- No unresolved imports at runtime

**If this phase fails**: document the failure mode and decide whether to:
1. Switch to a small esbuild/Vite build step (reopens question #1 from the plan intro)
2. Fall back to the original OT plan (loses per-character authorship as a hard requirement)
3. Use a hosted collaboration service (new dependency, likely rejected)

### Phase 1: SignalR foundation + live page updates

**Goal**: basic SignalR hub working; live meeting page auto-updates on votes/status changes. No collaborative editing yet.

**Effort estimate**: 4–6 hours

Steps:
1. Add `Microsoft.AspNetCore.SignalR.Client` package to `Quartermaster.Blazor`. Server already has `Microsoft.AspNetCore.SignalR` via `Microsoft.AspNetCore.App`.
2. Create `Quartermaster.Server/Meetings/MeetingHub.cs` with the lifecycle methods and `JoinMeeting` / `LeaveMeeting`. No collab methods yet.
3. Wire the hub into `Program.cs` middleware: `app.MapHub<MeetingHub>("/hubs/meeting")`.
4. Create `IMeetingNotifier` + `MeetingNotifier` service that existing endpoints call to broadcast change notifications. Inject `IHubContext<MeetingHub>`.
5. Inject `IMeetingNotifier` into:
   - `AgendaItemVoteEndpoint`, `MotionVoteEndpoint` → broadcast `AgendaItemChanged` after vote
   - `MeetingStatusUpdateEndpoint` → broadcast `MeetingStatusChanged`
   - `AgendaItemStartEndpoint`, `AgendaItemCompleteEndpoint`, `AgendaItemReopenEndpoint` → `AgendaItemChanged`
   - `AgendaItemPresenceEndpoint` → `PresenceChanged`
6. Blazor side: create `Quartermaster.Blazor/Services/MeetingHubClient.cs`, a scoped service that wraps `HubConnection` with:
   - Auto-connect on first use
   - Auto-reconnect with exponential backoff (SignalR built-in)
   - Events: `AgendaItemChanged`, `MeetingStatusChanged`, `PresenceChanged`
   - `JoinMeetingAsync(Guid meetingId)` / `LeaveMeetingAsync(Guid meetingId)`
7. Update `MeetingLive.razor.cs` to:
   - Inject `MeetingHubClient`, call `JoinMeetingAsync` on init
   - Subscribe to events; reload affected items when notifications arrive
   - Call `LeaveMeetingAsync` on dispose
8. Add integration test: open two `HubConnection` clients, verify one receives a broadcast after the other triggers an action

**Verification**: open the same live meeting page in two browser windows. Cast a vote in one. The other window's vote table updates within ~1 second. Do the same for agenda-item-complete, meeting-status-change, presence toggle.

### Phase 2: CodeMirror editor wrapper (no collaboration yet)

**Goal**: replace the notes textarea in `MeetingLive.razor` with a CodeMirror 6 editor. Line numbers visible (Goal #4). Still autosaving via the existing REST endpoint.

**Effort estimate**: 5–7 hours

Steps:
1. Download CodeMirror 6 + `@codemirror/lang-markdown` as pre-built ESM bundles. Candidate source: https://codemirror.net/docs/guide/#bundling or use the `@codemirror/basic-setup` bundle from unpkg. Place in `Quartermaster.Blazor/wwwroot/js/codemirror/`.
2. Create `Quartermaster.Blazor/wwwroot/js/codemirror-editor.js` — a small JS module exposing:
   - `createEditor(element, initialText, isReadOnly, dotnetHelper)` → returns editor handle
   - `setText(handle, text)` — replace whole document (used for REST-based saves)
   - `getText(handle)` — read current text
   - `dispose(handle)` — destroy the editor instance
3. Create `Quartermaster.Blazor/Components/Inputs/CodeMirrorEditor.razor` + `.razor.cs`. Implements `IAsyncDisposable`. Parameters: `Value`, `ValueChanged`, `ReadOnly`, `CssClass`.
4. In `MeetingLive.razor`, replace the existing notes `MarkdownEditorWithPreview`'s textarea with `CodeMirrorEditor`, keeping the preview pane fed by the same `ValueChanged`.
5. Test: InProgress meeting, type into an agenda item, switch between items, verify text persists, verify line numbers show, verify markdown preview updates.

**Verification**: notes editor shows line numbers, has markdown syntax highlighting, autosave still works (via the existing `AgendaItemNotesEndpoint`), preview pane updates correctly.

### Phase 3: Yjs integration + collaborative editing

**Goal**: real collaborative editing. Two users type simultaneously; all edits merge; all users converge.

**Effort estimate**: 8–12 hours

This is the meatiest phase, but simpler than the original OT plan because Yjs does the hard part.

Steps:
1. Add vendored JS libs: `yjs`, `y-codemirror.next`, `y-protocols`. Place bundles in `Quartermaster.Blazor/wwwroot/js/yjs/`. Document the vendor source and version in a `VERSIONS.md` in the same directory for future updates.
2. Create migration `MXXX_CollabDocumentsTable.cs` that creates the `CollabDocuments` table. Fold into the existing single migration file if we're still in pre-production (per the CLAUDE.md convention about one migration per release).
3. Create `Quartermaster.Data/Collab/CollabDocument.cs` entity + `CollabDocumentRepository.cs`. Repository methods: `Get(entityType, entityId)`, `Upsert(CollabDocument)`, `Delete(entityType, entityId)`.
4. Add hub methods:
   - `LoadDocument(Guid agendaItemId)` — permission check, return current snapshot bytes + `PlainText` + `ClientUserMap` (or empty state if the row doesn't exist yet; on first load, also seed from `AgendaItem.Notes` if non-empty)
   - `SendUpdate(Guid agendaItemId, byte[] update)` — permission check for edit, broadcast to group via `Clients.OthersInGroup(...).SendAsync("ReceiveUpdate", itemId, update)`
   - `SaveSnapshot(Guid agendaItemId, byte[] snapshot, string plainText, Dictionary<uint, Guid> clientUserMap)` — persist via repo
5. Server-side save throttling: the hub's `SaveSnapshot` is called by any active client on a timer; we accept the first call within a save interval and ignore subsequent ones for that interval. Use an in-memory `ConcurrentDictionary<Guid, DateTime>` for last-save timestamps. Save interval comes from the options system (`meeting.collab.save_interval_seconds`, default 10).
6. Extend `MeetingHubClient` with collab methods: `LoadDocumentAsync`, `SendUpdateAsync`, `OnUpdateReceived` event, etc.
7. Upgrade `CodeMirrorEditor` from Phase 2 to `CollaborativeEditor`:
   - On init: call `LoadDocument`, construct Yjs doc from the returned bytes, attach `y-codemirror.next` binding, start the snapshot timer
   - On local edit: Yjs generates update bytes via `Y.encodeStateAsUpdateV2` (or the observer pattern), we send via hub
   - On `ReceiveUpdate` from hub: apply to local Yjs doc via `Y.applyUpdateV2`
   - On snapshot timer: extract plain text via `ydoc.getText('content').toString()`, serialize Yjs state via `Y.encodeStateAsUpdateV2`, call `SaveSnapshot`
   - On dispose: trigger one final snapshot then detach
8. Add the `meeting.collab.save_interval_seconds` option definition to the options seeder, default 10, visible in the Options admin page
9. Permission gating: `ReadOnly` mode for users with only `ViewMeetings` (they get the editor but can't type; they still receive updates). `SendUpdate` from a read-only user is rejected at the hub level.
10. Tests:
    - Unit tests for `CollabDocumentRepository`
    - Integration test: two `HubConnection` clients construct Yjs docs, apply edits, verify snapshots match
    - Integration test: permission gating (read-only user can load but not send)
11. Migrate existing agenda item Notes: on first `LoadDocument` for an item without a `CollabDocuments` row, seed from `AgendaItem.Notes` into a fresh Yjs doc attributed to a synthetic "system" author

**Verification**: open the same agenda item in two browser windows. Type in both simultaneously at different positions — both edits land. Type in both at the same position — the result is a deterministic merge (Yjs uses clientID for tiebreaking). Close one window, wait 15s, reopen — the latest state is there. Test reconnect: disconnect the network briefly, see the reconnect indicator, reconnect, verify no data loss.

### Phase 4: Cursor and presence sync (Goal #2)

**Goal**: each user sees the cursors of other users in the same agenda item. Names visible on hover. User list shown above the editor.

**Effort estimate**: 3–5 hours

Much of this is free because Yjs already has an awareness API; we just wire it up and render.

Steps:
1. Add `Y.Awareness` to the client setup. Each client sets its local state: `{userId, displayName, color, cursor: {anchor, head}}`.
2. Add hub methods:
   - `SendAwareness(Guid agendaItemId, byte[] awareness)` — broadcast to group
   - Hub listens for `awareness` broadcasts, forwards to others
3. Color assignment: the client asks the hub on load for its assigned color. Server maintains a small per-document color map (`Dictionary<Guid, List<UserColorAssignment>>`) — when a user joins, assign the first unused color from the 8-color palette; on leave, free it. When a user reconnects they get their previous color if still available.
4. CodeMirror decorations: install a small custom extension that reads Yjs awareness state and renders:
   - A vertical bar in the user's color at the cursor position
   - A small label above the bar with the user's first name
   - Labels fade out after 5s of cursor inactivity, bars persist
5. Above the editor: small "presence pills" UI — avatar circles with user initials in their color, listed horizontally

**Verification**: two users in the same agenda item see each other's cursors. Moving the cursor in one window updates the other within ~100ms. Presence pills update when users join/leave.

### Phase 5: Per-character background color (Goal #3)

**Goal**: each character of the document has a faint background tint matching its author's color. Hover on a colored character shows the author's display name.

**Effort estimate**: 3–5 hours

Steps:
1. Update the local edit path to attach the `author` attribute on insertion: `ytext.applyDelta([{retain: pos}, {insert: text, attributes: {author: myUserId}}])`
2. On document load, if the loaded state was seeded from legacy `AgendaItem.Notes`, the seeded text has no author attribute — render it without color (neutral background)
3. Build a CodeMirror decoration plugin that reads `ytext.toDelta()`, walks the runs, and emits decorations:
   - Skip runs where `attributes.author === currentUserId` (don't color own writing)
   - For other authors, emit a mark decoration with inline style `background-color: rgba(r, g, b, 0.12)` and `title="Geschrieben von ${displayName}"`
4. Update the decoration plugin on every Yjs update event so colors stay current
5. Test all concurrent-edit scenarios to make sure the decoration survives merges
6. Edge case: when a user is deleted from the system, their color assignment is gone but their historical writing should still be marked. Store a deleted-user display name in `ClientUserMap` as a fallback

**Verification**: two users type in different paragraphs — each user sees the OTHER's characters highlighted in the other's color, their own are plain. Hovering over colored text shows a tooltip with the author's name. Inserting characters in the middle of someone else's text leaves their characters colored and the new characters uncolored (for the current user) or correctly colored (for the other user looking at it).

### Phase 6: Polish and edge cases

**Effort estimate**: 4–6 hours

- Read-only mode UX: users with only `ViewMeetings` see the editor but can't type. Show a "Schreibgeschützt" badge.
- Reconnection UX: show a "Verbindung wird wiederhergestellt…" banner during disconnects. Yjs handles state resync automatically on reconnect.
- Save indicator: small "Gespeichert" / "Wird gespeichert…" / "Änderungen nicht gespeichert" badge near the editor, tied to the snapshot timer
- Fallback: if the SignalR hub fails to connect at all (e.g., server blocks WebSockets), fall back to the existing REST notes save endpoint with a warning toast
- Performance: batch Yjs updates in a 50ms window before sending (Yjs has a built-in `Doc.transact` helper for this)
- Cleanup: remove the auto-save debounce code in `MeetingLive.razor.cs` for notes (replaced by Yjs persistence)
- Snapshot lifecycle: when a meeting transitions to `Completed` or `Archived`, trigger a final snapshot and mark the `CollabDocuments` row as frozen (no more edits accepted). The document content should be safely captured before the meeting freezes.
- Legacy `AgendaItem.Notes` column: during the transition period, write to both `Notes` and `CollabDocuments.PlainText` on snapshot so existing readers keep working. Document a future migration to drop the `Notes` column once all readers use the new table.
- Update `Quartermaster.Documentation/plans/meeting-system-design.md` to reflect the collaborative editing architecture
- End-to-end test in Chrome: walk through a full multi-user scenario with 3 browser windows

## Total estimate

| Phase | Hours |
|---|---|
| 0: Yjs vendoring prototype | 1–2 |
| 1: SignalR foundation + live page updates | 4–6 |
| 2: CodeMirror editor wrapper | 5–7 |
| 3: Yjs integration + collaborative editing | 8–12 |
| 4: Cursor and presence sync | 3–5 |
| 5: Per-character background color | 3–5 |
| 6: Polish and edge cases | 4–6 |
| **Total** | **28–43 hours** |

Phase 0 is a prerequisite risk-reducer for Phase 3. Phases 1 and 2 (~10h) deliver standalone value and don't depend on Phase 0 succeeding. Phase 3 is the make-or-break point for collaborative editing. Phases 4–6 layer polish on top — each is independently shippable.

## Risks and unknowns

1. **Vendoring Yjs bundles without a build step.** The official `yjs` package is distributed as CommonJS and ESM; we need pre-built ESM bundles we can `<script type="module" src="...">`. Strategy: download pre-built bundles from a CDN (`https://esm.sh/yjs` or similar) and commit them as vendored files. If the bundles have inter-dependencies that don't resolve cleanly, we'd need to add an import map to index.html. **Mitigation**: prototype this in isolation at the start of Phase 3 before sinking time into the rest.

2. **SignalR + Blazor WASM reconnect behavior with in-flight Yjs updates.** On reconnect Yjs will auto-sync via its update protocol, but if the hub lost messages during the disconnect we need the snapshot-load to be authoritative. **Mitigation**: on reconnect, always call `LoadDocument` again and apply the fetched snapshot before continuing with relay.

3. **JS interop performance for high-frequency Yjs updates.** Each keystroke may generate an update byte array that needs to cross the Blazor ⇄ JS boundary. At 100 chars/minute/user, this is modest. At 10 users typing concurrently, it's 1000 updates/minute across the hub = ~16/sec, well within SignalR's capacity. **Mitigation**: batch outbound updates in 50ms windows via `Y.Doc.transact`.

4. **Bundle size and initial load time.** 165KB of vendored JS for the editor is non-trivial on slow connections. **Mitigation**: the meeting live page is the only consumer — the bundles can be loaded lazily on first entry to that page, not on the initial app load.

5. **The "silent bug" class of issues**, as discussed during the DTO mapping pivot. CRDT behavior is hard to test exhaustively. **Mitigation**: rely on Yjs's own test coverage (extensive in the upstream repo). Our integration tests focus on the pieces we control: permission gating, persistence, authorship attribution, and the hub relay.

6. **Permission changes mid-session.** If an officer loses their `EditMeetings` permission while actively editing, the server should reject further `SendUpdate` calls. The client continues to see live updates from others in read-only mode. **Mitigation**: re-check permission on every `SendUpdate`, not just on `JoinMeeting`.

7. **Per-character authorship edge cases**:
   - Copy-paste from elsewhere: all pasted characters are attributed to the pasting user (correct)
   - Find-and-replace across authors: the replacement text is attributed to the replacing user, the original authors' attribution is lost (acceptable — this is a destructive operation)
   - Deleting someone else's text: no one's attribution changes for remaining text (correct)
   - The seeded-from-legacy-Notes content has no author → renders uncolored → correct (no phantom attribution)

## Open questions still to answer during implementation

None at the plan level. The 5 earlier questions have been answered. New questions may arise in Phase 3 as we verify the Yjs vendoring story — document them as they come up and add to this plan.
