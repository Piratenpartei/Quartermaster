# Feature TODOs

Features that aren't quality fixes — net-new functionality, infrastructure for new capabilities, etc. Tracked separately from `code-quality-todos.md` so the two lists don't get muddled.

---

## SignalR for live meeting updates + collaborative editing

- **Task:** Add SignalR hub for real-time push to meeting participants. Two use cases:
  1. Live meeting page auto-updates when another officer votes or completes an agenda item
  2. Collaborative editing for agenda item notes (prerequisite for the collaborative writing feature deferred from v1)
- **Why:** Currently the live meeting page requires manual refresh to see changes made by other participants. SignalR would enable real-time multi-officer minute-taking.
- **How to apply:** Add `Microsoft.AspNetCore.SignalR` package; create `MeetingHub` with groups per meeting ID; push events from meeting-mutating endpoints; Blazor UI subscribes via `HubConnection`.
- **Note:** This is also the foundation for the collaborative writing TODO noted in the meeting system design plan (last-write-wins → CRDT/OT for notes).
