using LinqToDB;
using Microsoft.Extensions.Logging.Abstractions;
using Quartermaster.Api.AssemblyMotions;
using Quartermaster.Api.Events;
using Quartermaster.Api.I18n;
using Quartermaster.Data.AssemblyMotions;
using Quartermaster.Data.Chapters;
using Quartermaster.Data.Events;
using Quartermaster.Data.Members;
using Quartermaster.Data.Options;
using Quartermaster.Data.Permissions;
using Quartermaster.Data.Roles;
using Quartermaster.Data.UserChapterPermissions;
using Quartermaster.Data.UserGlobalPermissions;
using Quartermaster.Data.Users;
using Quartermaster.Server.AssemblyMotions;
using Quartermaster.Server.AssemblyMotions.Forum;
using Quartermaster.Server.AssemblyMotions.Notifications;
using Quartermaster.Server.Events;
using Quartermaster.Server.Notifications;
using Quartermaster.Server.Tests.AssemblyMotions.Fakes;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.AssemblyMotions;

[NotInParallel(["FakeDiscourseClient", "FakeWikiBotClient"])]
public class AssemblyMotionForumReplyPollTests : RepositoryTestBase {
    private const string AuthorKeycloakId = "kc-anna";
    private const int AuthorForumUserId = 42;

    private readonly DateTime _now = new(2027, 3, 1, 12, 0, 0, DateTimeKind.Utc);
    private TestDataBuilder _builder = default!;
    private AssemblyMotionPortalRepository _portalRepo = default!;
    private AssemblyMotionRepository _motionRepo = default!;
    private AssemblyMotionService _service = default!;
    private CapturingNotificationQueue _queue = default!;
    private AssemblyMotionForumSync _sync = default!;
    private AssemblyMotionForumReplyPoll _poll = default!;
    private Chapter _chapter = default!;
    private Event _event = default!;
    private AssemblyMotionPortal _portal = default!;
    private AssemblyMotionSubscriptionRepository _subscriptions = default!;

    [Before(Test)]
    public void Setup() {
        FakeDiscourseClient.Reset();
        FakeWikiBotClient.Reset();
        AssemblyMotionForumSync.ResetUnconfiguredWarning();
        AssemblyMotionForumReplyPoll.ResetNotAdminWarning();
        _builder = new TestDataBuilder(Db);
        _queue = new CapturingNotificationQueue();
        _portalRepo = new AssemblyMotionPortalRepository(Db, AuditLog);
        _motionRepo = new AssemblyMotionRepository(Db, AuditLog);
        var notifier = new AssemblyMotionNotifier(_queue, Db, NullLogger<AssemblyMotionNotifier>.Instance);
        _service = new AssemblyMotionService(_motionRepo, _portalRepo, AuditLog, Db, notifier, new AssemblyMotionSubscriptionRepository(Db));
        var options = new OptionRepository(Db, AuditLog);
        options.SupplementDefaults();
        options.SetValue("discourse.base_url", null, "https://forum.test");
        options.SetValue("discourse.api_username", null, "antragsbot");
        options.SetValue("discourse.api_key", null, "key");
        options.SetValue(NotificationTemplateGlobals.BaseUrlOptionKey, null, "https://qm.test");
        _sync = new AssemblyMotionForumSync(_motionRepo, _portalRepo, new EventRepository(Db, AuditLog), Db, new FakeDiscourseClient(),
            new FakeWikiBotClient(), options, AuditLog, new I18nService("{}"), NullLogger<AssemblyMotionForumSync>.Instance);
        var users = new UserRepository(Db, new UserGlobalPermissionRepository(Db, new RoleRepository(Db)), new PermissionRepository(Db));
        new PermissionRepository(Db).SupplementDefaults();
        var roles = new RoleRepository(Db);
        roles.SupplementDefaults();
        var visibility = new EventUserVisibility(new UserGlobalPermissionRepository(Db, roles), new UserChapterPermissionRepository(Db, roles),
            new ChapterRepository(Db, AuditLog));
        _subscriptions = new AssemblyMotionSubscriptionRepository(Db);
        _poll = new AssemblyMotionForumReplyPoll(_motionRepo, users, new FakeDiscourseClient(), notifier, options, _subscriptions,
            new EventRepository(Db, AuditLog), visibility, NullLogger<AssemblyMotionForumReplyPoll>.Instance);
        _chapter = _builder.SeedChapter();
        _event = _builder.SeedEvent(_chapter.Id, publicName: "Bundesparteitag 2027.1", status: EventStatus.Active, visibility: EventVisibility.Public);
        _portal = SeedPortal();
    }

