# Meeting System — Design & Implementation Plan

**Goal:** Add a Meeting entity that organizes chapter business into an ordered agenda. Each agenda item optionally references a Motion (which is voted on live during the meeting). The system supports minute-taking during the meeting, then generates a protocol document (Markdown/PDF) from the completed agenda.

**Primary user:** Chapter officers running board meetings, mitgliederversammlungen (general assemblies), etc. The system replaces Word/PDF-based agendas and minutes with structured data that stays linked to the motions it resolves.

**Non-goals (for v1):**
- Attendee self-service (members checking in). This is officer-only.
- Live collaboration / simultaneous editing by multiple officers. Single-officer minute-taker is fine.
- Video/streaming integration.
- RSVP / attendance tracking beyond a free-form notes field.

---

## 1. Domain model

### Entities (new)

**`Meeting`** — `Quartermaster.Data/Meetings/Meeting.cs`

| Field | Type | Notes |
|---|---|---|
| `Id` | `Guid` PK | |
| `ChapterId` | `Guid` | FK to Chapters; the meeting "belongs to" a single chapter |
| `Title` | `string` | e.g. "Vorstandssitzung März 2026" |
| `MeetingDate` | `DateTime?` | scheduled date-time (nullable during drafting) |
| `Location` | `string?` | free-text: physical room OR video URL OR hybrid |
| `Description` | `string?` | markdown; preamble rendered at top of protocol |
| `Status` | `MeetingStatus` enum | Draft / Scheduled / InProgress / Completed / Archived |
| `Visibility` | `MeetingVisibility` enum | Public / Private (default Private) |
| `StartedAt` | `DateTime?` | set when Status → InProgress |
| `CompletedAt` | `DateTime?` | set when Status → Completed |
| `ArchivedPdfPath` | `string?` | relative path to the PDF snapshot written at archive time (see §7) |
| `CreatedAt` | `DateTime` | |
| `DeletedAt` | `DateTime?` | soft-delete (same pattern as Event/Motion) |

**`MeetingVisibility` enum:**
- `Public = 0` — anyone (including anonymous) can read the meeting + agenda + protocol
- `Private = 1` — only **officers or delegates of the meeting's exact chapter** can read it

Note: Private visibility is stricter than the general `meetings_view` permission. Even a user who would normally inherit `meetings_view` from a parent chapter cannot see a private meeting of a child chapter — they must hold a direct officer-or-delegate role assignment on `meeting.ChapterId` itself. See §8 for the access-control rule.

**`AgendaItem`** — `Quartermaster.Data/Meetings/AgendaItem.cs`

| Field | Type | Notes |
|---|---|---|
| `Id` | `Guid` PK | |
| `MeetingId` | `Guid` FK | cascade delete with meeting |
| `ParentId` | `Guid?` FK | self-referencing; null = root item; non-null = subitem under parent |
| `SortOrder` | `int` | position among siblings (scoped within parent), reorderable |
| `Title` | `string` | e.g. "TOP 3 — Beschluss Mitgliedsbeiträge 2026" |
| `ItemType` | `AgendaItemType` enum | Discussion / Motion / Protocol / Break / Information |
| `MotionId` | `Guid?` FK | nullable; required when `ItemType == Motion` |
| `Notes` | `string?` | markdown; minute-taker writes during meeting |
| `Resolution` | `string?` | markdown; summarizes outcome ("Beschlossen: ...") |
| `StartedAt` | `DateTime?` | set when item becomes active during meeting |
| `CompletedAt` | `DateTime?` | set when item marked done |

**Hierarchy rules:**
- Items form a tree per meeting: root items have `ParentId = null`, subitems point at a parent in the same meeting.
- `SortOrder` is scoped to siblings (all items sharing the same `ParentId` within a meeting).
- Hierarchical numbering ("1 / 1.1 / 1.2 / 2 / 2.1") is computed **client-side** from the tree structure — not stored.
- Validators enforce: parent (if set) must belong to the same meeting, no cycles, max depth 3 (TOP → Unterpunkt → Detailpunkt is enough for board meetings).
- Moving items between parents uses a dedicated "Move" endpoint (see §4). Reorder within siblings uses the swap pattern.
- On parent deletion: children are also deleted (cascade via DB FK).

**`AgendaItemType` enum:**
- `Discussion = 0` — generic discussion point, no vote
- `Motion = 1` — discuss + vote on a Motion; `MotionId` FK is required
- `Protocol = 2` — approving the previous meeting's protocol
- `Break = 3` — scheduled break
- `Information = 4` — announcements / reports, no decision

### Changes to existing entities

**`MotionVote`** — add `MeetingId` nullable FK.

| Field | Type | Notes |
|---|---|---|
| `MeetingId` | `Guid?` FK | set when vote was cast during a meeting; null for async votes |

Rationale: lets us show "this motion was resolved at Meeting X on date Y" and lets the meeting protocol include the exact vote tally from that session. Does not break anything because existing votes were async (null MeetingId).

