# Event Subscriptions Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Logged-in users subscribe to events by hand and get deadline reminders, start reminders, a change notice and "Antrag eingereicht" for the subscribed event's portal.

**Architecture:** Two tables (`EventSubscriptions`, `EventReminderDeliveries`) behind `EventSubscriptionRepository`. A pure `EventReminderSchedule` computes the planned reminders of an event; `EventReminderService` (run every minute by a hosted service) sends the due ones once per (subscription, kind, target) through `EventSubscriptionNotifier` and two new triggers. `EventUserVisibility` applies the event page's visibility rule to any user id. The existing "Antrag eingereicht" path gains subscribers through its payload and resolver.

**Tech Stack:** .NET 10, FastEndpoints, LinqToDB + FluentMigrator (MariaDB), Blazor WASM, TUnit, Fluid templates.

**Spec:** `Quartermaster.Documentation/plans/2026-11-03-event-subscriptions-design.md`

## Global Constraints

- Follow `CLAUDE.md` (K&R braces, if-body on the next line, guard-clause methods for 3+ term conditions, `== null`, never `is X or Y`, explicit access modifiers, one class per file, no `@code` in `.razor`, no bare catch, minimal comments, using directives instead of qualified names).
- No git commands at all. No em or en dashes anywhere (code, comments, texts, docs).
- English in code and routes; only UI and notification texts German.
- Schema changes go into M009 (`[Migration(017)]`); the dev DB (`quartermaster`, mysql root) is altered by hand with the same DDL. New tables go into the TRUNCATE list of `Quartermaster.Server.Tests/Infrastructure/WorkerDatabase.cs`.
- No names of other people in any notification; links only.
- Never contact a real mail or Telegram server; tests use `CapturingNotificationQueue` or the existing test infrastructure.
- Build: `taskset -c 0-9 dotnet build Quartermaster.sln`. Tests only per class: `taskset -c 0-9 dotnet run --no-build --project Quartermaster.Server.Tests -- --treenode-filter "/*/*/(ClassA)|(ClassB)/*" --progress off > log`. Never the full suite.
- Values from the spec, verbatim: deadline reminder 36 hours before; start reminders 24 hours and 30 minutes before; without start time the day-before reminder at 9:00 Berlin time the day before and no 30-minute reminder; hosted service every minute.
- Trigger ids `event_deadline_reminder` and `event_reminder`. Catalog: "Antragsfristen abonnierter Veranstaltungen" / "36 Stunden vor jeder Antragsfrist einer Veranstaltung, die du abonniert hast."; "Erinnerungen zu abonnierten Veranstaltungen" / "24 Stunden und 30 Minuten vor Beginn einer Veranstaltung, die du abonniert hast, und wenn sich ihr Termin ändert."
- "Antrag eingereicht" catalog: "Antrag eingereicht (Antragskommission und Abonnements)" / "Sobald ein Antrag zu einer Versammlung eingereicht wird: für die Antragskommission der Gliederung und für alle, die die Veranstaltung abonniert haben."

## Rulings against the spec

- Route of the caller's list is `GET /api/users/event-subscriptions` (next to `/api/users/notification-preferences`) instead of `/api/events/subscriptions`, so it cannot collide with `/api/events/{Id}`.
- Instead of an `IsSubscribed` flag on `EventDetailDTO`, both pages use one component that reads `GET /api/events/{id}/subscription` (`IsSubscribed`, `CanSubscribe`); the portal page does not load the event detail, so this avoids two DTO changes.
- The payload change for "Antrag eingereicht" is `Guid? SubscriberEventId` (null = no subscribers) rather than a bool, because the resolver needs the event id and the payload only carries the chapter id.

## Review Focus