    private AssemblyMotionPortal SeedPortal(bool mirror = true) {
        var portal = _portalRepo.Create(_event.Id, DateTime.UtcNow.AddHours(-1), DateTime.UtcNow.AddDays(7), DateTime.UtcNow);
        Db.AssemblyMotionPortals
            .Where(p => p.Id == portal.Id)
            .Set(p => p.ForumMirrorEnabled, mirror)
            .Set(p => p.ForumCategoryId, 7)
            .Update();
        return _portalRepo.Get(portal.Id)!;
    }

    /// <summary>A submitted motion whose author signed in through Keycloak, mirrored to a new topic.</summary>
    private async Task<(AssemblyMotion Motion, Member Author, int TopicId)> MirroredMotion(string title = "Testantrag") {
        var user = _builder.SeedUser();
        Db.Users
            .Where(u => u.Id == user.Id)
            .Set(u => u.KeycloakUserId, AuthorKeycloakId)
            .Update();
        var author = _builder.SeedMember(chapterId: _chapter.Id, firstName: "Anna", lastName: "Pirat", userId: user.Id);
        var type = _portalRepo.GetTypes(_portal.Id).First(t => t.MinSubmitters == 1);
        var draft = new AssemblyMotionDraft(type.Id, title, null, "Antragstext", "Begründung", "Kurzfassung", null, null);
        var motion = _service.CreateDraft(_portal, author, Guid.NewGuid(), draft, DateTime.UtcNow).Motion!;
        motion = _service.Release(motion.Id, author, DateTime.UtcNow).Motion!;
        await _sync.SyncDueAsync(_now, CancellationToken.None);
        _queue.Requests.Clear();
        return (motion, author, _motionRepo.Get(motion.Id)!.ForumTopicId!.Value);
    }

    private int? Seen(Guid motionId) => _motionRepo.Get(motionId)!.ForumSeenPostNumber;

    private List<NotificationDispatchRequest> ReplyNotifications()
        => _queue.Requests.Where(r => r.TriggerId == NotificationTriggers.AssemblyMotionForumReply).ToList();

    private List<NotificationDispatchRequest> SubscriberNotifications()
        => _queue.Requests.Where(r => r.TriggerId == NotificationTriggers.AssemblyMotionSubscribedForumReply).ToList();

    private Guid Subscriber(Guid motionId, string keycloakId = "kc-bernd") {
        var user = _builder.SeedUser();
        Db.Users
            .Where(u => u.Id == user.Id)
            .Set(u => u.KeycloakUserId, keycloakId)
            .Update();
        _subscriptions.Subscribe(motionId, user.Id, DateTime.UtcNow);
        return user.Id;
    }

    private static AssemblyMotionNotificationModel ModelFor(NotificationDispatchRequest request)
        => (AssemblyMotionNotificationModel)request.ModelFactory(new NotificationRecipient(null, "x@test.local"))["motion"];

    private Task<int> Poll() => _poll.PollAsync(_now, CancellationToken.None);

    [Test]
    public async Task New_topic_starts_at_its_first_post_and_notifies_the_author_of_a_reply() {
        var (motion, author, topicId) = await MirroredMotion();
        await Assert.That(Seen(motion.Id)).IsEqualTo(1);

        FakeDiscourseClient.AddReply(topicId, "bernd", 43);
        var notified = await Poll();

        var request = ReplyNotifications().Single();
        var payload = (AssemblyMotionNotificationPayload)request.Payload;
        await Assert.That(notified).IsEqualTo(1);
        await Assert.That(payload.MemberIds.Single()).IsEqualTo(author.Id);
        await Assert.That(payload.IncludeCommission).IsFalse();
        await Assert.That(ModelFor(request).ForumReplyUrl).IsEqualTo($"https://forum.test/t/{topicId}/2");
        await Assert.That(Seen(motion.Id)).IsEqualTo(2);
    }

    [Test]
    public async Task A_reply_notifies_only_once() {
        var (_, _, topicId) = await MirroredMotion();
        FakeDiscourseClient.AddReply(topicId, "bernd", 43);
        await Poll();
        _queue.Requests.Clear();

        var notified = await Poll();

        await Assert.That(notified).IsEqualTo(0);
        await Assert.That(ReplyNotifications()).IsEmpty();
    }

