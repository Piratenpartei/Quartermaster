# Code Quality TODOs

Accumulated during feature work. Fix in a dedicated code quality review pass.

---

## Complex Conditionals (extract to methods with guard clauses) — ✅ DONE

- [x] **Repeated 2-line permission check pattern** — extracted `EndpointAuthorizationHelper.HasPermission(userId, chapterId, permission, globalPermRepo, chapterPermRepo, chapterRepo)` that combines `HasGlobalPermission` + `HasPermissionWithInheritance` into a single call. Also added a split-permission overload for cases where global and chapter permission identifiers differ (e.g., `ViewAllMembers` globally vs `ViewMembers` per chapter). Replaced across 37+ endpoints in Events, Meetings, Admin, Members, Motions, and ChapterAssociates directories.
- [x] **MotionVoteEndpoint / AgendaItemVoteEndpoint 3-line delegation check** — reduced from 3 conditions to 2 by using `HasPermission` for the delegation permission check (`!callerIsOfficer && !HasPermission(VoteDelegateMotions)`).

---

## Tuples > 3 Values (replace with named class/record)

No remaining violations found. The `AdminDivisionImportService.ApplyChanges` tuple was already fixed.

---

## Other

### One-class-per-file audit
- **Task:** Walk the whole codebase and flag any file with multiple top-level classes/structs that don't qualify for the allowed exceptions (pure-data classes grouped together, or a request class paired with its endpoint). Split violations into separate files.
- **Why:** Newly-added style rule; older code hasn't been audited.
- **How to apply:** `grep -r "^public class" --include="*.cs"` per file; if a file has ≥2 matches, check whether the exceptions apply.

### Region-separator comment audit
- **Task:** Search for region-separator style comments (e.g., `// ---------- X ----------`, `#region`, banner comments that don't describe specific code) and remove them. If code needs visual separation, it should be split into separate methods/files instead.
- **How to apply:** `grep -rn "^// ----" --include="*.cs"` and review each hit.

### Frontend: extract components for common patterns — partial ✅
- [x] **`LoadingSpinner` component** — wraps the centered `spinner-border` pattern. Replaced ~40 usages across the Pages directory. Supports `Small` and `CssClass` parameters for variants.
- [x] **`EmptyState` component** — wraps the "Keine X vorhanden" pattern. Replaced 9 usages across 8 files.
- [x] **`PageBackLink` component** — wraps the standard back button (`<div class="mb-3">` + `btn-outline-secondary` link + arrow-left icon). Replaced 18 usages. Defaults `Text` to "Zurück zur Übersicht".

**Still open** (not yet extracted):
- Admin page header with title + action buttons (multi-button toolbars)
- Card + header + optional "Alle anzeigen" link (dashboard widgets)
- Status/visibility badges with consistent styling per value
- Confirmation delete button (outline-danger + trash icon + ConfirmDialog wiring)
- Table-with-card wrapper

### A11y: wire up form label associations properly
- **Task:** 74 `<label class="form-label">` elements across ~24 files don't have `for=` attributes linking them to their inputs. Currently works in practice for screen readers due to DOM adjacency (Bootstrap's standard pattern), but not semantically correct. Click-on-label-to-focus-input doesn't work either.
- **Fix:** Either generate unique IDs for each input + matching `for=` on labels, OR nest inputs inside `<label>` elements (valid HTML — no `for`/`id` needed).
- **Alternative:** Enhance `FormInput`/`FormSelect`/`FormTextarea` components to auto-generate IDs internally and accept a `Label` parameter that handles the association.
- **High-traffic forms to prioritize:** `Login/Manual.razor`, `MembershipApplication/*`, `Motions/Create.razor`, `DueSelector/*`, `EventDetail.razor`.

### Server error messages: identifiers instead of German strings
- **Task:** Replace hard-coded German error messages returned from server endpoints (e.g., `ThrowError("Vorlagen können nur aus Entwurfs-Events erstellt werden.")`) with stable identifier codes (e.g., `"error.events.template_requires_draft"`). The frontend translates identifiers into user-facing strings.
- **Why:** Makes future i18n trivial, decouples server from display language, enables translation of error messages per locale.
- **How to apply:** Introduce an error catalog (constants or an enum) as the single source of truth for identifiers; refactor `ThrowError`/`AddError` calls to use identifiers; build a translation table on the frontend for the German strings. Touches many endpoints — do in waves.

### Test coverage review — ✅ DONE
- Partial pass completed: added `PermissionInheritanceTests` (8 tests covering ancestor-chain inheritance, 10-level hierarchy, view vs write perm distinction, role-derived grants), `TokenAuthenticationTests` (9 tests covering expired/invalid/malformed/whitespace tokens, deleted user, wrong-type tokens), `LockoutLogicTests` (10 tests covering sliding window, per-IP+user isolation, threshold boundaries, success clearing), `EndpointAuthorizationHelperTests` (6 tests covering null-chapter-ids-for-global-perm, descendant inheritance), `SecurityHeadersMiddlewareTests` (6 tests covering all headers + HSTS-only-on-HTTPS), `EdgeCaseMarkdownTests` (12 tests covering unicode, emoji, RTL text, XSS vectors, data URLs, event handlers).
- Remaining for future: deeper per-suite audits of ChapterRepository, OptionRepository, MemberImportService, AdminDivisionImportService for their specific edge cases (timezone, duplicates, malformed input).

