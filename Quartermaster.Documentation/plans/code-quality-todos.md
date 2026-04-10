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

### One-class-per-file audit — ✅ DONE
- [x] Audited all `.cs` files for ≥2 top-level public classes. Found one production violation: `UserPermissionEndpoints.cs` (5 different endpoints in one file). Split it into 5 files: `GetUserPermissionsEndpoint.cs`, `GrantGlobalPermissionEndpoint.cs`, `RevokeGlobalPermissionEndpoint.cs`, `GrantChapterPermissionEndpoint.cs`, `RevokeChapterPermissionEndpoint.cs`.
- [x] Updated CLAUDE.md style rule to add an explicit exception: test files may contain multiple test classes when they cover the same region/feature (e.g., multiple validator test classes for one feature, multiple endpoint test classes for one resource). This formalized the exception that several test files were already relying on.
- [x] All other ≥2-class files were verified as legitimate exceptions: DTO files (pure data), Endpoint+Request pairs, Endpoint+Request+Response narrow pairings, `TokenAuthenticationHandler`+Options pattern, repository+return-DTO narrow pairings.

### Region-separator comment audit — ✅ DONE
- [x] Found 8 files with separator comments (39 total: 25 `// --- X ---` style + 14 `#region`/`#endregion` directives in `AdminDivisionImportServiceTests.cs`).
- [x] Removed all separator comments while preserving test code, docstrings, and explanatory comments.

### Frontend: extract components for common patterns — ✅ DONE
- [x] **`LoadingSpinner` component** — wraps the centered `spinner-border` pattern. Replaced ~40 usages across the Pages directory. Supports `Small` and `CssClass` parameters for variants.
- [x] **`EmptyState` component** — wraps the "Keine X vorhanden" pattern. Replaced 9 usages across 8 files.
- [x] **`PageBackLink` component** — wraps the standard back button (`<div class="mb-3">` + `btn-outline-secondary` link + arrow-left icon). Replaced 18 usages. Defaults `Text` to "Zurück zur Übersicht".
- [x] **`PageHeader` component** — wraps the `d-flex justify-content-between align-items-center mb-3` admin page header with title + optional `Actions` RenderFragment. Replaced 13 usages across list/detail pages.
- [x] **Status badge components** — created `EventStatusBadge`, `EventVisibilityBadge`, `MeetingStatusBadge`, `MeetingVisibilityBadge`, `MotionApprovalBadge`. Each component bakes in the German label + Bootstrap border CSS class for the relevant enum value. Replaced badge usages and removed 24 duplicate helper methods/properties from 7 page code-behinds.
- [x] **`DeleteButton` component** — wraps the `btn-outline-danger` + `bi-trash` icon button pattern. Supports `Text`, `AriaLabel` (for icon-only mode), `Small`, `CssClass`, `Disabled`. Replaced 5 usages.
- [x] **`DashboardCard` component** — wraps the `Home.razor` widget pattern: card → header (optional icon + title + optional total count badge + "Alle anzeigen" link) → empty state OR ChildContent. Replaced 4 widgets in `Home.razor`. The 2 cards in `ImportStatus.razor` were intentionally NOT migrated — they're a different pattern (stats grid + collapsible history, not dashboard widgets).
- **Decided NOT to extract a table-with-card wrapper.** The 18 files using `<div class="card mb-3">` with a table inside have too many variations to consolidate cleanly: some have `card-header`, some don't; some have action buttons in the header; some contain tables, some `list-group`, some forms; some put the title in `card-body`, some in `card-header`. The shared part is just the 1-line `<div class="card mb-3">` wrapper, which is idiomatic Bootstrap and doesn't benefit from extraction — a component would shift boilerplate without reducing it.

