# Approval mail after the SAGE entry Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** An approved membership application asks for the SAGE entry on its federal Redmine ticket, and the approval mail goes out only once the ticket reports the entry as done.

**Architecture:** A new forwarding step `AwaitSageEntry` follows the outcome note of every Quartermaster-handled application. The note on approval carries a templated SAGE request and assigns the SAGE group; the waiting step polls the ticket and sends the held approval mail. Membership applications are refused until forwarding and the SAGE setup are complete.

**Tech Stack:** .NET 10, FastEndpoints, LinqToDB + FluentMigrator (MariaDB), Blazor WASM, TUnit, Fluid templates.

**Spec:** `Quartermaster.Documentation/plans/2026-10-25-sage-entry-design.md`

## Global Constraints

- Follow `CLAUDE.md` (K&R, if-body on the next line, guard-clause methods for 3+ term conditions, explicit access modifiers, `using` directives, one class per file, no `@code` in `.razor`, `== null`, never `is X or Y`, `Lock` type, tuples of at most 3 values, minimal comments).
- Code and routes English; only UI text German, in `Quartermaster.Server/wwwroot/i18n/de.json` and `en.json` (edit textually, keep the blank-line grouping), constants in `Quartermaster.Api/I18n/I18nKey.cs`.
- No em or en dashes anywhere.
- Schema changes fold into `Quartermaster.Data/Migrations/M009.cs` (`[Migration(017)]`); the dev DB already ran it, so ALTER it by hand (mysql CLI, user root, DB `quartermaster`).
- Redmine calls stay https-only without redirects (`HttpRedmineClient.SendAsync`); never log request or response bodies.
- Never contact a real Redmine or mail server from tests; use `FakeRedmineClient`, `RecordingRedmineHandler` and `.invalid` hosts.
- Never run git; the user commits. Never run the full test suite.
- Build: `taskset -c 0-9 dotnet build Quartermaster.sln`. Tests: `taskset -c 0-9 dotnet run --no-build --project Quartermaster.Server.Tests -- --treenode-filter "/*/*/(ClassA)|(ClassB)/*" --no-progress > log` and read the log.
- Spec values: check interval default 15 minutes, minimum 5; provisional number `"RM" + federal ticket id`; partial due `yearly × (13 − entry month) / 12` rounded to the cent (`MidpointRounding.AwayFromZero`) from the requested entry date.

## Review Focus

- A SAGE group or done status deleted in Redmine after setup: the note or check fails with the Redmine error, retries, then sends the failure mail; the approval mail stays held. Pinned in Task 6 (`Check_error_retries_then_fails_and_keeps_mail_held`).
- Ticket assigned back to Quartermaster but status not yet done (or the reverse) with both signals configured: no mail. Pinned in Task 6 (`Both_signals_needed_when_both_set`).
- An application approved before its reduction motion is decided, and the reduction then rejected: the note goes out after the rejection with the rejection default amount. Pinned in Task 6 (`Note_waits_for_reduction_then_uses_rejection_default`).
- The SAGE setup removed after an application was submitted: the approval is still held and the note fails as `NotConfigured`. Pinned in Task 6 (`Missing_setup_fails_note_and_holds_mail`).
- An approval entered twice (motion and processing dialog race): only one approval mail. Covered by the existing `AlreadyDecided` guard plus the single `Done` transition of the step; pinned in Task 6 (`Mail_sent_once_when_check_runs_twice`).

---

### Task 1: Settings, step kind and setup completeness

