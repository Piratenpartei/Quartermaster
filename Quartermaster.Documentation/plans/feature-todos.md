# Feature TODOs

Features that aren't quality fixes — net-new functionality, infrastructure for new capabilities, etc. Tracked separately from `code-quality-todos.md` so the two lists don't get muddled.

---

## Collaborative meeting notes (SignalR + CodeMirror) — DONE (2026-05)

Shipped all 6 planned phases plus polish. Real-time collaborative editing of agenda-item notes during a meeting, with per-character authorship colors, cursor/presence sync, and live meeting page updates.

- **Server**: `MeetingHub` SignalR relay (`JoinMeeting`/`LeaveMeeting`, `LoadDocument`, `SendUpdate`/`ReceiveUpdate`, `SendAwareness`/`ReceiveAwareness`, `SaveSnapshot`); `CollabDocument` table (Yjs `DocumentState` + `PlainText` snapshot + `ClientUserMap` authorship map, folded into M001); `MeetingNotifier`/`IMeetingNotifier` for live page broadcasts (agenda/status/presence); `AgendaItemNotesEndpoint`.
- **Client**: Yjs CRDT + CodeMirror 5 vendored with no build step (`wwwroot/js/collab-editor/`, see its `VERSIONS.md`); `codemirror-editor.js` imperative API with per-character author tagging via `Y.Text.format` + `rebuildAuthorMarkers` + a known-authors cache seeded from the server snapshot; cursor/presence awareness; theme-aware dark mode. Wired into `MeetingLive` through `CodeMirrorEditorWithPreview` + `MeetingHubClient`.
- **Tests**: `CollabEditorE2ETests` (10) covering two-user color sync, authorship surviving disconnect + reload-via-snapshot, anonymous + read-only viewers, completed-meeting freeze, mid-line markers, legacy uncolored text, dark-mode toggle, save-indicator states.

The 5 pre-implementation questions were all resolved (recorded in the plan): vendored CM5 (no JS pipeline), CM5 single-file UMD over CM6, markdown preview kept, etc.

**Full plan**: [`2026-04-10-collaborative-meeting-notes.md`](./2026-04-10-collaborative-meeting-notes.md)

---

## Active Sessions UI (per-user session management)

**Status: shipped (2026-05-26).**

A "Meine Sitzungen" page at `/Sessions` where a logged-in user sees all their currently valid login tokens with audit metadata and can revoke individually or in bulk.

