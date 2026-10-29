# Chapter marks Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Chapters can be marked "Keine Mitgliederverwaltung" or "Handlungsunfähig"; Quartermaster then routes applications, reduced due selections and (for "Handlungsunfähig") all board motions past them, and hands over what is waiting there.

**Architecture:** A pure walk (`ChapterMarkRouting`) over a chapter's ancestor chain finds the deciding chapter and the skipped one. `ForwardingSettingsResolver.Build` feeds its results into `ForwardingChapterContext`, so `ForwardingRouter` stays pure. A `MotionHandover` service closes waiting motions without side effects and recreates them at the target; a marks endpoint sets the flags and runs the handover in one transaction.

**Tech Stack:** .NET 10, FastEndpoints, LinqToDB + FluentMigrator (MariaDB), Blazor WASM, TUnit.

**Spec:** `Quartermaster.Documentation/plans/2026-10-28-chapter-marks-design.md`

## Global Constraints

- Follow `CLAUDE.md` (K&R, if-body on the next line, guard-clause methods for 3+ term conditions, explicit access modifiers, `using` directives, one class per file, no `@code` in `.razor`, `== null`, never `is X or Y`, `Lock` type, tuples of at most 3 values, minimal comments).
- Code and routes English; only UI text German, in `Quartermaster.Server/wwwroot/i18n/de.json` and `en.json` (edit textually, keep the blank-line grouping), constants in `Quartermaster.Api/I18n/I18nKey.cs`.
- No em or en dashes anywhere.
- Schema changes fold into `Quartermaster.Data/Migrations/M009.cs` (`[Migration(017)]`, Up and Down); the dev DB already ran it, so ALTER it by hand (mysql CLI, user root, DB `quartermaster`).
- Enums persisted as ints: append values only.
- Never contact a real Redmine, mail or Telegram server from tests; use the existing fakes.
- Never run git; the user commits. Never run the full test suite.
- Build: `taskset -c 0-9 dotnet build Quartermaster.sln`. Tests: `taskset -c 0-9 dotnet run --no-build --project Quartermaster.Server.Tests -- --treenode-filter "/*/*/(ClassA)|(ClassB)/*" --progress off > log` and read the log.
- Marks are member-only information: no flag, `OnBehalfOf*` or handover field reaches an anonymous caller.
- The root chapter can never be marked; it is the last stop of every walk.
- A handover never creates Redmine tickets and never touches what was forwarded to a Redmine.
- German texts fixed by the spec: badges "Keine Mitgliederverwaltung", "Handlungsunfähig"; card "Handlungsfähigkeit"; permission "Handlungsfähigkeit verwalten"; notices "Wird im Namen von {0} entschieden; {0} ist handlungsunfähig." and "Zuständig wäre {0} (keine Mitgliederverwaltung)."; handover "An {0} übergeben"; dialog "{0} offene Anträge werden an {1} übergeben."

## Review Focus

- Consecutive marks (OV and KV both marked) with forwarding on, where only the KV has an application setting: the application goes to the next handling chapter above, and the notice names the KV. Pinned in Task 2 (`Handling_chapter_skips_consecutive_marks`).
- An officer of the marked chapter itself tries to set or lift its marks: refused with 403. Pinned in Task 4 (`Own_officer_cannot_mark_chapter`).
- A motion handed over twice (OV marked, later KV marked) keeps the OV as `OnBehalfOfChapterId`. Pinned in Task 3 (`Second_handover_keeps_original_chapter`).
- A waiting application whose motion is handed over is then approved at the new chapter: the application is approved, the forwarding case gets the outcome note, the old motion triggers nothing. Pinned in Task 3 (`Handed_over_motion_decides_application`).
- Anonymous callers of the chapter tree (`roots`, `children`) and of a public motion see no mark. Pinned in Task 4 (`Tree_hides_marks_from_anonymous`, `Public_motion_hides_on_behalf_from_anonymous`).

---

### Task 1: Data model and the deciding-chapter walk