### Better toast notifications — ✅ DONE
- [x] Added `DurationMs` property to `Toast` model (nullable int — null means persistent).
- [x] `ToastService` now sets `DurationMs = 3000` for success/default toasts, `null` for error/danger toasts.
- [x] `Toaster` component schedules auto-removal via `System.Threading.Timer` for toasts with a duration. Implements `IDisposable` to clean up timers. Error toasts remain until manually dismissed.

### SignalR for live meeting updates + collaborative editing
- **Task:** Add SignalR hub for real-time push to meeting participants. Two use cases: (1) live meeting page auto-updates when another officer votes or completes an agenda item, (2) collaborative editing for agenda item notes (prerequisite for the collaborative writing feature deferred from v1).
- **Why:** Currently the live meeting page requires manual refresh to see changes made by other participants. SignalR would enable real-time multi-officer minute-taking.
- **How to apply:** Add `Microsoft.AspNetCore.SignalR` package; create `MeetingHub` with groups per meeting ID; push events from meeting-mutating endpoints; Blazor UI subscribes via `HubConnection`.
- **Note:** This is also the foundation for the collaborative writing TODO noted in the meeting system design plan (last-write-wins → CRDT/OT for notes).

### DTO mapping standardization
- **Task:** Pick a single mapping style and apply it consistently. Currently the codebase mixes Mapperly (for simple entity↔DTO pairs) with hand-written mapping code (for complex projections that do joins or reshaping).
- **Why:** The mix is pragmatic today but inconsistent — new contributors need to learn which side of the line they're on for each DTO, and some hand-written mappers duplicate logic Mapperly could generate. Standardizing reduces cognitive load and cuts boilerplate.
- **How to apply:** Audit `Quartermaster.Data/**/*Mapper.cs` and inline mapping in endpoints. Decide on one of: (a) Mapperly everywhere (add `[MapProperty]`/`[MapperIgnoreSource]` annotations for the complex cases), or (b) hand-written everywhere (delete the Mapperly partial classes, replace `ToDto()` calls with explicit constructors). Option (a) is likely less code overall.

### Endpoint behavior review (discovered during integration test pass) — ✅ DONE

**Security — FIXED:**
- [x] **`MotionListEndpoint` / `MotionDetailEndpoint` now enforce `IsPublic` server-side.** List endpoint ignores `IncludeNonPublic=true` for anonymous/unauthorized callers; authenticated users with `ViewMotions` (chapter-scoped, with inheritance) see non-public motions only for their permitted chapters. Detail endpoint returns 404 for non-public motions when caller lacks `ViewMotions` on the motion's chapter. Tests in `MotionAccessControlPendingTests` now pass.

**Permission misuse — FIXED:**
- [x] **`MemberAdminDivisionUpdateEndpoint` now gates on `EditMembers` (chapter-scoped).** Changed from global `ViewAllMembers` to `EditMembers` with chapter-scoped inheritance check against the member's chapter. Tests in `MemberAdminDivisionAuthorizationPendingTests` now pass. Note: orphan flag recomputation was not needed — orphan state lives on the `AdministrativeDivision` entity (set during imports), not on the member.

**Semantics review — documented decisions (no changes needed):**
- **`EventArchiveEndpoint` requires `DeleteEvents`:** Intentional — archiving is a destructive-ish operation, gating on delete permission is appropriate. Documented via existing test assertions.
- **`ChecklistItemCheckEndpoint` non-idempotency:** Intentional for all item types — `CreateMotion`/`SendEmail` have irreversible side effects, and `Text` items benefit from consistent behavior. Documented via `Rejects_already_completed_item` test.
- **`RoleAssignmentDeleteEndpoint` and `ChapterOfficerDeleteEndpoint` return 200 for non-existent records:** Intentional idempotent-delete pattern. Documented via `Returns_OK_for_nonexistent_*` tests.
- **`MembershipApplicationProcessEndpoint` / `DueSelectionProcessEndpoint` reject `Pending`:** Correct — only terminal transitions allowed. Documented via `Rejects_invalid_target_status_Pending` tests.

### ToList vs IEnumerable on endpoint returns
- **Task:** Audit endpoint DTO construction for unnecessary `.ToList()` calls. Many endpoints shape data with LINQ (`items.Select(...).ToList()`) then pass to `SendAsync`. The JSON serializer can consume `IEnumerable<T>` directly, so eager materialization may be avoidable — potentially saving allocations on large responses.
- **Caveats to verify per site:**
  - LinqToDB queries that are still open (deferred execution) — must stay enumerable only if DbContext lifetime is sufficient, otherwise keep `.ToList()`.
  - Dictionary lookups inside `Select` that reference scope-captured state — safe.
  - Cases where the list is used twice (e.g., count + iterate) — keep `.ToList()`.
- **Why:** Pure performance — avoid double allocation when we build a list just to immediately serialize it.
- **How to apply:** grep for `.ToList();` in `Quartermaster.Server/`, inspect each return path, remove where the DTO list is write-only and deterred execution is safe. Benchmark before/after on a large list endpoint (members, events) to confirm measurable impact before applying broadly.
