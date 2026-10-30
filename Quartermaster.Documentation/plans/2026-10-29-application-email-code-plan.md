# Email Code in the Membership Application Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Anonymous membership applicants confirm their address with a 6-digit code in its own wizard step; the code replaces the confirmation link for applications.

**Architecture:** A code store (`EmailVerificationCodes`, one row per address) behind `EmailCodeService` issues, checks and consumes codes. Two anonymous endpoints send and check codes; the application submit checks the code again and stores the application directly. A new wizard step `MembershipEmailCodeStep` sits between "Erklärungen" and "Übersicht".

**Tech Stack:** .NET 10, FastEndpoints, LinqToDB + FluentMigrator (MariaDB), Blazor WASM, TUnit, Fluid templates.

**Spec:** `Quartermaster.Documentation/plans/2026-10-29-application-email-code-design.md`

## Global Constraints

- Follow `CLAUDE.md` (K&R braces, if-body on the next line, guard-clause methods for 3+ term conditions, `== null`, never `is X or Y`, explicit access modifiers, one class per file, no `@code` in `.razor`, no bare catch, minimal comments).
- No git commands at all. No em or en dashes anywhere (code, comments, texts, docs).
- English in code and routes; only UI texts German.
- Schema changes go into M009 (`[Migration(017)]`); the dev DB (`quartermaster`, mysql root) is altered by hand with the same DDL.
- Never contact a real mail server; tests use the existing dummy SMTP options and read `NotificationLogs`.
- Build: `taskset -c 0-9 dotnet build Quartermaster.sln`. Tests only per class: `taskset -c 0-9 dotnet run --no-build --project Quartermaster.Server.Tests -- --treenode-filter "/*/*/(ClassA)|(ClassB)/*" --progress off > log`. Never the full suite.
- Values from the spec, verbatim: code = 6 digits; lifetime option `membership_application.email_code.lifetime_minutes` default 15; per-address option `membership_application.email_code.sends_per_address_per_hour` default 10; per-IP options `auth.ratelimit.email_code_permits` default 10 and `auth.ratelimit.email_code_window_minutes` default 60; 5 wrong attempts per code; resend button disabled for 60 seconds.
- Mail subject "Bestätigungscode für deinen Mitgliedsantrag - Piratenpartei"; body "Dein Code lautet **{{ code }}**. Er ist {{ minutes }} Minuten gültig. Falls du keinen Mitgliedsantrag gestellt hast, ignoriere diese E-Mail."

## Review Focus

- Address typed with different case or surrounding spaces on send, check and submit: must hit the same row (tests in Task 1 and Task 3).
- A code checked correctly, then the applicant waits on the summary: the correct check extends the lifetime, so the submit still succeeds within the new lifetime (Task 3 test `Submit_after_check_uses_extended_lifetime`).
- The applicant edits the address on the code step after a successful check: the old check must not count for the new address (Task 4, state rule `IsEmailCodeComplete` compares the normalized current address).
- Submit refused for a reason other than the code (for example a reduction problem): the code stays usable (Task 3 test `Code_survives_submit_refused_for_reduction`).
- Requesting a new code after 5 wrong attempts unlocks checking again (Task 1 test `Reissue_resets_attempts`).

---

### Task 1: Code store and service

**Files:**
- Create: `Quartermaster.Data/EmailVerification/EmailVerificationCode.cs`, `Quartermaster.Data/EmailVerification/EmailVerificationCodeRepository.cs`
- Create: `Quartermaster.Server/MembershipApplications/EmailCodes/EmailCodeService.cs`, `EmailCodeCheckResult.cs`, `EmailCodeIssueResult.cs` (same folder)
- Modify: `Quartermaster.Data/Migrations/M009.cs` (create the table), `Quartermaster.Data/DbContext.cs` (table property), `Quartermaster.Data/Options/OptionRepository.cs` (option definitions), `Quartermaster.Server/Program.cs` (DI), `Quartermaster.Server.Tests/Infrastructure/WorkerDatabase.cs` (TRUNCATE the new table)
- Test: `Quartermaster.Server.Tests/MembershipApplications/EmailCodeServiceTests.cs` (RepositoryTestBase style, service built by hand with the repository and an `OptionRepository`)