    [Test]
    public async Task Topic_without_a_seen_post_is_recorded_first_without_notifying() {
        var (motion, _, topicId) = await MirroredMotion();
        FakeDiscourseClient.AddReply(topicId, "bernd", 43);
        FakeDiscourseClient.AddReply(topicId, "carla", 44);
        Db.AssemblyMotions
            .Where(m => m.Id == motion.Id)
            .Set(m => m.ForumSeenPostNumber, (int?)null)
            .Update();

        var first = await Poll();
        FakeDiscourseClient.AddReply(topicId, "dora", 45);
        var second = await Poll();

        await Assert.That(first).IsEqualTo(0);
        await Assert.That(second).IsEqualTo(1);
        await Assert.That(ModelFor(ReplyNotifications().Single()).ForumReplyUrl).EndsWith("/4");
    }

    [Test]
    public async Task Bot_posts_status_posts_deleted_and_hidden_posts_and_unseen_whispers_do_not_notify() {
        var (motion, _, topicId) = await MirroredMotion();
        FakeDiscourseClient.AddReply(topicId, "AntragsBot", 1);
        FakeDiscourseClient.AddReply(topicId, "moderator", 2, postType: 3);
        FakeDiscourseClient.AddReply(topicId, "bernd", 43, deleted: true);
        FakeDiscourseClient.AddReply(topicId, "carla", 44, hidden: true);
        FakeDiscourseClient.AddReply(topicId, "moderator", 2, postType: FakeDiscourseClient.WhisperPostType);

        var notified = await Poll();

        await Assert.That(notified).IsEqualTo(0);
        await Assert.That(Seen(motion.Id)).IsEqualTo(6);
    }

    [Test]
    public async Task Author_reply_is_skipped_when_the_bot_is_admin() {
        FakeDiscourseClient.CurrentUser = new DiscourseCurrentUser("antragsbot", true, true);
        FakeDiscourseClient.KeycloakUsers[AuthorKeycloakId] = AuthorForumUserId;
        var (_, _, topicId) = await MirroredMotion();
        FakeDiscourseClient.AddReply(topicId, "anna", AuthorForumUserId);
        FakeDiscourseClient.AddReply(topicId, "bernd", 43);

        var notified = await Poll();

        await Assert.That(notified).IsEqualTo(1);
        await Assert.That(ModelFor(ReplyNotifications().Single()).ForumReplyUrl).EndsWith("/3");
        await Assert.That(FakeDiscourseClient.Count("User")).IsEqualTo(1);
    }

    [Test]
    public async Task Author_reply_notifies_when_the_bot_is_no_admin() {
        FakeDiscourseClient.KeycloakUsers[AuthorKeycloakId] = AuthorForumUserId;
        var (_, _, topicId) = await MirroredMotion();
        FakeDiscourseClient.AddReply(topicId, "anna", AuthorForumUserId);

        var notified = await Poll();

        await Assert.That(notified).IsEqualTo(1);
        await Assert.That(FakeDiscourseClient.Count("User")).IsEqualTo(0);
    }

    [Test]
    [Arguments(EventStatus.Completed)]
    [Arguments(EventStatus.Archived)]
    public async Task Topics_of_a_closed_event_are_not_watched(EventStatus status) {
        var (_, _, topicId) = await MirroredMotion();
        Db.Events
            .Where(e => e.Id == _event.Id)
            .Set(e => e.Status, status)
            .Update();
        FakeDiscourseClient.AddReply(topicId, "bernd", 43);

        var notified = await Poll();

        await Assert.That(notified).IsEqualTo(0);
        await Assert.That(FakeDiscourseClient.Count("List")).IsEqualTo(0);
    }

    [Test]
    public async Task Topics_of_a_portal_without_mirror_are_not_watched() {
        var (_, _, topicId) = await MirroredMotion();
        Db.AssemblyMotionPortals
            .Where(p => p.Id == _portal.Id)
            .Set(p => p.ForumMirrorEnabled, false)
            .Update();
        FakeDiscourseClient.AddReply(topicId, "bernd", 43);

        var notified = await Poll();

        await Assert.That(notified).IsEqualTo(0);
        await Assert.That(FakeDiscourseClient.Count("CurrentUser")).IsEqualTo(1);
    }

    [Test]
    public async Task Rate_limit_ends_the_run_and_the_reply_notifies_later() {
        var (motion, _, topicId) = await MirroredMotion();
        FakeDiscourseClient.AddReply(topicId, "bernd", 43);
        FakeDiscourseClient.ThrowOn["Posts"] = new DiscourseException("Das Forum begrenzt die Anfragen gerade.", DiscourseException.RateLimited, 429);

        var limited = await Poll();
        FakeDiscourseClient.ThrowOn.Clear();
        var later = await Poll();

        await Assert.That(limited).IsEqualTo(0);
        await Assert.That(later).IsEqualTo(1);
        await Assert.That(Seen(motion.Id)).IsEqualTo(2);
    }