- An event on the day of a DST change: the 24-hour reminder is 24 real hours before the start, the 9:00 reminder is 9:00 Berlin wall-clock (Task 3 test `Day_before_without_start_time_is_nine_berlin_across_dst`).
- A user subscribes 10 hours before the start: no 24-hour reminder, but the 30-minute one (Task 4 test `Subscribing_late_skips_passed_reminder_only`).
- The start time moves after the 24-hour reminder went out: the new start gets a new reminder, the old one never repeats (Task 4 test `Moved_start_gets_new_reminder_once`).
- A private event whose subscriber lost `ViewEvents`: no reminder, no delivery row, and "Antrag eingereicht" not sent to them (Task 4 test `Invisible_event_sends_nothing_and_records_nothing`, Task 6 test `Subscriber_without_visibility_is_left_out`).
- Two types share one deadline and a third has a competing deadline: two deadline reminders, the first listing both type names (Task 3 test `Shared_deadline_is_one_reminder_with_both_types`).

---

### Task 1: Tables and repository

**Files:**
- Create: `Quartermaster.Data/Events/EventSubscription.cs`, `Quartermaster.Data/Events/EventReminderDelivery.cs`, `Quartermaster.Data/Events/EventReminderKind.cs`, `Quartermaster.Data/Events/EventSubscriptionRepository.cs`
- Modify: `Quartermaster.Data/Migrations/M009.cs`, `Quartermaster.Data/DbContext.cs`, `Quartermaster.Server/Program.cs` (DI), `Quartermaster.Server.Tests/Infrastructure/WorkerDatabase.cs`
- Test: `Quartermaster.Server.Tests/Events/EventSubscriptionRepositoryTests.cs` (RepositoryTestBase)

**Interfaces:**
- Produces:
  - `EventSubscription` (table `EventSubscriptions`): `Guid Id`, `Guid EventId`, `Guid UserId`, `DateTime CreatedAt`; unique index (`EventId`, `UserId`).
  - `enum EventReminderKind { Deadline = 0, StartDayBefore = 1, StartSoon = 2 }`.
  - `EventReminderDelivery` (table `EventReminderDeliveries`): `Guid Id`, `Guid SubscriptionId`, `EventReminderKind Kind`, `DateTime TargetAt`, `DateTime SentAt`; unique index (`SubscriptionId`, `Kind`, `TargetAt`). Date columns `DATETIME(6)` like the other M009 tables.
  - `EventSubscriptionRepository(DbContext db)`: `EventSubscription Subscribe(Guid eventId, Guid userId, DateTime nowUtc)` (returns the existing row unchanged when present); `void Unsubscribe(Guid eventId, Guid userId)` (deletes the row and its deliveries, no-op when absent); `bool IsSubscribed(Guid eventId, Guid userId)`; `List<EventSubscription> ListForUser(Guid userId)`; `List<EventSubscription> ListForEvent(Guid eventId)`; `List<EventSubscription> ListActive()` (subscriptions whose event is not deleted and has status neither `Completed` nor `Archived`); `bool TryRecordDelivery(Guid subscriptionId, EventReminderKind kind, DateTime targetAt, DateTime nowUtc)` (inserts; false when the row exists).

- [ ] **Step 1: Write the failing tests** `Subscribe_is_idempotent_and_keeps_created_at`, `Unsubscribe_removes_row_and_deliveries`, `Unsubscribe_without_row_is_noop`, `ListActive_skips_completed_archived_and_deleted_events`, `TryRecordDelivery_true_once_per_target` (second call false; same kind with another `TargetAt` true again).
- [ ] **Step 2: Run them, expect failure (types missing)**
- [ ] **Step 3: Implement** the entities, repository, M009 `Create.Table` for both (plus `Delete.Table` in `Down`), `DbContext` properties `EventSubscriptions` and `EventReminderDeliveries`, `AddScoped<EventSubscriptionRepository>()`, TRUNCATE lines; run the same `CREATE TABLE` statements on the dev DB. `TryRecordDelivery` checks for the row first and catches only the duplicate-key `MySqlException` (logged nowhere, it is the expected race) to return false.
- [ ] **Step 4: Run the tests, expect PASS**

### Task 2: Visibility for any user and the subscription endpoints

