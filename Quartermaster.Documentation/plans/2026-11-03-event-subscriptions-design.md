# Event subscriptions (V9.0)

## Goal

Members miss motion deadlines and the start of online events because nothing reminds them. Any logged-in user who can see an event can subscribe to it by hand and then gets reminders before each motion deadline of the event's motion portal and before the event starts, a notice when the date or start time changes, and the existing "Antrag eingereicht" notification for motions submitted to that portal. There are no automatic subscriptions.

## Data

Two new tables, folded into M009.

`EventSubscriptions`:

- `Id`, `EventId`, `UserId`, `CreatedAt`
- unique on (`EventId`, `UserId`)

Subscriptions belong to the user, not the member: notifications and event visibility both work per user, and accounts without a member (administrators) can subscribe too.

`EventReminderDeliveries`:

- `Id`, `SubscriptionId`, `Kind`, `TargetAt`, `SentAt`
- unique on (`SubscriptionId`, `Kind`, `TargetAt`)
- `Kind` is an enum stored as int: `Deadline = 0`, `StartDayBefore = 1`, `StartSoon = 2`
- `TargetAt` is the UTC instant the reminder is about (the deadline or the event start), not the send time

Unsubscribing deletes the subscription and its deliveries. Subscribing again creates a new row with a new `CreatedAt`.

M009 also renames the display names of the two existing system templates of `assembly_motion_submitted` (see below), because `TemplateRepository.SupplementDefaults` only adds missing templates and never updates existing ones. The dev DB gets the same changes by hand.

## Who can subscribe

