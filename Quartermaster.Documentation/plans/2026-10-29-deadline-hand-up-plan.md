# Deadline hand-up Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Undecided application motions move up to the Landesverband after 28 days and to the Bundesverband after 62 days (counted from submission, overridable per chapter), and the Bundesvorstand can take over reduced due selections by hand.

**Architecture:** Chapters get a technical level and an optional period. A pure helper (`TakeOverSchedule`) computes the due and the next take-over from candidates above a motion's chapter, which a scoped `TakeOverPlanner` builds from the ancestor chain, the marks and the forwarding settings. A 15-minute sweep hands due motions over through `MotionHandover`, which gains a single-motion entry with a reason; a take-over endpoint uses the same entry.

**Tech Stack:** .NET 10, FastEndpoints, LinqToDB + FluentMigrator (MariaDB), Blazor WASM, TUnit.

**Spec:** `Quartermaster.Documentation/plans/2026-10-29-deadline-hand-up-design.md`

## Global Constraints

- Follow `CLAUDE.md` (K&R, if-body on the next line, guard-clause methods for 3+ term conditions, explicit access modifiers, `using` directives, one class per file, no `@code` in `.razor`, `== null`, never `is X or Y`, `Lock` type, tuples of at most 3 values, minimal comments).
- Code and routes English; only UI text German, in `Quartermaster.Server/wwwroot/i18n/de.json` and `en.json` (edit textually, keep the blank-line grouping), constants in `Quartermaster.Api/I18n/I18nKey.cs`.
- No em or en dashes anywhere.
- Schema changes fold into `Quartermaster.Data/Migrations/M009.cs` (`[Migration(017)]`, Up and Down); the dev DB already ran it, so ALTER and UPDATE it by hand (mysql CLI, user root, DB `quartermaster`).
- Enums persisted as ints: append values only.
- Never contact a real Redmine, mail or Telegram server from tests; use the existing fakes (`FakeRedmineClient` and friends, see `MotionHandoverTests`).
- Never run git; the user commits. Never run the full test suite.
- Build: `taskset -c 0-9 dotnet build Quartermaster.sln`. Tests: `taskset -c 0-9 dotnet run --no-build --project Quartermaster.Server.Tests -- --treenode-filter "/*/*/(ClassA)|(ClassB)/*" --progress off > log` and read the log.
- Read-only inputs get both `readonly` and `disabled`.
- Handover and take-over fields are member-only: none reaches an anonymous caller of a public motion.
- A handover never creates Redmine tickets and never touches what was forwarded to a Redmine.
- Values fixed by the spec: levels `Federal` = 0 "Bundesverband", `State` = 1 "Landesverband", `District` = 2 "Bezirksverband", `County` = 3 "Kreisverband", `Local` = 4 "Ortsverband"; periods 1 to 365 days; defaults `State` 28, `Federal` 62; sweep every 15 minutes; `MotionHandoverReason` `ChapterMark` = 0, `Deadline` = 1, `TakenOver` = 2; route `POST /api/motions/{Id}/take-over`; error key `error.motion.handed_over`.
- German texts fixed by the spec: "Nach Fristablauf an {0} übergeben"; "Vom Bundesverband übernommen; hier kann nicht mehr abgestimmt werden."; "Von {0} nach Fristablauf übernommen (eingegangen am {1})."; "Geht am {0} an {1}, falls bis dahin nicht entschieden."; button "Übernehmen"; level "nicht gesetzt".

## Review Focus

- A chapter re-parented to the top (parent cleared) or a root moved below another chapter: the root is always `Federal`, a non-root never is. Pinned in Task 1 (`Update_forces_federal_on_new_root`, `Update_refuses_federal_below_root`).
- An application motion that was already handed over by a chapter mark (now at the LV, `OnBehalfOf` = KV) reaches the federal deadline: it moves on and keeps `OnBehalfOf` = KV. Pinned in Task 3 (`Deadline_move_keeps_on_behalf_of`).
- A vote recorded on the old motion after a deadline move: refused, and the new motion's result is untouched. Pinned in Task 4 (`Vote_on_handed_over_motion_is_refused`).
- The application is decided at the LV after a deadline move: the application is approved through the new motion and the old one triggers nothing. Pinned in Task 3 (`Moved_motion_decides_application`).
- Settings changed so that both deadlines have passed for a KV motion: it goes straight to the federal chapter in one sweep. Pinned in Task 2 (`Several_passed_goes_to_highest`) and Task 3 (`Sweep_moves_straight_to_highest_due`).