**Files:**
- Create: `Quartermaster.Server/Events/EventUserVisibility.cs`, `Quartermaster.Server/Events/Subscriptions/EventSubscribeEndpoint.cs`, `EventUnsubscribeEndpoint.cs`, `EventSubscriptionStateEndpoint.cs`, `Quartermaster.Server/Users/UserEventSubscriptionsEndpoint.cs`
- Create: `Quartermaster.Api/Events/EventSubscriptionDTOs.cs` (`EventSubscriptionStateDTO`, `EventSubscriptionListItemDTO`)
- Modify: `Quartermaster.Server/Program.cs`
- Test: `Quartermaster.Server.Tests/Integration/Events/EventSubscriptionEndpointTests.cs`, `Quartermaster.Server.Tests/Events/EventUserVisibilityTests.cs`

**Interfaces:**
- Consumes: Task 1 repository.
- Produces:
  - `EventUserVisibility` (scoped; constructor takes `UserGlobalPermissionRepository`, `UserChapterPermissionRepository`, `ChapterRepository`): `bool IsVisibleTo(Event ev, Guid userId)`. Builds a `PermissionContext` with a fresh `HttpContextAccessor`, calls `Bind` with a principal holding `ClaimTypes.NameIdentifier = userId`, and returns `AssemblyMotionEventVisibility.IsVisible(ev, perms)`, so the rule stays in one place.
  - `static bool EventSubscriptionRules.CanSubscribe(Event ev)` in `Quartermaster.Server/Events/Subscriptions/EventSubscriptionRules.cs`: false when deleted, `Completed` or `Archived`.
  - `EventSubscriptionStateDTO { bool IsSubscribed; bool CanSubscribe; }`, `EventSubscriptionListItemDTO { Guid EventId; string PublicName; DateTime? EventDate; TimeSpan? StartTime; EventStatus Status; }` (dates mapped with the same helpers as `EventDetailDTO`).
  - `POST /api/events/{Id}/subscription` (204), `DELETE /api/events/{Id}/subscription` (204), `GET /api/events/{Id}/subscription` (`EventSubscriptionStateDTO`), `GET /api/users/event-subscriptions` (`List<EventSubscriptionListItemDTO>`, ordered by date, events without a date last; deleted events left out).

- [ ] **Step 1: Write the failing tests.** Visibility: `Public_visible_to_any_user`, `MembersOnly_visible_to_any_user`, `Private_needs_ViewEvents_on_chapter`, `Private_visible_with_inherited_ViewEvents`. Endpoints: `Anonymous_gets_401` (all four), `Missing_event_gets_404`, `Private_event_without_ViewEvents_gets_403` (POST and GET state), `Completed_event_subscribe_gets_409` (and `Archived_event_subscribe_gets_409`), `Subscribe_twice_keeps_one_row`, `State_reports_subscription_and_can_subscribe`, `Unsubscribe_works_for_completed_event`, `Unsubscribe_without_subscription_gets_204`, `List_returns_own_subscriptions_ordered_by_date`.
- [ ] **Step 2: Run them, expect failure**
- [ ] **Step 3: Implement.** Order of checks in POST and GET state, like `EventDetailEndpoint`: 401 without login, 404 for missing or deleted, then the visibility rule through `AssemblyMotionEventVisibility.IsVisible(ev, _perms)` (403 when not visible), then for POST 409 via `SendErrorsAsync(409)` with the message "Abgeschlossene oder archivierte Veranstaltungen können nicht abonniert werden." DELETE only needs a login and an existing event (404 otherwise), no visibility check. All endpoints need the antiforgery handling the other authenticated POST/DELETE endpoints use.
- [ ] **Step 4: Run the tests, expect PASS**

### Task 3: Reminder schedule (pure)

**Files:**
- Create: `Quartermaster.Server/Events/Subscriptions/EventReminderSchedule.cs`, `PlannedReminder.cs`
- Test: `Quartermaster.Server.Tests/Events/EventReminderScheduleTests.cs` (plain unit tests)

