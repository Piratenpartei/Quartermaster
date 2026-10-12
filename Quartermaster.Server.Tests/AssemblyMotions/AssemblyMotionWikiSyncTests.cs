using System.Net.Http;
using LinqToDB;
using Microsoft.Extensions.Logging.Abstractions;
using Quartermaster.Api.AssemblyMotions;
using Quartermaster.Api.Events;
using Quartermaster.Data;
using Quartermaster.Data.AssemblyMotions;
using Quartermaster.Data.Chapters;
using Quartermaster.Data.Events;
using Quartermaster.Data.Members;
using Quartermaster.Data.Options;
using Quartermaster.Server.AssemblyMotions;
using Quartermaster.Server.AssemblyMotions.Notifications;
using Quartermaster.Server.AssemblyMotions.Wiki;
using Quartermaster.Server.Notifications;
using Quartermaster.Server.Tests.AssemblyMotions.Fakes;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.AssemblyMotions;

[NotInParallel("FakeWikiBotClient")]
public class AssemblyMotionWikiSyncTests : RepositoryTestBase {
    private const string Prefix = "Antrag:Bundesparteitag 2027.1/Antragsportal";
    private const string BaseUrl = "https://qm.test";

    private readonly DateTime _now = new(2027, 3, 1, 12, 0, 0, DateTimeKind.Utc);
    private TestDataBuilder _builder = default!;
    private AssemblyMotionPortalRepository _portalRepo = default!;
    private AssemblyMotionRepository _motionRepo = default!;
    private AssemblyMotionInquiryRepository _inquiryRepo = default!;
    private AssemblyMotionService _service = default!;
    private OptionRepository _options = default!;
    private AssemblyMotionWikiSync _sync = default!;
    private AssemblyMotionWikiReconcile _reconcile = default!;
    private Chapter _chapter = default!;
    private Event _event = default!;
    private AssemblyMotionPortal _portal = default!;

    [Before(Test)]
    public void Setup() {
        FakeWikiBotClient.Reset();
        AssemblyMotionWikiSync.ResetUnconfiguredWarning();
        _builder = new TestDataBuilder(Db);
        _portalRepo = new AssemblyMotionPortalRepository(Db, AuditLog);
        _motionRepo = new AssemblyMotionRepository(Db, AuditLog);
        _inquiryRepo = new AssemblyMotionInquiryRepository(Db);
        _service = new AssemblyMotionService(_motionRepo, _portalRepo, AuditLog, Db, new AssemblyMotionNotifier(new CapturingNotificationQueue(), Db, NullLogger<AssemblyMotionNotifier>.Instance));
        _options = new OptionRepository(Db, AuditLog);
        _options.SupplementDefaults();
        _options.SetValue("wiki.bot_username", null, "Antragsbot");
        _options.SetValue("wiki.bot_password", null, "secret");
        _options.SetValue(NotificationTemplateGlobals.BaseUrlOptionKey, null, BaseUrl);
        _sync = NewSync(Db);
        _reconcile = NewReconcile(Db);
        _chapter = _builder.SeedChapter();
        _event = _builder.SeedEvent(_chapter.Id);
        _portal = SeedPortal();
    }

    private static AssemblyMotionWikiSync NewSync(DbContext context, Microsoft.Extensions.Logging.ILogger<AssemblyMotionWikiSync>? logger = null) {
        var auditLog = new Quartermaster.Data.AuditLog.AuditLogRepository(context);
        return new AssemblyMotionWikiSync(
            new AssemblyMotionRepository(context, auditLog),
            new AssemblyMotionPortalRepository(context, auditLog),
            new AssemblyMotionInquiryRepository(context),
            new AssemblyMotionCompetitionRepository(context),
            new FakeWikiBotClient(),
            new OptionRepository(context, auditLog),
            auditLog,
            logger ?? NullLogger<AssemblyMotionWikiSync>.Instance);
    }

    private static AssemblyMotionWikiReconcile NewReconcile(DbContext context, Microsoft.Extensions.Logging.ILogger<AssemblyMotionWikiReconcile>? logger = null) {
        var auditLog = new Quartermaster.Data.AuditLog.AuditLogRepository(context);
        return new AssemblyMotionWikiReconcile(
            new AssemblyMotionRepository(context, auditLog),
            new AssemblyMotionPortalRepository(context, auditLog),
            new FakeWikiBotClient(),
            new OptionRepository(context, auditLog),
            logger ?? NullLogger<AssemblyMotionWikiReconcile>.Instance);
    }

    private AssemblyMotionPortal SeedPortal(bool mirror = true) {
        var portal = _portalRepo.Create(_event.Id, DateTime.UtcNow.AddHours(-1), DateTime.UtcNow.AddDays(7), DateTime.UtcNow);
        Db.AssemblyMotionPortals
            .Where(p => p.Id == portal.Id)
            .Set(p => p.WikiMirrorEnabled, mirror)
            .Set(p => p.WikiPagePrefix, Prefix)
            .Set(p => p.WikiAssemblyLabel, "Bundesparteitag 2027.1")
            .Update();
        return _portalRepo.Get(portal.Id)!;
    }

    private static AssemblyMotionDraft NewDraft(Guid typeId, string title = "Testantrag")
        => new(typeId, title, null, "Antragstext", "Begründung", null, null, null);

    private AssemblyMotionType TypeOf(string code, AssemblyMotionPortal? portal = null)
        => _portalRepo.GetTypes((portal ?? _portal).Id).First(t => t.Code == code);