    [Test]
    public async Task Listing_pages_on_until_every_watched_topic_was_listed() {
        var (_, _, oldest) = await MirroredMotion("Erster");
        var (_, _, middle) = await MirroredMotion("Zweiter");
        await MirroredMotion("Dritter");
        FakeDiscourseClient.ListPageSize = 1;
        FakeDiscourseClient.AddReply(middle, "bernd", 43);
        FakeDiscourseClient.AddReply(oldest, "carla", 44);

        var notified = await Poll();

        await Assert.That(notified).IsEqualTo(2);
        await Assert.That(FakeDiscourseClient.Count("List")).IsEqualTo(3);
    }

    /// <summary>Topics of other events or without a motion can be more active than a watched topic with a new reply.</summary>
    [Test]
    public async Task A_grown_topic_below_a_page_of_other_topics_is_read() {
        var (_, _, topicId) = await MirroredMotion();
        FakeDiscourseClient.ListPageSize = 1;
        FakeDiscourseClient.AddReply(topicId, "bernd", 43);
        FakeDiscourseClient.AddTopic("Allgemeine Diskussion", "Text");

        var notified = await Poll();

        await Assert.That(notified).IsEqualTo(1);
    }

    /// <summary>Discourse sends no user id for posts of a removed account; that must not stop the topic or pass for the author's own reply.</summary>
    [Test]
    public async Task Reply_of_a_removed_account_notifies_author_and_subscribers() {
        var (motion, _, topicId) = await MirroredMotion();
        Subscriber(motion.Id);
        FakeDiscourseClient.AddReply(topicId, "geloescht", null);

        var notified = await Poll();

        await Assert.That(notified).IsEqualTo(1);
        await Assert.That(ReplyNotifications().Count).IsEqualTo(1);
        await Assert.That(SubscriberNotifications().Count).IsEqualTo(1);
    }

    [Test]
    public async Task Replies_beyond_one_chunk_are_all_notified() {
        var (motion, _, topicId) = await MirroredMotion();
        for (var i = 0; i < 25; i++)
            FakeDiscourseClient.AddReply(topicId, "bernd", 43);

        var notified = await Poll();

        await Assert.That(notified).IsEqualTo(25);
        await Assert.That(Seen(motion.Id)).IsEqualTo(26);
    }

    [Test]
    public async Task Reply_notifies_author_and_subscribers_separately() {
        var (motion, author, topicId) = await MirroredMotion();
        var subscriber = Subscriber(motion.Id);
        FakeDiscourseClient.AddReply(topicId, "carla", 44);

        var notified = await Poll();

        var forSubscribers = SubscriberNotifications().Single();
        await Assert.That(notified).IsEqualTo(1);
        await Assert.That(((AssemblyMotionNotificationPayload)ReplyNotifications().Single().Payload).MemberIds.Single()).IsEqualTo(author.Id);
        await Assert.That(((AssemblyMotionNotificationPayload)forSubscribers.Payload).UserIds!.Single()).IsEqualTo(subscriber);
        await Assert.That(ModelFor(forSubscribers).ForumReplyUrl).IsEqualTo($"https://forum.test/t/{topicId}/2");
    }

    [Test]
    public async Task Author_reply_still_notifies_subscribers() {
        FakeDiscourseClient.CurrentUser = new DiscourseCurrentUser("antragsbot", true, true);
        FakeDiscourseClient.KeycloakUsers[AuthorKeycloakId] = AuthorForumUserId;
        var (motion, _, topicId) = await MirroredMotion();
        var subscriber = Subscriber(motion.Id);
        FakeDiscourseClient.AddReply(topicId, "anna", AuthorForumUserId);

        var notified = await Poll();

        await Assert.That(notified).IsEqualTo(1);
        await Assert.That(ReplyNotifications()).IsEmpty();
        await Assert.That(((AssemblyMotionNotificationPayload)SubscriberNotifications().Single().Payload).UserIds!.Single()).IsEqualTo(subscriber);
    }