- Login required. The event must be visible to the user by the rule of `AssemblyMotionEventVisibility.IsVisible` (public, members only for any logged-in user, private only with `ViewEvents` on the event's chapter).
- Only while the event is neither `Completed` nor `Archived` and not deleted. After that the button is hidden and subscribing is refused; existing subscriptions stay but no longer send anything.
- Unsubscribing is always allowed, also for completed, archived or no longer visible events.

## Reminders

A hosted service `EventReminderHostedService` runs every minute and calls `EventReminderService.SendDue(nowUtc)`, built like `AssemblyMotionDeadlineHostedService` (scope per run, errors logged, loop continues).

For every subscription of an event that is not deleted, not `Completed` and not `Archived`, the service works out the due reminders:

- Deadline reminders: for the event's motion portal (if any), the set of distinct effective deadlines over all its types, computed with `AssemblyMotionDeadlines.Effective` for normal motions and, where a type has a `CompetingDeadline`, for competing motions. Each distinct instant is one reminder with `Kind = Deadline` and `TargetAt` = the deadline; it lists the names of the types whose deadline it is (normal and competing named separately, e.g. "Satzungsänderungsantrag (Konkurrenzanträge)"). Send time is 36 hours before the deadline.
- Start reminders, only when `EventDate` is set: the start is `EventDate` plus `StartTime` read as Berlin local time and converted with `BerlinTime.ToUtc`.
  - `StartDayBefore`: 24 hours before the start. Without a start time, `TargetAt` is the event date at 00:00 Berlin time and the send time is 9:00 Berlin time the day before.
  - `StartSoon`: 30 minutes before the start. Skipped without a start time.
  - The day-before reminder is only due until the 30-minute one begins, so a start moved to within 30 minutes gets one reminder, not two. When it goes out on the day of the event itself (the start was moved to today), it says "beginnt heute" instead of "beginnt morgen".
  - A start time inside the spring-forward gap (02:00 to 03:00 on the last Sunday of March) does not exist; it is read one hour later (`BerlinTime.ToUtcLenient`). An event that still fails is logged and skipped, the other events keep their reminders.

A reminder is sent when all of these hold:

- the send time is reached (`sendAt <= now`)
- the target is still in the future (`TargetAt > now`)
- the send time is not before the subscription was created (`sendAt >= subscription.CreatedAt`), so reminders whose time had passed at subscribe time are skipped
- no delivery row exists for (subscription, kind, `TargetAt`)
- the user can still see the event (visibility checked with a `PermissionContext` bound to the user via `Bind`); otherwise the reminder is skipped without a delivery row, so it can still go out if the event becomes visible again before its target

When a time moves after its reminder went out, the new `TargetAt` has no delivery row, so the reminder is due again for the new time. If the service was down, a reminder still goes out late as long as its target is in the future. The delivery row is written before enqueueing, in the same way `SendCollectingReminders` marks the motion first, so a crash never sends twice.

## Change notice

`EventUpdateEndpoint` compares `EventDate` and `StartTime` before and after the update. When either changed and the event is still upcoming (the new start, or the old one if the date was removed, is in the future, and the event is neither `Completed` nor `Archived`), all subscribers who can see the event get a change notice under the `event_reminder` trigger. A removed date or start time is reported as "noch offen". Changes of a motion deadline only reschedule the deadline reminder; there is no notice for them.

## Notifications

Two new triggers in `NotificationTriggers`, both on by default through the normal channel defaults (email on):

- `event_deadline_reminder`, catalog name "Antragsfristen abonnierter Veranstaltungen", description "36 Stunden vor jeder Antragsfrist einer Veranstaltung, die du abonniert hast."
- `event_reminder`, catalog name "Erinnerungen zu abonnierten Veranstaltungen", description "24 Stunden und 30 Minuten vor Beginn einer Veranstaltung, die du abonniert hast, und wenn sich ihr Termin ändert."

A new payload (event id, the user ids) and a new `EventSubscriptionRecipientResolver` for both triggers resolve the users to recipients (not deleted, with an email address), like `AssemblyMotionRecipientResolver.ResolveMembers` does for members. The template model `event` carries the public name, the date and time as text, a `Kind` for the start reminders and the change notice (`day_before`, `soon`, `changed`), the type list for the deadline reminder, the meeting link (only filled for `soon`) and the path to the event page (`/Events/{id}`) or, for deadline reminders, the portal (`/Events/{id}/Motions`). No member names appear in any of these notifications.

Seeds in `SystemTemplateSeeds`, email and Telegram for each trigger; every seed contains the link to the event or portal page:

- Deadline reminder: subject "Antragsfrist endet bald: {{ event.Name }}", body "Die Antragsfrist für {{ event.TypeNames }} endet am {{ event.Date }} um {{ event.Time }} Uhr." plus [Zum Antragsportal].
- Start reminder, day before: subject "{{ event.Name }} beginnt morgen", body with date and time plus [Zur Veranstaltung].
- Start reminder, soon: subject "{{ event.Name }} beginnt in 30 Minuten", body plus [Zur Veranstaltung] and, when set, [Zum Treffen] with the meeting link.
- Change notice: subject "Neuer Termin: {{ event.Name }}", body with the new date and time ("noch offen" for a removed one) plus [Zur Veranstaltung].

One template per trigger and channel; the start reminder and change notice texts are selected inside the template by `event.Kind`.

## "Antrag eingereicht" for subscribers

- `AssemblyMotionNotificationPayload` gets `IncludeSubscribers`. `AssemblyMotionNotifier.Submitted` sets it.
- `AssemblyMotionRecipientResolver` then adds the users subscribed to the portal's event who can still see it; the existing de-duplication per user keeps one notification per person.
- Link: subscribers get the member view (`/AssemblyMotions/{id}`). A subscriber who is also on the motion commission of the event's chapter gets the commission view, so `PathFor` gives the commission path to commission recipients first.
- Texts: catalog name "Antrag eingereicht (Antragskommission und Abonnements)", description "Sobald ein Antrag zu einer Versammlung eingereicht wird: für die Antragskommission der Gliederung und für alle, die die Veranstaltung abonniert haben." The display names of both system templates change to match (in the seeds and through M009 for existing installations). Subject and body stay as they are.

## API

All require a login.

- `POST /api/events/{id}/subscription`: subscribes; 404 for a missing or deleted event, the visibility rule as on the event page (401/403/404 like `EventDetailEndpoint`), 409 with a reason for a completed or archived event. Idempotent: an existing subscription answers 204 and keeps its `CreatedAt`.
- `DELETE /api/events/{id}/subscription`: unsubscribes, 204 also when there was none.
- `GET /api/users/event-subscriptions`: the caller's subscriptions with event id, public name, date, start time and status, ordered by date (events without a date last). An event the caller can no longer see appears without name and date, so it can still be unsubscribed.
- `EventDetailDTO` gets `IsSubscribed` (false for anonymous callers). The portal page reads it from the same flag where it already loads the event, or from the portal context DTO if it does not load the event detail.

## UI

- `/Events/{id}` (`PublicEventDetail`) and `/Events/{id}/Motions` (`AssemblyMotionPortal`): a toggle button "Abonnieren" / "Abonniert" (bell icon, outline when not subscribed) for logged-in users while subscribing is possible; a subscribed user also sees it on completed events so they can unsubscribe. A short hint under the button: "Du erhältst Erinnerungen an Antragsfristen und den Beginn." Errors show as an alert.
- `/Account/Notifications`: a new card "Abonnierte Veranstaltungen" listing the subscriptions (name linked to the event, date) with a button "Abbestellen" each; an empty list shows "Du hast keine Veranstaltungen abonniert."
- All texts as i18n keys in `de.json` and `en.json`.

## Docs

- `Changelog/V9.0.md`: a section "Veranstaltungen" describing subscribing, the reminders and the change notice, and the subscribers in "Antrag eingereicht".
- PiratenWissen `quartermaster/`: a block about subscribing to events (German, "Quelle: Bundes-IT (Quartermaster Version 9.0), notiert 2026-11-03"), chronik entry.
- `todo.md`: tick the item with its description.

## Testing

- `EventReminderService` unit/integration tests with a fixed `nowUtc`: deadline reminder 36 hours before, one per distinct deadline with the type names, competing deadline, cap at `ClosesAt`; start reminders with start time (24 hours, 30 minutes) and without (9:00 the day before, no 30 minutes); DST change between reminder and start; skipped when the send time was before the subscription; sent once per target; sent again after the time moved; not sent after the target; not sent for completed, archived, deleted or invisible events.
- Change notice: sent on date change, start time change, removal; not sent without a change, for past or completed events.
- Subscription endpoints: anonymous 401, invisible event, completed event 409, idempotent subscribe, unsubscribe, list.
- "Antrag eingereicht": subscriber gets it with the member link, commission member who is also subscribed gets it once with the commission link, subscriber who can no longer see the event gets nothing.
- Template seeds render against the sample model (existing `Every_seed_renders_against_the_sample_model`), catalog test for the new triggers.