---

### Task 1: Chapter level and period

**Files:**
- Create: `Quartermaster.Api/Chapters/ChapterLevel.cs`
- Create: `Quartermaster.Api/Motions/MotionHandoverReason.cs`
- Modify: `Quartermaster.Data/Chapters/Chapter.cs` (`ChapterLevel? Level`, `int? TakeOverPeriodDays`, `int? EffectiveTakeOverPeriodDays()`, constants `TakeOverPeriodMinDays = 1`, `TakeOverPeriodMaxDays = 365`, `StateDefaultPeriodDays = 28`, `FederalDefaultPeriodDays = 62`)
- Modify: `Quartermaster.Data/Motions/Motion.cs` (`MotionHandoverReason? HandoverReason`)
- Modify: `Quartermaster.Data/Migrations/M009.cs`
- Modify: `Quartermaster.Api/Chapters/ChapterDTO.cs`, `ChapterCreateRequest.cs`, `ChapterUpdateRequest.cs` (`ChapterLevel? Level`, `int? TakeOverPeriodDays`)
- Modify: `Quartermaster.Server/Chapters/ChapterCreateEndpoint.cs`, `ChapterUpdateEndpoint.cs`
- Test: `Quartermaster.Server.Tests/Chapters/ChapterLevelTests.cs`

**Interfaces:**
- Produces: `Chapter.EffectiveTakeOverPeriodDays()`: `TakeOverPeriodDays` when set; else 62 for a root (`ParentChapterId == null`, whatever its stored level); else 28 for `Level == State`; else null.
- Produces: `ChapterDTO.Level` and `ChapterDTO.TakeOverPeriodDays` (public, in `ToDto`).
- Produces: create and update store the level and period: a chapter without a parent always gets `Federal`; `Federal` on a chapter with a parent is refused (400, `error.chapter.level.federal_root_only`); a period outside 1 to 365 is refused (400, `error.chapter.take_over_period.range`).

- [ ] **Step 1: Write failing tests** in `ChapterLevelTests`: `Effective_period_defaults_by_level` (State 28, root 62 with any level, County null, override wins), `Create_root_gets_federal`, `Create_refuses_federal_below_root`, `Update_forces_federal_on_new_root`, `Update_refuses_federal_below_root`, `Update_refuses_period_out_of_range` (0 and 366), `Update_stores_level_and_period`, `Dto_carries_level_and_period`.
- [ ] **Step 2: Run them**; expect compile failures.
- [ ] **Step 3: Implement.** M009 Up: `Chapters.Level` int nullable, `Chapters.TakeOverPeriodDays` int nullable, `Motions.HandoverReason` int nullable, then `Execute.Sql` setting `Level = 0` where `ParentChapterId IS NULL` and `Level = 1` on chapters whose parent has `ParentChapterId IS NULL`; Down drops the three columns. Apply the same ALTERs and UPDATEs to the dev DB. Error keys in `I18nKey.Error.Chapter` with German texts "Nur die oberste Gliederung kann Bundesverband sein." and "Die Frist muss zwischen 1 und 365 Tagen liegen."
- [ ] **Step 4: Run** `ChapterLevelTests` plus the existing chapter endpoint tests that create or update chapters (search `Quartermaster.Server.Tests` for `/api/chapters`); expect PASS.

### Task 2: Take-over schedule

**Files:**
- Create: `Quartermaster.Server/Chapters/TakeOverCandidate.cs` (`public record TakeOverCandidate(Guid ChapterId, string ChapterName, int? PeriodDays, bool HandlesApplications);`)
- Create: `Quartermaster.Server/Chapters/TakeOverStep.cs` (`public record TakeOverStep(Guid ChapterId, string ChapterName, DateTime DueAt);`)
- Create: `Quartermaster.Server/Chapters/TakeOverSchedule.cs` (static, pure)
- Create: `Quartermaster.Server/Chapters/TakeOverPlanner.cs` (scoped; register in `Program.cs` next to `MotionHandover`)
- Test: `Quartermaster.Server.Tests/Chapters/TakeOverScheduleTests.cs` (pure), `Quartermaster.Server.Tests/Chapters/TakeOverPlannerTests.cs` (integration)

