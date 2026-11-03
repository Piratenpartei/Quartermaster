# Motion subscriptions (V9.0)

## Goal

Members who support a motion, or who just follow it, miss the discussion in its mirrored forum topic. Only the author is told about new replies today. Logged-in users can now subscribe to a motion in the motion portal and then get a notification for each new forum reply under a new trigger of its own. Supporting a motion subscribes automatically, withdrawing the support unsubscribes. The author cannot subscribe to their own motion; they keep the existing "Antwort im Forum" notification, which works as if they were always subscribed.

## Data

One new table, folded into M009.

`AssemblyMotionSubscriptions`:

- `Id`, `MotionId`, `UserId`, `CreatedAt` (DATETIME(6))
- unique on (`MotionId`, `UserId`), index on `UserId`

Subscriptions belong to the user, like event subscriptions. A member is linked to their user through `Members.UserId`; supporting requires a login, so every supporter has one.

M009 also subscribes the existing supporters: one row per `AssemblyMotionSubmitters` entry with `IsAuthor = 0` whose member has a `UserId`, whose motion is not deleted and whose member is not the motion's `CreatedByMemberId`, with `CreatedAt` = the submitter's `JoinedAt` and `Id` = `UUID()`. The dev DB gets the same table and backfill by hand.

`EventSubscriptions` and `AssemblyMotionSubscriptions` stay separate tables; they share nothing beyond the shape.

## Who can subscribe

- Login required.
- The motion is not deleted and not a draft, and the user can see the portal's event (`AssemblyMotionEventVisibility.IsVisible`, as for event subscriptions).
- The event is neither `Completed` nor `Archived` and not deleted. After that, subscribing is refused; existing subscriptions stay but no longer send anything, because the forum poll stops watching the topic.
- The user is not the author (`CreatedByMemberId` is the user's member). The author gets 409.
- A portal without a forum mirror can still be subscribed; the subscription then has no effect.
- Unsubscribing is always allowed, also for deleted motions and completed or invisible events.

## Supporting and withdrawing

- `AssemblyMotionService.Join` subscribes the joining member's user (idempotent, keeps an existing subscription's `CreatedAt`) inside the same transaction as the new submitter row.
- `AssemblyMotionService.Leave` unsubscribes the leaving member's user in the same transaction.
- A member without a `UserId` is skipped without error.

## Forum replies

`AssemblyMotionForumReplyPoll` keeps its run, baseline and paging logic. What changes is who a new reply notifies:

- A reply is a candidate when it is a regular, visible post after the first one and not written by the bot (the existing checks minus the author check).
- The author is notified under `assembly_motion_forum_reply` as today, unless the reply is the author's own.
- Subscribers are notified under the new trigger with one request per reply carrying all their user ids. A subscriber is left out when:
  - they are the author's user (the author is handled above);
  - they can no longer see the portal's event (`EventUserVisibility.IsVisibleTo`);
  - the reply is their own: their `KeycloakUserId` is looked up as a forum account through `FindUserIdByKeycloakIdAsync`, cached per run like the author's. Without an admin bot or a Keycloak id the lookup gives nothing and their own replies notify them too, as for the author.
- A motion's subscribers and their visibility are loaded once per motion and run, not once per reply.

## Notification

New trigger in `NotificationTriggers`, on by default through the normal channel defaults (email on):

- `assembly_motion_subscribed_forum_reply`, catalog name "Antworten im Forum zu abonnierten Anträgen", description "Sobald jemand im Forum auf einen Antrag antwortet, den du abonniert oder unterstützt hast."

The payload is the existing `AssemblyMotionNotificationPayload` with a new optional list of user ids; `AssemblyMotionRecipientResolver` resolves them to users that are not deleted and have an email address, with the existing de-duplication. The model is the existing `motion` model with `ForumReplyUrl` set, so the template editor, palette and mock data already cover it. The link to the motion is the member view (`/AssemblyMotions/{id}`), also for subscribers on the motion commission. No member names appear.