**Files:**
- Create: `Quartermaster.Api/Chapters/ChapterMark.cs` (`NoMemberAdministration = 0, UnableToAct = 1`)
- Create: `Quartermaster.Data/Chapters/ChapterMarkPurpose.cs` (`MemberAdministration, Motion`; not persisted)
- Create: `Quartermaster.Data/Chapters/ChapterMarkSkip.cs` (`public record ChapterMarkSkip(Guid ChapterId, ChapterMark Reason);`)
- Create: `Quartermaster.Data/Chapters/ChapterMarkRoute.cs` (`public record ChapterMarkRoute(Guid DecidingChapterId, ChapterMarkSkip? Skip);`)
- Create: `Quartermaster.Data/Chapters/ChapterMarkRouting.cs`
- Modify: `Quartermaster.Data/Chapters/Chapter.cs` (`bool NoMemberAdministration`, `bool UnableToAct`; method `ChapterMark? MarkFor(ChapterMarkPurpose purpose)`)
- Modify: `Quartermaster.Data/Chapters/ChapterRepository.cs`
- Modify: `Quartermaster.Data/Motions/Motion.cs` (`Guid? OnBehalfOfChapterId`, `ChapterMark? OnBehalfOfReason`, `Guid? HandedOverToMotionId`; `Motion.Create` gets an optional last parameter `ChapterMarkSkip? onBehalfOf = null`)
- Modify: `Quartermaster.Data/Migrations/M009.cs`
- Test: `Quartermaster.Server.Tests/Chapters/ChapterMarkRoutingTests.cs`, `Quartermaster.Server.Tests/Chapters/ChapterRepositoryTests.cs`

**Interfaces:**
- Produces: `Chapter.MarkFor(purpose)`: `UnableToAct` when that flag is set (any purpose); `NoMemberAdministration` when that flag is set and the purpose is `MemberAdministration`; else null.
- Produces: `static ChapterMarkRoute ChapterMarkRouting.Resolve(IReadOnlyList<Chapter> chainFromChapterToRoot, ChapterMarkPurpose purpose)`: the first chapter with `MarkFor(purpose) == null`, the last entry (root) always counting as unmarked; `Skip` is the first skipped chapter with its mark. Throws `ArgumentException` on an empty chain (a bug, not control flow).
- Produces: `ChapterRepository.Route(Guid chapterId, ChapterMarkPurpose purpose)` returning `ChapterMarkRoute` (via `GetAncestorChain`), and `void SetMarks(Guid chapterId, bool noMemberAdministration, bool unableToAct)` that audit-logs each changed flag as a field change on entity type `Chapter` and runs in the caller's transaction when there is one.

- [ ] **Step 1: Write failing tests** in `ChapterMarkRoutingTests`: `Unmarked_chapter_decides_itself`, `Marked_chapter_goes_to_parent`, `Consecutive_marks_skip_to_first_unmarked` (Skip names the first one), `No_member_administration_is_ignored_for_motions`, `Unable_to_act_wins_over_no_member_administration` (both set: reason `UnableToAct`), `Root_decides_even_when_marked`. In `ChapterRepositoryTests`: `SetMarks_audits_each_changed_flag` (setting one flag writes one audit row; setting the same values again writes none).
- [ ] **Step 2: Run them**; expect compile failures.
- [ ] **Step 3: Implement** the files above. M009: two boolean columns on `Chapters` (not null, default false), three nullable columns on `Motions` (`OnBehalfOfChapterId` Guid, `OnBehalfOfReason` int, `HandedOverToMotionId` Guid). ALTER the dev DB to match.
- [ ] **Step 4: Run** `ChapterMarkRoutingTests` and `ChapterRepositoryTests`; expect PASS.

### Task 2: Routing new submissions

**Files:**
- Modify: `Quartermaster.Server/Forwarding/ForwardingChapterContext.cs`, `ForwardingPlan.cs`, `ForwardingRouter.cs`, `ForwardingSettingsResolver.cs`, `ForwardingIntake.cs`
- Modify: `Quartermaster.Server/MembershipApplications/ApplicationReviewService.cs`
- Create: `Quartermaster.Server/Motions/MotionSubmittedNotifier.cs`
- Modify: `Quartermaster.Server/Submissions/SubmissionMaterializer.cs`, DI registration in `Quartermaster.Server/Program.cs`
- Test: `Quartermaster.Server.Tests/Forwarding/ForwardingRouterTests.cs`, `ForwardingSettingsResolverTests.cs`, `ForwardingIntakeTests.cs`, `Quartermaster.Server.Tests/Integration/Motions/MotionCreateEndpointTests.cs`

