# Motion Subscriptions Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Users can subscribe to assembly motions, supporters are subscribed automatically, and subscribers get each new forum reply under a new notification trigger.

**Architecture:** A new `AssemblyMotionSubscriptions` table with its repository, written by the subscribe endpoints and by `AssemblyMotionService.Join`/`Leave`. `AssemblyMotionForumReplyPoll` sends the existing author notification and, per reply, one request for the visible subscribers through `AssemblyMotionNotifier`, whose payload gains a list of user ids. The member detail DTO carries the subscription state; a list endpoint feeds a card on the notification settings page.

**Tech Stack:** .NET 10, FastEndpoints, LinqToDB + FluentMigrator on MariaDB, Blazor WASM, TUnit.

**Spec:** `Quartermaster.Documentation/plans/2026-11-03-motion-subscriptions-design.md`

## Global Constraints

- Never run git. Never run the full test suite; run the classes a task names with `taskset -c 0-9 dotnet run --no-build --project Quartermaster.Server.Tests -- --treenode-filter "/*/*/(ClassA)|(ClassB)/*" --progress off > log`, after `taskset -c 0-9 dotnet build Quartermaster.sln`.
- CLAUDE.md style: K&R braces, if-body on the next line, guard-clause methods for 3+ term conditions, no expression-bodied methods beyond trivial ones, explicit access modifiers, `using` directives, `== null`, no `is X or Y`, no `@code` in `.razor`, minimal comments, DTO mapping on the type.
- No em or en dashes anywhere. English in code and routes, German only in UI texts.
- Schema changes go into M009 (`[Migration(017)]`); the dev DB gets the same DDL and backfill by hand (`mysql -uroot quartermaster`).
- New tables go into the TRUNCATE list in `Quartermaster.Server.Tests/Infrastructure/WorkerDatabase.cs`.
- Notifications carry no member names.
- Body-less POSTs in tests use `PostAsJsonAsync(url, new { })`.
- Trigger id `assembly_motion_subscribed_forum_reply`; catalog name "Antworten im Forum zu abonnierten Anträgen", description "Sobald jemand im Forum auf einen Antrag antwortet, den du abonniert oder unterstützt hast."

## Review Focus

1. A supporter who joins twice (join, leave, join) ends with exactly one subscription; the unique index must not turn a re-join into a 500. Pinned in Task 2.
2. The author's own reply must still reach subscribers, while the existing author-skip keeps working for the author notification. Pinned in Task 5.
3. A user without a member (admin account) can subscribe through the endpoint and sees the button; the author check must not crash on a null member. Pinned in Task 3.
4. The subscriber link opens the member view even for commission members and for users added after enqueueing. Pinned in Task 4.
5. The backfill must skip authors, deleted motions and members without a user, and must not fail when a row already exists. Pinned in Task 1.

---

### Task 1: Table, repository, migration and backfill

**Files:**
- Create: `Quartermaster.Data/AssemblyMotions/AssemblyMotionSubscription.cs`, `Quartermaster.Data/AssemblyMotions/AssemblyMotionSubscriptionRepository.cs`
- Modify: `Quartermaster.Data/DbContext.cs`, `Quartermaster.Data/Migrations/M009.cs`, `Quartermaster.Server.Tests/Infrastructure/WorkerDatabase.cs`
- Test: `Quartermaster.Server.Tests/AssemblyMotions/AssemblyMotionSubscriptionRepositoryTests.cs`

**Interfaces:**
- Produces: entity `AssemblyMotionSubscription { Guid Id; Guid MotionId; Guid UserId; DateTime CreatedAt }`, `TableName = "AssemblyMotionSubscriptions"`; `ITable<AssemblyMotionSubscription> AssemblyMotionSubscriptions` on `DbContext`; scoped `AssemblyMotionSubscriptionRepository(DbContext)` with `AssemblyMotionSubscription Subscribe(Guid motionId, Guid userId, DateTime nowUtc)` (idempotent, keeps `CreatedAt`, catches the duplicate key like `EventSubscriptionRepository.Subscribe`), `void Unsubscribe(Guid motionId, Guid userId)`, `bool IsSubscribed(Guid motionId, Guid userId)`, `List<AssemblyMotionSubscription> ListForUser(Guid userId)`, `List<Guid> ListUserIdsForMotion(Guid motionId)`; `public const string BackfillSubscriptionsSql` on `M009`.