    [Test]
    public async Task Subscriber_reply_does_not_notify_them() {
        FakeDiscourseClient.CurrentUser = new DiscourseCurrentUser("antragsbot", true, true);
        FakeDiscourseClient.KeycloakUsers[AuthorKeycloakId] = AuthorForumUserId;
        FakeDiscourseClient.KeycloakUsers["kc-bernd"] = 43;
        var (motion, _, topicId) = await MirroredMotion();
        Subscriber(motion.Id);
        var other = Subscriber(motion.Id, "kc-dora");
        FakeDiscourseClient.AddReply(topicId, "bernd", 43);

        await Poll();

        await Assert.That(ReplyNotifications().Count).IsEqualTo(1);
        await Assert.That(((AssemblyMotionNotificationPayload)SubscriberNotifications().Single().Payload).UserIds!.Single()).IsEqualTo(other);
    }

    [Test]
    public async Task Subscriber_reply_notifies_them_without_admin_bot() {
        FakeDiscourseClient.KeycloakUsers["kc-bernd"] = 43;
        var (motion, _, topicId) = await MirroredMotion();
        var subscriber = Subscriber(motion.Id);
        FakeDiscourseClient.AddReply(topicId, "bernd", 43);

        await Poll();

        await Assert.That(((AssemblyMotionNotificationPayload)SubscriberNotifications().Single().Payload).UserIds!.Single()).IsEqualTo(subscriber);
    }

    [Test]
    public async Task Subscriber_who_lost_visibility_gets_nothing() {
        var (motion, _, topicId) = await MirroredMotion();
        Subscriber(motion.Id);
        Db.Events.Where(e => e.Id == _event.Id).Set(e => e.Visibility, EventVisibility.Private).Update();
        FakeDiscourseClient.AddReply(topicId, "carla", 44);

        var notified = await Poll();

        await Assert.That(notified).IsEqualTo(1);
        await Assert.That(ReplyNotifications().Count).IsEqualTo(1);
        await Assert.That(SubscriberNotifications()).IsEmpty();
    }

    [Test]
    public async Task Author_who_subscribed_is_not_in_the_subscriber_request() {
        var (motion, author, topicId) = await MirroredMotion();
        _subscriptions.Subscribe(motion.Id, author.UserId!.Value, DateTime.UtcNow);
        var subscriber = Subscriber(motion.Id);
        FakeDiscourseClient.AddReply(topicId, "carla", 44);

        await Poll();

        await Assert.That(((AssemblyMotionNotificationPayload)SubscriberNotifications().Single().Payload).UserIds!.Single()).IsEqualTo(subscriber);
    }

    [Test]
    public async Task Rate_limited_subscriber_lookup_does_not_resend_the_author_notification() {
        FakeDiscourseClient.CurrentUser = new DiscourseCurrentUser("antragsbot", true, true);
        FakeDiscourseClient.KeycloakUsers[AuthorKeycloakId] = AuthorForumUserId;
        var (motion, _, topicId) = await MirroredMotion();
        var subscriber = Subscriber(motion.Id);
        FakeDiscourseClient.AddReply(topicId, "carla", 44);
        FakeDiscourseClient.ThrowOnKeycloakId["kc-bernd"] = new DiscourseException("Das Forum begrenzt die Anfragen gerade.", DiscourseException.RateLimited, 429);

        var limited = await Poll();
        FakeDiscourseClient.ThrowOnKeycloakId.Clear();
        var later = await Poll();

        await Assert.That(limited).IsEqualTo(0);
        await Assert.That(later).IsEqualTo(1);
        await Assert.That(ReplyNotifications().Count).IsEqualTo(1);
        await Assert.That(((AssemblyMotionNotificationPayload)SubscriberNotifications().Single().Payload).UserIds!.Single()).IsEqualTo(subscriber);
        await Assert.That(Seen(motion.Id)).IsEqualTo(2);
    }

    [Test]
    public async Task Subscriber_lookups_are_capped_per_run() {
        FakeDiscourseClient.CurrentUser = new DiscourseCurrentUser("antragsbot", true, true);
        FakeDiscourseClient.KeycloakUsers[AuthorKeycloakId] = AuthorForumUserId;
        var (motion, _, topicId) = await MirroredMotion();
        var subscribers = Enumerable.Range(0, AssemblyMotionForumReplyPoll.MaxSubscriberLookupsPerRun + 5)
            .Select(i => Subscriber(motion.Id, $"kc-sub-{i}"))
            .ToList();
        FakeDiscourseClient.AddReply(topicId, "carla", 44);

        await Poll();

        var payload = (AssemblyMotionNotificationPayload)SubscriberNotifications().Single().Payload;
        await Assert.That(FakeDiscourseClient.Count("User")).IsEqualTo(AssemblyMotionForumReplyPoll.MaxSubscriberLookupsPerRun + 1);
        await Assert.That(payload.UserIds!.ToHashSet().SetEquals(subscribers)).IsTrue();
    }
}
