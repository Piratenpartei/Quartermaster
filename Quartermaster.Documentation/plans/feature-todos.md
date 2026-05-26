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

**Phase 2 (next):** Two more triggers — `application_submitted` and `due_selection_submitted` — wired through their public-create endpoints. Recipients via `ProcessApplications` / `ProcessDueSelections`. Default templates seeded.

**Phase 3:** Per-user channel preferences. New `UserNotificationPreference(UserId, TriggerId, ChannelId, Enabled)` table; dispatcher consults before fanning out (default: email-on for every trigger). New `/Account/Benachrichtigungen` Blazor page.

**Phase 4:** Replace v1 raw-HTTP `TelegramMessageChannel` with `Telegram.Bot` NuGet + a long-polling `TelegramReceiverBackgroundService` for the `/start <link-token>` deeplink flow. New `TelegramLinkToken` table; account-page UI to start the link. Switches outbound to `ITelegramBotClient.SendTextMessageAsync` for the package's rate-limiting / retry.

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