    private (AssemblyMotion Motion, Member Author) Submit(string code = "SO", string title = "Testantrag", AssemblyMotionPortal? portal = null) {
        portal ??= _portal;
        var type = TypeOf(code, portal);
        var author = _builder.SeedMember(chapterId: _chapter.Id);
        var motion = _service.CreateDraft(portal, author, Guid.NewGuid(), NewDraft(type.Id, title), DateTime.UtcNow).Motion!;
        var released = _service.Release(motion.Id, author, DateTime.UtcNow).Motion!;
        for (var i = 1; i < type.MinSubmitters; i++)
            released = _service.Join(motion.Id, _builder.SeedMember(chapterId: _chapter.Id), DateTime.UtcNow).Motion!;

        return (released, author);
    }

    private string TitleOf(AssemblyMotion motion)
        => AssemblyMotionWikiPage.Title(_portal, _portalRepo.GetType(motion.TypeId)!, motion);

    private AssemblyMotion Reload(Guid id) => _motionRepo.Get(id)!;

    private void SetTemplate(string code, string template)
        => Db.AssemblyMotionTypes
            .Where(t => t.PortalId == _portal.Id && t.Code == code)
            .Set(t => t.WikiTemplate, template)
            .Update();

    [Test]
    public async Task First_sync_creates_and_protects_page() {
        var motion = Submit().Motion;
        var title = TitleOf(motion);

        var synced = await _sync.SyncDueAsync(_now, CancellationToken.None);

        await Assert.That(synced).IsEqualTo(1);
        await Assert.That(FakeWikiBotClient.Pages.ContainsKey(title)).IsTrue();
        await Assert.That(FakeWikiBotClient.Pages[title]).Contains($"{BaseUrl}/AssemblyMotions/{motion.Id}");
        await Assert.That(FakeWikiBotClient.Pages[title]).Contains("|wikiBenutzer=Antragsbot\n");
        await Assert.That(FakeWikiBotClient.Protected).IsEquivalentTo([title]);
        await Assert.That(FakeWikiBotClient.Edits.Single().Summary).Contains("angelegt");
        var reloaded = Reload(motion.Id);
        await Assert.That(reloaded.WikiSyncState).IsEqualTo(WikiSyncState.Synced);
        await Assert.That(reloaded.WikiSyncedRevision).IsEqualTo(1);
        await Assert.That(reloaded.WikiSyncedHash).IsEqualTo(WikiPageText.Hash(FakeWikiBotClient.Pages[title]));
        await Assert.That(reloaded.WikiLastSyncedAt).IsEqualTo(_now);
        await Assert.That(reloaded.WikiLastError).IsNull();
    }

    [Test]
    public async Task Protection_failure_sets_protection_failed_and_retries_later() {
        var motion = Submit().Motion;
        var title = TitleOf(motion);
        FakeWikiBotClient.FailProtect = true;

        await _sync.SyncDueAsync(_now, CancellationToken.None);

        var failed = Reload(motion.Id);
        await Assert.That(failed.WikiSyncState).IsEqualTo(WikiSyncState.ProtectionFailed);
        await Assert.That(failed.WikiLastError).Contains("Fake protect failure");
        await Assert.That(failed.WikiNextAttemptAt).IsEqualTo(_now.AddHours(1));
        await Assert.That(FakeWikiBotClient.Pages.ContainsKey(title)).IsTrue();

        FakeWikiBotClient.FailProtect = false;
        await Assert.That(await _sync.SyncDueAsync(_now.AddMinutes(30), CancellationToken.None)).IsEqualTo(0);
        await Assert.That(FakeWikiBotClient.Protected).IsEmpty();

        await Assert.That(await _sync.SyncDueAsync(_now.AddMinutes(61), CancellationToken.None)).IsEqualTo(1);
        await Assert.That(FakeWikiBotClient.Protected).IsEquivalentTo([title]);
        await Assert.That(FakeWikiBotClient.Edits.Count).IsEqualTo(1);
        await Assert.That(Reload(motion.Id).WikiSyncState).IsEqualTo(WikiSyncState.Synced);
        await Assert.That(Reload(motion.Id).WikiLastError).IsNull();
    }

    [Test]
    public async Task Unchanged_content_is_not_rewritten() {
        var motion = Submit().Motion;
        await _sync.SyncDueAsync(_now, CancellationToken.None);
        _motionRepo.MarkWikiPending(motion.Id, _now.AddMinutes(1));

        var synced = await _sync.SyncDueAsync(_now.AddMinutes(2), CancellationToken.None);

        await Assert.That(synced).IsEqualTo(1);
        await Assert.That(FakeWikiBotClient.Edits.Count).IsEqualTo(1);
        await Assert.That(Reload(motion.Id).WikiSyncState).IsEqualTo(WikiSyncState.Synced);
    }

    [Test]
    public async Task Wiki_roundtrip_normalisation_is_recognised_as_unchanged() {
        var motion = Submit().Motion;
        await _sync.SyncDueAsync(_now, CancellationToken.None);
        FakeWikiBotClient.TransformReturnedText = text => text.Replace("\n", "\r\n") + "\r\n\r\n";
        _motionRepo.MarkWikiPending(motion.Id, _now.AddMinutes(1));

        await _sync.SyncDueAsync(_now.AddMinutes(2), CancellationToken.None);

        await Assert.That(FakeWikiBotClient.Edits.Count).IsEqualTo(1);
        await Assert.That(AuditLog.GetForEntity("AssemblyMotion", motion.Id).Any(a => a.Action == "WikiContentDiffered")).IsFalse();
        await Assert.That(Reload(motion.Id).WikiSyncState).IsEqualTo(WikiSyncState.Synced);
    }