Implementation:
- `Quartermaster.Api/Users/SessionDTO.cs` — `(TokenId, IssuedAt, ExpiresAt, IssuedIp, IssuedUserAgent, IsCurrent)`.
- `TokenRepository` gained `GetActiveLoginTokensForUser`, `DeleteOwnedByUser` (silent no-op if the token doesn't belong to the caller — no ownership leak), and `DeleteOtherLoginTokensForUser`.
- `TokenAuthenticationHandler` now stamps a `qm:token_id` claim (constant in new `AuthClaimTypes`) so the sessions endpoints can identify which row backs the current request without hashing the bearer again.
- 3 endpoints: `GET /api/users/sessions`, `DELETE /api/users/sessions/{id}` (idempotent, 204 either way), `POST /api/users/sessions/revoke-others`.
- `Quartermaster.Blazor/Pages/UserSessions.razor[.cs]` — table with "Diese Sitzung" badge, per-row "Abmelden", top-right "Alle anderen abmelden". Revoking the current session force-navigates to `/Login`. Nav link is a shield-lock icon next to the user-name link in `MainLayout`.
- New i18n keys under `I18nKey.Ui.Toast.Session*` and `I18nKey.Ui.Confirm.Session*` (de + en).
- 9 integration tests in `SessionEndpointsTests.cs` covering list-only-mine, expired-token filtering, current-token marking, idempotent foreign-token revoke, revoke-current → next 401, revoke-others preserves bearer, revoke-others doesn't touch other users.

Deferred to v2: user-agent prettifying (raw UA shown for now); a "since" or "ago" relative-time column.

---

## Notification system (consumer-side wiring of `IMessageChannel`)

**Phase 1: shipped (2026-05-27).** Dispatch foundation + one trigger end-to-end.
- `Quartermaster.Server/Notifications/`: `NotificationTriggers` const catalog, `NotificationRecipient`, `IRecipientResolver`, `NotificationDispatcher`. Dispatcher resolves recipients, renders per-(trigger × channel) Fluid templates from `Options` (`notifications.{trigger}.email.subject` / `.body`), hands off to `EmailMessageChannel`.
- `EmailLog` generalized to `NotificationLog` (channel-agnostic; new `ChannelId` / `TriggerId` / `RecipientUserId` columns, `HtmlBody` → `Body`, folded into M001). `EmailLogEndpoint` → `NotificationLogEndpoint` at `/api/notificationlogs`. Permission renamed `ViewEmailLogs` → `ViewNotificationLogs`.
- First trigger: `motion_submitted`. `MotionSubmittedRecipientResolver` notifies users with `EditMotions` on the motion's chapter — direct grants, global perm, role-derived from the chapter or any ancestor where the role inherits to children. `MotionCreateEndpoint` dispatches after persist. Default template seeded.
- Tests: 10 resolver tests + 4 integration tests fanning the trigger through the endpoint into `NotificationLog`.

**Phase 2: shipped (2026-05-27).** Two more triggers wired end-to-end.
- `ChapterPermissionRecipientResolver<TPayload>` base extracted — `MotionSubmittedRecipientResolver` collapsed to a 5-line subclass; new resolvers follow the same shape.
- `application_submitted`: dispatched from `MembershipApplicationCreateEndpoint` when the application carries a chapter; recipients hold `ProcessApplications`. Notification is in addition to the linked motion the endpoint also spawns.
- `due_selection_submitted`: dispatched from `DueSelectionCreateEndpoint` when the submitter's `MemberNumber` resolves to a member with a chapter; recipients hold `ProcessDueSelections`. Standalone submissions without a resolvable chapter skip the notification (handled via the linked application's own trigger when bundled).
- Templates + mock data + DI registration in place. Resolver smoke tests + 3 integration tests per trigger.

**Phase 3: shipped (2026-05-27).** Per-user channel preferences.
- `UserNotificationPreference(UserId, TriggerId, ChannelId, Enabled)` table with composite PK + cascade-delete FK to User (folded into M001).
- `UserNotificationPreferenceRepository` with `IsEnabled(default)` lookup and atomic `Replace` (delete + inserts in one transaction).
- `NotificationTriggerCatalog` + `NotificationChannelCatalog` + `NotificationDefaults` for the per-channel "on by default?" answer (smtp on, others off until their underlying flow lands).
- `NotificationDispatcher` consults the repo per (recipient, trigger) before sending; anonymous recipients (no userId) fall back to channel default.
- `GET /api/users/notification-preferences` returns the full matrix (catalog × channels × cells with effective values); `PUT` replaces the caller's overrides (unknown trigger/channel ids silently dropped).
- Blazor `/Account/Benachrichtigungen` page — table with one row per trigger, one checkbox per channel; channels marked `Available = false` render disabled with a "bald" badge. Nav bell-icon next to the shield-lock.
- Tests: 5 repo, 6 endpoint, 3 dispatcher-gating integration.

