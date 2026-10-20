# Redmine Forwarding Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Route membership applications and due selections into Redmine tickets (federal always, chapters by setting) or Quartermaster motions, with per-chapter Redmine connections, templates, a retrying job, a failure list and two setup assistants.

**Architecture:** A pure `ForwardingRouter` turns a submission plus the resolved chapter settings into a plan (Quartermaster actions and ticket steps). `ForwardingIntake` executes the Quartermaster part in the request and stores a `ForwardingCase` with ordered `ForwardingSteps`. `ForwardingJob` (hosted, every minute) creates tickets through `IRedmineClient`, retries three times, then mails and lists failures. Motion resolution activates the outcome note step.

**Tech Stack:** .NET 10, FastEndpoints, LinqToDB + FluentMigrator (MariaDB), Blazor WASM, Fluid templates, TUnit.

**Spec:** `Quartermaster.Documentation/plans/2026-10-20-redmine-forwarding-design.md`

## Global Constraints

- Follow `CLAUDE.md`; no git commands at all; no commits.
- Never contact a real Redmine from dev or tests; `FakeRedmineClient` or a recording `HttpMessageHandler`.
- No em or en dashes anywhere; English identifiers and routes, German only in UI texts (i18n keys in `I18nKey.cs`, `de.json`, `en.json`).
- Migration `Quartermaster.Data/Migrations/M007.cs`, `class M007`, `[Migration(010)]`; new migration, the dev server applies it on startup.
- Build `nice -n 10 dotnet build Quartermaster.sln -m:1 -v:q -nologo`; tests from `Quartermaster.Server.Tests` with `dotnet run --no-build -- --treenode-filter "/*/<Namespace>/<Class>/*"`; never the full suite.
- Options: `dues.regular_minimum` (72), `dues.state_decision_minimum` (12), `forwarding.membership_application.enabled` (false), `forwarding.due_selection.enabled` (false).
- Retries: 3 attempts (now, +5 min, +15 min); 401/403/422 fail at once.
- Reference line in every description: `Quartermaster-Vorgang QM-<first 8 hex of case id, upper case>`.
- Redmine API key is write-only: never in a DTO, a log or an error.

## Review Focus

- A user without a linked member submits a due selection: it routes to F, fields stay editable, the case records "entered by hand". Pinned in Task 8 (`Due_selection_without_member_goes_to_federal_and_is_marked_by_hand`).
- A browser sends an application with a foreign `ChapterId`: the server's chapter wins. Pinned in Task 9 (`Client_chapter_is_ignored`).
- The server crashes after Redmine created a ticket: the next run adopts it, no duplicate. Pinned in Task 7 (`In_flight_step_adopts_ticket_found_by_reference`).
- A chapter is set to Redmine but its connection or target is missing: saving is refused; if it happens anyway (connection deleted later), the step fails with a clear error and the failure mail. Pinned in Task 10 (`Redmine_mode_without_connection_is_refused`) and Task 7 (`Missing_connection_fails_permanently`).
- The federal ticket step fails for good: the chapter step never runs, the payload stays until retry or manual resolution. Pinned in Task 7 (`Chapter_step_waits_for_federal_ticket`).

---

## Phase 1: data and Redmine client

### Task 1: Data model and migration

**Files:**
- Create: `Quartermaster.Api/Forwarding/ForwardingEnums.cs` (enums `ForwardingPurpose { Regular = 0, Reduction = 1, Application = 2 }`, `ForwardingMode { Quartermaster = 1, Redmine = 2 }`, `ForwardingCaseKind { Application = 0, DueSelection = 1 }`, `ForwardingCaseStatus { Open = 0, Done = 1, Failed = 2, ManuallyResolved = 3 }`, `ForwardingStepKind { CreateTicket = 0, AddOutcomeNote = 1 }`, `ForwardingStepState { Waiting = 0, Pending = 1, InFlight = 2, Done = 3, Failed = 4, Skipped = 5 }`)
- Create: `Quartermaster.Data/Forwarding/{RedmineConnection,ForwardingTarget,ForwardingChapterSetting,ForwardingCase,ForwardingStep}.cs`
- Create: `Quartermaster.Data/Forwarding/{RedmineConnectionRepository,ForwardingSettingsRepository,ForwardingCaseRepository}.cs`
- Create: `Quartermaster.Data/Migrations/M007.cs`
- Modify: `Quartermaster.Data/DbContext.cs` (tables, `AddRepositories`), `Quartermaster.Data/DueSelector/DueSelection.cs` (`Guid? ChapterId`), `Quartermaster.Api/DueSelector/DueSelectionStatus.cs` (append `Forwarded = 4`), `Quartermaster.Data/Options/OptionRepository.cs` (four options)
- Test: `Quartermaster.Server.Tests/Forwarding/ForwardingRepositoryTests.cs`