**Files:**
- Modify: `Quartermaster.Api/Forwarding/ForwardingEnums.cs` (add `AwaitSageEntry = 2` to `ForwardingStepKind`; new enum `SageSetupGap { ForwardingSwitch, FederalConnection, FederalTarget, SageGroup, DoneSignal }`)
- Modify: `Quartermaster.Api/Forwarding/ForwardingSettingsDTOs.cs` (`ForwardingTargetDTO`: `int? SageAssigneeId`, `int? SageDoneStatusId`, `bool SageAwaitReassignment`; new `SageSetupStatusDTO { List<SageSetupGap> Missing }`)
- Modify: `Quartermaster.Api/Forwarding/ForwardingOptionKeys.cs` (`SageCheckMinutes = "forwarding.sage.check_minutes"`, `DefaultSageCheckMinutes = 15`, `MinimumSageCheckMinutes = 5`)
- Modify: `Quartermaster.Data/Forwarding/ForwardingTarget.cs` (three properties, `ToDto`/`FromDto`)
- Modify: `Quartermaster.Data/Migrations/M009.cs` (Up: add `SageAssigneeId` int null, `SageDoneStatusId` int null, `SageAwaitReassignment` bool not null default false to `ForwardingTargets`; Down: drop them)
- Modify: `Quartermaster.Data/Options/OptionRepository.cs` (definition "Weiterleitung: SAGE-Prüfung alle (Minuten)", Number, default "15", description "So oft prüft Quartermaster das Ticket eines angenommenen Mitgliedsantrags auf die SAGE-Eintragung. Mindestens 5.")
- Modify: `Quartermaster.Blazor/Pages/Administration/Settings/SettingsAssistantCatalog.cs` (add the key to `Forwarding.ManagedKeys`)
- Create: `Quartermaster.Server/Forwarding/SageEntry/SageEntrySetup.cs`
- Create: `Quartermaster.Server/Forwarding/Endpoints/SageSetupStatusEndpoint.cs` (`GET /api/forwarding/sage-setup`, same permission check as `ForwardingChapterSaveEndpoint`)
- Test: `Quartermaster.Server.Tests/Forwarding/SageEntrySetupTests.cs`, `Quartermaster.Server.Tests/Forwarding/ForwardingRepositoryTests.cs`

**Interfaces:**
- Produces: `SageEntrySetup(OptionRepository options, ChapterRepository chapters, ForwardingSettingsRepository settings, ForwardingSettingsResolver resolver)`; `List<SageSetupGap> Missing()` (in enum order, empty when complete); `bool IsComplete()`; `ForwardingTarget? FederalApplicationTarget()`; `TimeSpan CheckInterval()` (option value clamped to at least 5 minutes, default 15). Registered scoped in `Program.cs`.
- Produces: `ForwardingStepKind.AwaitSageEntry`, the three target fields on entity and DTO.

- [ ] **Step 1: Write failing tests** in `SageEntrySetupTests` (repository test base, seeds a root chapter as federal):
  - `Nothing_set_up_lists_every_gap`: `Missing()` equals all five gaps.
  - `Complete_with_status_only`: switch on, federal connection, federal `Application` target with `SageAssigneeId = 12`, `SageDoneStatusId = 5` → `Missing()` empty, `IsComplete()` true.
  - `Complete_with_reassignment_only`: as above but `SageDoneStatusId = null`, `SageAwaitReassignment = true` → complete.
  - `Group_without_signal_is_incomplete`: `Missing()` equals `[DoneSignal]`.
  - `Check_interval_defaults_and_clamps`: no value → 15 min; "2" → 5 min; "30" → 30 min.
  - In `ForwardingRepositoryTests`: `Sage_fields_round_trip` saves and reads the three fields.
- [ ] **Step 2: Build and run the two classes**; expect compile errors or failures for the missing members.
- [ ] **Step 3: Implement** the enum values, DTO and entity fields, migration columns, option definition, catalog key, `SageEntrySetup` and the status endpoint. Check that `ForwardingChapterSaveEndpoint` passes the new DTO fields through `ForwardingTarget.FromDto` (it should, via the mapping).
- [ ] **Step 4: ALTER the dev DB**: `ALTER TABLE ForwardingTargets ADD COLUMN SageAssigneeId INT NULL, ADD COLUMN SageDoneStatusId INT NULL, ADD COLUMN SageAwaitReassignment TINYINT(1) NOT NULL DEFAULT 0;`
- [ ] **Step 5: Run the tests**; expect all green, plus the settings catalog test.