**Interfaces:**
- Consumes: `AssemblyMotionDeadlines.Effective`, `BerlinTime.ToUtc`.
- Produces:
  - `record PlannedReminder(EventReminderKind Kind, DateTime TargetAt, DateTime SendAt, string TypeNames)` (`TypeNames` empty for start reminders).
  - `static IReadOnlyList<PlannedReminder> EventReminderSchedule.For(Event ev, AssemblyMotionPortal? portal, IReadOnlyList<AssemblyMotionType> types)`.
  - `static DateTime? EventReminderSchedule.StartUtc(Event ev)`: `EventDate.Date + StartTime` as Berlin local, converted to UTC; null without date or without start time.
  - `static DateTime? EventReminderSchedule.UpcomingUntilUtc(Event ev)`: the start with a start time, otherwise 00:00 Berlin of the day after `EventDate`; null without date. Used by Task 5 for "still upcoming".

Rules:
- Deadline: for each type the normal effective deadline and, when `CompetingDeadline != null`, the competing one; group by instant; one `PlannedReminder(Deadline, instant, instant - 36h, names)` per instant. Names in type `SortOrder`, joined with ", ", a competing entry as "{Name} (Konkurrenzanträge)".
- `StartDayBefore`: with start time `TargetAt = start`, `SendAt = start - 24h`; without start time `TargetAt` = 00:00 Berlin of `EventDate`, `SendAt` = 9:00 Berlin of the day before, both via `BerlinTime.ToUtc`.
- `StartSoon`: only with start time, `TargetAt = start`, `SendAt = start - 30min`.

- [ ] **Step 1: Write the failing tests** `Deadline_reminder_is_36_hours_before`, `Shared_deadline_is_one_reminder_with_both_types`, `Competing_deadline_is_named_separately`, `Deadline_is_capped_at_portal_close`, `No_portal_no_deadline_reminders`, `Start_reminders_with_start_time` (24 h and 30 min, Berlin 19:00 on 2026-11-28 is 18:00 UTC), `Day_before_without_start_time_is_nine_berlin_across_dst` (event 2026-10-26, DST ended 2026-10-25: `SendAt` = 2026-10-25 08:00 UTC, `TargetAt` = 2026-10-25 23:00 UTC), `No_date_no_start_reminders`, `UpcomingUntil_without_start_time_is_next_midnight`.
- [ ] **Step 2: Run them, expect failure**
- [ ] **Step 3: Implement**
- [ ] **Step 4: Run the tests, expect PASS**

### Task 4: Notifications and the reminder job

**Files:**
- Modify: `Quartermaster.Server/Notifications/NotificationTriggers.cs`, `NotificationTriggerCatalog.cs`, `Quartermaster.Data/Templates/SystemTemplateSeeds.cs`, `Quartermaster.Server/Program.cs`
- Create: `Quartermaster.Server/Events/Subscriptions/EventReminderNotificationModel.cs`, `EventReminderNotificationPayload.cs`, `EventReminderRecipientResolver.cs`, `EventSubscriptionNotifier.cs`, `EventReminderService.cs`, `EventReminderHostedService.cs`
- Test: `Quartermaster.Server.Tests/Events/EventReminderServiceTests.cs`, `EventReminderNotificationTemplateTests.cs`, `EventReminderRecipientResolverTests.cs`