**Interfaces (Produces):**
- `RedmineConnection { Guid ChapterId (PK); string BaseUrl; string ApiKey; DateTime UpdatedAt }`.
- `ForwardingTarget { Guid Id; Guid ChapterId; ForwardingPurpose Purpose; int ProjectId; int TrackerId; int? PriorityId; int? AssigneeId; string CustomFieldsJson = "[]"; int? NoteStatusId; int? NoteAssigneeId }`, unique (ChapterId, Purpose).
- `ForwardingChapterSetting { Guid ChapterId (PK); ForwardingMode? ApplicationMode; ForwardingMode? ReductionMode; string? FailureEmail }`.
- `ForwardingCase { Guid Id; ForwardingCaseKind Kind; Guid? ApplicationChapterId; Guid? DecidingChapterId; Guid? StateChapterId; Guid? RelatedCaseId; Guid? MembershipApplicationId; Guid? DueSelectionId; Guid? MotionId; string? PayloadJson; ForwardingCaseStatus Status; DateTime CreatedAt; DateTime? CompletedAt; string Reference => "QM-" + Id.ToString("N")[..8].ToUpperInvariant() }`.
- `ForwardingStep { Guid Id; Guid CaseId; int Order; ForwardingStepKind Kind; Guid TargetChapterId; ForwardingPurpose Purpose; ForwardingStepState State; int? TicketId; string? TicketUrl; int Attempts; DateTime? NextAttemptAt; string? LastError; DateTime UpdatedAt }`.
- `RedmineConnectionRepository`: `RedmineConnection? Get(Guid chapterId)`, `List<RedmineConnection> List()`, `void Upsert(Guid chapterId, string baseUrl, string? apiKey, DateTime nowUtc)` (null key keeps the stored one), `bool Delete(Guid chapterId)`.
- `ForwardingSettingsRepository`: `ForwardingChapterSetting? GetChapter(Guid)`, `List<ForwardingChapterSetting> ListChapters()`, `void SaveChapter(ForwardingChapterSetting setting, IReadOnlyList<ForwardingTarget> targets)` (replaces the chapter's targets in one transaction), `bool DeleteChapter(Guid)` (setting and targets), `ForwardingTarget? GetTarget(Guid chapterId, ForwardingPurpose)`, `List<ForwardingTarget> ListTargets(Guid chapterId)`.
- `ForwardingCaseRepository`: `void Create(ForwardingCase case, IReadOnlyList<ForwardingStep> steps)`, `ForwardingCase? Get(Guid)`, `List<ForwardingStep> GetSteps(Guid caseId)`, `List<Guid> ListCasesWithDueSteps(DateTime nowUtc, int limit)` (a Pending step with `NextAttemptAt <= now` or null, or an InFlight step), `void UpdateStep(ForwardingStep step)`, `void SetCaseStatus(Guid id, ForwardingCaseStatus status, DateTime? completedAt)`, `void ClearPayload(Guid id)`, `List<ForwardingCase> ListByMotion(Guid motionId)`, `List<ForwardingCase> List(ForwardingCaseStatus? status, ForwardingCaseKind? kind)`.
- Migration: all tables with FKs to `Chapters`; `PayloadJson` as `MEDIUMTEXT`, `CustomFieldsJson` and `LastError` as `TEXT`, `ApiKey` `AsString(512)`, dates `DATETIME(6)`; `DueSelections.ChapterId` nullable with FK.

- [ ] Step 1: failing tests `Upsert_keeps_key_when_null`, `SaveChapter_replaces_targets`, `ListCasesWithDueSteps_includes_pending_due_and_in_flight`, `Reference_is_upper_short_id`.
- [ ] Step 2: build, expect compile failure.
- [ ] Step 3: implement entities, repositories, migration, options (labels "Beiträge: Regelbeitrag ab (EUR/Jahr)", "Beiträge: Entscheidung durch Bundesebene unter (EUR/Jahr)", "Weiterleitung: Mitgliedsanträge an Redmine", "Weiterleitung: Beitragseinstufungen an Redmine").
- [ ] Step 4: run the tests and `AssemblyMotionRepositoryTests` as a schema smoke test; restart the dev server and confirm migration 10 ran.

### Task 2: Redmine client

**Files:**
- Create: `Quartermaster.Server/Forwarding/Redmine/{IRedmineClient,HttpRedmineClient,RedmineException,RedmineResources,RedmineConnectionSettings}.cs`
- Modify: `Program.cs` (named client `"redmine"` with `AllowAutoRedirect = false`, singleton client)
- Test: `Quartermaster.Server.Tests/Forwarding/HttpRedmineClientTests.cs`, `Fakes/RecordingRedmineHandler.cs`

**Interfaces (Produces):**
- `record RedmineConnectionSettings(string BaseUrl, string ApiKey)` with `bool IsSecureUrl()` (via `WikiBotSettings.IsSecureUrl`), `string IssueUrl(int id)`, key-free `ToString`.
- `record RedmineIdName(int Id, string Name)`; `record RedmineCustomFieldValue(int Id, string Value)`; `record RedmineIssueCreate(int ProjectId, int TrackerId, string Subject, string Description, int? PriorityId, int? AssigneeId, IReadOnlyList<RedmineCustomFieldValue> CustomFields)`; `record RedmineIssueUpdate(string Notes, int? StatusId, int? AssigneeId)`.
- `RedmineException(string message, string code, int? statusCode = null)`; codes `Unauthorized` (401/403), `NotFound`, `Rejected` (422, message = joined `errors`), `InsecureUrl`, `HttpError`, `Timeout`, `InvalidResponse`.
- `IRedmineClient`: `Task<string> GetCurrentUserNameAsync(settings, ct)` (`GET /users/current.json`, `user.login`), `Task<List<RedmineIdName>> ListProjectsAsync` (`/projects.json?limit=100`, follows `offset` paging), `ListTrackersAsync` (`/trackers.json`), `ListIssueStatusesAsync` (`/issue_statuses.json`), `ListIssuePrioritiesAsync` (`/enumerations/issue_priorities.json`), `Task<int> CreateIssueAsync(settings, RedmineIssueCreate, ct)` (`POST /issues.json` body `{"issue":{project_id,tracker_id,subject,description,priority_id?,assigned_to_id?,custom_fields:[{id,value}]}}`, returns `issue.id`), `Task UpdateIssueAsync(settings, int id, RedmineIssueUpdate, ct)` (`PUT /issues/{id}.json` `{"issue":{notes,status_id?,assigned_to_id?}}`), `Task<int?> FindIssueByReferenceAsync(settings, int projectId, string reference, ct)` (`GET /issues.json?project_id=..&status_id=*&description=~<reference>&limit=1`).
- Header `X-Redmine-API-Key`, `Accept: application/json`, timeout 30 s; bodies never logged; 422 messages redacted for the key.

- [ ] Step 1: recording handler (copy of `RecordingDiscourseHandler`).
- [ ] Step 2: failing tests: `Sends_api_key_header`, `Create_issue_body_contains_all_fields`, `Optional_fields_are_left_out`, `Update_issue_sends_notes_status_and_assignee`, `Projects_follow_paging`, `Find_by_reference_queries_description_filter`, `Status_codes_are_translated` (401, 403, 404, 422, 500), `Insecure_url_sends_nothing`, `Key_never_appears_in_logs`.
- [ ] Step 3: implement; register in `Program.cs`.
- [ ] Step 4: run `HttpRedmineClientTests`.

## Phase 2: routing, intake, rendering, job

### Task 3: Settings resolution and router

**Files:**
- Create: `Quartermaster.Server/Forwarding/{ForwardingSettingsResolver,ForwardingRouter,ForwardingPlan,DueAmount}.cs`
- Test: `Quartermaster.Server.Tests/Forwarding/ForwardingRouterTests.cs`, `ForwardingSettingsResolverTests.cs`

**Interfaces (Produces):**
- `static class DueAmount`: `decimal Effective(DueSelectionDTO dto)` (Reduced: `ReducedAmount`, else `SelectedDue`), `bool NeedsDecision(DueSelectionDTO dto, decimal regularMinimum)` (`SelectedValuation == Reduced && Effective < regularMinimum`).
- `record ForwardingChapterContext(Guid? FederalId, Guid? StateId, ForwardingMode? FederalApplicationMode, ForwardingMode? FederalReductionMode, ForwardingMode? StateReductionMode, Guid? HandlingChapterId, ForwardingMode? HandlingApplicationMode)`.
- `ForwardingSettingsResolver(ChapterRepository, ForwardingSettingsRepository, RedmineConnectionRepository)`: `Guid? StateChapterOf(Guid? chapterId)` (ancestor directly below root, or the chapter itself when its parent is root; null for root or null), `(Guid? ChapterId, ForwardingMode? Mode) HandlingChapterFor(Guid? applicationChapterId)` (nearest from A upwards excluding root with an `ApplicationMode`), `RedmineConnection? ConnectionFor(Guid chapterId)` (own, then ancestors incl. root), `string? FailureEmailFor(Guid chapterId)` (own, then ancestors incl. root), `ForwardingChapterContext Build(Guid? memberOrApplicationChapterId, bool isApplication)`.
- `record PlannedTicket(Guid TargetChapterId, ForwardingPurpose Purpose)`; `record ForwardingPlan(IReadOnlyList<PlannedTicket> Tickets, Guid? MotionChapterId, bool StoreRecord, DueSelectionStatus? RecordStatus, bool NoteAfterDecision)`.
- `static class ForwardingRouter`: `ForwardingPlan PlanDueSelection(ForwardingChapterContext ctx, decimal amount, bool isReduced, bool forwardingEnabled, decimal regularMinimum, decimal stateDecisionMinimum, bool partOfApplication)`; `ForwardingPlan PlanApplication(ForwardingChapterContext ctx, Guid? applicationChapterId, bool forwardingEnabled)`. Rules exactly as the spec's Routing section; switches off means every mode counts as `Quartermaster`, no tickets, applications handled in A, non-reduced due selections stored `AutoApproved` (`StoreRecord` true) except when `partOfApplication` (the application record holds it).

- [ ] Step 1: failing router tests, one per spec case: `Regular_due_creates_only_federal_regular_ticket`, `Regular_due_in_application_creates_nothing`, `Reduction_at_state_with_quartermaster_creates_motion_and_note`, `Reduction_at_state_with_redmine_creates_two_tickets_federal_first`, `Reduction_at_state_unset_creates_federal_only`, `Reduction_below_state_minimum_goes_to_federal`, `Reduction_without_state_goes_to_federal`, `Federal_reduction_redmine_has_no_note`, `Application_handling_quartermaster`, `Application_handling_redmine`, `Application_abroad_uses_federal_mode`, `Application_lower_chapter_unset_federal_only`, `Switches_off_store_records_without_tickets`, `Underage_due_needs_no_decision`.
- [ ] Step 2: failing resolver tests (`State_chapter_is_child_of_root`, `Handling_chapter_skips_root`, `Connection_inherits_up_to_root`, `Failure_email_inherits`).
- [ ] Step 3: implement.
- [ ] Step 4: run both classes.

### Task 4: Template model and ticket renderer

**Files:**
- Create: `Quartermaster.Api/Forwarding/ForwardingTemplateModel.cs` (pure data: `ForwardingTemplateModel` with `Application`, `Dues`, `Member`, `Submission`, `Chapter`, `Case`, `FederalTicket`, `Outcome` sub-records exposed as Fluid variables `application`, `dues`, `member`, `submission`, `chapter`, `case`, `federalTicket`, `outcome`)
- Create: `Quartermaster.Server/Forwarding/{ForwardingPayload,ForwardingTicketRenderer}.cs`
- Modify: `Quartermaster.Data/Templates/SystemTemplateSeeds.cs` (five templates), `Quartermaster.Api/Templates/TemplateModelLookup.cs` (`forwarding.` prefix adds `ForwardingTemplateModel`), `Quartermaster.Rendering/TemplateMockDataProvider.cs` (mock for it)
- Test: `Quartermaster.Server.Tests/Forwarding/ForwardingTicketRendererTests.cs`

**Interfaces (Produces):**
- `record ForwardingPayload(MembershipApplicationDTO? Application, DueSelectionDTO? DueSelection, Guid? SubmittedByUserId, string? SubmittedByName, bool EnteredByHand, DateTime SubmittedAt)` serialized into `ForwardingCase.PayloadJson`.
- `record RenderedTicket(string Subject, string Description, IReadOnlyList<RedmineCustomFieldValue> CustomFields)`.
- `ForwardingTicketRenderer(TemplateRepository, ChapterRepository, MotionRepository)`: `Task<RenderedTicket> RenderTicketAsync(ForwardingCase c, ForwardingStep step, ForwardingTarget target, string? federalTicketUrl, int? federalTicketId)`; `Task<string> RenderNoteAsync(ForwardingCase c, Motion motion)`; `Task<(string Subject, string Html)> RenderFailureMailAsync(ForwardingCase c, ForwardingStep step, RenderedTicket? ticket)`. Template identifier by purpose: Regular `forwarding.redmine.due_selection`, Reduction `forwarding.redmine.reduction`, Application `forwarding.redmine.application`; resolved with `TemplateRepository.Resolve(identifier, step.TargetChapterId, chapterRepo)`. Appends `\n\nQuartermaster-Vorgang <Reference>` to every description. Custom fields render each snippet; empty results are left out; invalid `CustomFieldsJson` throws a `RedmineException` with code `Rejected` ("Feldzuordnung ungültig").
- Seed bodies (German, Markdown): application ticket lists all applicant fields, dues and IBAN; due selection and reduction tickets list member number, name, email, amount, valuation, justification, payment data, deciding chapter, and `federalTicket.Url` when set; the note lists result, deciding body, date and motion link; the failure mail shows reference, chapter, purpose, error and the rendered ticket.

- [ ] Step 1: failing tests `Application_ticket_contains_all_fields`, `Reference_line_is_appended`, `Chapter_override_template_wins`, `Custom_fields_render_and_skip_empty`, `Invalid_custom_field_json_is_rejected`, `Chapter_ticket_links_federal_ticket`, `Every_seed_renders_with_mock_data`.
- [ ] Step 2: implement.
- [ ] Step 3: run the class.

### Task 5: Intake

**Files:**
- Create: `Quartermaster.Server/Forwarding/ForwardingIntake.cs`
- Test: `Quartermaster.Server.Tests/Forwarding/ForwardingIntakeTests.cs`

**Interfaces:**
- Consumes: Task 3 resolver and router, Task 1 repositories, `ApplicationReviewService`, `MotionRepository`, `DueSelectionRepository`, `MembershipApplicationRepository`, `OptionRepository`.
- Produces `ForwardingIntake`: `Guid IntakeDueSelection(DueSelectionDTO dto, Guid? memberChapterId, Guid? userId, string? userName, bool enteredByHand)` (returns the case id, or the stored due selection id when switches are off); `Task<Guid?> IntakeApplicationAsync(MembershipApplicationDTO dto, Guid? applicationChapterId, CancellationToken ct)` (returns the application id when stored). Creates Quartermaster records and motions per plan (reduction motion via a new `ApplicationReviewService.CreateReductionMotion(DueSelection, Guid chapterId, MembershipApplication? application)`), the case with `PayloadJson`, the steps (federal ticket Order 1, chapter ticket Order 2, note Order 3 `Waiting` when `NoteAfterDecision`), and links application and reduction cases with `RelatedCaseId`. A reduced due selection of an application stored in Quartermaster whose reduction is decided outside Quartermaster gets status `Forwarded`.

- [ ] Step 1: failing tests `Regular_due_creates_case_without_record`, `Reduction_quartermaster_creates_record_motion_and_waiting_note`, `Application_quartermaster_creates_application_motion_without_reduction`, `Application_with_reduction_creates_two_linked_cases`, `Switches_off_stores_records_like_today`, `Payload_holds_submitted_data`.
- [ ] Step 2: implement.
- [ ] Step 3: run the class.

### Task 6: Outcome hook

**Files:**
- Create: `Quartermaster.Server/Forwarding/ForwardingOutcomeHook.cs`
- Modify: `Quartermaster.Server/Motions/MotionVoteEndpoint.cs`, `MotionStatusEndpoint.cs`, `Quartermaster.Server/Meetings/MeetingLifecycleService.cs` (call `OnMotionResolved(motionId, nowUtc)` after a resolution)
- Test: `Quartermaster.Server.Tests/Forwarding/ForwardingOutcomeHookTests.cs`

**Interfaces:** `ForwardingOutcomeHook(ForwardingCaseRepository, MotionRepository)`: `void OnMotionResolved(Guid motionId, DateTime nowUtc)` sets every `Waiting` note step of cases with that `MotionId` to `Pending` when the motion is no longer `Pending`.

- [ ] Step 1: failing tests `Resolved_motion_activates_note`, `Pending_motion_keeps_note_waiting`, plus an integration assertion in an existing motion vote test that the note becomes pending.
- [ ] Step 2: implement and wire the three call sites.
- [ ] Step 3: run the class and the motion vote tests.

### Task 7: Job and failure mail

**Files:**
- Create: `Quartermaster.Server/Forwarding/{ForwardingJob,ForwardingJobHostedService,ForwardingFailureMailer}.cs`
- Test: `Quartermaster.Server.Tests/Forwarding/ForwardingJobTests.cs`, `Fakes/FakeRedmineClient.cs`

**Interfaces:**
- `ForwardingJob(ForwardingCaseRepository, ForwardingSettingsRepository, ForwardingSettingsResolver, ForwardingTicketRenderer, IRedmineClient, MotionRepository, ForwardingFailureMailer, ILogger)`: `Task<int> RunAsync(DateTime nowUtc, CancellationToken ct)`, batch 20 cases. Per case: the first step that is not Done or Skipped; Waiting stops the case; a chapter ticket needs the federal ticket id. InFlight at the start means a crash: search by reference first. Before create: state InFlight. Success: Done with TicketId and TicketUrl. Failure: attempts+1; permanent codes (`Unauthorized`, `Rejected`, `InsecureUrl`, missing connection or target) or the third attempt mean Failed, case Failed, failure mail; otherwise Pending with `NextAttemptAt` +5 min after the first and +15 min after the second failure. All ticket steps Done: clear payload; case Done when no Waiting or Pending note step is left. Note step: `UpdateIssueAsync` on the federal ticket with the rendered note and the target's note status and assignee.
- `ForwardingFailureMailer(EmailMessageChannel, ForwardingTicketRenderer, ForwardingSettingsResolver, ILogger)`: `Task SendAsync(ForwardingCase c, ForwardingStep step, RenderedTicket? ticket, CancellationToken ct)`; logs a warning and does nothing when no address resolves.
- `ForwardingJobHostedService`: copy of `AssemblyMotionForumSyncHostedService` shape.
- Also `ForwardingCaseActions`: `bool Retry(Guid caseId)` (failed steps back to Pending, attempts 0, case Open), `bool Resolve(Guid caseId, DateTime nowUtc)` (non-Done ticket steps Skipped, a note step without federal ticket Skipped, payload cleared, case ManuallyResolved).

- [ ] Step 1: `FakeRedmineClient` (static state like `FakeDiscourseClient`: issues per base URL with description, notes, status, assignee; ThrowOn by method; counters).
- [ ] Step 2: failing tests `Federal_ticket_then_chapter_ticket_with_link`, `Chapter_step_waits_for_federal_ticket`, `Retries_after_5_and_15_minutes_then_fails_and_mails`, `Rejected_fails_at_once`, `Missing_connection_fails_permanently`, `In_flight_step_adopts_ticket_found_by_reference`, `Payload_cleared_when_tickets_done`, `Note_waits_for_decision_then_updates_federal_ticket`, `Note_sets_status_and_assignee`, `Retry_resets_failed_steps`, `Resolve_clears_payload_and_skips_steps`, `Failure_mail_goes_to_inherited_address`.
- [ ] Step 3: implement and register (scoped job, actions, mailer, resolver, renderer, intake, hook; hosted service).
- [ ] Step 4: run `ForwardingJobTests`.

## Phase 3: Quartermaster flows

### Task 8: Due selection login and locking

**Files:**
- Modify: `Quartermaster.Server/DueSelector/DueSelectionCreateEndpoint.cs` (no `AllowAnonymous`, no pending path; resolve the member by `UserId`; lock member number, name and email from the member; call `ForwardingIntake.IntakeDueSelection`)
- Create: `Quartermaster.Server/DueSelector/DueSelectionPrefillEndpoint.cs` (`GET /api/dueselector/prefill` returning `DueSelectionPrefillDTO { FirstName, LastName, Email, int? MemberNumber, bool Locked }`, DTO in `Quartermaster.Api/DueSelector`)
- Modify: `Quartermaster.Server/Submissions/SubmissionMaterializer.cs` (drop the due selection kind from the confirm path; keep deserialisation of old pending rows working by materialising them through intake with no member)
- Modify: Blazor `DueSelectionWizard.razor(.cs)`, `DueSelectionPersonalDataStep.razor` (login prompt for anonymous visitors with a login link; prefill from the endpoint; member number readonly and disabled when locked)
- Test: `Quartermaster.Server.Tests/Integration/DueSelector/DueSelectionForwardingEndpointTests.cs`

- [ ] Step 1: failing tests `Anonymous_submit_is_401`, `Member_fields_are_taken_from_the_member`, `Due_selection_without_member_goes_to_federal_and_is_marked_by_hand`, `Prefill_locks_for_linked_member`, `Submission_needs_no_confirmation`.
- [ ] Step 2: implement.
- [ ] Step 3: run the class plus existing `Integration.DueSelector` tests (update tests that relied on anonymous submission; record each as a ruling).

### Task 9: Application chapter on the server and intake

**Files:**
- Modify: `MembershipApplicationCreateEndpoint.cs` / `SubmissionMaterializer.MaterializeApplicationAsync` (server chapter from `AddressAdministrativeDivisionId` via `ChapterRepository.FindForDivision`; then `ForwardingIntake.IntakeApplicationAsync`; receipt mail always)
- Modify: `ApplicationReviewService` (application motion without the reduction; new `CreateReductionMotion`)
- Test: `Quartermaster.Server.Tests/Integration/MembershipApplications/ApplicationForwardingTests.cs`

- [ ] Step 1: failing tests `Client_chapter_is_ignored`, `Receipt_mail_is_sent_on_redmine_path`, `Quartermaster_path_creates_motion_without_reduction`, `Reduction_gets_its_own_motion_in_deciding_chapter`.
- [ ] Step 2: implement.
- [ ] Step 3: run the class plus existing `Integration.MembershipApplications` and `Submissions` tests.

## Phase 4: endpoints and UI

### Task 10: Settings endpoints

**Files:**
- Create: `Quartermaster.Api/Forwarding/ForwardingSettingsDTOs.cs` (`RedmineConnectionDTO { ChapterId, ChapterName, BaseUrl, HasApiKey }`, `RedmineConnectionSaveRequest { ChapterId, BaseUrl, string? ApiKey }`, `RedmineLookupDTO { Projects, Trackers, Statuses, Priorities }` of `IdNameDTO`, `ForwardingTargetDTO`, `ForwardingCustomFieldDTO { int FieldId; string Template }`, `ForwardingChapterSettingDTO { ChapterId, ChapterName, IsFederal, IsStateChapter, ApplicationMode, ReductionMode, FailureEmail, Targets }`, `ConnectionCheckResultDTO { Ok, Message }`)
- Create endpoints under `Quartermaster.Server/Forwarding/Endpoints/`: `GET/PUT/DELETE /api/forwarding/redmine-connections[/{ChapterId}]`, `POST /api/forwarding/redmine-connections/{ChapterId}/check`, `GET /api/forwarding/redmine-lookups/{ChapterId}`, `GET /api/forwarding/chapters`, `PUT/DELETE /api/forwarding/chapters/{ChapterId}`; all require global `EditOptions`.
- Validation on `PUT /chapters`: a Redmine mode needs a resolvable connection and a complete target for its purpose (Application, Reduction; F also Regular when the due selection switch is used); `ReductionMode` only for F and state chapters; email format; custom field json well-formed.
- Test: `Quartermaster.Server.Tests/Integration/Forwarding/ForwardingSettingsEndpointTests.cs`

- [ ] Step 1: failing tests `All_routes_require_edit_options`, `Api_key_is_never_returned`, `Empty_key_keeps_stored_key`, `Redmine_mode_without_connection_is_refused`, `Reduction_mode_refused_below_state_level`, `Lookups_use_resolved_connection`, `Check_returns_account_name_without_key`.
- [ ] Step 2: implement.
- [ ] Step 3: run the class.

### Task 11: Case list endpoints

**Files:**
- Create: `Quartermaster.Api/Forwarding/ForwardingCaseDTOs.cs` (`ForwardingCaseListItemDTO { Id, Reference, Kind, Status, ChapterName, CreatedAt, Steps: [{ Purpose, TargetChapterName, State, TicketUrl, LastError, Attempts }] }`)
- Create endpoints: `GET /api/forwarding/cases?status=&kind=`, `POST /api/forwarding/cases/{Id}/retry`, `POST /api/forwarding/cases/{Id}/resolve`; applications need global `ProcessApplications`, due selections global `ProcessDueSelections`; the list returns only the kinds the caller may see; no payload in the DTO.
- Test: `Quartermaster.Server.Tests/Integration/Forwarding/ForwardingCaseEndpointTests.cs`

- [ ] Step 1: failing tests `List_filters_by_permission`, `Retry_requires_matching_permission`, `Resolve_clears_payload`, `Anonymous_is_401`.
- [ ] Step 2: implement.
- [ ] Step 3: run the class.

### Task 12: Blazor pages and assistants

**Files:**
- Create: `Quartermaster.Blazor/Pages/Administration/Settings/RedmineSetup.razor(.cs)` (`/Administration/Setup/Redmine`), `ForwardingSetup.razor(.cs)` (`/Administration/Setup/Forwarding`), `Quartermaster.Blazor/Pages/Administration/Forwarding/ForwardingCases.razor(.cs)` (`/Administration/Forwarding`), `Quartermaster.Blazor/Components/Forwarding/ForwardingTargetEditor.razor(.cs)` (project, tracker, priority, status dropdowns from lookups; assignee and custom field rows)
- Modify: `SettingsAssistantCatalog.cs` (`Redmine` and `Forwarding` assistants in Integrations; Forwarding manages the four options, toggles on the two switches, links to the templates and the case list), navigation entry for the case list behind the two permissions, i18n keys.
- [ ] Step 1: build, restart, seed dummy data in the dev DB (a fake connection row with `https://redmine.invalid`, chapter settings, a few cases in each state) through SQL or the API.
- [ ] Step 2: check every page in Chrome at desktop and phone width: settings hub cards, both assistants (add chapter override, save refused without connection, key field shows "gespeichert" and never the key), case list with retry and resolve, due selection form logged out (login prompt) and logged in (locked fields).
- [ ] Step 3: remove the dummy data again.

## Phase 5: docs and wrap-up

### Task 13: Docs and changelog

- [ ] `Configuration.md`: section "Redmine forwarding" (connection, overrides, routing with examples, assistant, templates and variables, custom fields, failure list, first run checklist with a test project).
- [ ] `Translations.md`: Weiterleitung = forwarding, Vorgang = case, Zielprojekt = target project, Behandlung in Quartermaster/Redmine = handling mode.
- [ ] `Changelog/V7.0.md` (user-visible only).
- [ ] Sweep all changed files against `CLAUDE.md` and for dashes; run the touched test namespaces.