**Phase 4: shipped (2026-05-27).** Telegram channel end-to-end + multi-channel dispatcher.
- `Telegram.Bot` 22.10 NuGet; outbound rewritten to `ITelegramBotClient.SendMessage`; channel writes its own Pending → Sent/Failed `NotificationLog` row (was email-only before).
- `User.TelegramChatId` column + new `TelegramLinkToken` (Token PK, UserId, CreatedAt, ExpiresAt, ConsumedAt) table — FK to User with cascade-delete. Both folded into M001.
- `TelegramBotClientFactory` builds an `ITelegramBotClient` per call from the current bot-token option (so token swaps take effect without restart). Surfaces `IsConfigured` via "token present?".
- `TelegramUpdateHandler` — pure logic, unit-testable with synthetic `Update` objects; handles `/start <token>` (transactional Consume via `TelegramLinkTokenRepository`) and replies with a hint for everything else.
- `TelegramReceiverBackgroundService` — long-polling `IHostedService` (30s timeout, message-only allowedUpdates). Idle when no token configured; per-iteration scope so each handler invocation gets a fresh DbContext.
- **Multi-channel dispatcher** (real change): iterates `recipients × channels`, per-channel `IsConfigured` gate, per-channel address resolution (email = recipient address from resolver, telegram = `User.TelegramChatId` looked up in one batch query), per-channel template (`notifications.{trigger}.{channelId}.body`). Email channel id aligned to `"email"` (was `"smtp"`) so the audit id matches the option-key path.
- Telegram body templates seeded for all three triggers; channel marked `Available = true` in catalog.
- `NotificationLogMetadataKeys` central constants (was scattered on `EmailMessageChannel`).
- Endpoints: `GET /api/users/telegram-link` (status), `POST` (start — returns token + deeplink built from `messaging.telegram.bot_username` option, null when not configured), `DELETE` (unlink — clears chat id + revokes unconsumed tokens).
- Blazor `/Account/Benachrichtigungen` gains a Telegram-link section: button to start link → opens deeplink in new tab, or shows raw token if username not configured; "Verknüpfung aufheben" for linked users.
- Phase 3 `NotificationPreferencesGetEndpoint`'s query-syntax LINQ was refactored to fluent in the same sweep.
- Tests: 7 link-token repo, 5 update-handler (synthetic Updates against a stub HttpMessageHandler), 6 endpoint, 4 multi-channel dispatcher (with stub `TelegramBotClientFactory` so we never call `api.telegram.org`). Refreshed the 6 `TelegramMessageChannelTests` for the `Telegram.Bot` rewrite.

---

## PDF envelope rendering (postal-mail-ready printouts)

`PdfMessageChannel` currently does a 1:1 "text into PDF" dump. For real postal use:

- **Address block layout** matching DIN-Lang window envelopes (DIN 5008): sender top-right, recipient at the standard window position so the address shows through the envelope window when folded.
- **Front page = cover sheet** with just sender + recipient + return address. Page 2+ is the letter content.
- **Batch generation** — given a meeting/event invitation flow, render N PDFs in one call (one per recipient who lacks email), each correctly addressed from the member's `Street`/`HouseNbr`/`PostCode`/`City`. Possibly produce a single multi-page PDF with one letter per page for cheaper bulk printing.
- **Templating** — invitation text rendered via the same Fluid template engine used for emails, with `member.*` variables. Probably shares a template-resolution layer with the email path.
- **Page numbering, letterhead, signature block** — make these configurable via Options (e.g. `messaging.pdf.letterhead_text`).
- **Print run tracking** — log which member got which letter so resends can be coordinated.
- **Caller-side trigger** — an "invite to meeting" admin endpoint that walks meeting attendees, picks the PDF channel for those without email, and produces a print batch in the output dir.

V1 is the file-on-disk plumbing. This is the usable-by-an-actual-human layer.

## Applicant-facing membership-application confirmation email — DONE (2026-05-28)

**DONE (2026-05-28).** The applicant now receives an "Antrag eingegangen" mail after they confirm their email (fired from `SubmissionMaterializer` once the real row is created — not at submit, where they already get the confirm-link mail). Direct transactional send via `MembershipApplicationMailService` → `EmailMessageChannel` (not a notification trigger). Templates `templates.membershipapplication.received.email.{subject,body}` (schema `ApplicationSubmittedPayload`), seeded in `OptionRepository`. Sent regardless of chapter. Tests in `ApplicationReceivedMailTests` (no log before confirm; one `application_received` log to the applicant after confirm; fires without a chapter).

## Application + due-selection approved/rejected decision mails — DONE (2026-05-28)