**Interfaces:**
- Consumes: `Chapter.EffectiveTakeOverPeriodDays()`, `Chapter.MarkFor(ChapterMarkPurpose.MemberAdministration)`, `ChapterRepository.GetAncestorChain`, `OptionRepository.IsGlobalEnabled(ForwardingOptionKeys.MembershipApplicationEnabled)`, `ForwardingSettingsRepository.GetChapter(id)?.ApplicationMode`.
- Produces: `static TakeOverStep? TakeOverSchedule.Due(IReadOnlyList<TakeOverCandidate> above, DateTime startUtc, DateTime nowUtc)`: among candidates with `PeriodDays != null` and `HandlesApplications`, the one highest in the list (last index) whose `startUtc + PeriodDays` is at or before `nowUtc`; else null. `above` is nearest first, the root last.
- Produces: `static TakeOverStep? TakeOverSchedule.Next(IReadOnlyList<TakeOverCandidate> above, DateTime startUtc, DateTime nowUtc)`: among the same candidates, the one with the earliest due after `nowUtc`; else null.
- Produces: `TakeOverPlanner.CandidatesAbove(Guid chapterId)` returning `List<TakeOverCandidate>` for the chain above the chapter (not the chapter itself): `HandlesApplications` is false when the chapter is marked for member administration, or when forwarding is enabled and its application mode is not Quartermaster; the root always has `HandlesApplications = true`.
- Produces: `TakeOverPlanner.DueFor(Motion motion, MembershipApplication application, DateTime nowUtc)` and `NextFor(...)` (same arguments), both `TakeOverStep?`, using `application.SubmittedAt` as the start; both return null unless the motion is `Pending`, linked to that application, not handed over, and the application is `Pending`.

- [ ] **Step 1: Write failing tests.** `TakeOverScheduleTests`: `Nothing_due_before_first_deadline`, `State_takes_over_at_its_deadline`, `Federal_takes_over_at_its_deadline`, `Several_passed_goes_to_highest`, `Candidate_without_period_is_passed_over`, `Candidate_not_handling_applications_is_passed_over`, `Next_is_earliest_future_deadline`, `Next_is_null_when_nothing_ahead`, `Empty_list_gives_null`. `TakeOverPlannerTests` (tree root > LV (State) > BzV (District) > KV (County)): `Candidates_skip_nothing_but_period_less_chapters` (BzV has `PeriodDays` null; with a period set it is a candidate), `Marked_lv_does_not_handle`, `Redmine_lv_does_not_handle_with_forwarding`, `Root_handles_with_forwarding_to_redmine`, `Motion_at_root_has_no_candidates`, `Not_pending_application_has_no_step`, `Reduction_motion_has_no_step`.
- [ ] **Step 2: Run them**; expect compile failures.
- [ ] **Step 3: Implement** the files above.
- [ ] **Step 4: Run** `TakeOverScheduleTests` and `TakeOverPlannerTests`; expect PASS.

### Task 3: Handover with a reason, and the sweep

**Files:**
- Modify: `Quartermaster.Server/Chapters/MotionHandover.cs`
- Modify: `Quartermaster.Data/Motions/MotionRepository.cs` (`SetHandedOverTo(Guid motionId, Guid replacementId, MotionHandoverReason reason)` replaces the two-argument form; `List<Motion> ListPendingLinkedToApplications()`; `Motion? GetHandedOverFrom(Guid replacementId)`)
- Create: `Quartermaster.Server/Chapters/TakeOverSweep.cs` (scoped, register in `Program.cs`)
- Create: `Quartermaster.Server/Chapters/TakeOverHostedService.cs` (register with `AddHostedService` next to `AssemblyMotionDeadlineHostedService`)
- Test: `Quartermaster.Server.Tests/Chapters/TakeOverSweepTests.cs`; rerun `MotionHandoverTests`

**Interfaces:**
- Consumes: `TakeOverPlanner.DueFor`.
- Produces: `public bool MotionHandover.HandOverMotion(Motion motion, Guid targetChapterId, MotionHandoverReason reason, ChapterMarkSkip? onBehalfOf)`: runs in the caller's transaction, does exactly what the mark path does today per motion (status-conditional close, copy with links, `IsPublic`, forwarding cases, linked record moved, audit entry, notification queued for `SendNotifications`) and stores the reason with `HandedOverToMotionId`. The copy's `OnBehalfOf*` is the old motion's when set, else `onBehalfOf`. The mark path calls it with `ChapterMark` and its skip; returns false when the motion was decided meanwhile.
- Produces: `int TakeOverSweep.Run(DateTime nowUtc)`: for each due motion, its own transaction around `HandOverMotion(motion, step.ChapterId, MotionHandoverReason.Deadline, null)`, then `SendNotifications()` after the commit; returns the number moved.
- Produces: `MotionRepository.GetHandedOverFrom(replacementId)`: the motion whose `HandedOverToMotionId` is that id, or null.