**Interfaces:**
- Produces:
  - Entity `EmailVerificationCode` (table `EmailVerificationCodes`): `Guid Id`, `string Email` (unique index, max 320), `string CodeHash`, `string CodeSalt`, `DateTime ExpiresAt`, `int FailedAttempts`, `DateTime? VerifiedAt`, `int SendCount`, `DateTime SendWindowStartedAt`.
  - `EmailVerificationCodeRepository`: `EmailVerificationCode? Get(string normalizedEmail)`, `void Upsert(EmailVerificationCode row)`, `void Update(EmailVerificationCode row)`, `void Delete(string normalizedEmail)`, `int DeleteExpiredBefore(DateTime cutoffUtc)`.
  - `enum EmailCodeCheckResult { Valid, Wrong, Expired, Locked }`.
  - `record EmailCodeIssueResult(string? Code, bool LimitReached)`.
  - `EmailCodeService` (scoped): `static string Normalize(string email)` (trim + lower invariant); `EmailCodeIssueResult Issue(string email, DateTime nowUtc)`; `EmailCodeCheckResult Check(string email, string code, DateTime nowUtc)`; `void Consume(string email)`; `int LifetimeMinutes()`; `const int MaxFailedAttempts = 5`.
  - Option definitions `membership_application.email_code.lifetime_minutes` (15), `membership_application.email_code.sends_per_address_per_hour` (10), `auth.ratelimit.email_code_permits` (10), `auth.ratelimit.email_code_window_minutes` (60), German display names and descriptions in the style of the neighbouring rate-limit options.

- [ ] **Step 1: Write the failing tests**

`Issue_returns_six_digits` (`Code` matches `^[0-9]{6}$`, row stored with `CodeHash != Code`), `Check_valid_sets_verified_and_extends` (Valid; `VerifiedAt` set; `ExpiresAt == now + 15 min` when checked at now), `Check_wrong_counts` (Wrong; `FailedAttempts == 1`), `Check_locked_after_five` (5 x Wrong, then the right code gives Locked), `Check_expired` (check at issue time + 16 min gives Expired, no attempt counted), `Check_without_row_is_expired`, `Address_is_normalized` (issue for `" Alice@Test.Local "`, check for `"alice@test.local"` gives Valid), `Reissue_resets_attempts` (5 wrong, reissue, new code Valid), `Reissue_invalidates_old_code` (old code Wrong after reissue), `Send_limit_per_address` (10 issues within an hour succeed, the 11th has `LimitReached` and `Code == null`; an issue 61 minutes after the window start succeeds again), `Consume_deletes_row`, `DeleteExpiredBefore_removes_old_rows`.

- [ ] **Step 2: Run the tests, expect them to fail (types missing)**
- [ ] **Step 3: Implement**

M009: `Create.Table(EmailVerificationCode.TableName)` with the columns above and a unique index on `Email`; run the same `CREATE TABLE` on the dev DB. Code: `RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6", CultureInfo.InvariantCulture)`; salt 16 random bytes (Base64); hash SHA-256 over salt bytes followed by the UTF-8 code (Base64); compare with `CryptographicOperations.FixedTimeEquals`. `Issue` keeps `SendCount`/`SendWindowStartedAt` across reissues (window restarts when older than one hour). Lifetime and per-address limit are read live from `OptionRepository` with the defaults as fallback for missing or non-positive values.

- [ ] **Step 4: Run the tests, expect PASS**

---

### Task 2: Send endpoint, code mail, rate limit