The previously-dead approved/rejected templates are now wired and sent to the applicant/submitter on decision.

- Templates restructured from single body-only keys to split `.subject`/`.body` (consistent with the received/welcome mails): `templates.{membershipapplication,dueselection}.{approved,rejected}.email.{subject,body}`. The 4 old single keys were removed (and purged from the dev DB).
- `MembershipApplicationMailService.SendApplicationDecisionAsync` + new `DueSelectionMailService.SendDueSelectionDecisionAsync` pick approved/rejected by the entity's current status and send directly via `EmailMessageChannel` (skip if not a terminal Approved/Rejected status, or if no email).
- Fired on every real transition through a shared `MotionResolutionDecisionMailer` (resolves a motion's linked application/due-selection and mails each): after a non-meeting motion vote auto-resolves (`MotionVoteEndpoint` → `TryAutoResolve`), after meeting close/complete (`MeetingLifecycleService`), and on the direct admin process endpoints (`MembershipApplicationProcessEndpoint`, `DueSelectionProcessEndpoint`).
- **Cascade gap fixed:** `MotionRepository.UpdateApprovalStatus` now cascades the motion resolution into the linked membership application + due selection (extracted a shared `CascadeResolutionToLinkedEntities` used by both it and `TryAutoResolve`; `FormallyRejected`→Rejected, `ClosedWithoutAction`→no change). Previously meeting-resolved motions left the linked application stuck Pending.
- Tests: `ApplicationDecisionMailTests` (2), `DueSelectionDecisionMailTests` (2), `MotionDecisionMailTests` (2: vote auto-resolve + meeting-complete cascade, each asserting both the status flip and the mail).

## Member welcome email — manual activation flow — DONE (2026-05-28)

Reframed per user: no automated first-payment tracking. Instead, an officer manually assigns a member number and sends the welcome mail from the **approved application's** detail page.

- `MembershipApplication` gained `MemberNumber` (int?) + `WelcomeSentAt` (DateTime?), folded into M001.
- `POST /api/admin/membershipapplications/welcome` ({Id, MemberNumber}) — gated on `ProcessApplications` (chapter-scoped, global fallback), requires `Status == Approved`, single-use (guarded by `WelcomeSentAt`). Sets number + timestamp, then `MembershipApplicationMailService.SendWelcomeAsync` mails the applicant the welcome with their number.
- Templates `templates.member.welcome.email.{subject,body}` (new `MemberWelcome` palette schema: `member.{FirstName,LastName,Email,MemberNumber}` + `chapter`).
- UI: a "Mitglied aktivieren" card on `MembershipApplicationDetail` (shown when Approved + caller has `ProcessApplications`): member-number input + "Willkommens-Mail senden"; after sending it shows the number + sent timestamp.
- Tests in `MembershipApplicationWelcomeEndpointTests` (8): auth/perm gating, not-approved guard, non-positive number, success sets fields + sends mail with the number in the body, idempotency, chapter-scoped processor.

A real first-dues-payment ledger / auto-trigger remains a possible future enhancement (a `MemberPayment` table), but is out of scope for this manual flow.

## Guided setup pages for SMTP / SAML / OIDC — DONE (2026-05-28)

Shipped: three permission-gated pages (`/Administration/Setup/{Smtp,Saml,Oidc}`, gated on `EditOptions`) reached from a button row under the "System – Einstellungen" header. Built on a reusable `OptionGroupEditor` component that loads `/api/options`, renders each key as checkbox / number / password (secret fields blank with "(unverändert lassen)", only saved when non-empty) / text, and saves through the existing options API. SMTP page adds a test-send card (`POST /api/email/test` → `SmtpTestService`, synchronous, surfaces the SMTP error inline). Runtime pickup verified: all SAML/OIDC consumers read per-request via the uncached `OptionRepository.GetGlobalValue`, so server-side changes take effect with no restart; `OptionGroupEditor.OnSaved` force-refreshes the admin's client-config snapshot so the login-page SSO buttons update without a manual reload.

Original spec below for reference.

The generic options list (`/Administration/Options`) works but is a poor first-run experience for multi-field integrations. Add focused, permission-gated setup pages that group the related settings and (for SMTP) let you test it.

- **Entry points:** a row of buttons directly under the "System – Einstellungen" header (above the options list) linking to the three setup pages. Each button gated by the same permission that controls editing options (`ViewOptions`/`EditOptions`), so they only appear if the user may configure them.
- **SMTP page** — two stacked cards:
  - Top card: all 8 SMTP settings side-by-side / in a grid — `email.smtp.host`, `email.smtp.port`, `email.smtp.use_ssl`, `email.smtp.username`, `email.smtp.password`, `email.smtp.sender_address`, `email.smtp.sender_name`, `email.smtp.batch_size`. Save writes them through the existing options API.
  - Bottom card: a **test send** — an email field + "Test-E-Mail senden" button that hits a new endpoint which sends a fixed test message via the current SMTP config and reports success/failure inline (surface the SMTP error text on failure). Needs a small `POST /api/email/test` endpoint (permission-gated) that sends synchronously and returns the result rather than going through the queue, so the user sees the outcome immediately.
- **SAML page** — groups `auth.saml.endpoint`, `auth.saml.client_id`, `auth.saml.certificate`, `auth.saml.button_text`, `auth.saml.expected_audience`, `auth.saml.expected_destination`, plus `auth.sso.support_contact`. (No live test send — SAML needs a full browser round-trip; out of scope.)
- **OIDC page** — groups `auth.oidc.authority`, `auth.oidc.client_id`, `auth.oidc.client_secret`, `auth.oidc.button_text`, plus `auth.sso.support_contact`.
- These are just nicer views over the same `SystemOption` rows — no new persistence. The generic options page stays as the fallback/advanced editor.

## Authenticated submissions skip email confirmation — DONE (2026-05-29)

Shipped: `SubmissionAcceptedResponse` extended with `RequiresConfirmation` (default `true`) + optional `CreatedEntityId`. `SubmissionMaterializer` promoted per-kind methods (`MaterializeMotionDirect` / `MaterializeDueSelectionDirect` / `MaterializeApplicationDirectAsync`) returning the new entity id. Each create endpoint branches on `PermissionContext.UserId`: authed → materialize directly with `RequiresConfirmation=false` + entity id; anonymous → unchanged intake/confirm flow.

`LoginUserInfo` extended with `FirstName` + `LastName` (populated by login/session/user-settings endpoints) so the due-selector frontend can prefill them.

Frontend:
- Motion create (admin + public): prefill + disable AuthorName/AuthorEmail from `AuthService.CurrentUser` when authed; success branches on `RequiresConfirmation`. Admin path: side-effect fix — page was already broken before this work, navigating to `Guid.Empty` since it expected `MotionDTO` from a `SubmissionAcceptedResponse`-shaped endpoint.
- Due selector `UserDataInput` + `Summary`: prefill + disable FirstName/LastName/Email; success branches.
- Membership application: yellow "Du bist eingeloggt — Antrag im Namen einer anderen Person" warning on the wizard's first page when authed (no prefill, since it's an officer entering a paper application). ApplicationSummary success branches.
- All four pages await `AuthService.WaitForInitializationAsync()` in their initializers — a second `AuthStateProvider` TCS that completes after `/api/users/session` returns (not at the start of the call, which `WaitForInitialization` does for the CSRF handler). This fixes a hard-reload race where prefill/warning logic ran before auth state arrived.