- [ ] **Step 1: Write the failing tests** (`RepositoryTestBase`, `TestDataBuilder`, a motion seeded like in `AssemblyMotionServiceTests`)
  - `Subscribe_is_idempotent_and_keeps_created_at`: second call returns the same Id and the first `CreatedAt`; one row.
  - `Unsubscribe_removes_the_row` and `Unsubscribe_without_row_is_noop`.
  - `Lists_for_user_and_motion`: two motions, two users; `ListForUser(u1).Count == 2`, `ListUserIdsForMotion(m1)` equals `{u1, u2}`.
  - `Backfill_subscribes_supporters_but_not_authors`: author (IsAuthor) and supporter with users, a supporter without user, a supporter of a deleted motion; after `Db.Execute(M009.BackfillSubscriptionsSql)` only the supporter of the live motion is subscribed, with `CreatedAt == JoinedAt`.
  - `Backfill_skips_existing_rows`: an existing subscription for the supporter, running the SQL twice leaves one row.
- [ ] **Step 2: Build and run `AssemblyMotionSubscriptionRepositoryTests`.** Expected: compile error or FAIL for the missing types.
- [ ] **Step 3: Implement** entity, repository, DbContext table and `AddScoped`, M009 `Create.Table` (DATETIME(6), unique `UX_AssemblyMotionSubscriptions_MotionId_UserId`, index on `UserId`), `Execute.Sql(BackfillSubscriptionsSql)` after the table, `Delete.Table` in Down, TRUNCATE entry. The backfill: `INSERT INTO AssemblyMotionSubscriptions (Id, MotionId, UserId, CreatedAt) SELECT UUID(), s.MotionId, mem.UserId, s.JoinedAt FROM AssemblyMotionSubmitters s JOIN AssemblyMotions m ON m.Id = s.MotionId JOIN Members mem ON mem.Id = s.MemberId WHERE s.IsAuthor = 0 AND m.DeletedAt IS NULL AND mem.UserId IS NOT NULL AND m.CreatedByMemberId <> s.MemberId AND NOT EXISTS (SELECT 1 FROM AssemblyMotionSubscriptions x WHERE x.MotionId = s.MotionId AND x.UserId = mem.UserId)`.
- [ ] **Step 4: Run the class again.** Expected: PASS. Apply CREATE TABLE, indexes and the backfill to the dev DB by hand.

### Task 2: Supporting subscribes, withdrawing unsubscribes

**Files:**
- Modify: `Quartermaster.Server/AssemblyMotions/AssemblyMotionService.cs` (constructor, `Join`, `Leave`), every `new AssemblyMotionService(` in `Quartermaster.Server.Tests` (13 sites)
- Test: `Quartermaster.Server.Tests/AssemblyMotions/AssemblyMotionServiceTests.cs`

**Interfaces:**
- Consumes: `AssemblyMotionSubscriptionRepository` from Task 1.
- Produces: constructor `AssemblyMotionService(AssemblyMotionRepository, AssemblyMotionPortalRepository, AuditLogRepository, DbContext, AssemblyMotionNotifier, AssemblyMotionSubscriptionRepository)`.

- [ ] **Step 1: Write the failing tests**
  - `Join_subscribes_the_supporter`: after `Join`, `IsSubscribed(motion, supporterUser)` is true.
  - `Leave_unsubscribes_the_supporter`.
  - `Join_after_leave_subscribes_again`: join, leave, join gives exactly one row.
  - `Join_of_member_without_user_succeeds_without_subscription`: outcome succeeded, no row.
- [ ] **Step 2: Build and run `AssemblyMotionServiceTests`.** Expected: FAIL.
- [ ] **Step 3: Implement**: inside the existing transactions, `Join` calls `Subscribe(motion.Id, member.UserId.Value, nowUtc)` and `Leave` calls `Unsubscribe` when `member.UserId != null`. Update the 13 test constructions.
- [ ] **Step 4: Run `AssemblyMotionServiceTests` and `AssemblyMotionForumReplyPollTests`.** Expected: PASS.

### Task 3: Subscribe endpoints, detail flags and the list