**Interfaces:**
- Consumes: Task 1 (`ChapterRepository.Route`, `ChapterMarkSkip`, `Motion.Create(..., onBehalfOf)`).
- Produces: `ForwardingChapterContext` gains, as trailing parameters, `Guid? ApplicationDecidingChapterId` (route of the application's chapter for `MemberAdministration`; null without a chapter), `ChapterMarkSkip? ApplicationSkip` (its skip), `ChapterMarkSkip? HandlingSkip` (first marked chapter `HandlingChapterFor` passed over that had an application setting), `ChapterMarkSkip? StateSkip` (the state chapter's mark for `MemberAdministration`, or null).
- Produces: `ForwardingPlan` gains a trailing `ChapterMarkSkip? Skip`, the notice for the motion it plans.
- Produces: `ForwardingSettingsResolver.HandlingChapterFor` returns `HandlingChapter` (new record `HandlingChapter(Guid? ChapterId, ForwardingMode? Mode, ChapterMarkSkip? Skip)` in `Quartermaster.Server/Forwarding/HandlingChapter.cs`; marked chapters are skipped).
- Produces: `ApplicationReviewService.CreateReviewMotionAndNotify(MembershipApplication application, bool isReduced, ChapterMarkSkip? onBehalfOf)` and `CreateReductionMotion(DueSelection dueSelection, Guid chapterId, MembershipApplication? application, ChapterMarkSkip? onBehalfOf)`; public `NotifyApplicationSubmitted(MembershipApplication application, Guid chapterId, bool isReduced)` (the former private `NotifySubmitted`) and `NotifyDueSelectionSubmitted(DueSelection record)` (moved from `ForwardingIntake.NotifyProcessors`).
- Produces: `MotionSubmittedNotifier.Notify(Motion motion, Chapter chapter)` (the notification block moved out of `SubmissionMaterializer.MaterializeMotion`).

Rules (from the spec):
- `PlanApplication` without forwarding: motion and record chapter `ctx.ApplicationDecidingChapterId`, `Skip = ctx.ApplicationSkip`.
- With forwarding: the handling chapter already skips marks; `Skip = ctx.HandlingSkip` on the Quartermaster-handling plan. The paths without a handling chapter are unchanged.
- `PlanDueSelection` for a reduction: when `ctx.StateSkip != null` the state chapter counts as absent (federal decides, no state ticket); `Skip = ctx.StateSkip` only when the state would otherwise have decided (amount at or above the state decision minimum). The record chapter of a due selection needing no decision stays `ctx.StateId ?? ctx.FederalId`.
- `LinkApplication` and `RouteApplication` pass `plan.Skip` to the motion creation; reduction motions pass the reduction plan's `Skip`.
- `MaterializeMotion`: `route = _chapterRepo.Route(req.ChapterId, ChapterMarkPurpose.Motion)`; the motion is stored at `route.DecidingChapterId` with `onBehalfOf = route.Skip`; the notification goes to the deciding chapter.

- [ ] **Step 1: Write failing tests**:
  - `ForwardingRouterTests`: `Application_without_forwarding_goes_to_deciding_chapter` (Skip carried), `Handling_plan_carries_handling_skip`, `Reduction_skips_marked_state` (amount above state minimum: federal decides, one federal ticket, Skip = state), `Reduction_below_state_minimum_has_no_skip`.
  - `ForwardingSettingsResolverTests`: `Handling_chapter_skips_marked_chapter`, `Handling_chapter_skips_consecutive_marks` (OV and KV marked, KV has the setting, LV has one: LV handles, Skip = KV), `State_skip_set_for_marked_state`.
  - `ForwardingIntakeTests`: `Application_in_marked_chapter_goes_up_without_forwarding` (motion and application at the parent, motion `OnBehalfOfReason = NoMemberAdministration`), `Linking_to_marked_chapter_goes_up`.
  - `MotionCreateEndpointTests`: `Motion_for_unable_chapter_goes_to_parent` (logged-in direct path: stored at the parent, `OnBehalfOfChapterId` = submitted chapter, reason `UnableToAct`, notification for the parent), `Motion_for_chapter_without_member_administration_stays`.
- [ ] **Step 2: Run them**; expect failures.
- [ ] **Step 3: Implement** the interfaces and rules above; adjust every existing caller of the changed constructors and methods.
- [ ] **Step 4: Run** `ForwardingRouterTests`, `ForwardingSettingsResolverTests`, `ForwardingIntakeTests`, `MotionCreateEndpointTests`, `SubmissionMaterializerTests` (if present) and `MotionRepositoryTests`; expect PASS.

### Task 3: Motion handover

**Files:**
- Create: `Quartermaster.Server/Chapters/MotionHandover.cs`, `Quartermaster.Server/Chapters/HandoverPreview.cs` (`public record HandoverPreview(Guid TargetChapterId, string TargetChapterName, int Count);`)
- Modify: `Quartermaster.Data/Motions/MotionRepository.cs` (`List<Motion> ListPendingInChapter(Guid chapterId)`, `void SetHandedOverTo(Guid id, Guid newMotionId)`)
- Modify: `Quartermaster.Data/MembershipApplications/MembershipApplicationRepository.cs` (`void SetChapter(Guid id, Guid chapterId)`, audit logged)
- Modify: `Quartermaster.Data/Forwarding/ForwardingCaseRepository.cs` (`void PointAtMotion(Guid caseId, Guid motionId, Guid decidingChapterId)`)
- Test: `Quartermaster.Server.Tests/Chapters/MotionHandoverTests.cs`