DEBUG-only no-SMTP convenience: anonymous create endpoints also materialize directly under `#if DEBUG` when `SmtpConfig.ReadFrom(_optionRepo) == null`, so a dev box with no SMTP set up doesn't get a dead "check your email" notice. Inlined in each endpoint, the field/parameter/check are all `#if DEBUG`-gated. Tests seed dummy SMTP host/sender via `OptionRepository.SetValue` so the confirm-flow tests still exercise the real intake path.

Tests: 1 authed-create test per endpoint (entity in live table immediately, no `PendingSubmission`, linked motion present for application). 9 + 6 + 8 endpoint suites green.

## Async notification dispatch (off the request thread) — DONE (2026-05-28)

Shipped: `INotificationDispatchQueue` with `ChannelNotificationDispatchQueue` (singleton, unbounded `Channel<NotificationDispatchRequest>`) + `NotificationDispatchBackgroundService` draining it in a per-item scope. The three submit endpoints call `_notifications.Enqueue(...)` instead of `await DispatchAsync(...)`. Tests swap in `InlineNotificationDispatchQueue` (runs dispatch synchronously) so the "submit then assert on logs" tests stay deterministic. Submit-endpoint latency is now independent of recipient count / channel mix.

The model-factory closures turned out to already capture only plain data (entity POCOs + strings), so no reshaping into payload snapshots was needed — they run fine in the background scope as-is.