    [Test]
    public async Task Manual_edit_is_overwritten_and_audited() {
        var motion = Submit().Motion;
        var title = TitleOf(motion);
        await _sync.SyncDueAsync(_now, CancellationToken.None);
        var original = FakeWikiBotClient.Pages[title];
        FakeWikiBotClient.Pages[title] = "Vandalismus";
        _motionRepo.MarkWikiPending(motion.Id, _now.AddMinutes(1));

        await _sync.SyncDueAsync(_now.AddMinutes(2), CancellationToken.None);

        await Assert.That(FakeWikiBotClient.Pages[title]).IsEqualTo(original);
        await Assert.That(FakeWikiBotClient.Edits.Count).IsEqualTo(2);
        var entry = AuditLog.GetForEntity("AssemblyMotion", motion.Id).Single(a => a.Action == "WikiContentDiffered");
        await Assert.That(entry.NewValue).IsEqualTo(title);
    }

    [Test]
    public async Task Failure_backs_off_and_retries() {
        var motion = Submit().Motion;
        FakeWikiBotClient.FailNextEdit = true;

        await Assert.That(await _sync.SyncDueAsync(_now, CancellationToken.None)).IsEqualTo(0);

        var failed = Reload(motion.Id);
        await Assert.That(failed.WikiSyncState).IsEqualTo(WikiSyncState.Failed);
        await Assert.That(failed.WikiFailureCount).IsEqualTo(1);
        await Assert.That(failed.WikiNextAttemptAt).IsEqualTo(_now.AddMinutes(1));
        await Assert.That(AuditLog.GetForEntity("AssemblyMotion", motion.Id).Count(a => a.Action == "WikiSyncFailed")).IsEqualTo(1);

        var readsBefore = FakeWikiBotClient.GetCount;
        await Assert.That(await _sync.SyncDueAsync(_now.AddSeconds(30), CancellationToken.None)).IsEqualTo(0);
        await Assert.That(FakeWikiBotClient.GetCount).IsEqualTo(readsBefore);

        await Assert.That(await _sync.SyncDueAsync(_now.AddMinutes(1), CancellationToken.None)).IsEqualTo(1);
        var recovered = Reload(motion.Id);
        await Assert.That(recovered.WikiSyncState).IsEqualTo(WikiSyncState.Synced);
        await Assert.That(recovered.WikiFailureCount).IsEqualTo(0);
        await Assert.That(recovered.WikiLastError).IsNull();
        await Assert.That(recovered.WikiNextAttemptAt).IsNull();
    }

    [Test]
    public async Task Transient_error_uses_exponential_backoff() {
        var motion = Submit().Motion;
        FakeWikiBotClient.ThrowOn["GetContent"] = new WikiBotException("Wiki antwortet nicht", "http_error");
        var attemptAt = _now;
        var expectedMinutes = new[] { 1, 2, 4, 8, 16, 32, 64, 128, 256, 360, 360 };

        foreach (var minutes in expectedMinutes) {
            await _sync.SyncDueAsync(attemptAt, CancellationToken.None);

            var failed = Reload(motion.Id);
            await Assert.That(failed.WikiSyncState).IsEqualTo(WikiSyncState.Failed);
            await Assert.That(failed.WikiNextAttemptAt).IsEqualTo(attemptAt.AddMinutes(minutes));
            attemptAt = failed.WikiNextAttemptAt!.Value;
        }

        await Assert.That(AssemblyMotionWikiSync.BackoffFor(10_000)).IsEqualTo(TimeSpan.FromHours(6));
        await Assert.That(AssemblyMotionWikiSync.BackoffFor(int.MaxValue)).IsEqualTo(TimeSpan.FromHours(6));
    }

    [Test]
    [Arguments("readonly")]
    [Arguments("ratelimited")]
    [Arguments("maxlag")]
    [Arguments("blocked_html")]
    [Arguments("unexpected_response")]
    public async Task Transient_codes_use_normal_backoff(string code) {
        var motion = Submit().Motion;
        FakeWikiBotClient.ThrowOn["GetContent"] = new WikiBotException("Vorübergehend", code);

        await _sync.SyncDueAsync(_now, CancellationToken.None);

        await Assert.That(Reload(motion.Id).WikiNextAttemptAt).IsEqualTo(_now.AddMinutes(1));
    }

    [Test]
    public async Task Network_exceptions_are_recorded_with_normal_backoff() {
        var motion = Submit().Motion;
        FakeWikiBotClient.ThrowOn["GetContent"] = new HttpRequestException("Verbindung abgelehnt");

        await _sync.SyncDueAsync(_now, CancellationToken.None);
        await Assert.That(Reload(motion.Id).WikiLastError).Contains("Verbindung abgelehnt");
        await Assert.That(Reload(motion.Id).WikiNextAttemptAt).IsEqualTo(_now.AddMinutes(1));

        FakeWikiBotClient.ThrowOn["GetContent"] = new TaskCanceledException("Timeout");
        await _sync.SyncDueAsync(_now.AddMinutes(1), CancellationToken.None);
        await Assert.That(Reload(motion.Id).WikiLastError).Contains("Timeout");
        await Assert.That(Reload(motion.Id).WikiNextAttemptAt).IsEqualTo(_now.AddMinutes(3));

        FakeWikiBotClient.ThrowOn["GetContent"] = new TimeoutException("Zeitüberschreitung");
        await _sync.SyncDueAsync(_now.AddMinutes(3), CancellationToken.None);
        await Assert.That(Reload(motion.Id).WikiLastError).Contains("Zeitüberschreitung");
        await Assert.That(Reload(motion.Id).WikiNextAttemptAt).IsEqualTo(_now.AddMinutes(7));

        FakeWikiBotClient.ThrowOn["GetContent"] = new OperationCanceledException("abgebrochen");
        await _sync.SyncDueAsync(_now.AddMinutes(7), CancellationToken.None);
        await Assert.That(Reload(motion.Id).WikiLastError).Contains("abgebrochen");
        await Assert.That(Reload(motion.Id).WikiNextAttemptAt).IsEqualTo(_now.AddMinutes(15));
    }