### Why not combine `Event` + `Meeting` into one entity?

Considered and rejected. Events and meetings differ in purpose:
- Events are outward-facing (public name, visibility levels, recipients, checklists driving actions like "send invite email")
- Meetings are inward-facing governance work (ordered deliberative agenda, tied to motions, produces a protocol)

Their lifecycles look superficially similar (Draft → Active → Completed → Archived) but the state transitions and UI affordances differ enough that forcing them into a single entity would bloat both. Sharing a `LifecycleStatus` base concept isn't worth the coupling.

---

## 2. State machines

### Meeting status

```
Draft ──→ Scheduled ──→ InProgress ──→ Completed ──→ Archived
  ↑          │              │             │
  └──────────┘              │             │
             └──── (forced abort) ────────┘
```

- **Draft:** being built, agenda editable, no date required
- **Scheduled:** agenda frozen for attendees (officers can still edit), `MeetingDate` required
- **InProgress:** minute-taking mode active, votes on motion items feed back into Motion records
- **Completed:** read-only, protocol exportable, no further votes accepted
- **Archived:** long-term history, hidden from default list views

Allowed transitions (mirror `EventStatusUpdateEndpoint` style):
- `Draft ↔ Scheduled`
- `Scheduled → InProgress` (can't go back once started)
- `InProgress → Completed`
- `Completed ↔ Archived`
- Abort: `Scheduled → Draft` allowed; `InProgress → Draft/Scheduled` is a one-way cancellation requiring admin perm

**Side effects on Completed transition:**
- Auto-resolve all un-resolved motions linked via agenda items: for each `AgendaItem` where `ItemType == Motion && MotionId != null` and the referenced motion has `ResolvedAt == null`, tally current votes and set motion's `ApprovalStatus` + `ResolvedAt`. Agenda item's `Resolution` field is also auto-populated from the tally (if empty). This replaces the manual "close-vote" step for any agenda items the minute-taker forgot to close explicitly.

**Side effects on Archived transition:**
- Generate the final PDF snapshot and write it to disk (see §7). Path stored in `Meeting.ArchivedPdfPath`. Meeting becomes fully immutable after this point.

### Agenda item progress (within an InProgress meeting)

```
NotStarted ──→ InProgress ──→ Completed
```

- Derived from timestamps, no separate enum: item is `NotStarted` if `StartedAt == null`, `InProgress` if `StartedAt != null && CompletedAt == null`, `Completed` if both set
- Only one item should be "in progress" at a time per meeting (enforced by UI + endpoint); the "Start next item" action auto-completes the previous one

### Motion vote lifecycle interaction

When an agenda item of type `Motion` is `InProgress`:
- Officers vote via the existing motion-vote endpoint, but requests include `MeetingId`
- The existing `TryAutoResolve` logic runs after each vote — but during a meeting, resolution is usually a human decision after discussion. We'll add a "Close vote" action on the agenda item that:
  - Tallies current votes for this motion
  - Sets `Motion.ApprovalStatus` based on majority (or explicitly chosen outcome)
  - Sets `Motion.ResolvedAt = now`
  - Populates `AgendaItem.Resolution` with auto-generated text ("Angenommen mit X Ja / Y Nein / Z Enthaltungen") that the minute-taker can edit

---

## 3. Database migration

**`M003_MeetingSystem.cs`** (M002 is the most recent migration; M003 is the next slot):

1. `Create.Table("Meetings")` with columns above (including `Visibility` + `ArchivedPdfPath`) + indexes on `ChapterId`, `Status`, `MeetingDate`
2. FK `Meetings.ChapterId` → `Chapters.Id` (RESTRICT)
3. `Create.Table("AgendaItems")` with columns above + index on `MeetingId, ParentId, SortOrder`
4. FK `AgendaItems.MeetingId` → `Meetings.Id` (CASCADE)
5. FK `AgendaItems.ParentId` → `AgendaItems.Id` (CASCADE — deleting a parent deletes its subtree)
6. FK `AgendaItems.MotionId` → `Motions.Id` (SET NULL — motion may be deleted; agenda item keeps its notes but loses the link)
7. `Alter.Table("MotionVotes").AddColumn("MeetingId")` nullable Guid
8. FK `MotionVotes.MeetingId` → `Meetings.Id` (SET NULL — if meeting is deleted, vote record is preserved)
9. Seed permissions: `meetings_view`, `meetings_create`, `meetings_edit`, `meetings_delete` (chapter-scoped)
10. Seed new system role: `general_chapter_delegate` (identifier `GeneralChapterDelegate`). Same `DefaultOfficerPermissions` set seeded as role permissions. Role is marked `IsSystem = true` so it can't be edited/deleted via the UI.
11. Add column `Role.InheritsToChildren` (bool, default `true`). Set to `false` for the `general_chapter_delegate` seeded role, `true` for `chapter_officer`. Controls whether permissions granted via this role are inherited down the chapter tree (see §8).
12. Extend `PermissionIdentifier.DefaultOfficerPermissions` with `meetings_view`, `meetings_edit` (but NOT `meetings_delete` — scope that to senior officers). Both the officer and delegate system roles get these.

Migration notes:
- `Role.InheritsToChildren` defaults to `true`, preserving existing behavior for custom roles; seeded system roles get explicit values (officer=true, delegate=false).
- Existing motions and votes are untouched; `MotionVote.MeetingId` defaults to null.

---

## 4. API surface

All endpoints under `Quartermaster.Server/Meetings/`. Standard auth + CSRF. Permission names from step 3.

### Meeting CRUD
- `GET /api/meetings` → `MeetingListResponse` — paginated, filter by `ChapterId`, `Status`, date range; chapter-scoped via `ViewMeetings`
- `GET /api/meetings/{Id}` → `MeetingDetailDTO` (includes full agenda) — chapter-scoped via `ViewMeetings`
- `POST /api/meetings` → creates Draft meeting — `CreateMeetings`
- `PUT /api/meetings/{Id}` → updates Title/Location/Description/MeetingDate — `EditMeetings`
- `DELETE /api/meetings/{Id}` → soft-delete — `DeleteMeetings`
- `PUT /api/meetings/{Id}/status` → transition matrix — `EditMeetings` (archive uses `DeleteMeetings`, mirror events pattern)

### Agenda items
- `POST /api/meetings/{MeetingId}/agenda` — append new agenda item; body includes optional `ParentId` for subitems
- `PUT /api/meetings/{MeetingId}/agenda/{ItemId}` — update title/type/motion-link/notes
- `DELETE /api/meetings/{MeetingId}/agenda/{ItemId}` — hard-delete if meeting is Draft/Scheduled (cascades to children), block if Completed+
- `POST /api/meetings/{MeetingId}/agenda/{ItemId}/reorder` — body `{ Direction: -1|+1 }`; reorders within siblings (same ParentId) only
- `POST /api/meetings/{MeetingId}/agenda/{ItemId}/move` — body `{ NewParentId: Guid? }`; reparents an item (e.g. promote subitem to root, or nest under different parent). Validates no-cycle, same-meeting, depth-limit.
- `POST /api/meetings/{MeetingId}/agenda/{ItemId}/start` — sets `StartedAt`, auto-completes any previous in-progress item
- `POST /api/meetings/{MeetingId}/agenda/{ItemId}/complete` — sets `CompletedAt`, freezes Notes/Resolution
- `PUT /api/meetings/{MeetingId}/agenda/{ItemId}/notes` — live-update notes during meeting (separate from full update to allow faster auth path)

### Voting (extends existing motion-vote flow)
- `POST /api/meetings/{MeetingId}/agenda/{ItemId}/vote` — cast vote on the agenda item's linked motion; body `{ UserId, Vote }`. Requires the item to be `InProgress` AND the authenticated user to hold `VoteMotions` on the meeting's chapter. Records vote with `MeetingId` set. Delegation rules identical to `MotionVoteEndpoint`.
- `POST /api/meetings/{MeetingId}/agenda/{ItemId}/close-vote` — closes voting, sets motion status from tally, auto-fills agenda item Resolution (editable). Requires `EditMeetings`.

### Protocol export
- `GET /api/meetings/{Id}/protocol?format=md` → markdown text (on-demand regenerated from current data)
- `GET /api/meetings/{Id}/protocol?format=html` → HTML preview (markdown → HTML via `MarkdownService.ToHtml` Standard profile)
- `GET /api/meetings/{Id}/protocol?format=pdf` → PDF binary
  - If meeting is Archived → stream the frozen snapshot from `Meeting.ArchivedPdfPath` (immutable, always consistent with whoever saw it last).
  - Otherwise → generate on-the-fly via QuestPDF from current data.

Only meetings in status Completed or Archived expose the export endpoint by default. For InProgress meetings, officers can request `?draft=true` to get a live snapshot (useful for minute-taker review mid-meeting). Draft requests for md/html are always live; draft pdf always regenerates.

**Archive-time snapshot:** When a meeting transitions to Archived, the server generates the PDF once via QuestPDF and writes it to disk at `{configured_protocol_dir}/{YYYY}/{meeting_id}.pdf`. The path is stored on the meeting row. This guarantees every archived meeting has an immutable, authoritative PDF regardless of later schema changes.

---

## 5. DTOs

All under `Quartermaster.Api/Meetings/`.

- `MeetingDTO` — Id, ChapterId, ChapterName, Title, MeetingDate, Status, Visibility, Location, AgendaItemCount
- `MeetingDetailDTO` — MeetingDTO fields + Description, StartedAt, CompletedAt, ArchivedPdfPath, AgendaItems list (flat list, frontend reconstructs tree from ParentId)
- `AgendaItemDTO` — Id, ParentId, SortOrder, Title, ItemType, MotionId, MotionTitle (lookup), Notes, Resolution, StartedAt, CompletedAt, (for Motion items: MotionApprovalStatus, vote counts inline)
- `MeetingCreateRequest` — ChapterId, Title, Visibility, MeetingDate?, Location?, Description?
- `MeetingUpdateRequest` — as above minus ChapterId (immutable after creation)
- `MeetingStatusUpdateRequest` — Id, Status
- `MeetingListRequest : IPaginatedRequest` — Page, PageSize, ChapterId?, Status?, Visibility?, DateFrom?, DateTo?
- `AgendaItemCreateRequest` — MeetingId, ParentId?, Title, ItemType, MotionId?
- `AgendaItemUpdateRequest` — MeetingId, ItemId, Title?, ItemType?, MotionId?, Notes?, Resolution?
- `AgendaItemNotesRequest` — Notes (just the field, for fast partial updates during meeting)
- `AgendaItemMoveRequest` — MeetingId, ItemId, NewParentId?
- `AgendaItemVoteRequest` — MeetingId, ItemId, UserId, Vote (reuses existing `VoteType` enum)
- `ProtocolExportResponse` — Content (text), Format ("md"|"html")

### Validators
One validator per request under `Quartermaster.Server/Meetings/Validators/`:
- Title max length 200, required
- Location max 500, optional
- Description max 10000, markdown-sanitized server-side (Standard profile — same as events)
- ItemType enum range check
- Visibility enum range check
- When `ItemType == Motion`, `MotionId` required AND must belong to the same chapter as the meeting
- MeetingDate not-in-distant-past when transitioning to Scheduled
- **ParentId validation** (agenda items): parent must belong to same meeting, no cycles, max depth 3

---

## 6. UI

### Navigation
- Add "Sitzungen" menu item under Vorstandsarbeit dropdown, gated on `meetings_view` permission.

### Pages

**`/Administration/Meetings` — Meeting list** (`MeetingList.razor` + `.razor.cs`)
- Status filter tabs: All / Active (Draft+Scheduled+InProgress) / Completed / Archived
- Chapter filter (`ChapterPicker`) for users with access to multiple chapters
- Date range filter
- Table: Title | Chapter | Date | Status badge | Actions
- "Neue Sitzung" button (perm-gated on `meetings_create`)
- Pagination via existing `Pagination` component

**`/Administration/Meetings/Create` — New meeting form** (`MeetingCreate.razor`)
- Simple form: Chapter (picker), Title, Date/time, Location, **Visibility** (radio: Öffentlich / Privat), Description (MarkdownEditor)
- Default Visibility = Private for safety
- "Erstellen und Agenda bearbeiten" → creates then navigates to detail page

**`/Administration/Meetings/{Id}` — Meeting detail** (`MeetingDetail.razor`)
- Header with title, chapter, date/location, status badge, **visibility badge** (Öffentlich/Privat), transition buttons
- Tabs: **Agenda** (primary) / **Protokoll** (read-only rendered view) / **Audit-Log**
- Agenda tab:
  - Items rendered as a **tree** (subitems indented under their parents). Client computes hierarchical numbering (1 / 1.1 / 1.2 / 2 / ...)
  - Draft/Scheduled: full editor (add/edit/delete/reorder items, add subitems, motion picker, title/type/notes, move-between-parents dropdown)
  - InProgress: live minute-taking mode (see below)
  - Completed/Archived: read-only list
  - When Archived: additional "PDF-Snapshot herunterladen" button streaming `Meeting.ArchivedPdfPath`

**Live minute-taking mode** (within MeetingDetail when InProgress):
- Left rail: agenda list with progress indicators (○ not started / ⏵ in progress / ✓ complete)
- Center panel: currently-active item
  - Large title + "Jetzt starten" button for next item
  - Notes textarea (markdown editor) that auto-saves every ~3s to `/notes` endpoint
  - For Motion items: embedded vote panel reusing existing `MotionVoteButtons` component pattern + list of officer vote status + "Abstimmung beenden" button
  - "TOP abschließen" button → marks complete, advances to next item

**Agenda editor components** (reusable, under `Components/`):
- `AgendaItemEditor.razor` — renders one item in edit mode: title input, type selector, conditional motion picker (new component, see below), + "Unterpunkt hinzufügen" button
- `AgendaItemTree.razor` — recursive component that renders an agenda tree level with nested children indented
- `MotionPicker.razor` — searchable dropdown of open motions from the same chapter; also offers "+ Neuen Antrag anlegen" opening a modal with the motion create form inline
- `AgendaProgressRail.razor` — flattened tree walk with hierarchical numbering and progress icons

**`/Administration/Meetings/{Id}/Protocol` — Protocol view** (`MeetingProtocol.razor`)
- Rendered protocol (Markdown → HTML via existing `MarkdownService.ToHtml` with Standard profile)
- "Als Markdown herunterladen" button → hits `?format=md`
- "Als PDF herunterladen" button → hits `?format=pdf`

---

## 7. Protocol generation

**Template (markdown):**

```
# {Meeting.Title}

**Gliederung:** {Chapter.Name}
**Datum:** {MeetingDate formatted}
**Ort:** {Location}
**Beginn:** {StartedAt time}
**Ende:** {CompletedAt time}

## Teilnehmer (Vorstand)
{auto-generated list of officers who cast votes during the meeting}

## {Meeting.Description if present}

---

## Tagesordnung

### TOP 1 — {AgendaItem.Title}
*({ItemType label})*

{AgendaItem.Notes}

{if Motion item: include motion title + motion text + vote tally + resolution}

**Beschluss:** {AgendaItem.Resolution}

---

### TOP 2 — ...
```

**Markdown generation:** `Quartermaster.Server/Meetings/ProtocolRenderer.cs`
- Pure C# — takes `MeetingDetailDTO` + related chapter/motion data → returns markdown string
- Uses Fluid templates (already in the codebase for email) for the template string, so formatting is controlled by an Option setting and can be customized per-chapter
- Walks the agenda tree recursively (children rendered under their parents with indented numbering)
- Option: `meetings.protocol.template_md` (global, chapter-override supported later)

**HTML preview:** markdown → `MarkdownService.ToHtml(rendered, SanitizationProfile.Standard)`.

**PDF generation:** **QuestPDF** (chosen — more predictable output than Playwright-print and no browser-process overhead).
- Service: `Quartermaster.Server/Meetings/ProtocolPdfRenderer.cs`
- Builds a `Document` fluently from `MeetingDetailDTO`: title page, metadata block, agenda tree with nested sections, per-motion vote tables
- Runs in-process, no external dependencies at runtime
- License: QuestPDF Community (MIT) is free for companies under €1M revenue — add a LICENSE note in the project README so future maintainers are aware
- Add `QuestPDF` NuGet package to `Quartermaster.Server.csproj`; call `QuestPDF.Settings.License = LicenseType.Community;` in `Program.ConfigureServices`

**Archive-time snapshot:**
- On the Completed→Archived status transition, the endpoint calls `ProtocolPdfRenderer` and writes the output to `{protocols_dir}/{year}/{meeting_id}.pdf`
- `protocols_dir` is a new Option: `meetings.protocol.archive_dir` (default `./data/protocols`)
- The path (relative to `protocols_dir`) is stored in `Meeting.ArchivedPdfPath`
- If the write fails, the status transition is rolled back and an error returned
- Re-archiving (Archived→Completed→Archived round-trip) regenerates the snapshot

---

## 8. Permissions

### New permission identifiers (`PermissionIdentifier.cs`)

```csharp
// Chapter-scoped:
public static readonly string ViewMeetings = "meetings_view";
public static readonly string CreateMeetings = "meetings_create";
public static readonly string EditMeetings = "meetings_edit";
public static readonly string DeleteMeetings = "meetings_delete";
```

Add `ViewMeetings`, `CreateMeetings`, `EditMeetings` to `DefaultOfficerPermissions` (so new officers can manage meetings for their chapter). Leave `DeleteMeetings` opt-in.

Update `BoardWorkPermissions` nav grouping to include `ViewMeetings`.

### Endpoint checks

The access rule differs by meeting **Visibility**:

**Public meetings:**
- List + Detail + Protocol: anonymous access allowed. (The list endpoint auto-filters to Public for anonymous callers.)

**Private meetings:**
- List + Detail + Protocol: require the user to hold a **direct** `chapter_officer` or `general_chapter_delegate` role assignment on `meeting.ChapterId` — no inheritance. Even if the user would normally inherit `meetings_view` from a parent chapter, private meetings stay hidden unless they're specifically an officer/delegate of that exact chapter.

**Mutating endpoints (regardless of visibility):**
- Create: `CreateMeetings` on the target chapter
- Update / Status-transition (Draft↔Scheduled, Scheduled→InProgress, InProgress→Completed): `EditMeetings` on the meeting's chapter
- Delete + Archive transitions: `DeleteMeetings` on the meeting's chapter
- Vote during meeting: `VoteMotions` (reuses motion permission) + meeting must be InProgress + user must be able to view the meeting
- Close vote + write resolution: `EditMeetings`

**Implementation helper:** add `MeetingAccessHelper.CanUserViewMeeting(userId, meeting, roleRepo)` that encapsulates the Public/Private branch. For Private, it calls `RoleRepository.UserHasDirectRoleAssignment(userId, meeting.ChapterId, {ChapterOfficer, GeneralChapterDelegate})`.

### The `InheritsToChildren` role flag

Current behavior: when a user holds `ViewMotions` (or any `_view` permission) on Chapter A via the `chapter_officer` role, they also implicitly get it on Chapter A's descendants via `HasPermissionWithInheritance` walking the ancestor chain.

New behavior: inheritance only applies to permissions granted via roles where `InheritsToChildren = true`.

- `chapter_officer` → `InheritsToChildren = true` (current behavior preserved)
- `general_chapter_delegate` → `InheritsToChildren = false` (delegate of Chapter A gets permissions on A only, not on children)
- Custom user-created roles → default `true` for backwards compat

Implementation lives in `UserChapterPermissionRepository`:
- Split `GetChapterPermissionsViaRoles(userId, chapterId)` into two paths internally:
  - **Direct** lookup (all role assignments to `chapterId` regardless of `InheritsToChildren`) — used for exact-chapter checks
  - **Inheritable** lookup (only role assignments where the role has `InheritsToChildren=true`) — used by `HasPermissionWithInheritance` when walking ancestors
- Document the distinction in a code comment near the method, with a reference to this plan

---

## 9. Auditing

All entity mutations go through `AuditLogRepository` same as other entities:
- Meeting create / update / status-change / delete → field-level diffs
- AgendaItem create / update / delete / reorder → field-level diffs
- MotionVote with MeetingId → already logged via existing MotionVote audit, add MeetingId to logged fields

Meeting detail page gets an "Audit-Log" tab (same pattern as EventDetail / MemberDetail).

---

## 10. Test plan

### Unit tests (`Quartermaster.Server.Tests/Meetings/`)
- `MeetingRepositoryTests`: CRUD, chapter filtering, status filtering, visibility filtering, soft-delete
- `AgendaItemRepositoryTests`: insert-with-SortOrder, reorder, tree operations (create-subitem, move-between-parents, cycle-rejection, depth-limit), cascade-on-meeting-delete, cascade-on-parent-delete, nullify-on-motion-delete
- `ProtocolRendererTests`: template rendering with various agenda shapes (empty, all-discussion, nested subitems at depth 3, with motions + vote tallies, with resolutions, unicode content, long descriptions)
- `ProtocolPdfRendererTests`: PDF generation produces non-empty bytes, correct metadata (title, meeting date), contains all agenda item titles, handles nested items
- `MeetingStatusTransitionTests`: valid transition matrix, reject invalid transitions, timestamp side-effects
- `MeetingCompleteSideEffectsTests`: auto-resolve un-resolved motions on Completed transition, only resolves motions linked via this meeting's agenda, resolves with vote tally, skips already-resolved motions
- `MeetingArchiveSideEffectsTests`: PDF written to disk at expected path, `ArchivedPdfPath` set on meeting, re-archive regenerates, rollback on write failure
- `MeetingAccessHelperTests`: Public→allow all; Private→officer direct match allows, delegate direct match allows, parent-chapter officer denied, unrelated user denied, anonymous denied
- `RoleInheritanceFlagTests`: permissions from `InheritsToChildren=false` role don't propagate via `HasPermissionWithInheritance`; permissions from `InheritsToChildren=true` do

### Integration tests (`Quartermaster.Server.Tests/Integration/Meetings/`)
One test class per endpoint, 4–6 tests each (matching existing endpoint test pattern):
- `MeetingListEndpointTests`, `MeetingDetailEndpointTests`, `MeetingCreateEndpointTests`, `MeetingUpdateEndpointTests`, `MeetingDeleteEndpointTests`, `MeetingStatusUpdateEndpointTests`
- `AgendaItemAddEndpointTests`, `AgendaItemUpdateEndpointTests`, `AgendaItemDeleteEndpointTests`, `AgendaItemReorderEndpointTests`, `AgendaItemStartEndpointTests`, `AgendaItemCompleteEndpointTests`, `AgendaItemNotesEndpointTests`
- `AgendaItemVoteEndpointTests`, `AgendaItemCloseVoteEndpointTests`
- `MeetingProtocolEndpointTests` — exports each format, checks content, permissions, draft-mode

Behaviors to pin down specifically:
- Agenda items of type Motion MUST have a MotionId
- MotionId must reference a motion in the same chapter as the meeting
- Starting a new agenda item auto-completes the previous in-progress one
- Votes cast with MeetingId get persisted with that FK
- Closing a vote sets Motion.ApprovalStatus and .ResolvedAt
- Completed meetings reject further mutations
- Transitioning to Completed auto-resolves un-resolved motions linked via agenda items
- Transitioning to Archived writes a PDF to disk and stores the path
- Protocol endpoint returns 404 for Draft meetings (or 400 "not ready")
- **Visibility enforcement:** private meeting detail returns 404 to user with `meetings_view` inherited from parent chapter but no direct officer/delegate role on meeting's chapter
- **Visibility enforcement:** private meeting returns 200 to direct officer OR direct delegate
- **Visibility enforcement:** public meeting returns 200 to anonymous
- **Hierarchy:** POST agenda with ParentId in different meeting → 400
- **Hierarchy:** Move request creating a cycle → 400
- **Hierarchy:** Move request exceeding max depth → 400
- **Hierarchy:** Deleting parent cascades to children

### E2E tests (`Quartermaster.Server.Tests/E2E/MeetingE2ETests.cs`)
One end-to-end flow test:
1. Officer creates meeting
2. Adds 3 agenda items (one Discussion, one Motion linked to existing motion, one Information)
3. Transitions Draft → Scheduled → InProgress
4. Starts first item, types notes, completes it
5. Starts Motion item, votes Approve, closes vote
6. Verifies Motion.ApprovalStatus is Approved and ResolvedAt set
7. Completes meeting, opens Protocol tab, asserts content contains agenda items + motion tally

---

## 11. Implementation phases

Sequential; each phase ends at a point where the system is demoable.

### Phase 0 — Role inheritance flag + delegate role
(prerequisite; touches existing auth code)
- [ ] Add `Role.InheritsToChildren` column (migration step included in M003 below)
- [ ] Update `UserChapterPermissionRepository` to distinguish direct vs inheritable role lookups
- [ ] Seed `general_chapter_delegate` system role
- [ ] Add `PermissionIdentifier.SystemRole.GeneralChapterDelegate` constant
- [ ] `RoleInheritanceFlagTests` + update existing permission-inheritance tests to cover both role types

### Phase 1 — Core model + CRUD (no UI)
- [ ] M003 migration: Meeting, AgendaItem tables + MotionVote.MeetingId column + Role.InheritsToChildren column + new permissions + delegate system role
- [ ] Entities: `Meeting.cs`, `AgendaItem.cs`, `MeetingStatus` enum, `MeetingVisibility` enum, `AgendaItemType` enum
- [ ] Repositories: `MeetingRepository` (CRUD + list with visibility filter + status change + auto-resolve-on-complete + pdf-snapshot-on-archive), `AgendaItemRepository` (CRUD + reorder + move + complete/start timestamps + tree traversal)
- [ ] Tree helpers: `AgendaItemRepository.GetChildren`, `GetDescendants`, `GetDepth`, `WouldCreateCycle`
- [ ] DTOs under `Quartermaster.Api/Meetings/`
- [ ] Mappers (Mapperly for simple cases, manual for MeetingDetailDTO which joins agenda items)
- [ ] Permissions seeded, `DefaultOfficerPermissions` extended (shared by officer + delegate roles)
- [ ] `MeetingAccessHelper` for Public/Private visibility check
- [ ] Unit tests: `MeetingRepositoryTests`, `AgendaItemRepositoryTests`, `MeetingAccessHelperTests`

### Phase 2 — Meeting endpoints + integration tests
- [ ] Meeting CRUD endpoints (6 endpoints)
- [ ] Status transition endpoint with matrix validation
- [ ] Validators for meeting request DTOs
- [ ] Integration tests for all meeting endpoints

### Phase 3 — Agenda endpoints + integration tests
- [ ] Agenda CRUD + reorder + move (5 endpoints)
- [ ] Start/complete/notes endpoints (3 endpoints)
- [ ] Validators (enforce Motion FK requirements, chapter match, parent cycle/depth/same-meeting checks)
- [ ] Integration tests for all agenda endpoints (including hierarchy validation cases)

### Phase 4 — Voting integration + auto-resolve
- [ ] `AgendaItemVoteEndpoint` — thin wrapper that calls existing motion-vote logic with MeetingId set
- [ ] `AgendaItemCloseVoteEndpoint` — tally + update motion + auto-fill agenda item resolution
- [ ] `MotionRepository.CloseVoteWithTally` method
- [ ] Auto-resolve-on-Completed: `MeetingRepository.AutoResolveLinkedMotions(meetingId)` called from status transition
- [ ] Disable `TryAutoResolve` for votes cast with non-null `MeetingId` (meetings always require explicit close or the on-complete sweep)
- [ ] Integration tests for vote + close-vote + auto-resolve-on-complete flows

### Phase 5 — Protocol generation (QuestPDF + archive snapshot)
- [ ] Add `QuestPDF` package to `Quartermaster.Server.csproj`; set `LicenseType.Community` in startup
- [ ] `ProtocolRenderer` service (Fluid template → markdown, walks agenda tree recursively)
- [ ] `ProtocolPdfRenderer` service (QuestPDF Document → PDF bytes)
- [ ] Protocol endpoint with format switch (md / html / pdf, `draft=true` for live-regenerate)
- [ ] Archive-time snapshot: write PDF to `{meetings.protocol.archive_dir}/{year}/{meeting_id}.pdf` on Completed→Archived transition
- [ ] Option entries: `meetings.protocol.template_md`, `meetings.protocol.archive_dir`
- [ ] Unit tests for `ProtocolRenderer` + `ProtocolPdfRenderer` against various agenda shapes
- [ ] Integration tests for protocol endpoint + archive-time snapshot

### Phase 6 — Frontend: read-only views
- [ ] `/Administration/Meetings` list page
- [ ] `/Administration/Meetings/{Id}` detail page (read-only initially, agenda as static list)
- [ ] Status badges + transition buttons (wired to existing permission checks)
- [ ] Nav menu entry
- [ ] `MotionPicker` component (selects from existing motions in chapter)

### Phase 7 — Frontend: agenda editor (with tree)
- [ ] Agenda editor on detail page (visible when Draft/Scheduled)
- [ ] `AgendaItemTree` recursive component with indented rendering + hierarchical numbering
- [ ] Add root item / add subitem buttons; reorder within siblings; move-between-parents dropdown
- [ ] `AgendaItemEditor` component with conditional motion picker
- [ ] Inline "create new motion" modal reusing motion-create flow
- [ ] Delegate-role management UI: extend ChapterOfficer management page to also list/add/remove delegates (or add a sibling tab)

### Phase 8 — Frontend: live minute-taking mode
- [ ] InProgress view on detail page: progress rail + active-item panel
- [ ] Auto-save notes (debounced, 3s)
- [ ] Vote buttons + officer status list per motion item
- [ ] Close-vote action with auto-generated resolution preview
- [ ] "Complete item" / "Start next" advance flow

### Phase 9 — Frontend: protocol view + export
- [ ] Protocol preview tab on detail page (HTML-rendered)
- [ ] Download buttons (md / pdf) — pdf button streams archived snapshot for Archived meetings, regenerates for Completed
- [ ] Visibility badge + public-access marketing copy on public-visible detail page

### Phase 10 — E2E test + polish
- [ ] E2E flow test (full meeting lifecycle)
- [ ] Audit log tab on meeting detail
- [ ] German translations pass (all user-facing strings)
- [ ] Mobile responsiveness check (agenda editor on tablet)

---

## 12. Decisions + deferred items

**Decided (from user input):**
- PDF rendering: **QuestPDF** (not Playwright)
- PDF snapshot: hard-written to disk on Archive, streamed on subsequent reads
- Cross-chapter motion references: **no** — validator enforces same-chapter
- `TryAutoResolve` during meetings: **disabled for votes with non-null `MeetingId`** — meetings always require explicit close. Auto-resolve sweep runs on Completed transition for any still-open linked motions.
- **Manual resolve is always available** regardless of meeting linkage: existing `MotionStatusEndpoint` (POST /api/motions/status) stays unchanged. If a motion's agenda-item is scheduled for a future meeting but the vote reaches threshold early, a chapter officer can manually resolve it — the auto-sweep on meeting Completed simply skips already-resolved motions.
- Concurrent note-taking: **last-write-wins for v1** — see deferred item below
- Meeting visibility: **Public/Private** two-state; Private = direct-only officer/delegate access on meeting's chapter

**Deferred to v2 (tracked as follow-up todos, not blockers):**
- **Collaborative note-taking** — v1 uses last-write-wins on the Notes field. v2 should support simple collaborative editing for the notes textarea (e.g., OT/CRDT for plain text, or server-push "locked by X" indicator). Add to `code-quality-todos.md` when Phase 8 completes.
- **Attendance tracking** — v1 uses free-text in Description. v2 could add a lightweight attendee list entity.
- **Protocol template per-chapter** — v1 uses one global Fluid template in Options. Per-chapter override can be added later.
- **Meeting recurrence / series** — v1 supports this via a "Duplicate meeting" action cloning the prior agenda. Full recurrence rules deferred.
- **Async votes already on motion before the meeting** — the vote panel should surface "X existing votes cast asynchronously" and let the officer decide whether to invalidate them. Low priority.

---

## 13. Risks

- **Scope creep:** minute-taking has long-tail UX requirements (templates, speaker queues, amendment tracking). Ship v1 as described, iterate.
- **Protocol PDF quality:** generated PDF needs to look professional enough to serve as official Vereinsprotokoll. QuestPDF gives us full layout control — budget real effort in Phase 5 for typography, page breaks, table-of-contents. Get officer review before Phase 10.
- **Vote-during-meeting vs async vote conflicts:** if a motion received async votes before its meeting agenda item runs, the auto-resolve sweep will tally them alongside meeting votes. Whether that's desired is a UX question — surface a warning in the close-vote UI if the motion already has votes when the meeting item starts.
- **Protocol disk writes:** archived PDFs accumulate. Plan for the `protocols_dir` to be mounted on durable storage (not ephemeral container FS) and included in backups. Document in deployment guide.
- **Role inheritance flag is a breaking behavior change for existing custom roles:** existing custom roles are defaulted to `InheritsToChildren=true`, preserving current semantics. But administrators creating *new* custom roles need to understand the flag. Add clear UI copy on the role-create form.
