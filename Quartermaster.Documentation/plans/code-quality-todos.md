# Code Quality TODOs

Accumulated during feature work. Fix in a dedicated code quality review pass.

---

## Complex Conditionals (extract to methods with guard clauses)

### MotionVoteEndpoint — 3-line delegation auth check
- [MotionVoteEndpoint.cs:72](../../../Quartermaster.Server/Motions/MotionVoteEndpoint.cs#L72)
- **Issue:** 3-condition `if` spanning 3 lines — extract to a method like `CanDelegateVote()`
```csharp
if (!callerIsOfficer &&
    !EndpointAuthorizationHelper.HasGlobalPermission(...) &&
    !_chapterPermRepo.HasPermissionWithInheritance(...)) {
```

### Repeated 2-line permission check pattern (DRY violation)
- **Issue:** ~22 endpoints repeat the same 2-line permission check: `!HasGlobalPermission(x) && !HasPermissionWithInheritance(x)`. Each instance is borderline (2 simple conditions), but the repetition is the real problem. Extract into `EndpointAuthorizationHelper.HasPermission(userId, chapterId, permission, ...)` or similar.
- **Locations:**
  - [ChapterOfficerAddEndpoint.cs:42](../../../Quartermaster.Server/ChapterAssociates/ChapterOfficerAddEndpoint.cs#L42)
  - [ChapterOfficerDeleteEndpoint.cs:47](../../../Quartermaster.Server/ChapterAssociates/ChapterOfficerDeleteEndpoint.cs#L47)
  - [DueSelectionDetailEndpoint.cs:61](../../../Quartermaster.Server/Admin/DueSelectionDetailEndpoint.cs#L61)
  - [DueSelectionProcessEndpoint.cs:56](../../../Quartermaster.Server/Admin/DueSelectionProcessEndpoint.cs#L56)
  - [MembershipApplicationDetailEndpoint.cs:65](../../../Quartermaster.Server/Admin/MembershipApplicationDetailEndpoint.cs#L65)
  - [MembershipApplicationProcessEndpoint.cs:52](../../../Quartermaster.Server/Admin/MembershipApplicationProcessEndpoint.cs#L52)
  - [ChecklistItemAddEndpoint.cs:46](../../../Quartermaster.Server/Events/ChecklistItemAddEndpoint.cs#L46)
  - [ChecklistItemCheckEndpoint.cs:48](../../../Quartermaster.Server/Events/ChecklistItemCheckEndpoint.cs#L48)
  - [ChecklistItemDeleteEndpoint.cs:45](../../../Quartermaster.Server/Events/ChecklistItemDeleteEndpoint.cs#L45)
  - [ChecklistItemReorderEndpoint.cs:51](../../../Quartermaster.Server/Events/ChecklistItemReorderEndpoint.cs#L51)
  - [ChecklistItemUncheckEndpoint.cs:45](../../../Quartermaster.Server/Events/ChecklistItemUncheckEndpoint.cs#L45)
  - [ChecklistItemUpdateEndpoint.cs:45](../../../Quartermaster.Server/Events/ChecklistItemUpdateEndpoint.cs#L45)
  - [EventArchiveEndpoint.cs:49](../../../Quartermaster.Server/Events/EventArchiveEndpoint.cs#L49)
  - [EventCreateEndpoint.cs:39](../../../Quartermaster.Server/Events/EventCreateEndpoint.cs#L39)
  - [EventDetailEndpoint.cs:50](../../../Quartermaster.Server/Events/EventDetailEndpoint.cs#L50)
  - [EventFromTemplateEndpoint.cs:42](../../../Quartermaster.Server/Events/EventFromTemplateEndpoint.cs#L42)
  - [EventTemplateCreateEndpoint.cs:48](../../../Quartermaster.Server/Events/EventTemplateCreateEndpoint.cs#L48)
  - [EventTemplateDeleteEndpoint.cs:51](../../../Quartermaster.Server/Events/EventTemplateDeleteEndpoint.cs#L51)
  - [EventTemplateDetailEndpoint.cs:52](../../../Quartermaster.Server/Events/EventTemplateDetailEndpoint.cs#L52)
  - [EventUpdateEndpoint.cs:45](../../../Quartermaster.Server/Events/EventUpdateEndpoint.cs#L45)
  - [MemberDetailEndpoint.cs:58](../../../Quartermaster.Server/Members/MemberDetailEndpoint.cs#L58)
  - [MotionStatusEndpoint.cs:45](../../../Quartermaster.Server/Motions/MotionStatusEndpoint.cs#L45)

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

### Frontend: extract components for common patterns
- **Task:** Scan the Blazor frontend for repeated markup patterns and extract them into reusable components.
- **Likely candidates:**
  - Admin page header (title + action buttons right-aligned, e.g., "Back" + "New X" buttons)
  - Card + header + optional "Alle anzeigen" link (dashboard widgets, import status cards)
  - Empty-state message ("Keine X vorhanden.")
  - Loading spinner centered in container (repeated `<div class="d-flex justify-content-center my-4"><div class="spinner-border"></div></div>`)
  - Status/visibility badges with consistent styling per value (event status, motion status, role scope)
  - Confirmation delete button (outline-danger + trash icon + ConfirmDialog wiring)
  - Table-with-card wrapper (header + table inside card-body)
- **How to apply:** Walk the Pages/ directory, identify ≥3 repeated instances of a pattern, extract a component into `Components/`, migrate callers. Measure: lines removed per call site.

### A11y: wire up form label associations properly
- **Task:** 74 `<label class="form-label">` elements across ~24 files don't have `for=` attributes linking them to their inputs. Currently works in practice for screen readers due to DOM adjacency (Bootstrap's standard pattern), but not semantically correct. Click-on-label-to-focus-input doesn't work either.
- **Fix:** Either generate unique IDs for each input + matching `for=` on labels, OR nest inputs inside `<label>` elements (valid HTML — no `for`/`id` needed).
- **Alternative:** Enhance `FormInput`/`FormSelect`/`FormTextarea` components to auto-generate IDs internally and accept a `Label` parameter that handles the association.
- **High-traffic forms to prioritize:** `Login/Manual.razor`, `MembershipApplication/*`, `Motions/Create.razor`, `DueSelector/*`, `EventDetail.razor`.

### Server error messages: identifiers instead of German strings
- **Task:** Replace hard-coded German error messages returned from server endpoints (e.g., `ThrowError("Vorlagen können nur aus Entwurfs-Events erstellt werden.")`) with stable identifier codes (e.g., `"error.events.template_requires_draft"`). The frontend translates identifiers into user-facing strings.
- **Why:** Makes future i18n trivial, decouples server from display language, enables translation of error messages per locale.
- **How to apply:** Introduce an error catalog (constants or an enum) as the single source of truth for identifiers; refactor `ThrowError`/`AddError` calls to use identifiers; build a translation table on the frontend for the German strings. Touches many endpoints — do in waves.

### Test coverage review
- **Task:** Walk through the existing test suites and identify edge cases that should be covered. Areas to consider: empty/null inputs, boundary values (0, 1, max), unicode/special characters, timezone edge cases, concurrent modifications, FK cascade behaviors under various delete orders, permission inheritance with deeply-nested chapter trees, malformed CSV rows, duplicate member numbers, expired/invalid tokens, race conditions in background services.
- **Why:** Tests grew organically alongside features; coverage is adequate for happy paths but edge cases likely vary in depth across suites.
- **How to apply:** Pick one suite at a time during code quality pass, list missing scenarios, add focused tests.

### ToList vs IEnumerable on endpoint returns
- **Task:** Audit endpoint DTO construction for unnecessary `.ToList()` calls. Many endpoints shape data with LINQ (`items.Select(...).ToList()`) then pass to `SendAsync`. The JSON serializer can consume `IEnumerable<T>` directly, so eager materialization may be avoidable — potentially saving allocations on large responses.
- **Caveats to verify per site:**
  - LinqToDB queries that are still open (deferred execution) — must stay enumerable only if DbContext lifetime is sufficient, otherwise keep `.ToList()`.
  - Dictionary lookups inside `Select` that reference scope-captured state — safe.
  - Cases where the list is used twice (e.g., count + iterate) — keep `.ToList()`.
- **Why:** Pure performance — avoid double allocation when we build a list just to immediately serialize it.
- **How to apply:** grep for `.ToList();` in `Quartermaster.Server/`, inspect each return path, remove where the DTO list is write-only and deterred execution is safe. Benchmark before/after on a large list endpoint (members, events) to confirm measurable impact before applying broadly.