### Task 2: Membership applications only with complete setup

**Files:**
- Modify: `Quartermaster.Server/PublicForms/PublicFormSettings.cs` (inject `SageEntrySetup`; `IsMembershipApplicationReady()`; `IsMembershipApplicationEnabled()` = public switch AND ready)
- Modify: `Quartermaster.Server/MembershipApplications/MembershipApplicationCreateEndpoint.cs` (before the existing public check: if not ready, `ThrowError(I18nKey.Error.Admin.Application.NotSetUp)` with 400, for everyone)
- Modify: i18n files and `I18nKey.cs` (`error.admin.application.not_set_up`: "Mitgliedsanträge sind noch nicht eingerichtet." / "Membership applications are not set up yet.")
- Create: `Quartermaster.Server.Tests/Infrastructure/SageSetupSeeder.cs` (static `Seed(...)` that turns application forwarding on, gives the root chapter a connection to `https://redmine.invalid` and an `Application` target with `SageAssigneeId = 12`, `SageDoneStatusId = 5`, and sets `ApplicationMode = Quartermaster` on a given handling chapter)
- Modify tests that submit applications: `MembershipApplicationCreateEndpointTests`, `ApplicationForwardingTests`, `MembershipApplicationLinkDivisionEndpointTests`, `PublicFormTests`, `ApplicationReceivedMailTests`, `ApplicationSubmittedNotificationTests`, `AnonymousCreateRateLimitTests`
- Test: `MembershipApplicationCreateEndpointTests`, `ClientConfigEndpointTests`, `PublicFormTests`

**Interfaces:**
- Consumes: `SageEntrySetup.IsComplete()` (Task 1), `ForwardingOptionKeys.MembershipApplicationEnabled`.
- Produces: `PublicFormSettings.IsMembershipApplicationReady()`; `SageSetupSeeder.Seed(...)` for later integration tests.

- [ ] **Step 1: Write failing tests**:
  - `Refused_without_setup` (create endpoint, public switch on, no forwarding): 400 with the `NotSetUp` message.
  - `Hand_entry_refused_without_setup`: as a user with `ProcessApplications`, form switch off, no setup → 400 `NotSetUp`.
  - `Accepted_with_setup`: after `SageSetupSeeder.Seed`, 200 and the application is stored.
  - `ClientConfig_closes_form_without_setup`: switch on, no setup → `MembershipApplicationEnabled == false`; with setup → true.
- [ ] **Step 2: Run them**; expect failures.
- [ ] **Step 3: Implement** the gate and the error key.
- [ ] **Step 4: Move the listed test classes onto `SageSetupSeeder`.** Where a test asserted behaviour without forwarding (no case, immediate mail), seed the setup and update the assertion to the forwarding behaviour; list every assertion that changed meaning in the task report.
- [ ] **Step 5: Run the listed classes**; expect all green.

### Task 3: Redmine reads