**Files:**
- Create: `Quartermaster.Server/AssemblyMotions/Endpoints/Members/AssemblyMotionSubscribeEndpoint.cs`, `AssemblyMotionUnsubscribeEndpoint.cs`, `Quartermaster.Server/AssemblyMotions/AssemblyMotionSubscriptionRules.cs`, `Quartermaster.Server/Users/UserAssemblyMotionSubscriptionsEndpoint.cs`, `Quartermaster.Api/AssemblyMotions/AssemblyMotionSubscriptionDTOs.cs`
- Modify: `Quartermaster.Api/AssemblyMotions/AssemblyMotionMemberDTOs.cs` (detail DTO), `Quartermaster.Server/AssemblyMotions/AssemblyMotionDetailBuilder.cs`, its three callers (`AssemblyMotionDetailEndpoint`, `AssemblyMotionMemberActions`, `AssemblyMotionCommissionActions`), `Quartermaster.Data/AssemblyMotions/AssemblyMotion.cs`, `Quartermaster.Api/I18n/I18nKey.cs`, `de.json`, `en.json`
- Test: `Quartermaster.Server.Tests/Integration/AssemblyMotions/AssemblyMotionSubscriptionEndpointTests.cs`

**Interfaces:**
- Consumes: Task 1 repository.
- Produces:
  - `AssemblyMotionSubscriptionRules.Problem(AssemblyMotion motion, Event ev, Guid? userMemberId) -> (int StatusCode, string? ErrorKey)?`: null when allowed; 404 for deleted or draft motion, 409 `Error.AssemblyMotion.SubscriptionAuthor` ("Den eigenen Antrag kannst du nicht abonnieren; Antworten im Forum erhältst du ohnehin.") when `CreatedByMemberId == userMemberId`, 409 `Error.AssemblyMotion.SubscriptionClosed` ("Anträge abgeschlossener oder archivierter Veranstaltungen können nicht abonniert werden.") for deleted, completed or archived events.
  - `POST` and `DELETE /api/assemblymotions/motions/{Id}/subscription`, request `AssemblyMotionIdRequest`; status codes as in the spec (403 when `AssemblyMotionEventVisibility.IsVisible` is false). DELETE checks only `_db.AssemblyMotions.Any(m => m.Id == req.Id)`.
  - `AssemblyMotionDetailDTO.IsSubscribed`, `CanSubscribe`; `Build(..., DateTime nowUtc, Guid? viewerUserId)`: both false without a user; `CanSubscribe` = rules allow.
  - `AssemblyMotionSubscriptionListItemDTO { Guid MotionId; bool IsVisible; string Title; string EventName; AssemblyMotionStatus Status }`; `AssemblyMotion.ToSubscriptionListItemDto(bool isVisible, string eventName)` sets only `MotionId` and `Status` when not visible.
  - `GET /api/users/assembly-motion-subscriptions` -> `List<AssemblyMotionSubscriptionListItemDTO>`, ordered by event date (undated last) then title; visible = motion not deleted and the caller sees the event.

- [ ] **Step 1: Write the failing tests**
  - `Anonymous_gets_401`, `Draft_motion_gets_404`, `Author_gets_409`, `Completed_event_gets_409`, `Private_event_without_view_gets_403`.
  - `Subscribe_is_idempotent` (two POSTs: 204, 204, one row) and `Unsubscribe_of_deleted_motion_returns_204`.
  - `Detail_reports_subscription_state`: supporter-free viewer sees `CanSubscribe == true`, `IsSubscribed == false`; after POST `IsSubscribed == true`; the author sees `CanSubscribe == false`.
  - `User_without_member_can_subscribe`: a user with no member gets 204 and `CanSubscribe == true` in the detail.
  - `List_hides_details_of_motions_no_longer_visible`: a subscription to a motion of a now private event lists `IsVisible == false` with empty `Title`.
- [ ] **Step 2: Build and run `AssemblyMotionSubscriptionEndpointTests`.** Expected: FAIL.
- [ ] **Step 3: Implement** the rules, endpoints, DTOs, builder parameter (callers pass `_perms.UserId`), mapping and the two error keys with de/en texts.
- [ ] **Step 4: Run `AssemblyMotionSubscriptionEndpointTests` and the existing `AssemblyMotionDetailEndpointTests`-style classes that call `Build` (find them with `grep -rln "assemblymotions/motions/" Quartermaster.Server.Tests/Integration`).** Expected: PASS.