**Files:**
- Create: `Quartermaster.Server/MembershipApplications/EmailCodes/EmailCodeSendEndpoint.cs`, `EmailCodeMailService.cs`
- Create: `Quartermaster.Api/MembershipApplications/EmailCodeSendRequest.cs` (`string Email`, `Guid? ChapterId` from the address step), `EmailCodeSendResponse.cs` (`bool CodeRequired`, `int LifetimeMinutes`)
- Modify: `Quartermaster.Server/Program.cs` (policy `EmailCodeRateLimitPolicy = "email-code"`, per IP, options from Task 1, fallbacks 10 and 60), `Quartermaster.Data/Templates/SystemTemplateSeeds.cs` (new seed), `Quartermaster.Api/I18n/I18nKey.cs` + `Quartermaster.Server/wwwroot/i18n/de.json` + `en.json` (error keys), `Quartermaster.Server.Tests/Infrastructure/IntegrationTestBase.cs` (raise `auth.ratelimit.email_code_permits` to 10000 like the other permits)
- Test: `Quartermaster.Server.Tests/Integration/MembershipApplications/EmailCodeSendEndpointTests.cs`

**Interfaces:**
- Consumes: `EmailCodeService.Issue`, `LifetimeMinutes`.
- Produces: `POST /api/membershipapplications/email-code` (anonymous, rate-limited by `EmailCodeRateLimitPolicy`); trigger id `membership_application_email_code`; template `templates.submission.membershipapplication.code.email` (display name "E-Mail: Bestätigungscode für den Mitgliedsantrag", `AllowsChapterFields: true`, model `code`, `minutes`, `globals`, `chapter`); `I18nKey.Error.EmailCode` with `AddressInvalid = "error.email_code.address_invalid"`, `TooManySends = "error.email_code.too_many_sends"`, `Required = "error.email_code.required"`, `Wrong = "error.email_code.wrong"`, `Expired = "error.email_code.expired"`, `Locked = "error.email_code.locked"`, `ChapterNotFound = "error.email_code.chapter_not_found"` (all seven added here; Task 3 uses `Required`, `Wrong`, `Expired`, `Locked`).
- German texts: AddressInvalid "Bitte gib eine gültige E-Mail-Adresse ein.", TooManySends "Es wurden zu viele Codes angefordert. Bitte versuche es später erneut.", Required "Bitte gib den Code aus der E-Mail ein.", Wrong "Der Code stimmt nicht.", Expired "Der Code ist abgelaufen. Bitte fordere einen neuen Code an.", Locked "Zu viele falsche Eingaben. Bitte fordere einen neuen Code an.", ChapterNotFound "Die Gliederung wurde nicht gefunden." English equivalents in `en.json`.

- [ ] **Step 1: Write the failing tests**

`Sends_code_mail` (one `NotificationLogs` row with `TriggerId == "membership_application_email_code"`, `Recipient == "alice@test.local"`, `Subject == "Bestätigungscode für deinen Mitgliedsantrag - Piratenpartei"`, `Body` contains the six digits that `Check` accepts and "15 Minuten"), `Refused_without_setup` (400 `error.admin.application.not_set_up`, like the submit), `Refused_when_form_disabled` (403 `error.public_form.disabled`), `Refused_for_implausible_address` (400 `AddressInvalid`, no row), `Per_address_limit` (11th send 429 with `TooManySends`), `Per_ip_limit` (set `auth.ratelimit.email_code_permits` to 2 in the test, 3rd call 429; follow `AnonymousCreateRateLimitTests` for isolating the IP bucket), `Response_reports_lifetime` (`CodeRequired == true`, `LifetimeMinutes == 15`), `Uses_chapter_template` (a chapter override of the template is used when `ChapterId` names that chapter), `Refused_for_unknown_chapter` (400 `ChapterNotFound`, no mail).

- [ ] **Step 2: Run, expect FAIL**
- [ ] **Step 3: Implement**

Gates in the order of `MembershipApplicationCreateEndpoint` (`IsMembershipApplicationReady`, then enabled). In the debug build without SMTP (`SmtpConfig.ReadFrom(...) == null`, same `#if DEBUG` pattern as the submit) answer `CodeRequired = false` without issuing. `EmailCodeMailService.SendAsync(string email, Guid? chapterId, string code, int minutes, CancellationToken ct)` renders like `SubmissionConfirmationEmailService` (resolve via `TemplateRepository.Resolve(identifier, req.ChapterId, _chapterRepo)`, model `chapter` from `_chapterRepo.WithTemplateContact(...)` as there, `TemplateRenderer.RenderTextAsync`/`RenderHtmlAsync`, `EmailMessageChannel.SendAsync` with `TriggerId` and `TemplateIdentifier` metadata). Ruling recorded here: the rendered body (with the code) stays in `NotificationLogs.Body` like every mail, since the sender drains from it; the code is useless after expiry or use, and only administrators read the log.