- [ ] **Step 1: Write failing tests** in `TakeOverSweepTests` (tree as in Task 2, application submitted at a fixed past time, motion at the KV): `Sweep_moves_due_motion_to_state` (29 days: new motion at LV with links, old one `ClosedWithoutAction`, reason `Deadline`, application `ChapterId` = LV, forwarding case points at the new motion), `Sweep_leaves_motion_before_deadline` (27 days), `Sweep_moves_straight_to_highest_due` (63 days, LV period untouched: one move, target root), `Sweep_moves_motion_with_votes`, `Sweep_skips_decided_motion`, `Sweep_leaves_reduction_motion`, `Sweep_leaves_federal_approval_motion`, `Deadline_move_keeps_on_behalf_of`, `Moved_motion_decides_application`, `Sweep_notifies_target_only` (the LV's officers get the new-application notification, the KV's get none). In `MotionHandoverTests`: `Mark_handover_stores_reason`.
- [ ] **Step 2: Run them**; expect failures.
- [ ] **Step 3: Implement.** `TakeOverHostedService` follows `AssemblyMotionDeadlineHostedService` (scope per run, log the count when above zero, log and continue on errors) with a 15-minute interval.
- [ ] **Step 4: Run** `TakeOverSweepTests`, `MotionHandoverTests`, `ChapterMarksEndpointTests`; expect PASS.

### Task 4: Take-over endpoint, vote refusal and DTO fields

**Files:**
- Create: `Quartermaster.Server/Motions/MotionTakeOverEndpoint.cs`
- Modify: `Quartermaster.Server/Motions/MotionVoteEndpoint.cs`
- Modify: `Quartermaster.Api/Motions/MotionDetailDTO.cs`, `MotionDTO.cs`, `Quartermaster.Server/Motions/MotionDetailEndpoint.cs`, `MotionListEndpoint.cs`
- Modify: `Quartermaster.Api/MembershipApplications/MembershipApplicationDetailDTO.cs`, `Quartermaster.Server/Admin/MembershipApplicationDetailEndpoint.cs`
- Test: `Quartermaster.Server.Tests/Motions/MotionTakeOverEndpointTests.cs`, `Quartermaster.Server.Tests/Motions/MotionHandoverFieldsTests.cs`