    [Test]
    public async Task Unexpected_exception_is_recorded_with_backoff_without_details() {
        var motion = Submit().Motion;
        FakeWikiBotClient.ThrowOn["GetContent"] = new InvalidOperationException("geheimes Detail");

        await Assert.That(await _sync.SyncOneAsync(motion.Id, _now, CancellationToken.None)).IsFalse();

        var failed = Reload(motion.Id);
        await Assert.That(failed.WikiSyncState).IsEqualTo(WikiSyncState.Failed);
        await Assert.That(failed.WikiLastError).Contains(AssemblyMotionWikiSync.InternalErrorCode);
        await Assert.That(failed.WikiLastError).DoesNotContain("geheimes Detail");
        await Assert.That(failed.WikiNextAttemptAt).IsEqualTo(_now.AddMinutes(1));
    }

    [Test]
    public async Task Missing_page_prefix_uses_max_backoff() {
        var motion = Submit().Motion;
        Db.AssemblyMotionPortals.Where(p => p.Id == _portal.Id).Set(p => p.WikiPagePrefix, (string?)null).Update();

        await _sync.SyncDueAsync(_now, CancellationToken.None);

        var failed = Reload(motion.Id);
        await Assert.That(failed.WikiSyncState).IsEqualTo(WikiSyncState.Failed);
        await Assert.That(failed.WikiNextAttemptAt).IsEqualTo(_now.AddHours(6));
    }

    [Test]
    public async Task Foreign_page_is_not_edited_or_protected() {
        var motion = Submit().Motion;
        var title = TitleOf(motion);
        FakeWikiBotClient.Pages[title] = "Handgeschriebener Antrag des letzten Jahres";

        await _sync.SyncDueAsync(_now, CancellationToken.None);

        var failed = Reload(motion.Id);
        await Assert.That(failed.WikiSyncState).IsEqualTo(WikiSyncState.Failed);
        await Assert.That(failed.WikiLastError).IsEqualTo(AssemblyMotionWikiSync.ForeignPageMessage);
        await Assert.That(failed.WikiNextAttemptAt).IsEqualTo(_now.AddHours(6));
        await Assert.That(FakeWikiBotClient.Edits).IsEmpty();
        await Assert.That(FakeWikiBotClient.Protected).IsEmpty();
        await Assert.That(FakeWikiBotClient.Pages[title]).IsEqualTo("Handgeschriebener Antrag des letzten Jahres");

        await _sync.SyncDueAsync(_now.AddHours(7), CancellationToken.None);

        var entries = AuditLog.GetForEntity("AssemblyMotion", motion.Id);
        await Assert.That(entries.Count(a => a.Action == "WikiForeignPageRefused")).IsEqualTo(1);
        await Assert.That(entries.Single(a => a.Action == "WikiForeignPageRefused").NewValue).IsEqualTo(title);
        await Assert.That(entries.Any(a => a.Action == "WikiSyncFailed")).IsFalse();
        await Assert.That(Reload(motion.Id).WikiFailureCount).IsEqualTo(2);
    }

    [Test]
    public async Task Page_containing_own_portal_url_is_taken_over_and_protected() {
        var motion = Submit().Motion;
        var title = TitleOf(motion);
        FakeWikiBotClient.Pages[title] = $"Alter Stand unseres Eintrags {BaseUrl}/AssemblyMotions/{motion.Id}";

        await _sync.SyncDueAsync(_now, CancellationToken.None);

        await Assert.That(FakeWikiBotClient.Edits.Count).IsEqualTo(1);
        await Assert.That(FakeWikiBotClient.Pages[title]).StartsWith("{{");
        await Assert.That(FakeWikiBotClient.Protected).IsEquivalentTo([title]);
        await Assert.That(Reload(motion.Id).WikiSyncState).IsEqualTo(WikiSyncState.Synced);
    }

    [Test]
    public async Task Existing_page_with_equal_hash_and_no_bookkeeping_is_adopted_and_protected() {
        var motion = Submit().Motion;
        var title = TitleOf(motion);
        await _sync.SyncDueAsync(_now, CancellationToken.None);
        LoseBookkeeping(motion.Id);
        FakeWikiBotClient.Protected.Clear();

        await _sync.SyncDueAsync(_now.AddMinutes(1), CancellationToken.None);

        await Assert.That(FakeWikiBotClient.Edits.Count).IsEqualTo(1);
        await Assert.That(FakeWikiBotClient.Protected).IsEquivalentTo([title]);
        var reloaded = Reload(motion.Id);
        await Assert.That(reloaded.WikiSyncState).IsEqualTo(WikiSyncState.Synced);
        await Assert.That(reloaded.WikiSyncedHash).IsEqualTo(WikiPageText.Hash(FakeWikiBotClient.Pages[title]));
    }

    [Test]
    public async Task Protection_is_not_skipped_after_lost_bookkeeping() {
        var motion = Submit().Motion;
        var title = TitleOf(motion);
        FakeWikiBotClient.FailProtect = true;
        await _sync.SyncDueAsync(_now, CancellationToken.None);
        FakeWikiBotClient.FailProtect = false;
        LoseBookkeeping(motion.Id);

        await _sync.SyncDueAsync(_now.AddMinutes(1), CancellationToken.None);

        await Assert.That(FakeWikiBotClient.Edits.Count).IsEqualTo(1);
        await Assert.That(FakeWikiBotClient.Protected).IsEquivalentTo([title]);
    }

