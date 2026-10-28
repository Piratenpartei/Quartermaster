# Register of rejected applicants Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** New membership applications are checked against a register of rejected and expelled people; a hit, or the applicant's own statement of an earlier rejection, needs the Bundesvorstand's approval after the chapter's.

**Architecture:** A register table plus a hit table, a pure matcher run in `ForwardingIntake.RouteApplication`, and a new application status `PendingFederalApproval` that the motion cascade and the processing dialog use instead of `Approved` while the federal approval is outstanding. A flow service creates the federal motion after the chapter's approval, adds register entries on explicit rejections and handles "Andere Person". A global permission and per-trigger visibility keep everything out of sight of everyone but its holders.

**Tech Stack:** .NET 10, FastEndpoints, LinqToDB + FluentMigrator (MariaDB), Blazor WASM, TUnit, Fluid templates.

**Spec:** `Quartermaster.Documentation/plans/2026-10-25-rejection-register-design.md`

## Global Constraints

- Follow `CLAUDE.md` (K&R, if-body on the next line, guard-clause methods for 3+ term conditions, explicit access modifiers, `using` directives, one class per file, no `@code` in `.razor`, `== null`, never `is X or Y`, `Lock` type, tuples of at most 3 values, minimal comments).
- Code and routes English; only UI text German, in `Quartermaster.Server/wwwroot/i18n/de.json` and `en.json` (edit textually, keep the blank-line grouping), constants in `Quartermaster.Api/I18n/I18nKey.cs`.
- No em or en dashes anywhere.
- Schema changes fold into `Quartermaster.Data/Migrations/M009.cs` (`[Migration(017)]`); the dev DB already ran it, so ALTER it by hand (mysql CLI, user root, DB `quartermaster`).
- Confidentiality: nothing about the register in `Changelog/V9.0.md`, the front page, PiratenWissen or any response a caller without `applicant_register_manage` receives, apart from the boolean "needs federal approval" and the status `PendingFederalApproval`.
- Never contact a real Redmine, mail or Telegram server from tests; use the existing fakes and `.invalid` hosts.
- Never run git; the user commits. Never run the full test suite.
- Build: `taskset -c 0-9 dotnet build Quartermaster.sln`. Tests: `taskset -c 0-9 dotnet run --no-build --project Quartermaster.Server.Tests -- --treenode-filter "/*/*/(ClassA)|(ClassB)/*" --no-progress > log` and read the log.
- Enums persisted as ints: append values only.
- Spec values: three matching fields make a hit; name words within edit distance 1, or 2 for words of 8 letters or more; street within 2 after folding "strasse"/"str"; city within 1; post code, house number (ignoring spaces and case), date of birth and email (ignoring case and surrounding whitespace) equal; empty fields never match.

## Review Focus

- A chapter officer with `applications_process` on the chapter tries to approve an application in `PendingFederalApproval` through the processing dialog: refused with 403, only processors on the root chapter decide. Pinned in Task 5 (`Chapter_cannot_decide_the_federal_step`).
- The vote endpoint, a meeting close and the agenda-item close all resolve the chapter motion; resolving twice or through two paths must create exactly one federal motion. Pinned in Task 5 (`Federal_motion_created_once`).
- The approval mail, the outcome note and the SAGE request after the chapter's approval but before the Bundesvorstand's: all held. Pinned in Task 5 (`Chapter_approval_holds_mail_and_sage`).
- Register data in chapter-level output: the application detail without the permission, the chapter motion text, the notification preferences and the template list. Pinned in Tasks 3, 4 and 5 (`Detail_hides_hits_without_permission`, `Trigger_hidden_without_permission`, `Templates_hidden_without_permission`, `Chapter_motion_names_no_entry`).
- An application waiting for its division (no chapter yet) that has a hit, linked later to a chapter deciding in Redmine: its federal ticket already carries the notice and the deleted application leaves no orphan hits. Pinned in Task 4 (`Waiting_application_notice_survives_linking_to_redmine`).

---

### Task 1: Data model, status and cascade

