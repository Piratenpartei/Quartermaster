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

## Authenticated submissions skip email confirmation

Context: as of 2026-05-28 every public submission (motion / due selection / membership application) is held in `PendingSubmission` until the submitter clicks an emailed confirm link — the spam barrier. This applies to *everyone*, including logged-in officers/admins, because the three create endpoints (`MotionCreateEndpoint`, `DueSelectionCreateEndpoint`, `MembershipApplicationCreateEndpoint`) are `AllowAnonymous` and treat all callers identically. An authenticated user creating a motion from the admin UI currently has to email-confirm it too, which is wrong.

Goal: authenticated users create directly (no confirmation), anonymous users keep the confirm flow.

**Backend**
- In each create endpoint, branch on `_perms.UserId != null` (possibly gate on a permission): authed → call `SubmissionMaterializer` directly (entity created + notifications fire immediately); anonymous → keep `SubmissionIntakeService.AcceptAsync` (stash + confirm email). The materializer is already factored out and callable.
- Response shape differs per branch (materialized entity vs `SubmissionAcceptedResponse`) — pick a response the frontend can disambiguate, or return a flag like `{ requiresConfirmation: bool }`.

**Frontend** (the reason this isn't a trivial backend change)
- **Membership application wizard:** if the user is authenticated, warn up front that they are logged in and may therefore only submit an application *on behalf of another person* (an officer entering a paper application). Don't pre-fill from their account — it's explicitly for someone else.
- **Motion + due selection:** if authenticated, auto-fill the author/submitter fields (name, email) from the logged-in user's account and disable those inputs — no need to ask. On submit, the entity is created immediately (no "check your email" step; go straight to the success/redirect path).
- Keep the current anonymous flow (manual fields + confirmation notice) unchanged when not logged in.

**Tests:** authed create → entity exists immediately + notifications fire, no `PendingSubmission` row; anonymous create → unchanged (pending + confirm).

## Async notification dispatch (off the request thread) — DONE (2026-05-28)

Shipped: `INotificationDispatchQueue` with `ChannelNotificationDispatchQueue` (singleton, unbounded `Channel<NotificationDispatchRequest>`) + `NotificationDispatchBackgroundService` draining it in a per-item scope. The three submit endpoints call `_notifications.Enqueue(...)` instead of `await DispatchAsync(...)`. Tests swap in `InlineNotificationDispatchQueue` (runs dispatch synchronously) so the "submit then assert on logs" tests stay deterministic. Submit-endpoint latency is now independent of recipient count / channel mix.

The model-factory closures turned out to already capture only plain data (entity POCOs + strings), so no reshaping into payload snapshots was needed — they run fine in the background scope as-is.

**Still open (deferred):** crash-recovery re-enqueue. The in-memory channel loses queued-but-undispatched requests on a hard restart. Lower priority than the email re-queue because a lost notification dispatch just means officers don't get pinged about one submission — the underlying motion/application/due-selection row is still safely persisted. If we want durability, the dispatch request itself needs persistence (a `PendingDispatch` table drained on startup), since `NotificationLog` rows are only written once dispatch reaches a channel.

## Motion full edit + audit log

Today motions support partial mutations only: approval status / realized flag (`MotionStatusEndpoint`) and visibility toggle (`POST /api/motions/status` with `IsPublic`). All three already write `AuditEntry` rows via `MotionRepository.SetRealized`/`SetPublic`/`UpdateApprovalStatus`.

Missing: editing the substantive fields (Title, Text, AuthorName, AuthorEmail, LinkedMembershipApplicationId, LinkedDueSelectionId).

- New endpoint (e.g. `PUT /api/motions/{id}`) gated by `EditMotions` on the motion's chapter.
- Diff every changed field against the stored row and emit one `AuditEntry` per change via `_auditLog.LogFieldChange` (the pattern is already used by every other motion mutation). For long Text changes the audit row stores the full before/after — fine for now, can be moved to a diff/patch representation later if volume becomes a problem.
- For motions already linked to a meeting (or already resolved), think about whether edits should be allowed at all or require an explicit "supersede" workflow. Simplest first cut: lock editing once `ApprovalStatus != Pending`.
- Audit-log viewer page on the motion detail to make change history visible to officers — the entries exist already but nothing surfaces them yet.