    [Test]
    public async Task Never_mirrored_motion_without_request_timestamp_syncs() {
        var motion = Submit().Motion;
        Db.AssemblyMotions.Where(m => m.Id == motion.Id)
            .Set(m => m.WikiSyncState, WikiSyncState.NotMirrored)
            .Set(m => m.WikiSyncRequestedAt, (DateTime?)null)
            .Update();

        await Assert.That(_motionRepo.ListWikiDue(_now, 20).Select(m => m.Id)).IsEquivalentTo([motion.Id]);
        await Assert.That(await _sync.SyncDueAsync(_now, CancellationToken.None)).IsEqualTo(1);

        await Assert.That(Reload(motion.Id).WikiSyncState).IsEqualTo(WikiSyncState.Synced);
    }

    [Test]
    public async Task Never_mirrored_motion_without_request_timestamp_records_failure() {
        var motion = Submit().Motion;
        Db.AssemblyMotions.Where(m => m.Id == motion.Id)
            .Set(m => m.WikiSyncState, WikiSyncState.NotMirrored)
            .Set(m => m.WikiSyncRequestedAt, (DateTime?)null)
            .Update();
        FakeWikiBotClient.ThrowOn["GetContent"] = new WikiBotException("Fehler", "http_error");

        await _sync.SyncDueAsync(_now, CancellationToken.None);

        await Assert.That(Reload(motion.Id).WikiSyncState).IsEqualTo(WikiSyncState.Failed);
    }

    [Test]
    public async Task Not_mirrored_motion_is_due_only_in_mirrored_portal() {
        var mirrored = Submit().Motion;
        var plainPortal = _portalRepo.Create(_builder.SeedEvent(_chapter.Id).Id, DateTime.UtcNow.AddHours(-1), DateTime.UtcNow.AddDays(7), DateTime.UtcNow);
        var plain = Submit(portal: plainPortal).Motion;
        foreach (var id in new[] { mirrored.Id, plain.Id }) {
            Db.AssemblyMotions.Where(m => m.Id == id)
                .Set(m => m.WikiSyncState, WikiSyncState.NotMirrored)
                .Set(m => m.WikiSyncRequestedAt, (DateTime?)null)
                .Update();
        }

        var due = _motionRepo.ListWikiDue(_now, 20);

        await Assert.That(due.Select(m => m.Id)).IsEquivalentTo([mirrored.Id]);
    }

    private void LoseBookkeeping(Guid motionId)
        => Db.AssemblyMotions.Where(m => m.Id == motionId)
            .Set(m => m.WikiSyncState, WikiSyncState.Pending)
            .Set(m => m.WikiSyncedHash, (string?)null)
            .Set(m => m.WikiSyncedRevision, (int?)null)
            .Set(m => m.WikiLastSyncedAt, (DateTime?)null)
            .Update();

    [Test]
    [Arguments("login_failed")]
    [Arguments("permissiondenied")]
    [Arguments("protectedpage")]
    [Arguments("insecure_url")]
    public async Task Permanent_error_uses_max_backoff(string code) {
        var motion = Submit().Motion;
        FakeWikiBotClient.ThrowOn["GetContent"] = new WikiBotException("Dauerhaft", code);

        await _sync.SyncDueAsync(_now, CancellationToken.None);

        var failed = Reload(motion.Id);
        await Assert.That(failed.WikiSyncState).IsEqualTo(WikiSyncState.Failed);
        await Assert.That(failed.WikiNextAttemptAt).IsEqualTo(_now.AddHours(6));
    }

    [Test]
    public async Task Broken_template_fails_with_readable_error() {
        var motion = Submit().Motion;
        SetTemplate("SO", "{% if motion.Title %}unclosed");

        await _sync.SyncDueAsync(_now, CancellationToken.None);

        var failed = Reload(motion.Id);
        await Assert.That(failed.WikiSyncState).IsEqualTo(WikiSyncState.Failed);
        await Assert.That(failed.WikiLastError).Contains("Template parse error");
        await Assert.That(failed.WikiNextAttemptAt).IsEqualTo(_now.AddHours(6));
        await Assert.That(FakeWikiBotClient.Pages).IsEmpty();
        await Assert.That(FakeWikiBotClient.GetCount).IsEqualTo(0);
    }

    [Test]
    public async Task Missing_base_url_fails_sync() {
        var motion = Submit().Motion;
        _options.SetValue(NotificationTemplateGlobals.BaseUrlOptionKey, null, "");

        await _sync.SyncDueAsync(_now, CancellationToken.None);

        var failed = Reload(motion.Id);
        await Assert.That(failed.WikiSyncState).IsEqualTo(WikiSyncState.Failed);
        await Assert.That(failed.WikiLastError).Contains("Basis-URL");
        await Assert.That(failed.WikiNextAttemptAt).IsEqualTo(_now.AddHours(6));
        await Assert.That(FakeWikiBotClient.Pages).IsEmpty();
    }

    [Test]
    public async Task Last_error_is_truncated_to_column_length() {
        var motion = Submit().Motion;
        FakeWikiBotClient.ThrowOn["GetContent"] = new WikiBotException(new string('x', 10_000), "http_error");

        await _sync.SyncDueAsync(_now, CancellationToken.None);

        await Assert.That(Reload(motion.Id).WikiLastError!.Length).IsEqualTo(AssemblyMotionRepository.WikiLastErrorMaxLength);
    }

