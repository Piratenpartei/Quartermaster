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
