using LinqToDB;
using Microsoft.Extensions.Logging.Abstractions;
using Quartermaster.Api.AssemblyMotions;
using Quartermaster.Api.I18n;
using Quartermaster.Data;
using Quartermaster.Data.AssemblyMotions;
using Quartermaster.Data.AuditLog;
using Quartermaster.Data.Chapters;
using Quartermaster.Data.Events;
using Quartermaster.Data.Members;
using Quartermaster.Data.Options;
using Quartermaster.Server.AssemblyMotions;
using Quartermaster.Server.AssemblyMotions.Forum;
using Quartermaster.Server.AssemblyMotions.Notifications;
using Quartermaster.Server.Notifications;
using Quartermaster.Server.Tests.AssemblyMotions.Fakes;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.AssemblyMotions;

[NotInParallel("FakeDiscourseClient")]
public class AssemblyMotionForumSyncTests : RepositoryTestBase {
    private const string BaseUrl = "https://qm.test";
    private static readonly I18nService I18n = new($$"""
        {
          "{{I18nKey.Ui.AssemblyMotion.StatusSubmitted}}": "Eingereicht",
          "{{I18nKey.Ui.AssemblyMotion.StatusAdmitted}}": "Zugelassen",
          "{{I18nKey.Ui.AssemblyMotion.StatusInRevision}}": "In Überarbeitung"
        }
        """);

    private readonly DateTime _now = new(2027, 3, 1, 12, 0, 0, DateTimeKind.Utc);
    private TestDataBuilder _builder = default!;
    private AssemblyMotionPortalRepository _portalRepo = default!;
    private AssemblyMotionRepository _motionRepo = default!;
    private AssemblyMotionService _service = default!;
    private OptionRepository _options = default!;
    private AssemblyMotionForumSync _sync = default!;
    private Chapter _chapter = default!;
    private Event _event = default!;
    private AssemblyMotionPortal _portal = default!;

    [Before(Test)]
    public void Setup() {
        FakeDiscourseClient.Reset();
        AssemblyMotionForumSync.ResetUnconfiguredWarning();
        _builder = new TestDataBuilder(Db);
        _portalRepo = new AssemblyMotionPortalRepository(Db, AuditLog);
        _motionRepo = new AssemblyMotionRepository(Db, AuditLog);
        _service = new AssemblyMotionService(_motionRepo, _portalRepo, AuditLog, Db, new AssemblyMotionNotifier(new CapturingNotificationQueue(), Db, NullLogger<AssemblyMotionNotifier>.Instance));
        _options = new OptionRepository(Db, AuditLog);
        _options.SupplementDefaults();
        _options.SetValue("discourse.base_url", null, "https://forum.test");
        _options.SetValue("discourse.api_username", null, "antragsbot");
        _options.SetValue("discourse.api_key", null, "key");
        _options.SetValue(NotificationTemplateGlobals.BaseUrlOptionKey, null, BaseUrl);
        _sync = NewSync(Db);
        _chapter = _builder.SeedChapter();
        _event = _builder.SeedEvent(_chapter.Id, publicName: "Bundesparteitag 2027.1");
        _portal = SeedPortal();
    }

    private static AssemblyMotionForumSync NewSync(DbContext context) {
        var auditLog = new AuditLogRepository(context);
        return new AssemblyMotionForumSync(
            new AssemblyMotionRepository(context, auditLog),
            new AssemblyMotionPortalRepository(context, auditLog),
            new EventRepository(context, auditLog),
            context,
            new FakeDiscourseClient(),
            new OptionRepository(context, auditLog),
            auditLog,
            I18n,
            NullLogger<AssemblyMotionForumSync>.Instance);
    }

    private AssemblyMotionPortal SeedPortal(bool mirror = true, int? categoryId = 7) {
        var portal = _portalRepo.Create(_event.Id, DateTime.UtcNow.AddHours(-1), DateTime.UtcNow.AddDays(7), DateTime.UtcNow);
        Db.AssemblyMotionPortals
            .Where(p => p.Id == portal.Id)
            .Set(p => p.ForumMirrorEnabled, mirror)
            .Set(p => p.ForumCategoryId, categoryId)
            .Update();
        return _portalRepo.Get(portal.Id)!;
    }

    private AssemblyMotion Submit(string title = "Testantrag", string text = "Antragstext") {
        var type = _portalRepo.GetTypes(_portal.Id).First(t => t.MinSubmitters == 1);
        var author = _builder.SeedMember(chapterId: _chapter.Id, firstName: "Anna", lastName: "Pirat");
        var draft = new AssemblyMotionDraft(type.Id, title, null, text, "Begründung", "Kurzfassung", null, null);
        var motion = _service.CreateDraft(_portal, author, Guid.NewGuid(), draft, DateTime.UtcNow).Motion!;
        return _service.Release(motion.Id, author, DateTime.UtcNow).Motion!;
    }