**Interfaces:**
- Consumes: `MotionHandover.HandOverMotion`, `MotionRepository.GetHandedOverFrom`, `TakeOverPlanner.NextFor`.
- Produces: `POST /api/motions/{Id}/take-over` (no body): 404 unknown motion; 401 anonymous; 400 when the motion is not `Pending`, not linked to a due selection, already at a root, or handed over (`error.motion.take_over.not_possible`, "Dieser Antrag kann nicht übernommen werden."); 403 without `dueselections_process` on the root of the motion's chapter or globally; else hands over to the root with `TakenOver` in one transaction, sends the notifications and returns the new motion id.
- Produces: votes on a motion with `HandedOverToMotionId != null` are refused with 400 and `error.motion.handed_over` ("Dieser Antrag wurde übergeben; hier kann nicht mehr abgestimmt werden."), checked right after the 404.
- Produces on `MotionDetailDTO` (logged-in callers only, null otherwise): `MotionHandoverReason? HandoverReason`; `string? HandedOverFromChapterName`, `MotionHandoverReason? HandedOverFromReason`, `DateTimeOffset? ApplicationSubmittedAt` (from the predecessor and the linked application); `DateTimeOffset? NextTakeOverAt`, `string? NextTakeOverChapterName`; `bool CanTakeOver` (the endpoint's conditions hold for the caller). `MotionDTO` gets `HandoverReason` the same way. `MembershipApplicationDetailDTO` gets `NextTakeOverAt` and `NextTakeOverChapterName` from its pending, not handed-over motion.

- [ ] **Step 1: Write failing tests.** `MotionTakeOverEndpointTests`: `Take_over_moves_reduction_motion_to_root` (reason `TakenOver`, due selection follows), `Take_over_refuses_application_motion`, `Take_over_refuses_motion_at_root`, `Take_over_refuses_decided_motion`, `Take_over_needs_permission_on_root` (LV officer 403, federal officer and global admin OK), `Take_over_unknown_motion_is_404`, `Vote_on_handed_over_motion_is_refused`. `MotionHandoverFieldsTests`: `Detail_shows_next_take_over`, `Detail_of_replacement_names_predecessor`, `Detail_can_take_over_only_for_federal`, `Public_motion_hides_handover_fields_from_anonymous`, `Application_detail_shows_next_take_over`.
- [ ] **Step 2: Run them**; expect failures.
- [ ] **Step 3: Implement.** Keep the status order of the take-over endpoint as listed; reuse the route pattern and permission helpers of `ChapterMarksEndpoint`.
- [ ] **Step 4: Run** both new classes plus `MotionHandoverTests`; expect PASS.

### Task 5: Blazor

**Files:**
- Modify: `Quartermaster.Blazor/Pages/Administration/ChapterCreate.razor(.cs)`, `ChapterDetail.razor(.cs)` (the edit form and the detail view)
- Modify: `Quartermaster.Blazor/Pages/Administration/MotionDetail.razor(.cs)`, `MotionList.razor(.cs)`
- Modify: `Quartermaster.Blazor/Pages/Administration/MembershipApplicationDetail.razor(.cs)`
- Modify: `Quartermaster.Server/wwwroot/i18n/de.json`, `en.json`, `Quartermaster.Api/I18n/I18nKey.cs`

**Interfaces:**
- Consumes: the DTO fields and the take-over route of Task 4, `ChapterDTO.Level` and `TakeOverPeriodDays` of Task 1.

- [ ] **Step 1: Chapter forms.** A level dropdown ("nicht gesetzt" plus the four non-federal levels; on a root it shows "Bundesverband", `readonly` and `disabled`) and a number field "Übernahmefrist (Tage)" with the effective default as placeholder (62 on a root, 28 for Landesverband, empty otherwise) and a help text saying how long the chapters below have before this chapter takes over. The detail view shows the level and the effective period.
- [ ] **Step 2: Motion detail and list.** Closed motion: the reason text (`Deadline`: "Nach Fristablauf an {0} übergeben", `TakenOver`: "Vom Bundesverband übernommen; hier kann nicht mehr abgestimmt werden.", `ChapterMark`: unchanged) with the existing link; vote buttons hidden when `HandedOverToMotionId` is set. Replacement after a deadline: "Von {0} nach Fristablauf übernommen (eingegangen am {1})." (date as `dd.MM.yyyy`). Waiting motion with `NextTakeOverAt`: alert-info "Geht am {0} an {1}, falls bis dahin nicht entschieden.". `CanTakeOver`: button "Übernehmen" behind the existing `ConfirmDialog` ("Der Bundesverband übernimmt diesen Antrag; die Abstimmung hier endet."), then navigate to the new motion. The list's muted handover line uses the reason text.
- [ ] **Step 3: Application detail.** The same info box when `NextTakeOverAt` is set.
- [ ] **Step 4: Build** the solution; restart the dev server (stop by PID: `ps -eo pid,args | grep "[Q]uartermaster.Server"`, then `kill <pid>` in a separate call; start with `nohup taskset -c 0-9 dotnet run --no-build --project Quartermaster.Server/Quartermaster.Server.csproj --launch-profile https > <scratchpad>/devserver.log 2>&1 &`) and check with curl that `/api/chapters/{rootId}` carries `level` and that the page loads. No Chrome.

### Task 6: Documentation

**Files:**
- Modify: `Quartermaster.Server/Changelog/V9.0.md` (section "### Mitgliedsantrag")
- Modify: `/home/joscha/Projects/PiratenWissen/quartermaster/mitgliedschaft.md`, `chronik.md`
- Modify: `todo.md` (tick the item)

- [ ] **Step 1: Changelog.** A short German paragraph: undecided applications move to the Landesverband after 4 weeks and to the Bundesverband after 2 months from receipt, chapters have a level and an adjustable period, the Bundesvorstand can take over reduced due selections. User-visible only, no internals.
- [ ] **Step 2: PiratenWissen.** In `mitgliedschaft.md` a section "## Fristen und Übernahme" in reading German (one paragraph per line, no hard wraps, functions not names, no hostnames or paths), ending with the paragraph "Quelle: Bundes-IT (Quartermaster Version 9.0), notiert 2026-10-29". A newest-first entry in `chronik.md` dated 2026-10-29.
- [ ] **Step 3: todo.md.** Tick "Hand undecided applications up after a deadline." (`[x]`, keep *(V9.0)*).
- [ ] **Step 4: Grep** the touched files for em and en dashes; expect none.