    [Test]
    public async Task Failure_for_outdated_request_keeps_pending() {
        var motion = Submit().Motion;
        FakeWikiBotClient.ThrowOn["Edit"] = new WikiBotException("Bearbeiten fehlgeschlagen", "http_error");
        FakeWikiBotClient.OnGetContent = _ => {
            _motionRepo.MarkWikiPending(motion.Id, _now.AddSeconds(5));
            return Task.CompletedTask;
        };

        await _sync.SyncDueAsync(_now, CancellationToken.None);

        var reloaded = Reload(motion.Id);
        await Assert.That(reloaded.WikiSyncState).IsEqualTo(WikiSyncState.Pending);
        await Assert.That(reloaded.WikiLastError).Contains("Bearbeiten fehlgeschlagen");
        await Assert.That(reloaded.WikiFailureCount).IsEqualTo(0);
        await Assert.That(reloaded.WikiNextAttemptAt).IsNull();
        await Assert.That(reloaded.WikiSyncRequestedAt).IsEqualTo(_now.AddSeconds(5));
    }

    [Test]
    public async Task Change_during_sync_stays_pending() {
        var motion = Submit().Motion;
        FakeWikiBotClient.OnEdit = _ => {
            _motionRepo.MarkWikiPending(motion.Id, _now.AddSeconds(5));
            return Task.CompletedTask;
        };

        await _sync.SyncDueAsync(_now, CancellationToken.None);

        var reloaded = Reload(motion.Id);
        await Assert.That(reloaded.WikiSyncState).IsEqualTo(WikiSyncState.Pending);
        await Assert.That(reloaded.WikiSyncRequestedAt).IsEqualTo(_now.AddSeconds(5));
        await Assert.That(reloaded.WikiSyncedHash).IsNotNull();

        FakeWikiBotClient.OnEdit = null;
        await _sync.SyncDueAsync(_now.AddMinutes(1), CancellationToken.None);

        await Assert.That(Reload(motion.Id).WikiSyncState).IsEqualTo(WikiSyncState.Synced);
        await Assert.That(FakeWikiBotClient.Edits.Count).IsEqualTo(1);
        await Assert.That(AuditLog.GetForEntity("AssemblyMotion", motion.Id).Any(a => a.Action == "WikiContentDiffered")).IsFalse();
    }

    [Test]
    public async Task Overlapping_syncs_of_one_motion_are_consistent() {
        var motion = Submit().Motion;
        var title = TitleOf(motion);
        using var otherDb = Worker.CreateDbContext();
        var otherSync = NewSync(otherDb);
        var innerResult = false;
        FakeWikiBotClient.OnEdit = async _ => {
            FakeWikiBotClient.OnEdit = null;
            innerResult = await otherSync.SyncOneAsync(motion.Id, _now, CancellationToken.None);
        };

        var outerResult = await _sync.SyncOneAsync(motion.Id, _now, CancellationToken.None);

        await Assert.That(outerResult).IsTrue();
        await Assert.That(innerResult).IsTrue();
        var reloaded = Reload(motion.Id);
        await Assert.That(reloaded.WikiSyncState).IsEqualTo(WikiSyncState.Synced);
        await Assert.That(reloaded.WikiSyncedHash).IsEqualTo(WikiPageText.Hash(FakeWikiBotClient.Pages[title]));
        await Assert.That(FakeWikiBotClient.Protected.Distinct()).IsEquivalentTo([title]);
        await Assert.That(reloaded.WikiLastError).IsNull();
    }

    [Test]
    public async Task Shutdown_cancellation_records_nothing() {
        var motion = Submit().Motion;
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        FakeWikiBotClient.ThrowOn["GetContent"] = new OperationCanceledException(cts.Token);

        await Assert.That(async () => await _sync.SyncOneAsync(motion.Id, _now, cts.Token)).Throws<OperationCanceledException>();

        var reloaded = Reload(motion.Id);
        await Assert.That(reloaded.WikiSyncState).IsEqualTo(WikiSyncState.Pending);
        await Assert.That(reloaded.WikiLastError).IsNull();
        await Assert.That(reloaded.WikiFailureCount).IsEqualTo(0);
    }

    [Test]
    public async Task Disabled_mirror_is_ignored() {
        var disabledPortal = _portalRepo.Create(_builder.SeedEvent(_chapter.Id).Id, DateTime.UtcNow.AddHours(-1), DateTime.UtcNow.AddDays(7), DateTime.UtcNow);
        var motion = Submit(portal: disabledPortal).Motion;

        await Assert.That(await _sync.SyncDueAsync(_now, CancellationToken.None)).IsEqualTo(0);
        await Assert.That(await _sync.SyncOneAsync(motion.Id, _now, CancellationToken.None)).IsFalse();

        await Assert.That(FakeWikiBotClient.Pages).IsEmpty();
        await Assert.That(FakeWikiBotClient.GetCount).IsEqualTo(0);
        await Assert.That(Reload(motion.Id).WikiSyncState).IsEqualTo(WikiSyncState.NotMirrored);
    }

    [Test]
    public async Task Never_submitted_collecting_motion_gets_no_page() {
        var motion = Submit("GP").Motion;
        Db.AssemblyMotions.Where(m => m.Id == motion.Id).Set(m => m.Number, (int?)null).Set(m => m.Status, AssemblyMotionStatus.CollectingCoSubmitters).Update();
        _motionRepo.MarkWikiPending(motion.Id, _now);

        await Assert.That(await _sync.SyncDueAsync(_now, CancellationToken.None)).IsEqualTo(0);
        await Assert.That(await _sync.SyncOneAsync(motion.Id, _now, CancellationToken.None)).IsFalse();

        await Assert.That(FakeWikiBotClient.Pages).IsEmpty();
        await Assert.That(FakeWikiBotClient.GetCount).IsEqualTo(0);
    }