    private AssemblyMotion Reload(Guid id) => _motionRepo.Get(id)!;

    private string Number() {
        var type = _portalRepo.GetTypes(_portal.Id).First(t => t.MinSubmitters == 1);
        return AssemblyMotionNumber.Format(type.Code, 1);
    }

    private void ChangeTitle(Guid motionId, string title) {
        Db.AssemblyMotions
            .Where(m => m.Id == motionId)
            .Set(m => m.Title, title)
            .Set(m => m.Revision, 2)
            .Update();
        _motionRepo.MarkMirrorsPending(motionId, _now);
    }

    [Test]
    public async Task Creates_topic_with_external_id_and_stores_ids() {
        var motion = Submit();

        var synced = await _sync.SyncDueAsync(_now, CancellationToken.None);

        var topic = FakeDiscourseClient.Topics.Values.Single();
        var reloaded = Reload(motion.Id);
        await Assert.That(synced).IsEqualTo(1);
        await Assert.That(topic.ExternalId).IsEqualTo($"qm-motion-{motion.Id}");
        await Assert.That(topic.CategoryId).IsEqualTo(7);
        await Assert.That(topic.Title).StartsWith(Number() + ": Testantrag");
        await Assert.That(reloaded.ForumSyncState).IsEqualTo(ForumSyncState.Synced);
        await Assert.That(reloaded.ForumTopicId).IsEqualTo(topic.TopicId);
        await Assert.That(reloaded.ForumPostId).IsEqualTo(topic.PostId);
    }

    [Test]
    public async Task Takes_over_topic_found_by_external_id() {
        var motion = Submit();
        var existing = FakeDiscourseClient.AddTopic("alt", "alt", $"qm-motion-{motion.Id}");

        await _sync.SyncDueAsync(_now, CancellationToken.None);

        var reloaded = Reload(motion.Id);
        await Assert.That(FakeDiscourseClient.Count("Create")).IsEqualTo(0);
        await Assert.That(reloaded.ForumTopicId).IsEqualTo(existing.TopicId);
        await Assert.That(existing.Raw).Contains("Testantrag");
        await Assert.That(reloaded.ForumSyncState).IsEqualTo(ForumSyncState.Synced);
    }

    [Test]
    public async Task Unchanged_hash_sends_nothing() {
        var motion = Submit();
        await _sync.SyncDueAsync(_now, CancellationToken.None);
        FakeDiscourseClient.Calls.Clear();
        _motionRepo.MarkMirrorsPending(motion.Id, _now);

        await _sync.SyncDueAsync(_now, CancellationToken.None);

        await Assert.That(FakeDiscourseClient.Calls.Where(c => c != "CurrentUser").ToArray()).IsEmpty();
        await Assert.That(Reload(motion.Id).ForumSyncState).IsEqualTo(ForumSyncState.Synced);
    }

    [Test]
    public async Task Changed_motion_edits_post_with_revision_reason_and_updates_title() {
        var motion = Submit();
        await _sync.SyncDueAsync(_now, CancellationToken.None);
        ChangeTitle(motion.Id, "Neuer Titel");

        await _sync.SyncDueAsync(_now, CancellationToken.None);

        var topic = FakeDiscourseClient.Topics.Values.Single();
        await Assert.That(FakeDiscourseClient.Edits.Single().Reason).IsEqualTo("Antragsportal: Revision 2 eingereicht");
        await Assert.That(topic.Raw).Contains("Neuer Titel");
        await Assert.That(topic.Title).StartsWith(Number() + ": Neuer Titel");
        await Assert.That(Reload(motion.Id).ForumSyncedRevision).IsEqualTo(2);
    }

    [Test]
    public async Task Status_change_uses_status_reason() {
        var motion = Submit();
        await _sync.SyncDueAsync(_now, CancellationToken.None);
        Db.AssemblyMotions
            .Where(m => m.Id == motion.Id)
            .Set(m => m.Status, AssemblyMotionStatus.Admitted)
            .Update();
        _motionRepo.MarkMirrorsPending(motion.Id, _now);

        await _sync.SyncDueAsync(_now, CancellationToken.None);

        await Assert.That(FakeDiscourseClient.Edits.Single().Reason).IsEqualTo("Antragsportal: Status aktualisiert (Revision 1)");
        await Assert.That(FakeDiscourseClient.Topics.Values.Single().Raw).Contains("Status: Zugelassen");
        await Assert.That(FakeDiscourseClient.Count("Title")).IsEqualTo(0);
    }