**Interfaces:**
- Consumes: Tasks 1 to 3.
- Produces:
  - `NotificationTriggers.EventDeadlineReminder = "event_deadline_reminder"`, `NotificationTriggers.EventReminder = "event_reminder"`; catalog entries with the texts from Global Constraints.
  - `record EventReminderNotificationModel` with init properties `string Name`, `string Date` (`dd.MM.yyyy`, Berlin, or "noch offen"), `string Time` (`HH:mm`, Berlin, or empty), `string Kind` (`day_before`, `soon`, `changed`; empty for deadline reminders), `string TypeNames`, `string MeetingUrl` (only for `soon`, else empty), `string Path`. Template key `event`.
  - `record EventReminderNotificationPayload(Guid EventId, IReadOnlyList<Guid> UserIds)`.
  - `EventReminderRecipientResolver(string triggerId, DbContext db)` with `static IReadOnlyList<string> TriggerIds` = both triggers; resolves users not deleted and with an email, de-duplicated. Registered in `Program.cs` like `AssemblyMotionRecipientResolver`.
  - `EventSubscriptionNotifier(INotificationDispatchQueue queue, ILogger<EventSubscriptionNotifier> logger)`: `void DeadlineReminder(Event ev, IReadOnlyList<Guid> userIds, DateTime deadlineUtc, string typeNames)` (path `/Events/{id}/Motions`), `void StartReminder(Event ev, IReadOnlyList<Guid> userIds, EventReminderKind kind)` (path `/Events/{id}`), `void ScheduleChanged(Event ev, IReadOnlyList<Guid> userIds)` (path `/Events/{id}`). Each wraps its body like `AssemblyMotionNotifier.Safely` (log and swallow), escapes `ev.PublicName` with `AssemblyMotionNotifier.EscapeMarkdown`, `SourceEntityType: "Event"`.
  - `EventReminderService(EventSubscriptionRepository subs, DbContext db, EventUserVisibility visibility, EventSubscriptionNotifier notifier)`: `int SendDue(DateTime nowUtc)` returning the number of reminders enqueued (one per user and reminder).
  - `EventReminderHostedService`: loop like `AssemblyMotionDeadlineHostedService`, interval 1 minute, logs the count when above 0.

Seeds (identifiers `notifications.{trigger}.email` and `.telegram`, display names "Benachrichtigung: Antragsfristen abonnierter Veranstaltungen (E-Mail)" etc., `AllowsChapterFields: false`):
- `event_deadline_reminder`: subject "Antragsfrist endet bald: {{ event.Name }}"; body "Hallo,\n\ndie Antragsfrist für {{ event.TypeNames }} endet am {{ event.Date }} um {{ event.Time }} Uhr.\n\n[Zum Antragsportal]({{ globals.BaseUrl }}{{ event.Path }})\n". Telegram: "Antragsfrist endet bald: *{{ event.Name }}*\n\n{{ event.TypeNames }}: {{ event.Date }} um {{ event.Time }} Uhr\n\n[Zum Antragsportal]({{ globals.BaseUrl }}{{ event.Path }})".
- `event_reminder`: subject `{% if event.Kind == "changed" %}Neuer Termin: {{ event.Name }}{% elsif event.Kind == "soon" %}{{ event.Name }} beginnt in 30 Minuten{% else %}{{ event.Name }} beginnt morgen{% endif %}`; the body names the date ("am {{ event.Date }}{% if event.Time != "" %} um {{ event.Time }} Uhr{% endif %}"), says for `changed` "Der Termin von {{ event.Name }} hat sich geändert: ...", has [Zur Veranstaltung]({{ globals.BaseUrl }}{{ event.Path }}) and, when `event.MeetingUrl != ""`, [Zum Treffen]({{ event.MeetingUrl }}). Telegram the same in short form.

`SendDue` algorithm: `ListActive()` grouped by event; load event, portal by event and its types once per event; `EventReminderSchedule.For`; for each planned reminder and subscription send when `SendAt <= now`, `TargetAt > now`, `SendAt >= subscription.CreatedAt`, the user can see the event (`EventUserVisibility`, skipped without a delivery row otherwise) and `TryRecordDelivery` returns true; collect the user ids per planned reminder and call the notifier once per planned reminder.

- [ ] **Step 1: Write the failing tests.** Templates (`TemplateRepository.SupplementDefaults` as in `AssemblyMotionNotificationTemplateTests`): `Catalog_covers_both_triggers`, `Every_seed_renders_and_links_the_path` (all kinds, body contains `https://qm.test/Events/abc`), `Soon_seed_contains_meeting_link`, `Changed_seed_says_noch_offen`. Resolver: `Resolves_users_with_email_only`, `Deleted_user_left_out`. Service (fixed `nowUtc`, `CapturingNotificationQueue`): `Sends_deadline_reminder_36_hours_before`, `Sends_each_reminder_once`, `Subscribing_late_skips_passed_reminder_only`, `Moved_start_gets_new_reminder_once`, `Not_sent_after_target`, `Completed_and_archived_events_send_nothing`, `Invisible_event_sends_nothing_and_records_nothing`, `Reminder_model_has_no_member_names` (model fields are the event name, dates, type names, link).
- [ ] **Step 2: Run them, expect failure**
- [ ] **Step 3: Implement** (register the model with the Fluid member access the same way `AssemblyMotionNotificationModel` is, if the renderer needs it)
- [ ] **Step 4: Run the tests plus `AssemblyMotionNotificationTemplateTests`, expect PASS**