**Files:**
- Modify: `Quartermaster.Server/Forwarding/Redmine/RedmineResources.cs` (`public record RedmineIssueState(int StatusId, int? AssigneeId);`)
- Modify: `IRedmineClient.cs`, `HttpRedmineClient.cs`
- Modify: `Quartermaster.Server.Tests/Forwarding/Fakes/FakeRedmineClient.cs` (`CurrentUserId = 42`, login "qm-bot"; `GetIssueAsync` reads the stored issue's `StatusId`/`AssigneeId`, throws `RedmineException(..., NotFound, 404)` for an unknown id; method names "Issue" and "CurrentUser" for `ThrowOn`)
- Test: `Quartermaster.Server.Tests/Forwarding/HttpRedmineClientTests.cs`

**Interfaces:**
- Produces: `Task<RedmineIssueState> GetIssueAsync(RedmineConnectionSettings settings, int issueId, CancellationToken ct)` (`GET /issues/{id}.json`, `issue.status.id`, `issue.assigned_to.id` or null); `Task<RedmineIdName> GetCurrentUserAsync(RedmineConnectionSettings settings, CancellationToken ct)` (`user.id`, `user.login`). `GetCurrentUserNameAsync` returns `GetCurrentUserAsync(...).Name`.

- [ ] **Step 1: Write failing tests**: `Get_issue_reads_status_and_assignee` (URL `https://redmine.invalid/issues/77.json`, status 5, assignee 42), `Get_issue_without_assignee` (null), `Current_user_reads_id_and_login` (3, "qm-bot"), `Get_issue_refuses_redirect` (expects `InsecureUrl` or the code the existing redirect test expects).
- [ ] **Step 2: Run**; expect compile failure.
- [ ] **Step 3: Implement** through the existing `SendAsync`/`Property` helpers.
- [ ] **Step 4: Run `HttpRedmineClientTests`**; expect green.

### Task 4: Queue the waiting step and hold the approval mail

**Files:**
- Modify: `Quartermaster.Server/Forwarding/ForwardingIntake.cs` (`Steps()`: for `notePurpose == ForwardingPurpose.Application` with `NoteAfterDecision`, add an `AwaitSageEntry` step after the note, `State = Waiting`, same target chapter)
- Modify: `Quartermaster.Server/Forwarding/ForwardingOutcomeHook.cs` (release `AwaitSageEntry` together with `AddOutcomeNote`; when the decided case is a due selection case with a `MembershipApplicationId`, also release the application's cases, but only if that application is `Approved` or `Rejected`)
- Create: `Quartermaster.Server/Forwarding/SageEntry/SageEntryGate.cs`, `Quartermaster.Server/Forwarding/SageEntry/ReductionOutcome.cs` (enum)
- Modify: `Quartermaster.Server/Motions/MotionResolutionDecisionMailer.cs`, `Quartermaster.Server/Admin/MembershipApplicationProcessEndpoint.cs` (skip `SendApplicationDecisionAsync` when `HoldsApprovalMail`)
- Test: `ForwardingIntakeTests`, `ForwardingOutcomeHookTests`, `Quartermaster.Server.Tests/Forwarding/SageEntryGateTests.cs`, `Integration/Admin/MembershipApplicationProcessEndpointTests.cs`

**Interfaces:**
- Produces: `enum ReductionOutcome { None, Pending, Approved, Rejected, Outside }`.
- Produces: `SageEntryGate(ForwardingCaseRepository cases, MembershipApplicationRepository applications, DueSelectionRepository dueSelections)`:
  - `bool HoldsApprovalMail(Guid applicationId)`: true when the application is `Approved` and one of its application cases has an `AwaitSageEntry` step.
  - `ReductionOutcome ReductionOf(MembershipApplication application)`: from the application's own due record: none or not reduced → `None`; `Pending` → `Pending`; `Approved`/`Rejected` → same; `Forwarded` → `Outside`; `AutoApproved` → `None`.
  - Registered scoped. Task 7 adds `StatusOf`.

- [ ] **Step 1: Write failing tests**:
  - Intake: `Quartermaster_handled_application_queues_note_and_sage_step` (steps: CreateTicket, AddOutcomeNote Waiting, AwaitSageEntry Waiting); `Linked_application_queues_sage_step`; `Redmine_handled_application_has_no_sage_step`; `Reduction_case_has_no_sage_step`.
  - Hook: `Release_frees_note_and_sage_step`; `Reduction_decision_releases_decided_application`; `Reduction_decision_leaves_undecided_application_waiting`.
  - Gate: `Holds_only_approved_with_sage_step`; `Reduction_outcome_per_due_status` (one assertion per status above).
  - Process endpoint: `Approval_with_sage_step_sends_no_mail`; `Rejection_mails_at_once`.
- [ ] **Step 2: Run**; expect failures.
- [ ] **Step 3: Implement.**
- [ ] **Step 4: Run** the five classes plus `ForwardingDirectDecisionTests` and `ApplicationForwardingTests`; expect green.

### Task 5: SAGE request template, partial due and failure mail wording

**Files:**
- Modify: `Quartermaster.Api/Forwarding/ForwardingTemplateModels.cs` (`ForwardingSageModel` with string properties `ProvisionalMemberNumber`, `EntryDate`, `EntryYear`, `EntryMonth`, `YearlyAmount`, `PartialAmount`, `ReductionResult`, `PartialAmountIfReduced`, `DoneStatus`, `DoneAccount`; `ForwardingFailureModel.Action`)
- Create: `Quartermaster.Server/Forwarding/SageEntry/SageDue.cs`
- Modify: `Quartermaster.Server/Forwarding/ForwardingTicketRenderer.cs` (`SageRequestTemplate = "forwarding.redmine.sage_request"`; `RenderSageRequestAsync`; `failure.Action` by step kind: CreateTicket "angelegt", AddOutcomeNote "um die Entscheidung ergänzt", AwaitSageEntry "auf die SAGE-Eintragung geprüft")
- Modify: `Quartermaster.Data/Templates/SystemTemplateSeeds.cs` (new seed "Redmine-Notiz: Bitte um SAGE-Eintragung", no subject, `AllowsChapterFields: false`, body exactly as in the spec; failure mail body "nicht angelegt werden" → "nicht {{ failure.Action }} werden")
- Modify: `Quartermaster.Data/Migrations/M009.cs` (REPLACE that phrase in `forwarding.failure.email` with a `NOT LIKE '%failure.Action%'` guard; Down reverses)
- Modify: `Quartermaster.Rendering/TemplateMockDataProvider.cs` (`sage` sample: "RM4711", "01.11.2026", "2026", "November", "120", "20", "", "", "In SAGE erfasst", "qm-bot"; `failure.Action` "angelegt")
- Test: `Quartermaster.Server.Tests/Forwarding/SageDueTests.cs`, `ForwardingTicketRendererTests`, the existing seed/migration body test

**Interfaces:**
- Produces: `static decimal SageDue.Partial(decimal yearly, DateOnly entryDate)`; `static string SageDue.MonthName(int month)` (German, de-DE culture).
- Produces: `Task<string> RenderSageRequestAsync(ForwardingCase forwardingCase, ForwardingStep step, MembershipApplicationDTO application, ForwardingSageModel sage)`: model with `application` (`ApplicationModel`), `dues` (`DuesModel` of `application.DueSelection`, empty model when null), `case`, `sage`; template resolved for `step.TargetChapterId`; trimmed result.

- [ ] **Step 1: Write failing tests**:
  - `SageDueTests`: `Partial(120, 2026-01-15) == 120`, `Partial(120, 2026-10-01) == 30`, `Partial(120, 2026-12-31) == 10`, `Partial(100, 2026-11-01) == 16.67m`; `MonthName(11) == "November"`.
  - Renderer: `Sage_request_lists_number_entry_and_partial_due` (contains "Vorläufige Mitgliedsnummer: RM1001", "Gewünschtes Eintrittsdatum: 01.11.2026", "Beitrag 2026 anteilig ab November: 20 €"); `Sage_request_with_open_reduction_names_both_amounts` ("6 € bei Genehmigung der Minderung, sonst 20 €"); `Sage_request_done_hint_status_only` / `_account_only` / `_both` (the closing sentence per spec); `Failure_mail_names_the_action` for each step kind.
  - Seed test: migrated failure mail body equals the seed.
- [ ] **Step 2: Run**; expect failures.
- [ ] **Step 3: Implement.** Verify the migration on the dev DB by hand: run the REPLACE once, compare the stored failure mail body with the seed (byte-identical), as done for the other M009 templates.
- [ ] **Step 4: Run** the three classes; expect green.

### Task 6: The job writes the SAGE request and waits for the entry

**Files:**
- Create: `Quartermaster.Server/Forwarding/SageEntry/SageEntryNote.cs`, `Quartermaster.Server/Forwarding/SageEntry/SageEntryCheck.cs`
- Modify: `Quartermaster.Server/Forwarding/ForwardingJob.cs` (constructor gets `SageEntryGate`, `SageEntryNote`, `SageEntryCheck`, `SageEntrySetup`; dispatch `AwaitSageEntry`; approval branch of `AddNoteAsync`)
- Modify: `Quartermaster.Server/Program.cs` (register the new services scoped)
- Test: `ForwardingJobTests` (setup gains the federal `Application` target with SAGE fields and the new services)

**Interfaces:**
- Consumes: Tasks 1, 3, 4, 5.
- Produces: `SageEntryNote.BuildAsync(ForwardingCase forwardingCase, ForwardingStep step, MembershipApplication application, int federalTicketId, ForwardingTarget target, RedmineConnectionSettings connection, CancellationToken ct) -> Task<string>`: fills `ForwardingSageModel` (yearly amount by `ReductionOutcome`: `Approved` → reduced amount, `Rejected`/`Outside` → `SelectedDue`, `None` → `DueAmount.Effective`; `PartialAmountIfReduced` only for `Outside`; `ReductionResult` "genehmigt"/"abgelehnt"/"offen"/""; `DoneStatus` from `ListIssueStatusesAsync` by `SageDoneStatusId`; `DoneAccount` from `GetCurrentUserAsync` when `SageAwaitReassignment`) and returns `RenderSageRequestAsync`.
- Produces: `SageEntryCheck.IsEnteredAsync(ForwardingTarget target, int federalTicketId, RedmineConnectionSettings connection, CancellationToken ct) -> Task<bool>` (status matches when set AND assignee equals current user id when required).

Job behaviour:
- `AddNoteAsync`, decision `Approved` on an `Application` case:
  - reduction `Pending` → step back to `Waiting`, no attempt, return false;
  - setup incomplete or target fields missing → `RedmineException(NotConfigured)`;
  - note = outcome note + "\n\n" + SAGE request;
  - update `RedmineIssueUpdate(note, null, target.SageAssigneeId)`.
- `AddNoteAsync`, any other decision: unchanged.
- `AwaitSageEntry` step:
  - decision missing or not `Approved` → `Skipped`;
  - not entered → `Pending`, `NextAttemptAt = now + CheckInterval()`, `Attempts = 0`, `LastError = null`, stop the case loop;
  - entered → `SendApplicationDecisionAsync` (the job also takes `MembershipApplicationMailService`), then `Done`;
  - Redmine errors → `RecordFailureAsync`.

- [ ] **Step 1: Write failing tests** in `ForwardingJobTests` (FakeRedmineClient issue state set directly on `FakeRedmineClient.Issues`):
  - `Approval_note_adds_sage_request_and_assigns_group` (note contains outcome and "Bitte um Eintragung in SAGE"; `AssigneeId == 12`; `StatusId` unchanged).
  - `Rejection_note_uses_note_status_and_skips_sage_step` (no SAGE text, `NoteStatusId`/`NoteAssigneeId` applied, await step `Skipped`).
  - `Waits_and_reschedules_without_counting_attempts` (status not done → `Pending`, `NextAttemptAt == now + 15 min`, `Attempts == 0`, no mail in `_mails`).
  - `Status_signal_sends_mail` and `Reassignment_signal_sends_mail` (one approval mail to the applicant, step `Done`, case `Done`).
  - `Both_signals_needed_when_both_set` (status only → no mail; then assignee 42 → mail).
  - `Mail_sent_once_when_check_runs_twice` (two runs after done → one mail).
  - `Check_error_retries_then_fails_and_keeps_mail_held` (`ThrowOn["Issue"]` → attempts 1, 2, then `Failed`, failure mail, no approval mail).
  - `Missing_setup_fails_note_and_holds_mail` (remove `SageAssigneeId` → note `Failed` at once with `NotConfigured`).
  - `Note_waits_for_reduction_then_uses_rejection_default` (pending reduction → note `Waiting`; reject the due record, hook release, run → note contains the partial due of the rejection default).
  - `Outside_reduction_lists_both_amounts`.
  - `Check_interval_comes_from_setting` ("30" → +30 min).
- [ ] **Step 2: Run `ForwardingJobTests`**; expect failures.
- [ ] **Step 3: Implement.**
- [ ] **Step 4: Run** `ForwardingJobTests`, `ForwardingIntakeTests`, `ForwardingOutcomeHookTests`, `ApplicationForwardingTests`, `ForwardingDirectDecisionTests`; expect green.

### Task 7: Officer display

**Interfaces:**
- Produces: `Quartermaster.Api/MembershipApplications/ApprovalMailState.cs` (`enum ApprovalMailState { None, WaitingForReduction, WaitingForSage, Sent }`); `public record ApprovalMailStatus(ApprovalMailState State, DateTime? SentAt, string? FederalTicketUrl);` in `Quartermaster.Server/Forwarding/SageEntry/`; `SageEntryGate.StatusOf(Guid applicationId) -> ApprovalMailStatus`.

**Files:**
- Modify: `Quartermaster.Api/MembershipApplications/MembershipApplicationDetailDTO.cs` (`ApprovalMailState ApprovalMailState`, `DateTime? ApprovalMailSentAt`, `string? FederalTicketUrl`)
- Modify: `Quartermaster.Server/Admin/MembershipApplicationDetailEndpoint.cs` (fill them from `SageEntryGate.StatusOf`)
- Modify: `Quartermaster.Blazor/Pages/Administration/MembershipApplicationDetail.razor(.cs)` (info alert per state, ticket link)
- Modify: `Quartermaster.Blazor/Pages/Administration/Forwarding/ForwardingCases.razor(.cs)` (`StepTitle` for `AwaitSageEntry`; while `Pending` with `NextAttemptAt`, "Nächste Prüfung: {time}")
- Modify: i18n files, `I18nKey.cs`
- Test: `Integration/Admin/MembershipApplicationDetailEndpointTests.cs` (create if missing), `Quartermaster.Server.Tests/Forwarding/SageEntryGateTests.cs`

UI copy (German / English):
- `ui.forwarding_cases.sage_step`: "Wartet auf SAGE-Eintragung" / "Waiting for the SAGE entry"
- `ui.forwarding_cases.next_check`: "Nächste Prüfung: {time}" / "Next check: {time}"
- `ui.application_detail.approval_waiting_sage`: "Angenommen. Die Aufnahmebestätigung wird nach der SAGE-Eintragung verschickt." / "Approved. The confirmation is sent once the member is entered in SAGE."
- `ui.application_detail.approval_waiting_reduction`: "Angenommen. Wartet auf die Entscheidung über die Beitragsminderung." / "Approved. Waiting for the decision on the due reduction."
- `ui.application_detail.approval_sent`: "Aufnahmebestätigung verschickt am {date}" / "Confirmation sent on {date}"
- `ui.application_detail.federal_ticket`: "Ticket der Bundesebene" / "Federal ticket"

- [ ] **Step 1: Write failing tests**: gate `Status_per_step_state` (no step → `None`; approved with note waiting and reduction pending → `WaitingForReduction`; note done, await pending → `WaitingForSage`; await done → `Sent` with `SentAt == step.UpdatedAt`); endpoint `Detail_reports_waiting_for_sage_with_ticket_url`.
- [ ] **Step 2: Run**; expect failures.
- [ ] **Step 3: Implement** server and UI.
- [ ] **Step 4: Run** the classes; build the full solution; expect green and no warnings in touched files.

### Task 8: Setup UI, form notice, changelog

**Files:**
- Modify: `Quartermaster.Blazor/Components/Forwarding/ForwardingTargetEditor.razor(.cs)` (parameter `ShowSageFields`; block "SAGE-Eintragung": group id number input, done status select from `Lookups.Statuses` with a number fallback like `NoteStatusId`, reassignment checkbox, hint)
- Modify: `Quartermaster.Blazor/Components/Forwarding/ForwardingChapterEditor.razor` (`ShowSageFields="@(Setting.IsFederal && purpose == ForwardingPurpose.Application)"`)
- Modify: `Quartermaster.Blazor/Pages/Administration/Settings/ForwardingSetup.razor(.cs)` (check interval input next to the thresholds, saved with them, client-side minimum 5)
- Modify: `Quartermaster.Blazor/Pages/Administration/Settings/PublicFormsSetup.razor(.cs)` (load `/api/forwarding/sage-setup`; while the membership switch is on and `Missing` is not empty, a warning under the switch naming the gaps, link to `/Administration/Setup/Forwarding`)
- Modify: i18n files, `I18nKey.cs`
- Modify: `Quartermaster.Documentation/Changelog/V9.0.md` (section "Mitgliedsantrag": approval confirmation only after the SAGE entry; membership form needs the forwarding setup)
- Modify: `todo.md` (tick the SAGE item with "*(V9.0)*")
- Test: `Integration/Forwarding/ForwardingSettingsEndpointTests.cs` (`Sage_fields_saved_through_chapter_endpoint`, `Sage_setup_status_lists_gaps`, `Sage_setup_status_needs_permission`)

UI copy (German / English):
- `ui.forwarding_setup.sage_title`: "SAGE-Eintragung" / "SAGE entry"
- `ui.forwarding_setup.sage_group`: "SAGE-Gruppe (ID)" / "SAGE group (ID)"
- `ui.forwarding_setup.sage_done_status`: "Erledigt-Status" / "Done status"
- `ui.forwarding_setup.sage_reassignment`: "Erledigt, wenn das Ticket wieder dem Quartermaster-Konto zugewiesen ist" / "Done when the ticket is assigned back to the Quartermaster account"
- `ui.forwarding_setup.sage_hint`: "Nach einer Annahme wird das Ticket dieser Gruppe zugewiesen, mit der Bitte um Eintragung in SAGE. Die Aufnahmebestätigung geht raus, sobald das Ticket den gewählten Status hat und, falls angehakt, wieder dem Quartermaster-Konto zugewiesen ist. Für Gruppen muss in Redmine „Zuweisung an Gruppen erlauben“ eingeschaltet sein." / "After an approval the ticket is assigned to this group with a request to enter the member in SAGE. The confirmation goes out once the ticket has the chosen status and, if ticked, is assigned back to the Quartermaster account. Assigning to groups needs \"Allow issue assignment to groups\" in Redmine."
- `ui.forwarding_setup.sage_check_minutes`: "SAGE-Prüfung alle (Minuten)" / "Check for the SAGE entry every (minutes)"
- `ui.public_forms_setup.membership_not_ready`: "Mitgliedsanträge sind noch nicht eingerichtet, das Formular bleibt geschlossen. Es fehlt: {missing}." / "Membership applications are not set up yet, the form stays closed. Missing: {missing}."
- `ui.public_forms_setup.open_forwarding`: "Zur Weiterleitung" / "Open forwarding"
- Gap labels `ui.public_forms_setup.gap.*`: forwarding_switch "Weiterleitung von Mitgliedsanträgen" / "forwarding of membership applications"; federal_connection "Redmine-Verbindung der Bundesebene" / "federal Redmine connection"; federal_target "Zielprojekt für Mitgliedsanträge der Bundesebene" / "federal target project for applications"; sage_group "SAGE-Gruppe" / "SAGE group"; done_signal "Erledigt-Signal" / "done signal"

- [ ] **Step 1: Write failing endpoint tests** as listed.
- [ ] **Step 2: Run**; expect failures.
- [ ] **Step 3: Implement** UI, endpoint wiring, changelog and todo.
- [ ] **Step 4: Run** `ForwardingSettingsEndpointTests`, `ClientConfigEndpointTests`, the settings catalog test; build the full solution; restart the dev server (stop by PID) and check `/api/config/client` and `/api/forwarding/sage-setup` with curl.
- [ ] **Step 5: Self-review** the whole diff against `CLAUDE.md` and grep the touched files for em/en dashes and `lock (`.