### Task 4: Trigger, payload, resolver, notifier and seeds

**Files:**
- Modify: `Quartermaster.Server/Notifications/NotificationTriggers.cs`, `NotificationTriggerCatalog.cs`, `Quartermaster.Server/AssemblyMotions/Notifications/AssemblyMotionNotificationPayload.cs`, `AssemblyMotionRecipientResolver.cs`, `AssemblyMotionNotifier.cs`, `Quartermaster.Data/Templates/SystemTemplateSeeds.cs`
- Test: `AssemblyMotionRecipientResolverTests.cs`, `AssemblyMotionNotificationTests.cs`, `AssemblyMotionNotificationTemplateTests.cs`

**Interfaces:**
- Produces:
  - `NotificationTriggers.AssemblyMotionSubscribedForumReply = "assembly_motion_subscribed_forum_reply"`, in `AssemblyMotionRecipientResolver.TriggerIds` and the catalog (name and description from Global Constraints).
  - `AssemblyMotionNotificationPayload(..., Guid? SubscriberEventId = null, IReadOnlyList<Guid>? UserIds = null)`; the resolver adds those users when not deleted and with an email.
  - `AssemblyMotionNotifier.SubscribedForumReply(AssemblyMotion motion, string replyUrl, IReadOnlyList<Guid> userIds)`: no request for an empty list; `Enqueue` gains `IReadOnlyList<Guid>? userIds = null`, and those users count as member users in `PathFor`.
  - Seeds `notifications.assembly_motion_subscribed_forum_reply.email` and `.telegram` with display names, subject and bodies verbatim from the spec.

- [ ] **Step 1: Write the failing tests**
  - Resolver: `Resolves_payload_user_ids` (deleted and email-less users dropped) and `Every_trigger_has_a_resolver_id` now expects 14.
  - Notifier: `Subscribed_forum_reply_carries_users_and_member_link`: payload `UserIds` equals the list, `MemberIds` empty, `ModelFor` with a commission user still gives `/AssemblyMotions/{id}`, `ForumReplyUrl` set; `Subscribed_forum_reply_without_users_enqueues_nothing`.
  - Templates: the existing `Every_seed_renders_against_the_sample_model` covers the two seeds; the catalog test covers the trigger.
- [ ] **Step 2: Build and run the three classes plus the catalog test class.** Expected: FAIL.
- [ ] **Step 3: Implement.**
- [ ] **Step 4: Run them again.** Expected: PASS.

### Task 5: Forum poll notifies subscribers

**Files:**
- Modify: `Quartermaster.Data/AssemblyMotions/ForumWatchedMotion.cs` (add `Guid EventId`), `AssemblyMotionRepository.ListForumWatched`, `Quartermaster.Server/AssemblyMotions/Forum/AssemblyMotionForumReplyPoll.cs`, its construction in `AssemblyMotionForumReplyPollTests`
- Test: `Quartermaster.Server.Tests/AssemblyMotions/AssemblyMotionForumReplyPollTests.cs`

**Interfaces:**
- Consumes: `ListUserIdsForMotion` (Task 1), `SubscribedForumReply` (Task 4), `EventUserVisibility.IsVisibleTo(Event, Guid)`, `EventRepository.Get`.
- Produces: constructor `AssemblyMotionForumReplyPoll(AssemblyMotionRepository, UserRepository, IDiscourseClient, AssemblyMotionNotifier, OptionRepository, AssemblyMotionSubscriptionRepository, EventRepository, EventUserVisibility, ILogger<AssemblyMotionForumReplyPoll>)`; `PollAsync` returns the number of replies that notified anyone.