### Task 5: Change notice on event update

**Files:**
- Create: `Quartermaster.Server/Events/Subscriptions/EventScheduleChangeNotice.cs`
- Modify: `Quartermaster.Server/Events/EventUpdateEndpoint.cs`, `Quartermaster.Server/Program.cs`
- Test: `Quartermaster.Server.Tests/Integration/Events/EventScheduleChangeNoticeTests.cs` (integration through `PUT /api/events/{Id}`, reading the captured or logged notifications the way other integration tests do)

**Interfaces:**
- Consumes: `EventSubscriptionRepository.ListForEvent`, `EventUserVisibility`, `EventSubscriptionNotifier.ScheduleChanged`, `EventReminderSchedule.UpcomingUntilUtc`.
- Produces: `EventScheduleChangeNotice` (scoped): `void NotifyIfChanged(Event before, Event after, DateTime nowUtc)`. Sends when `EventDate` or `StartTime` differ and the event is still upcoming: `UpcomingUntilUtc(after)` or, when `after` has no date, `UpcomingUntilUtc(before)` is after `nowUtc`, and `after.Status` is neither `Completed` nor `Archived`. Recipients are the subscribers who can see the event.

`EventUpdateEndpoint` keeps the loaded `existing`, reloads the event after `_eventRepo.Update(ev)` and calls `NotifyIfChanged(existing, reloaded, DateTime.UtcNow)`.

- [ ] **Step 1: Write the failing tests** `Date_change_notifies_subscribers`, `Start_time_change_notifies`, `Removed_date_notifies_with_noch_offen`, `No_change_no_notice` (only the name changed), `Past_event_no_notice`, `Completed_event_no_notice`, `Unsubscribed_or_invisible_user_gets_nothing`.
- [ ] **Step 2: Run them, expect failure**
- [ ] **Step 3: Implement**
- [ ] **Step 4: Run the tests plus `EventUpdateEndpointTests`, expect PASS**

### Task 6: "Antrag eingereicht" for subscribers

**Files:**
- Create: `Quartermaster.Server/AssemblyMotions/Notifications/AssemblyMotionCommissionUsers.cs`
- Modify: `AssemblyMotionNotificationPayload.cs`, `AssemblyMotionRecipientResolver.cs`, `AssemblyMotionNotifier.cs` (same folder), `Quartermaster.Server/Notifications/NotificationTriggerCatalog.cs`, `Quartermaster.Data/Templates/SystemTemplateSeeds.cs`, `Quartermaster.Data/Migrations/M009.cs`, `Quartermaster.Server/Program.cs` (resolver gets `EventUserVisibility`)
- Test: `Quartermaster.Server.Tests/AssemblyMotions/AssemblyMotionNotificationTests.cs`, `AssemblyMotionRecipientResolverTests.cs`

**Interfaces:**
- Consumes: Task 1 repository, Task 2 `EventUserVisibility`.
- Produces:
  - `static List<Guid> AssemblyMotionCommissionUsers.UserIds(DbContext db, Guid chapterId)`: the user id part of today's `ResolveCommission`, moved out unchanged; the resolver uses it.
  - `AssemblyMotionNotificationPayload` gets a last positional parameter `Guid? SubscriberEventId = null`.
  - `AssemblyMotionRecipientResolver(string triggerId, DbContext db, EventUserVisibility visibility)`: when `SubscriberEventId` is set, adds the subscribed users who can see the event; existing de-duplication per user stays.
  - `AssemblyMotionNotifier.Submitted` passes the portal's event id as `SubscriberEventId`. `PathFor`: member path when the recipient is in the member ids, else commission path when the recipient is in `AssemblyMotionCommissionUsers.UserIds(...)` (computed only when `SubscriberEventId` is set), else member path. With `SubscriberEventId` null the paths stay exactly as today.
  - Catalog name and description from Global Constraints. Seed display names "Benachrichtigung: Antrag eingereicht (Antragskommission und Abonnements) (E-Mail)" and "(Telegram)"; subject and body unchanged. M009 `Execute.Sql` updates `Templates.DisplayName` from the old to the new name for both identifiers (only rows still holding the old name); the dev DB gets the same UPDATE.

