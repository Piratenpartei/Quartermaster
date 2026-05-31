# Future enhancements

Tracker for things deliberately deferred from the current codebase. Not a backlog of bugs — a parking lot for ideas the design noted as "we should do this later."

## Scheduling tags

Some unchecked items aren't simply "next up" — they've been actively pushed down the priority list. To keep the working order honest:

- **(V*x*)** — soft defer. Scheduled for a specific upcoming version (e.g. `(V2)`, `(V3)`). Still on the roadmap; expected to ship in that release.
- **(Backlog)** — hard defer. May be dropped entirely. Other items take priority; reconsider only after everything else is done. Don't pick up unless explicitly re-prioritised.

Items without either tag are eligible for normal scheduling (ASAP). The current codebase is **V1**; everything below targets **V2** or later.

## Notifications

- [ ] **Email MDN (read-receipt) tracking + stats.** *(V2)* Request a Message Disposition Notification on outbound emails (RFC 8098: `Disposition-Notification-To` header), capture inbound MDN replies, and surface per-send / per-template / per-chapter delivery + read stats. Stats worth exposing: send count, MDN-opt-in rate (some clients suppress the receipt entirely), read rate, time-to-read distribution, and per-template / per-trigger breakdowns so officers can see which templates actually land. Storage probably extends `NotificationLog` with `MdnRequestedAt` / `MdnReceivedAt` / `MdnDisposition` columns. UI lives on a new tab inside the template detail page (per-template stats) plus a global view under Administration/Notifications.

## Templates

- [ ] **Surface custom-tag values + parse errors in the template preview.** *(V2)* The markdown preview pane already shows the body with Fluid + mock data substituted. Extend it to also show: (a) a table / panel listing every captured envelope (and any future custom tag) name → evaluated value, so authors can see exactly what their tags produced without rendering the PDF; (b) any Fluid parse / render error inline (right now `TemplateRenderer.RenderTextAsync` returns the error string but the preview transform silently falls back to the raw source on failure). Probable shape: small "Tag values" / "Parse errors" sub-cards under the preview pane, populated from `EnvelopeTags.Extract(context)` + the error tuple.
- [ ] **Custom PDF layouts.** *(V2)* Today the PDF renderer ships two hard-coded modes (Simple + Envelope/DIN 5008 B). For real-world chapter use we want to define richer layouts on a per-chapter (or per-template) basis — e.g. a first page with the chapter logo in the top-right, a column on the right with post address / bank details / chair names, then the template body filling the rest. Design intent: layouts are editable in-app via an online editor (visual or structured-form, not a code editor), so a non-technical officer can lay out their chapter's letterhead. Templates pick a layout (the existing `RenderMode` dropdown becomes a layout selector). The layout owns: page margins, fixed-position blocks (logo / sender / sidebar / footer), font defaults, address-window placement, body region bounds. Big feature — needs its own design pass before implementation.
- [ ] **PDF / template i18n per recipient language.** *(Backlog)* Render PDF templates (and probably emails too) in the recipient's preferred language. Requires: (a) a `Language` attribute on `Member` (and probably `MembershipApplication` / `DueSelection`) with a sensible default per chapter; (b) a tag system inside templates to declare per-language variants of strings — sketch: `{% i18n "greeting" %}Sehr geehrte/r ...{% else "en" %}Dear ...{% endi18n %}` or per-language template overrides keyed by language code; (c) dispatcher picks the right variant based on recipient language. Out of scope for V1.
