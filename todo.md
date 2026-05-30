# Future enhancements

Tracker for things deliberately deferred from the current codebase. Not a backlog of bugs — a parking lot for ideas the design noted as "we should do this later."

## Scheduling tags

Some unchecked items aren't simply "next up" — they've been actively pushed down the priority list. To keep the working order honest:

- **(V*x*)** — soft defer. Scheduled for a specific upcoming version (e.g. `(V2)`, `(V3)`). Still on the roadmap; expected to ship in that release.
- **(Backlog)** — hard defer. May be dropped entirely. Other items take priority; reconsider only after everything else is done. Don't pick up unless explicitly re-prioritised.

Items without either tag are eligible for normal scheduling (ASAP). The current codebase is **V1**; everything below targets **V2** or later.

## Notifications

- [ ] **Email MDN (read-receipt) tracking + stats.** *(V2)* Request a Message Disposition Notification on outbound emails (RFC 8098: `Disposition-Notification-To` header), capture inbound MDN replies, and surface per-send / per-template / per-chapter delivery + read stats. Stats worth exposing: send count, MDN-opt-in rate (some clients suppress the receipt entirely), read rate, time-to-read distribution, and per-template / per-trigger breakdowns so officers can see which templates actually land. Storage probably extends `NotificationLog` with `MdnRequestedAt` / `MdnReceivedAt` / `MdnDisposition` columns. UI lives on a new tab inside the template detail page (per-template stats) plus a global view under Administration/Notifications.