- [ ] **Step 1: Write the failing tests** `Submitted_reaches_subscriber_with_member_link`, `Commission_member_also_subscribed_gets_one_with_commission_link`, `Subscriber_without_visibility_is_left_out`, `Other_triggers_ignore_subscribers` (e.g. Lapsed), and update the existing resolver test constructions to the new constructor.
- [ ] **Step 2: Run them, expect failure**
- [ ] **Step 3: Implement**
- [ ] **Step 4: Run `AssemblyMotionNotificationTests`, `AssemblyMotionRecipientResolverTests`, `AssemblyMotionNotificationTemplateTests`, expect PASS**

### Task 7: UI

**Files:**
- Create: `Quartermaster.Blazor/Components/Events/EventSubscriptionToggle.razor` + `.razor.cs`
- Modify: `Quartermaster.Blazor/Pages/Events/PublicEventDetail.razor`, `Quartermaster.Blazor/Pages/AssemblyMotions/AssemblyMotionPortal.razor`, `Quartermaster.Blazor/Pages/UserNotificationPreferences.razor` + `.razor.cs`, `Quartermaster.Api/I18n/I18nKey.cs`, `Quartermaster.Server/wwwroot/i18n/de.json` and `en.json`

**Interfaces:**
- Consumes: Task 2 endpoints and DTOs.
- Produces: `EventSubscriptionToggle` with parameter `Guid EventId`. Renders nothing for anonymous users (same login check the pages already use) or when the state call fails with 401/403/404. Shows the button when `CanSubscribe` or `IsSubscribed`: not subscribed "Abonnieren" (`btn-outline-primary`, `bi-bell`), subscribed "Abonniert" (`btn-primary`, `bi-bell-fill`, click unsubscribes); spinner while busy; hint below "Du erhältst Erinnerungen an Antragsfristen und den Beginn."; errors as an alert.

Notification preferences: new card "Abonnierte Veranstaltungen" after the existing cards, loading `GET /api/users/event-subscriptions`; each row the event name linked to `/Events/{id}`, the date (or nothing), and a button "Abbestellen" (`btn-outline-secondary btn-sm`) calling DELETE and removing the row; empty text "Du hast keine Veranstaltungen abonniert."

- [ ] **Step 1: Implement** the component, both page placements (next to the page's existing header actions), the card and the i18n keys in both locales.
- [ ] **Step 2: Build the solution, restart the dev server, check by curl that the pages and the four endpoints answer (no Chrome).**

### Task 8: Docs

**Files:**
- Modify: `Quartermaster.Server/Changelog/V9.0.md`, `todo.md`, `PiratenWissen/quartermaster/` (the page covering events or the motion portal, plus `chronik.md`)

- [ ] **Step 1:** Changelog section "### Veranstaltungen" (German, only user-visible change): subscribing on the event and portal page, reminders 36 hours before each motion deadline and 24 hours and 30 minutes before the start, change notice, the list in "Benachrichtigungen"; one sentence in "### Antragsportal" that subscribers also get "Antrag eingereicht".
- [ ] **Step 2:** PiratenWissen block in German with "Quelle: Bundes-IT (Quartermaster Version 9.0), notiert 2026-11-03", chronik entry 2026-11-03 at the top, conventions of `konventionen.md`.
- [ ] **Step 3:** Tick the todo item with a one-line description of what shipped.
- [ ] **Step 4:** Grep all touched files for em and en dashes; none.
