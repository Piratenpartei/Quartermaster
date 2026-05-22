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

A "Meine Sitzungen" page where a logged-in user can see all their currently valid login tokens with last-seen metadata, and revoke any of them. Came out of the 2026-05-22 code review: chosen as the user-visible counterpart to IP/UA audit columns on `Token`, in place of automatic IP-binding (which has bad mobile-UX tradeoffs).

Infrastructure already in place after the May 2026 token-auth pass:
- `Token.IssuedAt`, `Token.IssuedIp`, `Token.IssuedUserAgent` columns populated on login (manual + SAML + OIDC paths)
- `TokenRepository.DeleteAllForUser` exists; needs a single-token revoke counterpart

Sketch of the work:
- New `GET /api/users/sessions` endpoint → list of `(TokenId, IssuedAt, ExpiresAt, IssuedIp, IssuedUserAgent, IsCurrent)` for the calling user. `IsCurrent` marks the bearer token used to make the call.
- New `DELETE /api/users/sessions/{id}` endpoint → revoke one token (must belong to caller). Returns 204 even if already gone (idempotent).
- New `POST /api/users/sessions/revoke-others` endpoint → revoke all of caller's tokens except the current one.
- New `Quartermaster.Blazor/Pages/UserSessions.razor(.cs)` page — table of sessions, "Diese Sitzung" badge on the current one, "Abmelden" button per row, "Alle anderen abmelden" button at the top. Link from the nav menu (next to UserSettings).
- Tests: list-only-mine isolation, revoke-only-mine isolation, revoke-current works (and next request returns 401), revoke-others preserves the bearer.
- Optionally: user-agent string prettifying (UA-Parser-style) so users see "Firefox on Windows" instead of the raw header. Defer until v2 — raw UA is acceptable for a v1.

**Why a feature, not a quality fix:** the audit columns are already populated and stable. This is a new surface; tracked here rather than in `code-quality-review-todos.md`.