- [ ] **Step 4: Run, expect PASS**

---

### Task 3: Check endpoint, submit with code, test migration

**Files:**
- Create: `Quartermaster.Server/MembershipApplications/EmailCodes/EmailCodeCheckEndpoint.cs`, `Quartermaster.Api/MembershipApplications/EmailCodeCheckRequest.cs` (`string Email`, `string Code`)
- Modify: `Quartermaster.Api/MembershipApplications/MembershipApplicationDTO.cs` (`string EmailVerificationCode { get; set; } = ""`, never stored), `Quartermaster.Server/MembershipApplications/MembershipApplicationCreateEndpoint.cs`, `Quartermaster.Server/Submissions/SubmissionConfirmationEmailService.cs` (drop the `MembershipApplication` branches), `Quartermaster.Data/Templates/SystemTemplateSeeds.cs` (drop the `templates.submission.membershipapplication.confirmation.email` seed), `Quartermaster.Server/Submissions/PendingSubmissionCleanupHostedService.cs` (also `DeleteExpiredBefore(now - 1 day)` on the code store)
- Modify (tests): `Quartermaster.Server.Tests/Infrastructure/IntegrationTestBase.cs` (helper), and every test that posts an anonymous application (`grep -rln 'PostAsJsonAsync("/api/membershipapplications"' Quartermaster.Server.Tests`: 12 files, about 27 posts); drop `ConfirmAllPendingSubmissionsAsync()` calls that only served applications
- Test: `Quartermaster.Server.Tests/Integration/MembershipApplications/EmailCodeSubmitTests.cs`

**Interfaces:**
- Consumes: `EmailCodeService.Check`, `Consume`, error keys from Task 2.
- Produces: `POST /api/membershipapplications/email-code/check` (anonymous; 204 on Valid, else 400 with `Wrong`/`Expired`/`Locked`); test helper `protected string IssueEmailCode(string email)` in `IntegrationTestBase` (issues through `EmailCodeService` in a scope and returns the code).

- [ ] **Step 1: Write the failing tests**

`Check_endpoint_valid_and_wrong` (204, then 400 `Wrong` with a different code), `Anonymous_submit_without_code_refused` (400 `Required`, no application, no `PendingSubmissions` row), `Anonymous_submit_with_wrong_code_refused` (400 `Wrong`, `FailedAttempts == 1`), `Anonymous_submit_with_code_for_other_address_refused` (code issued for `bob@test.local`, application for `alice@test.local`: 400 `Expired`), `Anonymous_submit_with_code_stores_directly` (200, `RequiresConfirmation == false`, one `MembershipApplications` row, no `PendingSubmissions` row, code row deleted, receipt mail `application_received` logged), `Submit_after_check_uses_extended_lifetime` (issue, check at issue time + 14 min through the service, submit at + 20 min must pass: drive the time through the service, or set `ExpiresAt` on the row directly before the submit), `Code_survives_submit_refused_for_reduction` (submit with a reduction problem: 400, code row still there and still Valid), `Logged_in_submit_needs_no_code` (as today).

- [ ] **Step 2: Run, expect FAIL**
- [ ] **Step 3: Implement**

Submit order: form gates, reduction rules, then for anonymous callers (and not the debug no-SMTP skip): empty code gives `Required`; otherwise `Check(req.Email, req.EmailVerificationCode, now)` mapped to the error keys; then `MaterializeApplicationDirectAsync`, then `Consume(req.Email)`, response `RequiresConfirmation = false`. The anonymous path no longer calls `SubmissionIntakeService`. Then add the helper and migrate the existing tests: each anonymous application post sets `EmailVerificationCode = IssueEmailCode(dto.Email)`, and assertions that expected a pending row or a confirm step now expect the stored application.

- [ ] **Step 4: Run the new class and every migrated class, expect PASS**