    [Test]
    public async Task Moderator_edit_is_overwritten_and_audited() {
        var motion = Submit();
        await _sync.SyncDueAsync(_now, CancellationToken.None);
        var topic = FakeDiscourseClient.Topics.Values.Single();
        topic.Raw = "Vom Moderator geändert";
        ChangeTitle(motion.Id, "Neuer Titel");

        await _sync.SyncDueAsync(_now, CancellationToken.None);

        await Assert.That(topic.Raw).Contains("Neuer Titel");
        await Assert.That(topic.Raw).DoesNotContain("Vom Moderator");
        await Assert.That(AuditLog.GetForEntity("AssemblyMotion", motion.Id).Count(a => a.Action == "ForumContentDiffered")).IsEqualTo(1);
    }

    [Test]
    public async Task Rejected_long_post_is_retried_with_notices() {
        var motion = Submit(text: new string('a', 5000));
        FakeDiscourseClient.RejectRawLongerThan = 2000;

        await _sync.SyncDueAsync(_now, CancellationToken.None);

        var topic = FakeDiscourseClient.Topics.Values.Single();
        await Assert.That(FakeDiscourseClient.Count("Create")).IsEqualTo(2);
        await Assert.That(topic.Raw).Contains("Der Antragstext ist zu lang für das Forum");
        await Assert.That(Reload(motion.Id).ForumSyncState).IsEqualTo(ForumSyncState.Synced);
    }

    [Test]
    public async Task Rejected_short_post_records_failure() {
        var motion = Submit();
        FakeDiscourseClient.RejectRawLongerThan = 10;

        await _sync.SyncDueAsync(_now, CancellationToken.None);

        var reloaded = Reload(motion.Id);
        await Assert.That(reloaded.ForumSyncState).IsEqualTo(ForumSyncState.Failed);
        await Assert.That(reloaded.ForumLastError).Contains("Der Beitrag ist zu lang.");
        await Assert.That(reloaded.ForumNextAttemptAt).IsEqualTo(_now.AddMinutes(1));
        await Assert.That(AuditLog.GetForEntity("AssemblyMotion", motion.Id).Count(a => a.Action == "ForumSyncFailed")).IsEqualTo(1);
    }

    [Test]
    public async Task Unauthorized_is_permanent() {
        var motion = Submit();
        FakeDiscourseClient.ThrowOn["Find"] = new DiscourseException("abgelehnt", DiscourseException.Unauthorized, 403);

        await _sync.SyncDueAsync(_now, CancellationToken.None);

        await Assert.That(Reload(motion.Id).ForumNextAttemptAt).IsEqualTo(_now.AddHours(6));
    }

    [Test]
    public async Task Rate_limit_ends_run_without_failure() {
        var first = Submit();
        var second = Submit("Zweiter");
        FakeDiscourseClient.ThrowOn["Find"] = new DiscourseException("langsam", DiscourseException.RateLimited, 429);

        await _sync.SyncDueAsync(_now, CancellationToken.None);

        await Assert.That(FakeDiscourseClient.Count("Find")).IsEqualTo(1);
        await Assert.That(Reload(first.Id).ForumSyncState).IsEqualTo(ForumSyncState.Pending);
        await Assert.That(Reload(first.Id).ForumFailureCount).IsEqualTo(0);
        await Assert.That(Reload(second.Id).ForumSyncState).IsEqualTo(ForumSyncState.Pending);
    }

    [Test]
    public async Task Failed_session_check_touches_no_motion() {
        var motion = Submit();
        FakeDiscourseClient.ThrowOn["CurrentUser"] = new DiscourseException("Nicht erreichbar", DiscourseException.HttpError);

        var synced = await _sync.SyncDueAsync(_now, CancellationToken.None);

        var reloaded = Reload(motion.Id);
        await Assert.That(synced).IsEqualTo(0);
        await Assert.That(reloaded.ForumSyncState).IsEqualTo(ForumSyncState.Pending);
        await Assert.That(reloaded.ForumLastError).IsNull();
        await Assert.That(FakeDiscourseClient.Count("Find")).IsEqualTo(0);
    }