**Still open (deferred):** crash-recovery re-enqueue. The in-memory channel loses queued-but-undispatched requests on a hard restart. Lower priority than the email re-queue because a lost notification dispatch just means officers don't get pinged about one submission — the underlying motion/application/due-selection row is still safely persisted. If we want durability, the dispatch request itself needs persistence (a `PendingDispatch` table drained on startup), since `NotificationLog` rows are only written once dispatch reaches a channel.

## Motion full edit + audit log — DONE (2026-05-30)

Shipped:
- **`PUT /api/motions/{id}`** (`MotionUpdateEndpoint` + `MotionUpdateRequest` + validator). Gated by `EditMotions` on the motion's chapter; locked once `ApprovalStatus != Pending` (409). Validates `LinkedMembershipApplicationId` / `LinkedDueSelectionId` exist when non-null.
- **Markdown source on Motion**: new `TextMarkdown` column folded into M001 (`Text` remains the rendered HTML for display). `SubmissionMaterializer`, `ApplicationReviewService`, and `ChecklistItemExecutor` populate both. Audit diffs the Markdown, not the HTML.
- **`MotionRepository.Update`**: per-field diff + transactional persist + one `_auditLog.LogFieldChange` per change. Unchanged fields produce zero audit entries.
- **`AuditLogEndpoint` loosened**: for `entityType == "Motion"`, accepts `ViewMotions` on the motion's chapter (global `ViewAudit` still works as a superset). Other entity types still require global `ViewAudit`.
- **`MotionDetailDTO.TextMarkdown`** populated only when the caller has `EditMotions` (used to prefill the edit form).
- **Frontend**: `MotionDetail` page gained a "Bearbeiten" button (visible when `EditMotions` + Pending) toggling an inline edit form with `MarkdownEditor`. New "Änderungsverlauf" card at the bottom lists audit entries with German field labels.
- **Tests**: 6 `MotionUpdateEndpointTests` (auth/perm gates, lock when not Pending, per-field diff produces correct rows, no-op when unchanged, linked-id validation), 2 new `AuditLogEndpointTests` for the chapter-perm path.

## Frontend timezone sweep — show local time, not UTC — DONE (2026-05-30)