Classes: `EmailCodeSubmitTests`, `MembershipApplicationCreateEndpointTests`, `ApplicationReceivedMailTests`, `ApplicationForwardingTests`, `ApplicationSubmittedNotificationTests`, `MembershipApplicationLinkDivisionEndpointTests`, `RegisterIntakeTests`, `RegisterHitDismissTests`, `FederalApprovalFlowTests`, `PublicFormTests`, `AnonymousCreateRateLimitTests`, `TakeOverSweepTests`, `MotionHandoverTests`, `EmailCodeSendEndpointTests`, `EmailCodeServiceTests`.

---

### Task 4: Wizard step

**Files:**
- Create: `Quartermaster.Blazor/Pages/MembershipApplication/MembershipEmailCodeStep.razor`, `.razor.cs`
- Modify: `MembershipApplicationEntryState.cs`, `MembershipApplicationWizard.razor(.cs)`, `Quartermaster.Blazor/Components/Forms/FormWizard.razor.cs` (public `Task ShowStepAsync(int index)` that sets the step and syncs the URL), `I18nKey.cs`, `de.json`, `en.json`

**Interfaces:**
- Consumes: the three endpoints from Tasks 2 and 3; `EmailCodeSendResponse`.
- Produces: entry state fields `string EmailCode`, `string? EmailCodeVerifiedFor`, `string? EmailCodeSentFor`, `DateTime? EmailCodeSentAt`, and `bool IsEmailCodeComplete()` (true when `EmailCodeVerifiedFor` equals the trimmed lower-case `Email`).
- UI keys `ui.email_code_step.*`: step title "E-Mail bestätigen" (`ui.public_form_wizard.step.email_code`), `sent_to` "Wir haben einen Code an <strong>{email}</strong> geschickt.", `code_label` "Code aus der E-Mail", `change_address` "Adresse ändern", `save_address` "Speichern und neuen Code senden", `resend` "Neuen Code senden", `resend_wait` "Neuen Code senden ({seconds} s)", `not_required` "Ohne eingerichteten Mailserver entfällt der Code." (debug only), `required` toast = `error.email_code.required`.

- [ ] **Step 1: Implement the step**

The step is only added for anonymous users (`AuthService` not logged in), between `StepDeclarations` and `StepSummary`. On first render it sends a code (address and `State.ChapterId`) unless `EmailCodeSentFor` equals the current address and `EmailCodeSentAt` is less than 60 seconds ago. With `CodeRequired == false` it sets `EmailCodeVerifiedFor` and shows `not_required`. "Adresse ändern" shows an input with the current address; saving writes `State.Email`, clears `EmailCode`/`EmailCodeVerifiedFor` and sends a new code. The resend button counts down 60 seconds (a timer that is disposed with the component). The code input takes digits only (`inputmode="numeric"`, `autocomplete="one-time-code"`, `maxlength="6"`). The step's `Validate` calls the check endpoint with 6 digits (else toast `Required`), sets `EmailCodeVerifiedFor` on 204 and shows the error toast otherwise; `IsValid` is `IsEmailCodeComplete`. The submit sends `EmailVerificationCode = State.EmailCode`; on 400 with `error.email_code.expired` it clears `EmailCodeVerifiedFor`, calls `ShowStepAsync` for the code step (held through `@ref`), which then sends a new code.

- [ ] **Step 2: Build, restart the dev server, check `/MembershipApplication` loads (curl 200)**

---

### Task 5: Documentation

**Files:**
- Modify: `Quartermaster.Server/Changelog/V9.0.md` (section "### Mitgliedsantrag": one paragraph on the code step, the address correction on the same step and that the confirmation link is gone for applications)
- Modify: `/home/joscha/Projects/PiratenWissen/quartermaster/mitgliedschaft.md` (code step in the description of the online application, source line "Quelle: Bundes-IT (Quartermaster Version 9.0), notiert 2026-10-29") and `/home/joscha/Projects/PiratenWissen/quartermaster/chronik.md` (entry, newest first)
- Modify: `todo.md` (tick the item, tag *(V9.0)*)

- [ ] **Step 1: Write the texts** (German, one paragraph per line, no dashes, no hostnames or settings names in PiratenWissen)
