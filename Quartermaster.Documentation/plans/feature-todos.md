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