    [Test]
    public async Task Unconfigured_settings_leave_pending() {
        var motion = Submit().Motion;
        _options.SetValue("wiki.bot_password", null, "");

        await Assert.That(await _sync.SyncDueAsync(_now, CancellationToken.None)).IsEqualTo(0);
        await Assert.That(await _sync.SyncOneAsync(motion.Id, _now, CancellationToken.None)).IsFalse();

        await Assert.That(FakeWikiBotClient.GetCount).IsEqualTo(0);
        await Assert.That(FakeWikiBotClient.LoginCount).IsEqualTo(0);
        var reloaded = Reload(motion.Id);
        await Assert.That(reloaded.WikiSyncState).IsEqualTo(WikiSyncState.Pending);
        await Assert.That(reloaded.WikiLastError).IsNull();
    }

    [Test]
    public async Task Unconfigured_warning_is_logged_at_most_once_per_hour() {
        var logger = new CapturingLogger<AssemblyMotionWikiSync>();
        var sync = NewSync(Db, logger);
        _options.SetValue("wiki.bot_password", null, "");
        var start = _now;

        await sync.SyncDueAsync(start, CancellationToken.None);
        await sync.SyncDueAsync(start.AddMinutes(1), CancellationToken.None);
        await sync.SyncDueAsync(start.AddMinutes(59), CancellationToken.None);

        await Assert.That(logger.Entries.Count(e => e.StartsWith("Warning"))).IsEqualTo(1);

        await sync.SyncDueAsync(start.AddMinutes(61), CancellationToken.None);

        await Assert.That(logger.Entries.Count(e => e.StartsWith("Warning"))).IsEqualTo(2);
    }

    [Test]
    public async Task Edited_motion_updates_wiki_to_in_revision() {
        var (motion, author) = Submit("SÄA");
        var title = TitleOf(motion);
        await _sync.SyncDueAsync(_now, CancellationToken.None);
        await Assert.That(FakeWikiBotClient.Pages[title]).DoesNotContain("wird überarbeitet");

        var type = TypeOf("SÄA");
        var outcome = _service.UpdateByAuthor(Reload(motion.Id), author, Guid.NewGuid(), NewDraft(type.Id, "Neuer Titel"), baseRevision: 1, DateTime.UtcNow);
        await Assert.That(outcome.Succeeded).IsTrue();
        await Assert.That(Reload(motion.Id).WikiSyncState).IsEqualTo(WikiSyncState.Pending);

        await _sync.SyncDueAsync(_now.AddMinutes(1), CancellationToken.None);

        await Assert.That(FakeWikiBotClient.Pages[title]).StartsWith("'''Dieser Antrag wird überarbeitet");
        await Assert.That(FakeWikiBotClient.Pages[title]).Contains("|titel=Neuer Titel\n");
        await Assert.That(FakeWikiBotClient.Edits.Last().Summary).IsEqualTo("Antragsportal: Revision 2 eingereicht");
        var reloaded = Reload(motion.Id);
        await Assert.That(reloaded.WikiSyncState).IsEqualTo(WikiSyncState.Synced);
        await Assert.That(reloaded.WikiSyncedRevision).IsEqualTo(2);
    }

    [Test]
    public async Task Withdrawn_motion_page_shows_icon_6() {
        var (motion, author) = Submit();
        var title = TitleOf(motion);
        await _sync.SyncDueAsync(_now, CancellationToken.None);

        _service.Withdraw(motion.Id, author, DateTime.UtcNow);
        await _sync.SyncDueAsync(_now.AddMinutes(1), CancellationToken.None);

        await Assert.That(FakeWikiBotClient.Pages[title]).Contains("|prüficon=6\n");
        await Assert.That(FakeWikiBotClient.Pages[title]).Contains("|abstimmung=4\n");
    }

    [Test]
    public async Task Numbered_withdrawn_motion_keeps_page_with_icon_6() {
        var (motion, author) = Submit();
        var title = TitleOf(motion);
        await _sync.SyncDueAsync(_now, CancellationToken.None);

        _service.Withdraw(motion.Id, author, DateTime.UtcNow);
        await Assert.That(await _sync.SyncDueAsync(_now.AddMinutes(1), CancellationToken.None)).IsEqualTo(1);

        await Assert.That(FakeWikiBotClient.Pages.ContainsKey(title)).IsTrue();
        await Assert.That(FakeWikiBotClient.Pages[title]).Contains("|prüficon=6\n");
        await Assert.That(Reload(motion.Id).WikiSyncState).IsEqualTo(WikiSyncState.Synced);
    }

    [Test]
    public async Task Open_inquiry_sets_icon_7() {
        var motion = Submit().Motion;
        _inquiryRepo.Insert(new AssemblyMotionInquiry {
            MotionId = motion.Id,
            QuestionMarkdown = "Frage?",
            QuestionHtml = "<p>Frage?</p>",
            AskedByUserId = Guid.NewGuid(),
            AskedAt = _now,
            ResponseDueAt = _now.AddDays(2)
        });

        await _sync.SyncDueAsync(_now, CancellationToken.None);

        await Assert.That(FakeWikiBotClient.Pages[TitleOf(motion)]).Contains("|prüficon=7\n");
    }

    [Test]
    public async Task Competing_numbers_only_for_numbered_motions() {
        var first = Submit().Motion;
        var second = Submit().Motion;
        var unnumbered = Submit("GP").Motion;
        Db.AssemblyMotions.Where(m => m.Id == unnumbered.Id).Set(m => m.Number, (int?)null).Update();
        foreach (var other in new[] { second, unnumbered }) {
            var pair = new[] { first.Id, other.Id }.Order().ToArray();
            Db.Insert(new AssemblyMotionCompetition {
                MotionAId = pair[0],
                MotionBId = pair[1],
                DeclaringMotionId = first.Id,
                DeclaredByUserId = Guid.NewGuid(),
                DeclaredAt = _now
            });
        }

        await _sync.SyncOneAsync(first.Id, _now, CancellationToken.None);

        var page = FakeWikiBotClient.Pages[TitleOf(first)];
        await Assert.That(page).Contains("|konkurrenz=SO002\n");
    }