### A11y: wire up form label associations properly — ✅ DONE
- [x] Added matching `id`/`for` attributes to **53 label-input pairs across 22 files**. IDs follow camelCase naming derived from the C# field/property (e.g., `@bind="AuthorName"` → `id="authorName"`).
- [x] Verified end-to-end in Chrome: click-on-label now focuses the input (the key a11y win).
- **Intentionally skipped** (~16 labels): compound Blazor components like `<ChapterPicker>`, `<AdminDivisionPicker>`, `<RadioGroup>`, `<Checkbox>`, `<MarkdownEditor>` that render multiple child elements with no stable single focusable target. These labels stay without `for=` — screen readers still associate via DOM proximity (Bootstrap's standard pattern). Fixing them would require threading `Id` parameters through each picker component, which is a much larger refactor with unclear benefit.

### Server error messages: identifiers instead of German strings — ✅ DONE (infrastructure)
- [x] **Built generic i18n infrastructure** in `Quartermaster.Api/I18n/`:
  - `I18nKey.cs` — central catalog of ~150 stable identifier constants grouped by feature (e.g., `I18nKey.Error.Motion.TitleRequired`)
  - `I18nService.cs` — translation service that loads embedded JSON locale files via reflection. Supports `{placeholder}` substitution and falls back to the raw key for missing translations.
  - `I18nParams.cs` — helper for building query-string-encoded parameterized keys (e.g., `I18nParams.With(key, ("from", "Draft"), ("to", "Completed"))` → `"...?from=Draft&to=Completed"`)
  - `de.json` — embedded resource with all German translations
  - Both server and client load the same JSON via reflection on `Quartermaster.Api`'s embedded resources (single source of truth)
- [x] **Migrated 53 server files** (~166 string sites): 32 files in Meetings/Events area + 21 files in Users/Roles/Motions/Members/Admin area. Both `ThrowError`/`AddError` and FluentValidation `.WithMessage()` calls now reference `I18nKey` constants. 4 parameterized errors use `I18nParams.With(...)`.
- [x] **API wire format decoupled**: validation errors now return `{"name":"authorEMail","reason":"error.motion.email_invalid"}` instead of `{"name":"authorEMail","reason":"E-Mail-Adresse muss ein @ enthalten."}`. Any API consumer (current Blazor frontend, future mobile app, integrations, tests) gets language-independent codes.
- [x] **Frontend translation pipeline wired up**:
  - `I18nService` registered as singleton in Blazor DI
  - `Http/ApiErrorHelper.cs` parses the FastEndpoints error response shape and translates each `reason` field via `I18nService`
  - `ToastService.ErrorAsync(HttpResponseMessage)` reads the response body, translates the errors, and displays a German error toast
  - `ToastService.Translate(key)` exposed for callers needing the raw translated string
- [x] **Updated 89 validator test assertions** across 9 test files from German strings to `I18nKey` references. All 915 tests passing.
- [x] **End-to-end verified in Chrome**: submitted a motion form with an invalid email, observed the API return `error.motion.email_invalid`, and saw the German translation `"E-Mail-Adresse muss ein @ enthalten."` appear in a translated error toast.

**Open**: 45 frontend page call sites still use the legacy `ToastService.Error(ex)` pattern (generic "Ein Fehler ist aufgetreten") instead of the new `ToastService.ErrorAsync(response)` that surfaces specific translated errors. Migrating them is a separate UX-improvement pass — see "Frontend page error-handling migration" below.

### Frontend page error-handling migration
- **Task:** Migrate 45 page call sites from `ToastService.Error(ex)` (generic error) to `await ToastService.ErrorAsync(response)` (translated specific errors from the API). Currently only `MotionCreate.razor.cs` uses the new pattern as a reference.
- **Why:** Users currently see "Ein Fehler ist aufgetreten" for any failed API call. With the i18n pipeline complete, they could see specific messages like "E-Mail-Adresse muss ein @ enthalten." that pinpoint the issue.
- **How to apply:** For each page that has a `if (response.IsSuccessStatusCode) { ... } else { ToastService.Error(...); }` pattern, replace the else branch with `await ToastService.ErrorAsync(response);`. For pages that wrap the call in `try/catch (HttpRequestException)`, refactor to check the response status code instead so the body is parseable.

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

### ToList vs IEnumerable on endpoint returns — ❌ CLOSED (not worth doing)

**Decision:** Audited and closed without changes. The realistic savings are in the noise and the risk of runtime errors outweighs them.

**Reasoning:**
1. **All paginated endpoints cap at 100 items** via `PaginationValidationExtensions.AddPaginationRules` (`PageSize` between 1–100). Maximum saving per response is ~824 bytes (one List header + 100-element pointer array), which is negligible compared to the kilobytes already allocated for JSON serialization buffers and the DTOs themselves.
2. **Most `.ToList()` calls are structurally required** because the receiving DTO field is typed as `List<T>` (e.g., `MemberSearchResponse.Items`, `MeetingDetailDTO.AgendaItems`). Removing materialization would require changing DTO field types to `IEnumerable<T>` — a wire-format-compatible but breaking-for-internal-callers contract change with large blast radius.
3. **Risk vs reward is bad.** Removing `.ToList()` from any chain that touches a LinqToDB query causes runtime errors when the DbContext is disposed before serialization. Hard to test, fails only under load.
4. **Repository methods already return materialized `List<T>`** (e.g., `MemberRepository.Search`, `MotionRepository.List`). The `.Select(...).ToList()` chain in endpoints runs purely on in-memory data, so there's no deferred-execution problem — but also no significant allocation to save.

If a future workload turns out to be allocation-bound on a specific endpoint (which would only show up in a large profile), revisit that single endpoint with measurements.