**Files:**
- Create: `Quartermaster.Api/ApplicantRegister/ApplicantRegisterKind.cs` (`Rejected = 0, Expelled = 1, LeftDuringExpulsion = 2`)
- Create: `Quartermaster.Api/ApplicantRegister/RegisterMatchFields.cs` (`[Flags]`: `None = 0, FirstName = 1, LastName = 2, DateOfBirth = 4, Street = 8, PostCodeCity = 16, Email = 32`)
- Modify: `Quartermaster.Api/MembershipApplications/ApplicationStatus.cs` (append `PendingFederalApproval` with a summary: approved by its chapter, waiting for the Bundesvorstand under § 3 Abs. 2 BS)
- Create: `Quartermaster.Data/ApplicantRegister/ApplicantRegisterEntry.cs` (table `ApplicantRegisterEntries`: `Id`, `Kind`, `FirstName`, `LastName`, `DateOfBirth` (DateTime?), `Street`, `HouseNumber`, `PostCode`, `City`, `Email` (string?), `OccurredOn` (DateTime), `Note` (string?), `ChapterId`, `SourceApplicationId` (Guid?), `CreatedAt`, `CreatedByUserId` (Guid?); max lengths as constants: names 256, street 256, house number 32, post code 16, city 256, email 256, note 2000)
- Create: `Quartermaster.Data/ApplicantRegister/ApplicationRegisterHit.cs` (table `ApplicationRegisterHits`: `Id`, `ApplicationId`, `EntryId`, `MatchedFields` (`RegisterMatchFields`), `CreatedAt`, `DismissedAt` (DateTime?), `DismissedByUserId` (Guid?))
- Create: `Quartermaster.Data/ApplicantRegister/ApplicantRegisterRepository.cs`
- Modify: `Quartermaster.Data/DbContext.cs` (two tables)
- Modify: `Quartermaster.Data/MembershipApplications/MembershipApplication.cs` (`bool RequiresFederalApproval`, `Guid? FederalApprovalMotionId`; method `ApplicationStatus ApprovedStatus(Guid? rootChapterId)`)
- Modify: `Quartermaster.Data/MembershipApplications/MembershipApplicationRepository.cs` (`SetFederalApproval`, `SetFederalApprovalMotion`; `Delete` also deletes the application's hits)
- Modify: `Quartermaster.Data/Motions/MotionRepository.cs` (cascade uses `ApprovedStatus`; `GetByLinkedApplicationId` orders by `CreatedAt` so the chapter motion comes first)
- Modify: `Quartermaster.Data/Migrations/M009.cs`
- Modify: `Quartermaster.Server.Tests/Infrastructure/TestDataBuilder.cs` (`SeedRegisterEntry(...)` with defaults for every field)
- Test: `Quartermaster.Server.Tests/ApplicantRegister/ApplicantRegisterRepositoryTests.cs`, `Quartermaster.Server.Tests/Motions/MotionRepositoryTests.cs`

**Interfaces:**
- Produces: `ApplicantRegisterRepository(DbContext context, AuditLogRepository auditLog)` with `List<ApplicantRegisterEntry> All()`, `(List<ApplicantRegisterEntry> Items, int TotalCount) Search(string? query, int page, int pageSize)` (query matches first or last name, `LIKE`, newest `OccurredOn` first), `ApplicantRegisterEntry? Get(Guid id)`, `void Create(ApplicantRegisterEntry entry)`, `void Update(ApplicantRegisterEntry entry)` (per-field audit), `void Delete(Guid id)` (also deletes hits of the entry), `void AddHits(Guid applicationId, IReadOnlyList<ApplicationRegisterHit> hits)`, `List<ApplicationRegisterHit> HitsFor(Guid applicationId)`, `bool Dismiss(Guid hitId, Guid userId, DateTime nowUtc)` (false when already dismissed), `bool HasOpenHits(Guid applicationId)`. Every write is audit logged under entity type `ApplicantRegisterEntry` / `ApplicationRegisterHit`.
- Produces: `MembershipApplication.ApprovedStatus(Guid? rootChapterId)`: `PendingFederalApproval` when `RequiresFederalApproval`, `Status == Pending` and `ChapterId != rootChapterId`; otherwise `Approved`.
- Produces: `MembershipApplicationRepository.SetFederalApproval(Guid id, bool required)` and `SetFederalApprovalMotion(Guid id, Guid? motionId)`, both audit logged.

- [ ] **Step 1: Write failing tests**:
  - `ApplicantRegisterRepositoryTests`: `Search_finds_by_last_name`, `Delete_removes_hits`, `Dismiss_only_once` (second call false), `Open_hits_ignore_dismissed`.
  - `MotionRepositoryTests`: `Approval_waits_for_federal_when_required` (application in a state chapter with `RequiresFederalApproval`, motion approved → `PendingFederalApproval`), `Approval_of_federal_step_approves` (status `PendingFederalApproval`, second motion approved → `Approved`), `Root_chapter_application_approves_directly`, `Rejection_rejects_in_both_steps`.
- [ ] **Step 2: Build and run the classes**; expect compile errors.
- [ ] **Step 3: Implement.** In `CascadeResolutionToLinkedEntities`, read the application, compute the root chapter id the same way as `ChapterRepository.GetRootChapter` (parentless, not deleted, first by name) and use `ApprovedStatus` for `MotionApprovalStatus.Approved`. Migration Up: create both tables (indexes on `ApplicationRegisterHits.ApplicationId` and `.EntryId`, on `ApplicantRegisterEntries.LastName`), add `RequiresFederalApproval` bool not null default false and `FederalApprovalMotionId` guid null to `MembershipApplications`; Down drops them.
- [ ] **Step 4: ALTER the dev DB** with the matching `CREATE TABLE` and `ALTER TABLE MembershipApplications ADD COLUMN RequiresFederalApproval TINYINT(1) NOT NULL DEFAULT 0, ADD COLUMN FederalApprovalMotionId CHAR(36) NULL;` (check the column type FluentMigrator uses for Guid in an existing table first).
- [ ] **Step 5: Grep every `switch` and comparison on `ApplicationStatus`** (list filters, labels, retention sweep, forwarding) and make sure `PendingFederalApproval` counts as undecided everywhere; add the German label "Wartet auf Bundesvorstand" (en "Awaiting the federal board") under `ui.application_status`.
- [ ] **Step 6: Run the tests**; expect green.

### Task 2: Matcher

**Files:**
- Create: `Quartermaster.Server/ApplicantRegister/RegisterPerson.cs` (record `RegisterPerson(string FirstName, string LastName, DateOnly? DateOfBirth, string? Street, string? HouseNumber, string? PostCode, string? City, string? Email)` with static `FromApplication(MembershipApplicationDTO dto)` and `FromEntry(ApplicantRegisterEntry entry)`)
- Create: `Quartermaster.Server/ApplicantRegister/ApplicantRegisterMatcher.cs`
- Test: `Quartermaster.Server.Tests/ApplicantRegister/ApplicantRegisterMatcherTests.cs`

**Interfaces:**
- Produces: `public static class ApplicantRegisterMatcher` with `const int Threshold = 3`, `RegisterMatchFields Compare(RegisterPerson candidate, RegisterPerson entry)`, `List<ApplicationRegisterHit> Match(RegisterPerson candidate, IReadOnlyList<ApplicantRegisterEntry> entries, DateTime nowUtc)` (one hit per entry reaching the threshold; `ApplicationId` left empty for the caller), `bool NamesMatch(string a, string b)`, `int CountFields(RegisterMatchFields fields)`.

- [ ] **Step 1: Write failing tests** (all pure):
  - `Umlaut_and_ascii_spelling_match`: "Jörg Müller" against "Joerg Mueller" and "Jorg Muller".
  - `Typo_matches_within_distance`: "Schmidt" against "Schmitt"; "Alexandra" against "Alexandar" (8 letters, distance 2); "Anna" against "Ania" matches, "Anna" against "Arno" does not.
  - `Dropped_and_swapped_middle_names_match`: "Anna Maria" against "Anna"; "Maria Anna" against "Anna Maria".
  - `Double_last_name_matches_part`: "Müller-Lüdenscheidt" against "Müller".
  - `Street_folds_strasse`: "Hauptstraße 12a" against "Hauptstr. 12 A"; "Haupt Straße" against "Hauptstrasse"; different house number does not match.
  - `Post_code_and_city_both_needed`: same post code, other city → no; same city, other post code → no; both → yes.
  - `Email_ignores_case_and_whitespace`.
  - `Empty_fields_never_match`: entry with only names and an empty email against an application with an empty-looking value never counts the email.
  - `Three_fields_make_a_hit`: first name, last name and street → hit; first name and last name only → no hit; last name, date of birth and email → hit.
- [ ] **Step 2: Run**; expect failures.
- [ ] **Step 3: Implement** with `DivisionNameTokenizer.Normalize` and `EditDistance.Bounded`. Names: split both into words, take the side with fewer words, each of its words must match some word of the other side. Street: normalize, join the words without spaces, replace "strasse" with "str". City: normalize, join without spaces. Post code: trim, remove spaces.
- [ ] **Step 4: Run**; expect green.

### Task 3: Permission, hit notification and visibility

**Files:**
- Modify: `Quartermaster.Api/PermissionIdentifier.cs` (`ManageApplicantRegister = "applicant_register_manage"`; not in any default list)
- Modify: `Quartermaster.Data/Permissions/PermissionRepository.cs` (`AddOrRename(ManageApplicantRegister, "Ablehnungsregister verwalten", true)`)
- Modify: `Quartermaster.Server/Notifications/NotificationTriggers.cs` (`ApplicantRegisterHit = "applicant_register_hit"`)
- Modify: `Quartermaster.Server/Notifications/NotificationTriggerDescriptor.cs` (optional `string? RequiredGlobalPermission = null`)
- Modify: `Quartermaster.Server/Notifications/NotificationTriggerCatalog.cs` (entry "Treffer im Ablehnungsregister", "Sobald ein Mitgliedsantrag einem Eintrag im Ablehnungsregister entspricht.", permission `ManageApplicantRegister`; static `VisibleFor(PermissionContext perms)`)
- Modify: `NotificationPreferencesGetEndpoint.cs`, `NotificationPreferencesUpdateEndpoint.cs`, `NotificationStatsCatalogEndpoint.cs`, `NotificationStatsEndpoint.cs` (use `VisibleFor`; the update endpoint treats a hidden trigger as unknown)
- Create: `Quartermaster.Server/Notifications/ApplicantRegisterHitPayload.cs` (record `ApplicantRegisterHitPayload(Guid RootChapterId, string FirstName, string LastName, string City, string Path, IReadOnlyList<string> Entries)`; `Path` is the application page, or the first entry's page when no application is kept; `Entries` are ready-made lines such as "Aufnahme abgelehnt am 03.05.2021: Anna Muster (übereinstimmend: Vorname, Nachname, Straße)")
- Create: `Quartermaster.Server/Notifications/ApplicantRegisterHitRecipientResolver.cs` (`ChapterPermissionRecipientResolver<ApplicantRegisterHitPayload>`, chapter = `RootChapterId`, permission `ManageApplicantRegister`); register in `Program.cs`
- Modify: `Quartermaster.Data/Templates/SystemTemplateSeeds.cs` (`notifications.applicant_register_hit.email` and `.telegram`, German, listing the entries and linking `{{ globals.BaseUrl }}{{ hit.Path }}`)
- Create: `Quartermaster.Server/Templates/RestrictedTemplates.cs` (`bool IsVisible(string identifier, PermissionContext perms)`: templates whose identifier starts with `notifications.applicant_register_hit.` need the permission)
- Modify: every endpoint under `Quartermaster.Server/Templates/` that lists, returns, updates, copies or previews a template (filter or 404 via `RestrictedTemplates`); also the template field palette or mock data if they enumerate triggers
- Test: `Quartermaster.Server.Tests/Integration/ApplicantRegister/ApplicantRegisterVisibilityTests.cs`, existing notification catalog tests

**Interfaces:**
- Consumes: `ChapterPermissionRecipientResolver<T>`.
- Produces: `PermissionIdentifier.ManageApplicantRegister`, `NotificationTriggers.ApplicantRegisterHit`, `ApplicantRegisterHitPayload`, `NotificationTriggerCatalog.VisibleFor`, `RestrictedTemplates.IsVisible`.

- [ ] **Step 1: Write failing tests**: `Trigger_hidden_without_permission` (preferences of a plain user do not list it; with the global grant they do; an update for it without the permission is rejected like an unknown trigger), `Templates_hidden_without_permission` (template list of a chapter template viewer has no `applicant_register_hit` template; a direct get returns 404), `Permission_not_in_chapter_lists` (whatever endpoint lists chapter-assignable permissions does not return it), `Hit_notification_reaches_only_holders` (dispatch with one holder and one root officer without it).
- [ ] **Step 2: Run**; expect failures.
- [ ] **Step 3: Implement.** Search the server for other places that enumerate all triggers, templates or permissions for non-admins and filter them the same way; report each one in the task report.
- [ ] **Step 4: Run**; expect green, including the existing notification and template test classes.

### Task 4: Intake, federal ticket notice and hit notification

**Files:**
- Create: `Quartermaster.Server/ApplicantRegister/RegisterNoticeText.cs` (static: `string? Build(IReadOnlyList<ApplicationRegisterHit> hits, IReadOnlyList<ApplicantRegisterEntry> entries, bool selfDeclared)` returning null when there is nothing; `string EntryLine(ApplicantRegisterEntry entry, RegisterMatchFields fields)`; `string KindLabel(ApplicantRegisterKind kind)` with "Aufnahme abgelehnt", "Ausgeschlossen", "Während eines Ausschlussverfahrens ausgetreten"; field labels "Vorname", "Nachname", "Geburtsdatum", "Straße", "Postleitzahl und Ort", "E-Mail")
- Modify: `Quartermaster.Server/Forwarding/ForwardingPayload.cs` (append `string? RegisterNotice = null`)
- Modify: `Quartermaster.Server/Forwarding/ForwardingTicketRenderer.cs` (`RenderTicketAsync`: for the step with `Order == 1` and a non-empty `RegisterNotice`, append it after the template body and before the reference line)
- Modify: `Quartermaster.Server/Forwarding/ForwardingIntake.cs` (`RouteApplication`: match before planning; set `RequiresFederalApproval` on the stored application; add hits after `Create`; put the notice into the payload; enqueue the hit notification when there is at least one hit)
- Modify: `Quartermaster.Server/MembershipApplications/ApplicationReviewService.cs` (`CreateReviewMotionAndNotify`: when `RequiresFederalApproval`, add the line "Benötigt zusätzlich die Zustimmung des Bundesvorstands (§ 3 Abs. 2 BS)." and nothing about the entry)
- Test: `Quartermaster.Server.Tests/Integration/ApplicantRegister/RegisterIntakeTests.cs`, `Quartermaster.Server.Tests/Forwarding/ForwardingTicketRendererTests.cs`

**Interfaces:**
- Consumes: `ApplicantRegisterMatcher.Match` (Task 2), `ApplicantRegisterRepository.All/AddHits` (Task 1), `ApplicantRegisterHitPayload` (Task 3).
- Produces: `ForwardingPayload.RegisterNotice`; `RegisterNoticeText.KindLabel` and `EntryLine` for Tasks 5 and 7.

- [ ] **Step 1: Write failing tests** (submit through `/api/membershipapplications` as in `ApplicationForwardingTests`, with `SageSetupSeeder`):
  - `Hit_requires_federal_approval_and_records_hit`.
  - `Self_declaration_requires_federal_approval_without_hit` (no hit rows, no hit notification).
  - `No_hit_leaves_application_normal`.
  - `Chapter_motion_names_no_entry` (motion text has the § 3 line, not the entry's kind, date or note).
  - `Hit_notification_links_application` and, on a Redmine path, `Hit_notification_links_entry_without_application`.
  - `Federal_ticket_carries_notice_chapter_ticket_does_not` (renderer test with two steps).
  - `Waiting_application_notice_survives_linking_to_redmine` (no division, hit; then link to a Redmine chapter: the payload of the federal step kept the notice, the application and its hits are gone).
- [ ] **Step 2: Run**; expect failures.
- [ ] **Step 3: Implement.**
- [ ] **Step 4: Run** the new classes plus `ApplicationForwardingTests`, `ForwardingIntakeTests`, `MembershipApplicationLinkDivisionEndpointTests`; expect green.

### Task 5: Federal approval flow

**Files:**
- Create: `Quartermaster.Server/ApplicantRegister/FederalApprovalFlow.cs`
- Modify: `Quartermaster.Server/MembershipApplications/ApplicationReviewService.cs` (`Guid CreateFederalApprovalMotion(MembershipApplication application, Guid federalChapterId, string hearing)`: title "Mitgliedsantrag (Zustimmung nach § 3 Abs. 2 BS): <Vorname> <Nachname>", text with the application data, "Anhörung: <hearing>" and the link to the application, linked to the application; notifies `ApplicationSubmitted` for the federal chapter)
- Modify: `Quartermaster.Server/Motions/MotionVoteEndpoint.cs`, `Quartermaster.Server/Meetings/MeetingLifecycleService.cs` (both sites) (call `FederalApprovalFlow.OnMotionResolvedAsync` after the forwarding hook)
- Modify: `Quartermaster.Server/Admin/MembershipApplicationProcessEndpoint.cs` (accept `PendingFederalApproval` as undecided; for it require `ProcessApplications` on the root chapter; approve through `ApprovedStatus`; call `FederalApprovalFlow.OnRecordDecidedAsync`)
- Modify: `Quartermaster.Server/Admin/MembershipApplicationDetailEndpoint.cs` and `Quartermaster.Api/MembershipApplications/MembershipApplicationDetailDTO.cs` (`RequiresFederalApproval`, `FederalApprovalMotionId`, `List<ApplicationRegisterHitDTO>? RegisterHits` filled only with the global permission)
- Create: `Quartermaster.Api/ApplicantRegister/ApplicationRegisterHitDTO.cs` (`Id`, `EntryId`, `Kind`, `OccurredOn`, `FirstName`, `LastName`, `MatchedFields`, `DismissedAt`)
- Register `FederalApprovalFlow` scoped in `Program.cs`
- Test: `Quartermaster.Server.Tests/Integration/ApplicantRegister/FederalApprovalFlowTests.cs`

**Interfaces:**
- Consumes: Tasks 1, 3 and 4; `ForwardingCaseRepository.ListByRecord` and `UpdateRouting`; `ForwardingOutcomeHook.OnRecordDecided`; `MotionRepository.GetVotes`.
- Produces: `FederalApprovalFlow` with `Task OnMotionResolvedAsync(Guid motionId, CancellationToken ct)` and `Task OnRecordDecidedAsync(Guid applicationId, CancellationToken ct)`. Both: when the application is `PendingFederalApproval` and has no `FederalApprovalMotionId`, create the federal motion (hearing: chapter name, date, "Ja / Nein / Enthaltung" counts of the chapter motion, or "im Bearbeitungsdialog entschieden"), store its id, and point every application forwarding case at it (`MotionId`, `DecidingChapterId` = root) so the outcome note reports the federal decision. When the application is `Rejected` by a motion resolved as `Rejected` or by the dialog, add a `Rejected` register entry from the application's data (`OccurredOn` today, `ChapterId` the deciding chapter, `SourceApplicationId`). Formal rejections and closing without action add nothing.

- [ ] **Step 1: Write failing tests**:
  - `Chapter_approval_creates_federal_motion`.
  - `Federal_motion_created_once` (resolve hook called twice, and dialog after motion).
  - `Chapter_approval_holds_mail_and_sage` (no approval mail logged, outcome note and SAGE step still waiting).
  - `Federal_approval_releases_mail_and_note` (the note names the federal chapter).
  - `Chapter_cannot_decide_the_federal_step` (403 for the chapter processor; 200 for a root processor).
  - `Rejection_adds_register_entry` (chapter motion rejected; federal motion rejected; dialog rejection).
  - `Formal_rejection_adds_no_entry`.
  - `Detail_hides_hits_without_permission` (chapter viewer gets `RequiresFederalApproval` true and `RegisterHits` null; a holder gets the hits).
- [ ] **Step 2: Run**; expect failures.
- [ ] **Step 3: Implement.**
- [ ] **Step 4: Run** the new class plus `MembershipApplicationProcessEndpointTests`, `MotionApprovalMailHold`-related classes, `ForwardingJobTests`; expect green.

### Task 6: "Andere Person"

**Files:**
- Create: `Quartermaster.Server/ApplicantRegister/Endpoints/RegisterHitDismissEndpoint.cs` (`POST /api/admin/membershipapplications/{ApplicationId}/register-hits/{HitId}/dismiss`, `HasGlobal(ManageApplicantRegister)` else 403; 404 for an unknown or foreign hit; 409 when already dismissed or the application is decided)
- Modify: `FederalApprovalFlow` (`Task DismissAsync(Guid applicationId, Guid hitId, Guid userId, CancellationToken ct)`)
- Test: `Quartermaster.Server.Tests/Integration/ApplicantRegister/RegisterHitDismissTests.cs`

**Interfaces:**
- Produces: `DismissAsync`: marks the hit; when no open hit is left and the applicant did not self-declare, sets `RequiresFederalApproval` false. If the application is `PendingFederalApproval` then: close the federal motion `ClosedWithoutAction`, set `Approved`, point the forwarding cases back at the chapter motion and chapter, call `ForwardingOutcomeHook.OnRecordDecided` and send the approval mail unless `SageEntryGate.HoldsApprovalMail`.

- [ ] **Step 1: Write failing tests**: `Dismiss_before_chapter_decision_makes_it_normal`, `Dismiss_after_chapter_approval_approves`, `Self_declared_stays_federal_after_dismiss`, `Two_hits_need_both_dismissed`, `Dismiss_needs_permission`, `Dismiss_twice_conflicts`.
- [ ] **Step 2: Run**; expect failures.
- [ ] **Step 3: Implement.**
- [ ] **Step 4: Run**; expect green.

### Task 7: Register endpoints

**Files:**
- Create: `Quartermaster.Api/ApplicantRegister/ApplicantRegisterEntryDTO.cs` (entry fields plus `ChapterName`, `SourceApplicationId`) and `ApplicantRegisterEntrySaveRequest.cs`
- Create: `Quartermaster.Server/ApplicantRegister/Endpoints/` list (`GET /api/admin/applicant-register?query=&page=&pageSize=`), get (`GET /api/admin/applicant-register/{Id}`), create (`POST /api/admin/applicant-register`), update (`PUT /api/admin/applicant-register/{Id}`), delete (`DELETE /api/admin/applicant-register/{Id}`); all `HasGlobal(ManageApplicantRegister)` else 403
- Create: `ApplicantRegisterEntrySaveRequestValidator` (names required; at least one of date of birth, street, post code and city, email; lengths from Task 1; kind in enum; email through `EmailAddressFormat.IsPlausible` when set; `OccurredOn` not in the future)
- I18n: error keys under `error.applicant_register.*`
- Test: `Quartermaster.Server.Tests/Integration/ApplicantRegister/ApplicantRegisterEndpointTests.cs`

- [ ] **Step 1: Write failing tests**: CRUD round trip with audit entries, `Names_alone_are_refused`, `Plain_member_gets_403` on every route, `Search_by_name`.
- [ ] **Step 2: Run**; expect failures.
- [ ] **Step 3: Implement.**
- [ ] **Step 4: Run**; expect green.

### Task 8: Blazor pages

**Files:**
- Create: `Quartermaster.Blazor/Pages/Administration/ApplicantRegister/ApplicantRegisterList.razor` + `.razor.cs` (`/Administration/ApplicantRegister`: search, table with kind, date, name, city, link to edit; "Neuer Eintrag" button)
- Create: `Quartermaster.Blazor/Pages/Administration/ApplicantRegister/ApplicantRegisterEdit.razor` + `.razor.cs` (`/Administration/ApplicantRegister/New` and `/Administration/ApplicantRegister/{Id:guid}`: form for every field, hint "Neben dem Namen mindestens Geburtsdatum, Anschrift oder E-Mail, sonst kann kein Treffer entstehen.", delete with `ConfirmDialog`, link to the source application when set)
- Modify: `Quartermaster.Blazor/Layout/MainNavBar.razor` (entry "Ablehnungsregister" under "Verwaltung" inside `RequirePermission GlobalPermission="@PermissionIdentifier.ManageApplicantRegister"`)
- Modify: `Quartermaster.Blazor/Pages/Administration/MembershipApplicationDetail.razor` + `.razor.cs` (info box "Benötigt zusätzlich die Zustimmung des Bundesvorstands (§ 3 Abs. 2 BS)." when `RequiresFederalApproval`; link to the federal motion; for holders a card with the hits, matched fields, "nach eigener Angabe" for the self-declaration, and "Andere Person" with `ConfirmDialog`)
- I18n for all UI text under `ui.applicant_register.*` and `ui.application_detail.*`

- [ ] **Step 1: Build the solution**; fix warnings in the touched files.
- [ ] **Step 2: Restart the dev server** (PID lookup, kill in a separate call, start with nohup) and check with curl that the new API routes answer 401/403 without a login.

### Task 9: Documentation

- [ ] Tick the todo item (`todo.md`, "*(V9.0)*").
- [ ] Do not touch `Changelog/V9.0.md`, the front page or PiratenWissen.
- [ ] Run a final self-review of the diff against `CLAUDE.md` and the Global Constraints.