Seeds in `SystemTemplateSeeds`:

- `notifications.assembly_motion_subscribed_forum_reply.email`, display name "Benachrichtigung: Antworten im Forum zu abonnierten Anträgen (E-Mail)", subject "Neue Antwort im Forum: {{ motion.Title }}", body "Hallo,\n\nim Forum gibt es eine neue Antwort zu einem Antrag, den du abonniert hast: *{{ motion.TitleEscaped }}* ({{ motion.TypeName }}, {{ motion.EventName }}).\n\n[Antwort im Forum lesen]({{ motion.ForumReplyUrl }})\n\n[Antrag öffnen]({{ globals.BaseUrl }}{{ motion.Path }})\n".
- `notifications.assembly_motion_subscribed_forum_reply.telegram`, display name "Benachrichtigung: Antworten im Forum zu abonnierten Anträgen (Telegram)", body "Neue Antwort im Forum zu *{{ motion.TitleEscaped }}* (abonniert).\n\n[Antwort lesen]({{ motion.ForumReplyUrl }})\n[Antrag öffnen]({{ globals.BaseUrl }}{{ motion.Path }})".

## API

All require a login.

- `POST /api/assemblymotions/motions/{id}/subscription`: subscribes; 401, 404 for a missing, deleted or draft motion, 403 when the event is not visible, 409 with a reason for the author or a completed or archived event. Idempotent; answers 204.
- `DELETE /api/assemblymotions/motions/{id}/subscription`: unsubscribes; 404 only for a motion that never existed, otherwise 204, also when there was no subscription.
- `AssemblyMotionDetailDTO` gets `IsSubscribed` and `CanSubscribe` (false for the author and for anonymous callers), so the page needs no extra request and join and leave refresh them with the detail they already return.
- `GET /api/users/assembly-motion-subscriptions`: the caller's motion subscriptions with motion id, title, event name and motion status, ordered by event date and title. A motion whose event the caller can no longer see, or that was deleted, appears without title and event name, so it can still be unsubscribed.

## UI

- `/AssemblyMotions/{id}` (`AssemblyMotionDetail`): the bell button "Abonnieren" / "Abonniert" like on events, for logged-in users who are not the author, while `CanSubscribe` or `IsSubscribed`. Hint under it while subscribing is possible: "Du erhältst eine Benachrichtigung bei neuen Antworten im Forum." The author sees no button.
- `/Account/Notifications`: a card "Abonnierte Anträge" under "Abonnierte Veranstaltungen", with the title linked to the motion, the event name and "Abbestellen" per row; empty: "Du hast keine Anträge abonniert."; hidden rows: "Antrag nicht mehr sichtbar".
- All texts as i18n keys in `de.json` and `en.json`.

## Docs

- `Changelog/V9.0.md`, section "Antragsportal": one paragraph on subscribing, the automatic subscription when supporting and the new notification.
- PiratenWissen `quartermaster/`: extend the block about subscriptions (Quelle: Bundes-IT (Quartermaster Version 9.0), notiert 2026-11-03), chronik entry.
- `todo.md`: note it under the V9.0 items.

## Testing

- Repository: subscribe idempotent, unsubscribe, list for user and motion, unique index.
- Join subscribes, leave unsubscribes, join again after leave subscribes again; member without user is skipped.
- Endpoints: anonymous 401, draft 404, author 409, completed event 409, invisible event 403, idempotent subscribe, unsubscribe of a deleted motion, list with a hidden row.
- Forum poll: a reply notifies author and subscribers in separate triggers; the author's own reply still notifies subscribers; a subscriber's own reply does not notify them; a subscriber who lost visibility gets nothing; without admin bot the subscriber's own reply notifies them.
- Migration backfill against the dev DB: supporters subscribed, authors not.
- Seeds render against the sample model, catalog test for the new trigger, resolver trigger count.