    [Test]
    public async Task Topic_with_deleted_at_marks_the_motion_deleted() {
        var motion = Submit();
        await _sync.SyncDueAsync(_now, CancellationToken.None);
        FakeDiscourseClient.Topics.Values.Single().Deleted = true;
        ChangeTitle(motion.Id, "Neuer Titel");

        await _sync.SyncDueAsync(_now, CancellationToken.None);

        var reloaded = Reload(motion.Id);
        await Assert.That(reloaded.ForumSyncState).IsEqualTo(ForumSyncState.Deleted);
        await Assert.That(reloaded.ForumLastError).IsEqualTo("Das Thema wurde im Forum gelöscht.");
        await Assert.That(FakeDiscourseClient.Count("Edit")).IsEqualTo(0);
        await Assert.That(AuditLog.GetForEntity("AssemblyMotion", motion.Id).Count(a => a.Action == "ForumTopicDeleted")).IsEqualTo(1);
    }

    [Test]
    public async Task Missing_topic_is_a_permanent_failure_not_a_deletion() {
        var motion = Submit();
        await _sync.SyncDueAsync(_now, CancellationToken.None);
        FakeDiscourseClient.Topics.Clear();
        ChangeTitle(motion.Id, "Neuer Titel");

        await _sync.SyncDueAsync(_now, CancellationToken.None);

        var reloaded = Reload(motion.Id);
        await Assert.That(reloaded.ForumSyncState).IsEqualTo(ForumSyncState.Failed);
        await Assert.That(reloaded.ForumNextAttemptAt).IsEqualTo(_now.AddHours(6));
        await Assert.That(reloaded.ForumLastError).Contains("nicht mehr sehen");
        _motionRepo.MarkMirrorsPending(motion.Id, _now);
        await Assert.That(Reload(motion.Id).ForumSyncState).IsEqualTo(ForumSyncState.Pending);
    }

    [Test]
    public async Task Disabled_portal_is_not_synced() {
        Db.AssemblyMotionPortals
            .Where(p => p.Id == _portal.Id)
            .Set(p => p.ForumMirrorEnabled, false)
            .Update();
        Submit();

        await _sync.SyncDueAsync(_now, CancellationToken.None);

        await Assert.That(FakeDiscourseClient.Topics).IsEmpty();
    }

    [Test]
    public async Task Unconfigured_settings_do_nothing() {
        _options.SetValue("discourse.api_key", null, "");
        Submit();

        var synced = await _sync.SyncDueAsync(_now, CancellationToken.None);

        await Assert.That(synced).IsEqualTo(0);
        await Assert.That(FakeDiscourseClient.Calls).IsEmpty();
    }

    [Test]
    public async Task Missing_category_is_permanent_failure() {
        Db.AssemblyMotionPortals
            .Where(p => p.Id == _portal.Id)
            .Set(p => p.ForumCategoryId, (int?)null)
            .Update();
        var motion = Submit();

        await _sync.SyncDueAsync(_now, CancellationToken.None);

        var reloaded = Reload(motion.Id);
        await Assert.That(reloaded.ForumSyncState).IsEqualTo(ForumSyncState.Failed);
        await Assert.That(reloaded.ForumLastError).IsEqualTo("Für das Portal ist keine Forum-Kategorie eingestellt.");
        await Assert.That(reloaded.ForumNextAttemptAt).IsEqualTo(_now.AddHours(6));
    }

    [Test]
    public async Task Post_contains_submitter_names_and_wiki_link_when_wiki_mirrors() {
        Db.AssemblyMotionPortals
            .Where(p => p.Id == _portal.Id)
            .Set(p => p.WikiMirrorEnabled, true)
            .Set(p => p.WikiPagePrefix, "Antrag:BPT 2027.1")
            .Update();
        _portal = _portalRepo.Get(_portal.Id)!;
        Submit();

        await _sync.SyncDueAsync(_now, CancellationToken.None);

        var raw = FakeDiscourseClient.Topics.Values.Single().Raw;
        await Assert.That(raw).Contains("Antragsteller: Anna Pirat");
        await Assert.That(raw).Contains("Antrag im Wiki: https://wiki.piratenpartei.de/index.php?title=Antrag:BPT_2027.1/" + Number());
        await Assert.That(raw).Contains("Antrag im Antragsportal: https://qm.test/AssemblyMotions/");
    }

    [Test]
    public async Task Newer_request_during_sync_keeps_motion_pending() {
        var motion = Submit();
        FakeDiscourseClient.OnCreate = () => {
            _motionRepo.MarkMirrorsPending(motion.Id, _now.AddSeconds(5));
            return Task.CompletedTask;
        };

        await _sync.SyncDueAsync(_now, CancellationToken.None);

        var reloaded = Reload(motion.Id);
        await Assert.That(reloaded.ForumSyncState).IsEqualTo(ForumSyncState.Pending);
        await Assert.That(reloaded.ForumTopicId).IsNotNull();
    }
}