Shipped:
- **`Quartermaster.Api.DateTimeExtensions`**: `ToDtoUtc()` (DateTime→UTC-anchored DateTimeOffset), `ToDtoDate()` (DateTime→DateOnly), `ToStorage()` (DateOnly→midnight-UTC DateTime). Nullable companions for each.
- **Storage stays `DateTime` UTC** (date-only fields with time component zeroed). M001 timestamp columns bumped to `DATETIME(6)` for microsecond precision so the audit log sorts deterministically under rapid edits; applied via `ALTER TABLE … MODIFY COLUMN … DATETIME(6)` on the dev DB.
- **DTOs split by intent**: timestamps → `DateTimeOffset` (CreatedAt, ResolvedAt, SubmittedAt, ProcessedAt, VotedAt, StartedAt, CompletedAt, IssuedAt, ExpiresAt, SentAt, WelcomeSentAt, ImportedAt, LastImportedAt, Timestamp); calendar dates → `DateOnly` (DateOfBirth, EntryDate, ExitDate, ReducedFeeEnd, MeetingDate, EventDate, DateFrom/DateTo).
- **Endpoint mappings updated** in every site that emits one of these (26 files: motion/meeting/event/application/due-selection/member/dashboard/audit/notification/session/lockout endpoints, plus `MeetingLifecycleService`/`MeetingProtocolEndpoint`/`SubmissionMaterializer`).
- **`<LocalTime>`** Blazor component: `Value` (DateTimeOffset?) renders browser-local with default `dd.MM.yyyy HH:mm`; `DateValue` (DateOnly?) renders `dd.MM.yyyy`; both customizable via `Format`/`DateFormat`. Null renders nothing.
- **Frontend sweep**: every `.ToString("dd.MM.yyyy[ HH:mm]")` in razor pages → `<LocalTime ...>` (25 files: Home, UserSessions, UserSettings, all admin detail/list pages, public event pages, membership-application summary, EventChecklistEditor component).
- **Tests**: all `new DateTime(yyyy, mm, dd)` for date-only fields → `new DateOnly(yyyy, mm, dd)`; `DateTime.UtcNow.Date` → `DateOnly.FromDateTime(DateTime.UtcNow)`. 1269/1270 green (the one failing is a pre-existing E2E flake — passes in isolation).
- **`Quartermaster.Rendering.TemplateMockDataProvider`** updated for the type changes so template-preview mock data stays representative.

## Pre-production i18n sweep + language switcher — DONE (2026-05-30)

Shipped in three phases.

**Phase A — Infrastructure.** `I18nService` gained a `Reload(json)` for hot-swap + a razor-friendly indexer (`@I18n["motions.title"]`). New `LanguageService` resolves the initial language (localStorage → `navigator.language` → `"de"`), fetches `i18n/{lang}.json`, swaps it into the singleton, and force-reloads on switch so every page re-renders without per-component event wiring. `<LanguageSwitcher>` dropdown lives next to the dark-mode toggle in `MainNavBar`.

**Phase B — Backend `AddError` audit.** 13 raw-German `AddError(...)` calls converted to `I18nKey.Error.*` constants across `ChapterCreate/Update/Delete`, `EmailTest`, `MotionCreate`, `MotionUpdate`. 10 new error keys + their de/en translations. Also backfilled two officer-error keys that were missing from `en.json`.

**Phase C — Razor sweep.** Every `.razor` and `.razor.cs` under `Quartermaster.Blazor/Pages/`, `Layout/`, and `Components/` swept. Hardcoded German replaced with `@I18n[I18nKey.Ui.<Page>.<Key>]`. Page-specific keys live under `I18nKey.Ui.<PageName>.*`; cross-cutting labels (Save/Cancel/Edit, status enums, officer roles) under `I18nKey.Ui.Common.*`, `Ui.MotionStatus.*`, `Ui.OfficerRole.*`, etc. Static switch helpers (`FieldLabel`, `ValuationLabel`, `RoleLabel`, …) became instance methods using `I18n[…]`. Date format strings stay as-is on `<LocalTime>` since browser-local formatting is handled there.

Totals: **~960 new keys** across ~80 razor files. `I18nKey.cs` grew to 1607 lines; `de.json` / `en.json` to ~1335 lines each.

Coverage caveats: tests assert on status codes (not strings), so the sweep didn't break test suites. Some technical strings remain hardcoded by intent (CSS class names, enum value sentinels like `"AdministrativeDivision"`, Mustache template literals in `EventChecklistEditor` preview text). The notification-log page DTO exists server-side but no Blazor list page was found.

Date/number culture-aware formatting is **not** wired into the language switch yet — `<LocalTime>` still uses the hardcoded `dd.MM.yyyy[ HH:mm]` format. Folding that into the locale toggle is a small follow-up; tracking under whichever sub-todo picks it up first.