**Interfaces:**
- Consumes: Task 1 routing, Task 2 notifiers and `ForwardingSettingsResolver`.
- Produces: `MotionHandover` with `List<HandoverPreview> Preview(Guid chapterId, bool noMemberAdministration, bool unableToAct)` (targets as if the flags were set; motions grouped by target; empty when nothing moves) and `int HandOver(Guid chapterId)` (runs in the caller's transaction after the flags are stored; returns the number handed over; queues the notifications in a list it exposes as `void SendNotifications()` for the caller to call after commit).

Rules (from the spec):
- Which motions: `Pending` motions with `ChapterId == chapterId`; under `NoMemberAdministration` only those with `LinkedMembershipApplicationId` or `LinkedDueSelectionId`; under `UnableToAct` all.
- Target: linked to an application with application forwarding enabled: the nearest ancestor above that is not skipped for `MemberAdministration` and has application mode Quartermaster, else the root. Other linked motions: `Route(chapterId, MemberAdministration)`. Unlinked motions: `Route(chapterId, Motion)`.
- `onBehalfOf`: the motion's existing `OnBehalfOfChapterId`/`OnBehalfOfReason` when set, else the marked chapter with its mark (`UnableToAct` wins).
- Per motion: `UpdatePendingApprovalStatusInTransaction(id, ClosedWithoutAction)`; false skips the motion. Then create the copy (title, markdown, html, author, links, `IsPublic`), `SetHandedOverTo`, move the application (`SetChapter`) or due selection (`SetDecidingChapter`, status stays `Pending`), `PointAtMotion` for every case in `ListByMotion(oldId)`, and an audit entry on the old motion (`"HandedOver"`, new value the new motion id). No decision mail, forwarding outcome, federal approval or register call.
- Notifications after commit: application motion: `NotifyApplicationSubmitted` (isReduced from its due selection); reduction motion: `NotifyDueSelectionSubmitted`; other: `MotionSubmittedNotifier.Notify`.

- [ ] **Step 1: Write failing tests** in `MotionHandoverTests`: `Application_motion_moves_to_parent` (old closed `ClosedWithoutAction` with `HandedOverToMotionId`, new pending at parent with links and `OnBehalfOfReason = NoMemberAdministration`, application `ChapterId` = parent), `Votes_do_not_carry_over`, `Unlinked_motion_stays_without_unable_to_act`, `Unlinked_motion_moves_when_unable_to_act`, `Reduction_motion_moves_to_federal` (due selection chapter = root, still `Pending`), `Second_handover_keeps_original_chapter`, `Handed_over_motion_decides_application` (vote approval on the new motion approves the application and the forwarding case points at it; no decision mail for the old one), `Forwarding_target_is_next_quartermaster_chapter` (LV with Redmine mode is passed over to the root), `Decided_motion_is_skipped`, `Preview_groups_by_target`.
- [ ] **Step 2: Run them**; expect failures.
- [ ] **Step 3: Implement.**
- [ ] **Step 4: Run** `MotionHandoverTests`, `MotionRepositoryTests`, `ForwardingOutcomeHookTests`; expect PASS.

### Task 4: Permission, endpoints and DTOs

**Files:**
- Modify: `Quartermaster.Api/PermissionIdentifier.cs` (`ManageChapterCapacity = "chapters_manage_capacity"`, in `DefaultOfficerPermissions`), `Quartermaster.Data/Permissions/PermissionRepository.cs` (`AddOrRename(..., "Handlungsfähigkeit verwalten", false)`)
- Create: `Quartermaster.Api/Chapters/ChapterMarksRequest.cs` (`Id`, `NoMemberAdministration`, `UnableToAct`), `ChapterMarksResponse.cs` (`int HandedOver`), `ChapterMarksPreviewDTO.cs` (list of `TargetChapterName`, `Count`)
- Create: `Quartermaster.Server/Chapters/ChapterMarksEndpoint.cs` (`PUT /api/chapters/{Id}/marks`), `ChapterMarksPreviewEndpoint.cs` (`GET /api/chapters/{Id}/marks/preview?NoMemberAdministration=&UnableToAct=`)
- Modify: `Quartermaster.Api/Chapters/ChapterDTO.cs` (`NoMemberAdministration`, `UnableToAct`), `ChapterRepository.ToDtos` (flags only with `includeMemberFields`, renamed from `includeContactEmail`), `ChapterDetailEndpoint`, `ChapterListEndpoint`, `ChapterSearchEndpoint`, `ChapterRootsEndpoint`, `ChapterChildrenEndpoint`
- Modify: `Quartermaster.Api/Motions/MotionDetailDTO.cs`, `MotionDTO.cs` (`OnBehalfOfChapterId`, `OnBehalfOfChapterName`, `OnBehalfOfReason`, `HandedOverToMotionId`, `HandedOverToChapterName`), `MotionDetailEndpoint`, `MotionListEndpoint` (filled only for logged-in callers)
- Test: `Quartermaster.Server.Tests/Integration/Chapters/ChapterMarksEndpointTests.cs`, `Quartermaster.Server.Tests/Integration/Motions/MotionDetailEndpointTests.cs`

**Interfaces:**
- Consumes: Task 1 `SetMarks`, Task 3 `MotionHandover`.
- Marks endpoint: 401 without login, 404 unknown chapter, 400 for the root (`error.chapter.marks.root`), 403 unless `Has(parentId, ManageChapterCapacity)`; then in one transaction `SetMarks` and `HandOver`, commit, `SendNotifications`, return the count. Preview endpoint: same checks, returns `Preview(...)`.

- [ ] **Step 1: Write failing tests** in `ChapterMarksEndpointTests`: `Higher_officer_marks_chapter` (officer of the LV marks an OV two levels down), `Own_officer_cannot_mark_chapter`, `Global_admin_marks_chapter`, `Root_cannot_be_marked`, `Marking_hands_over_and_returns_count`, `Lifting_moves_nothing_back`, `Preview_counts_without_changing`, `Tree_hides_marks_from_anonymous` (`roots`, `children`, list, search, detail all false for anonymous, true for a logged-in member). In `MotionDetailEndpointTests`: `Public_motion_hides_on_behalf_from_anonymous`, `On_behalf_shown_to_member`.
- [ ] **Step 2: Run them**; expect failures.
- [ ] **Step 3: Implement**, with i18n keys for the new errors.
- [ ] **Step 4: Run** `ChapterMarksEndpointTests`, `ChapterContactEmailTests`, `ChapterEndpointsIntegrationTests`, `MotionDetailEndpointTests`, `MotionListEndpointTests`; expect PASS.

### Task 5: Blazor

**Files:**
- Modify: `Quartermaster.Blazor/Pages/Administration/ChapterDetail.razor(.cs)` ("Handlungsfähigkeit" card with two switches, shown with the permission on the parent; the client checks via the existing permission service and the server decides)
- Modify: `ChapterList.razor`, `ChapterTree.razor`, `ChapterDetail.razor` (badges, `text-bg-danger` for "Handlungsunfähig", `text-bg-warning` for "Keine Mitgliederverwaltung", next to the archived badge)
- Modify: `MotionDetail.razor(.cs)` (info box `alert alert-info` for the notice; `alert alert-secondary` "An Y übergeben" with a link to the new motion), `MotionList.razor` (the notice as a small muted line under the title)
- Modify: `de.json`, `en.json`, `I18nKey.cs`

Turning a mark on: fetch the preview and open `ConfirmDialog` with "{0} offene Anträge werden an {1} übergeben." per target (or a plain confirmation when nothing moves); turning off: plain confirmation. After saving, show a toast with the number handed over and reload the chapter.

- [ ] **Step 1: Implement** the pages; read-only state follows the permission (switches `disabled` without it).
- [ ] **Step 2: Build** the solution; expect 0 errors. Restart the dev server and check `GET /api/chapters/{id}/marks/preview` with curl returns 401 without login.

### Task 6: Documentation

**Files:**
- Modify: `Quartermaster.Server/Changelog/V9.0.md` (section "Gliederungen": one paragraph on both marks, who sets them, what moves, the notice on the motion)
- Modify: `/home/joscha/Projects/PiratenWissen/quartermaster/vorstandsarbeit.md` and `chronik.md` next to it (PiratenWissen conventions: German reading text, one paragraph per line, a `Quelle: Bundes-IT (Quartermaster Version 9.0), notiert 2026-10-28` paragraph, no hostnames or paths)
- Modify: `todo.md` (tick the chapter marks item with *(V9.0)*)

- [ ] **Step 1: Write** the texts.
- [ ] **Step 2: Check** the changed files for em and en dashes (U+2014, U+2013); none may remain.
