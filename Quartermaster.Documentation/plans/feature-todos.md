# Feature TODOs

Features that aren't quality fixes — net-new functionality, infrastructure for new capabilities, etc. Tracked separately from `code-quality-todos.md` so the two lists don't get muddled.

---

## Collaborative meeting notes (SignalR + CodeMirror)

Real-time collaborative editing of agenda item notes during a meeting, plus live meeting page updates (votes, status changes, agenda completions). Six implementation phases, ~29–43 hours total estimated effort. Phases 1–2 (~10h) deliver standalone value (live page updates + a much better notes editor with line numbers); Phase 3 is the make-or-break collaborative-editing core.

**Full plan**: [`2026-04-10-collaborative-meeting-notes.md`](./2026-04-10-collaborative-meeting-notes.md)

**Open questions for the user before implementation begins** (see end of plan):
1. Vendored CodeMirror vs add a JS build pipeline?
2. Acceptable worst-case edit loss on server restart? (30s with the proposed save interval)
3. Keep the markdown preview pane in the editor?
4. Color palette preference (suggesting Tol Bright)?
5. Does the protocol PDF need real-time consistency, or is "few seconds behind live" acceptable?

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

## Async notification dispatch (off the request thread)

Today every submit endpoint (`MotionCreateEndpoint`, `MembershipApplicationCreateEndpoint`, `DueSelectionCreateEndpoint`) calls `NotificationDispatcher.DispatchAsync` inline before responding. Email is fine — it queues to a `Channel<EmailMessage>` and returns immediately. Telegram is not — `TelegramMessageChannel.SendAsync` does a synchronous HTTPS round-trip to `api.telegram.org` per recipient, so the user's POST hangs for roughly `recipientsWithTelegram × ~300ms` before the page can redirect.

- New `Channel<NotificationDispatchRequest>` singleton holding `(triggerId, payload-snapshot, modelFactoryArgs, sourceEntityType, sourceEntityId)`. Payload has to be a snapshot the background worker can use without the original request scope — most current payloads are already pure records, but the model factory closures capture per-recipient state and need to be reshaped (probably move template-model construction into the resolver or onto the payload itself).
- New `NotificationDispatchBackgroundService` drains the queue in its own scope and runs the existing dispatcher logic. Errors get logged per-trigger; failed sends already get tracked in `NotificationLog` rows via the channels themselves.
- All three submit endpoints (plus future ones) call a thin `_notifications.Enqueue(...)` instead of `await DispatchAsync(...)`.
- Re-enqueue any "Pending" `NotificationLog` rows on startup for crash recovery, mirroring `EmailSendingBackgroundService.RequeuePendingLogs`. Caveat: NotificationLog rows are written by channels DURING dispatch, so the queue itself needs its own persistence (or the dispatcher writes a "queued" log row before handing off to channels).

Effect: submit-endpoint latency drops to single-digit ms regardless of recipient count or channel mix.

## Motion full edit + audit log

Today motions support partial mutations only: approval status / realized flag (`MotionStatusEndpoint`) and visibility toggle (`POST /api/motions/status` with `IsPublic`). All three already write `AuditEntry` rows via `MotionRepository.SetRealized`/`SetPublic`/`UpdateApprovalStatus`.

Missing: editing the substantive fields (Title, Text, AuthorName, AuthorEmail, LinkedMembershipApplicationId, LinkedDueSelectionId).

- New endpoint (e.g. `PUT /api/motions/{id}`) gated by `EditMotions` on the motion's chapter.
- Diff every changed field against the stored row and emit one `AuditEntry` per change via `_auditLog.LogFieldChange` (the pattern is already used by every other motion mutation). For long Text changes the audit row stores the full before/after — fine for now, can be moved to a diff/patch representation later if volume becomes a problem.
- For motions already linked to a meeting (or already resolved), think about whether edits should be allowed at all or require an explicit "supersede" workflow. Simplest first cut: lock editing once `ApprovalStatus != Pending`.
- Audit-log viewer page on the motion detail to make change history visible to officers — the entries exist already but nothing surfaces them yet.