- [ ] **Step 1: Write the failing tests** (seed a subscriber user with a member and `KeycloakUserId = "kc-bernd"`, subscribed through the repository)
  - `Reply_notifies_author_and_subscribers_separately`: one `AssemblyMotionForumReply` request for the author, one `AssemblyMotionSubscribedForumReply` request whose `UserIds` is the subscriber.
  - `Author_reply_still_notifies_subscribers` (admin bot, author's forum id known): no author request, one subscriber request.
  - `Subscriber_reply_does_not_notify_them` (admin bot, `KeycloakUsers["kc-bernd"] = 43`, reply by user 43): no subscriber request for them, author request present.
  - `Subscriber_reply_notifies_them_without_admin_bot`.
  - `Subscriber_who_lost_visibility_gets_nothing`: event private, subscriber without `ViewEvents`.
  - `Author_who_subscribed_is_not_in_the_subscriber_request`: a subscription row for the author's user is ignored.
- [ ] **Step 2: Build and run `AssemblyMotionForumReplyPollTests`.** Expected: FAIL.
- [ ] **Step 3: Implement**: split `IsNotifiedReplyAsync` into the candidate checks and the per-recipient own-reply check; per run cache subscriber ids and visibility per motion and forum ids per user (`Dictionary<Guid, int?>` keyed by user id, looked up through the user's `KeycloakUserId`); register the new constructor arguments in `Program.cs` if the poll is built there.
- [ ] **Step 4: Run `AssemblyMotionForumReplyPollTests` and `AssemblyMotionForumSyncTests`.** Expected: PASS.

### Task 6: UI

**Files:**
- Create: `Quartermaster.Blazor/Components/AssemblyMotions/AssemblyMotionSubscriptionToggle.razor(.cs)`, `AssemblyMotionSubscriptionList.razor(.cs)`
- Modify: `Quartermaster.Blazor/Pages/AssemblyMotions/AssemblyMotionDetail.razor(.cs)`, `Quartermaster.Blazor/Pages/Account/UserNotificationPreferences.razor` (or where `EventSubscriptionList` sits), `I18nKey.cs`, `de.json`, `en.json`

**Interfaces:**
- Consumes: detail flags and endpoints from Task 3.
- Produces: `<AssemblyMotionSubscriptionToggle MotionId IsSubscribed CanSubscribe OnChanged />` built like `EventSubscriptionToggle` (bell icons, Busy reset in `finally`, `PostAsJsonAsync(url, new { })`), shown when `IsSubscribed || CanSubscribe`, hint only while `CanSubscribe`; `OnChanged` reloads the detail. `<AssemblyMotionSubscriptionList />` built like `EventSubscriptionList`, links to `/AssemblyMotions/{id}`.
- Texts (class `Ui.AssemblyMotionSubscription`): Subscribe "Abonnieren", Subscribed "Abonniert", UnsubscribeTooltip "Klicken, um das Abonnement zu beenden", Hint "Du erhältst eine Benachrichtigung bei neuen Antworten im Forum.", ChangeFailed "Das Abonnement konnte nicht geändert werden.", ListTitle "Abonnierte Anträge", ListEmpty "Du hast keine Anträge abonniert.", Unsubscribe "Abbestellen", HiddenMotion "Antrag nicht mehr sichtbar".

- [ ] **Step 1: Implement** the components and place the toggle next to the join/leave buttons; the list card under `EventSubscriptionList`.
- [ ] **Step 2: Build the solution.** Expected: no errors, no new warnings. Restart the dev server and check `/` answers 200.

### Task 7: Docs

**Files:**
- Modify: `Quartermaster.Server/Changelog/V9.0.md` (section "Antragsportal"), `PiratenWissen/quartermaster/vorstandsarbeit.md` or the page that holds the event subscription block, `PiratenWissen/quartermaster/chronik.md`, `todo.md`

- [ ] **Step 1: Write** one changelog paragraph ("Anträge lassen sich im Antragsportal abonnieren. Wer einen Antrag unterstützt, abonniert ihn automatisch, und wer die Unterstützung zurückzieht, beendet das Abonnement. Abonnierte Anträge melden jede neue Antwort im Forum; die neue Benachrichtigung \"Antworten im Forum zu abonnierten Anträgen\" ist eingeschaltet und lässt sich unter \"Benachrichtigungen\" abschalten, wo auch alle abonnierten Anträge stehen."), the PiratenWissen block with "Quelle: Bundes-IT (Quartermaster Version 9.0), notiert 2026-11-03", the chronik entry and the todo note.
- [ ] **Step 2: Check** the changed files for em and en dashes (U+2014, U+2013). Expected: none.