    [Test]
    public async Task Reconcile_queues_missing_and_mismatched_pages() {
        var intact = Submit().Motion;
        var missing = Submit().Motion;
        var edited = Submit().Motion;
        await _sync.SyncDueAsync(_now, CancellationToken.None);
        FakeWikiBotClient.Pages.Remove(TitleOf(missing));
        FakeWikiBotClient.Pages[TitleOf(edited)] = "Geändert";

        var queued = await _reconcile.RunAsync(_now.AddDays(1), CancellationToken.None);

        await Assert.That(queued).IsEqualTo(2);
        await Assert.That(Reload(intact.Id).WikiSyncState).IsEqualTo(WikiSyncState.Synced);
        await Assert.That(Reload(missing.Id).WikiSyncState).IsEqualTo(WikiSyncState.Pending);
        await Assert.That(Reload(edited.Id).WikiSyncState).IsEqualTo(WikiSyncState.Pending);
    }

    [Test]
    public async Task Reconcile_skips_archived_events() {
        var motion = Submit().Motion;
        await _sync.SyncDueAsync(_now, CancellationToken.None);
        FakeWikiBotClient.Pages.Clear();
        Db.Events.Where(e => e.Id == _event.Id).Set(e => e.Status, EventStatus.Archived).Update();
        var readsBefore = FakeWikiBotClient.GetCount;

        var queued = await _reconcile.RunAsync(_now.AddDays(1), CancellationToken.None);

        await Assert.That(queued).IsEqualTo(0);
        await Assert.That(FakeWikiBotClient.GetCount).IsEqualTo(readsBefore);
        await Assert.That(Reload(motion.Id).WikiSyncState).IsEqualTo(WikiSyncState.Synced);
    }

    [Test]
    public async Task Reconcile_does_nothing_when_unconfigured() {
        var motion = Submit().Motion;
        await _sync.SyncDueAsync(_now, CancellationToken.None);
        FakeWikiBotClient.Pages.Clear();
        _options.SetValue("wiki.bot_password", null, "");
        var readsBefore = FakeWikiBotClient.GetCount;

        var queued = await _reconcile.RunAsync(_now.AddDays(1), CancellationToken.None);

        await Assert.That(queued).IsEqualTo(0);
        await Assert.That(FakeWikiBotClient.GetCount).IsEqualTo(readsBefore);
        await Assert.That(Reload(motion.Id).WikiSyncState).IsEqualTo(WikiSyncState.Synced);
    }

    [Test]
    public async Task Reconcile_rotates_window_so_all_motions_are_checked_over_three_days() {
        var motions = new[] { Submit().Motion, Submit().Motion, Submit().Motion };
        await _sync.SyncDueAsync(_now, CancellationToken.None);
        var checkedTitles = new List<string>();
        FakeWikiBotClient.OnGetContent = title => {
            checkedTitles.Add(title);
            return Task.CompletedTask;
        };

        for (var day = 0; day < 3; day++) {
            var before = checkedTitles.Count;
            await _reconcile.RunAsync(_now.AddDays(day + 1), CancellationToken.None, batchSize: 2);
            await Assert.That(checkedTitles.Count - before).IsEqualTo(2);
        }

        await Assert.That(checkedTitles.Distinct().Count()).IsEqualTo(3);
        await Assert.That(motions.Select(TitleOf).All(checkedTitles.Contains)).IsTrue();
    }

    [Test]
    public async Task Reconcile_reads_at_most_batch_size_pages_and_default_is_500() {
        Submit();
        Submit();
        Submit();
        await _sync.SyncDueAsync(_now, CancellationToken.None);
        var readsBefore = FakeWikiBotClient.GetCount;

        await _reconcile.RunAsync(_now.AddDays(1), CancellationToken.None, batchSize: 2);

        await Assert.That(FakeWikiBotClient.GetCount - readsBefore).IsEqualTo(2);
        await Assert.That(AssemblyMotionWikiReconcile.MaxPagesPerRun).IsEqualTo(500);
    }

    [Test]
    public async Task Reconcile_stops_after_three_consecutive_errors() {
        for (var i = 0; i < 5; i++)
            Submit();
        await _sync.SyncDueAsync(_now, CancellationToken.None);
        FakeWikiBotClient.ThrowOn["GetContent"] = new WikiBotException("Wiki nicht erreichbar", "http_error");
        var readsBefore = FakeWikiBotClient.GetCount;

        var queued = await _reconcile.RunAsync(_now.AddDays(1), CancellationToken.None);

        await Assert.That(queued).IsEqualTo(0);
        await Assert.That(FakeWikiBotClient.GetCount - readsBefore).IsEqualTo(3);
    }

    [Test]
    public async Task Reconcile_logs_and_counts_motion_with_missing_prefix_as_skipped() {
        var motion = Submit().Motion;
        await _sync.SyncDueAsync(_now, CancellationToken.None);
        Db.AssemblyMotionPortals.Where(p => p.Id == _portal.Id).Set(p => p.WikiPagePrefix, (string?)null).Update();
        var logger = new CapturingLogger<AssemblyMotionWikiReconcile>();
        var reconcile = NewReconcile(Db, logger);

        var queued = await reconcile.RunAsync(_now.AddDays(1), CancellationToken.None);

        await Assert.That(queued).IsEqualTo(0);
        await Assert.That(logger.Entries.Any(e => e.Contains(motion.Id.ToString()) && e.StartsWith("Warning"))).IsTrue();
        await Assert.That(logger.Entries.Any(e => e.Contains("skipped 1"))).IsTrue();
    }
}
