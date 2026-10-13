using LinqToDB;
using Microsoft.Extensions.Logging.Abstractions;
using Quartermaster.Api.AssemblyMotions;
using Quartermaster.Api.I18n;
using Quartermaster.Data.AssemblyMotions;
using Quartermaster.Data.Chapters;
using Quartermaster.Data.Events;
using Quartermaster.Data.Members;
using Quartermaster.Data.Options;
using Quartermaster.Server.AssemblyMotions;
using Quartermaster.Server.AssemblyMotions.Notifications;
using Quartermaster.Server.AssemblyMotions.OpenSlides;
using Quartermaster.Server.Notifications;
using Quartermaster.Server.Tests.AssemblyMotions.Fakes;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.AssemblyMotions;

[NotInParallel("FakeOpenSlidesClient")]
public class AssemblyMotionOpenSlidesPushTests : RepositoryTestBase {
    private const string Password = "os-secret-pw";

    private readonly DateTime _now = new(2027, 3, 1, 12, 0, 0, DateTimeKind.Utc);
    private TestDataBuilder _builder = default!;
    private AssemblyMotionPortalRepository _portalRepo = default!;
    private AssemblyMotionRepository _motionRepo = default!;
    private AssemblyMotionService _service = default!;
    private OptionRepository _options = default!;
    private Chapter _chapter = default!;
    private AssemblyMotionPortal _portal = default!;
    private CapturingLogger<AssemblyMotionOpenSlidesPush> _logger = default!;
    private AssemblyMotionOpenSlidesPush _push = default!;

    [Before(Test)]
    public void Setup() {
        FakeOpenSlidesClient.Reset();
        _builder = new TestDataBuilder(Db);
        _portalRepo = new AssemblyMotionPortalRepository(Db, AuditLog);
        _motionRepo = new AssemblyMotionRepository(Db, AuditLog);
        _service = new AssemblyMotionService(_motionRepo, _portalRepo, AuditLog, Db, new AssemblyMotionNotifier(new CapturingNotificationQueue(), Db, NullLogger<AssemblyMotionNotifier>.Instance));
        _options = new OptionRepository(Db, AuditLog);
        _options.SupplementDefaults();
        _options.SetValue("openslides.url", null, "https://os.test");
        _options.SetValue("openslides.username", null, "pusher");
        _options.SetValue("openslides.password", null, Password);
        _chapter = _builder.SeedChapter();
        var ev = _builder.SeedEvent(_chapter.Id);
        var portal = _portalRepo.Create(ev.Id, DateTime.UtcNow.AddHours(-1), DateTime.UtcNow.AddDays(7), DateTime.UtcNow);
        Db.AssemblyMotionPortals
            .Where(p => p.Id == portal.Id)
            .Set(p => p.OpenSlidesMeetingId, 7)
            .Update();
        Db.AssemblyMotionTypes
            .Where(t => t.PortalId == portal.Id)
            .Set(t => t.MinSubmitters, 1)
            .Update();
        _portal = _portalRepo.Get(portal.Id)!;
        _logger = new CapturingLogger<AssemblyMotionOpenSlidesPush>();
        _push = NewPush();
    }

    private AssemblyMotionOpenSlidesPush NewPush()
        => new(Db, _motionRepo, _portalRepo, new AssemblyMotionUpvoteRepository(Db), new FakeOpenSlidesClient(), _options, AuditLog, _logger);

    private async Task<OpenSlidesPushResultDTO?> PushAsync(bool includeSubmitted = false, DateTime? now = null)
        => (await _push.PushAsync(_portalRepo.Get(_portal.Id)!, includeSubmitted, now ?? _now, CancellationToken.None)).Result;

    private AssemblyMotionType TypeOf(string code)
        => _portalRepo.GetTypes(_portal.Id).First(t => t.Code == code);

    private AssemblyMotion Submit(string code = "SÄA", string title = "Testantrag", Member? author = null, bool admit = true) {
        var type = TypeOf(code);
        author ??= _builder.SeedMember(chapterId: _chapter.Id);
        var draft = new AssemblyMotionDraft(type.Id, title, null, $"Text {title}", $"Grund {title}", null, null, null);
        var created = _service.CreateDraft(_portal, author, Guid.NewGuid(), draft, DateTime.UtcNow).Motion!;
        var released = _service.Release(created.Id, author, DateTime.UtcNow).Motion!;
        if (admit)
            SetStatus(released.Id, AssemblyMotionStatus.Admitted);
        return Reload(released.Id);
    }

    private void SetStatus(Guid id, AssemblyMotionStatus status)
        => Db.AssemblyMotions
            .Where(m => m.Id == id)
            .Set(m => m.Status, status)
            .Update();

    private AssemblyMotion Reload(Guid id) => _motionRepo.Get(id)!;

    private string NumberOf(AssemblyMotion motion)
        => AssemblyMotionNumber.Format(_portalRepo.GetType(motion.TypeId)!.Code, motion.Number!.Value);

    [Test]
    public async Task Creates_category_once_per_type() {
        Submit("SÄA", "A");
        Submit("SÄA", "B");
        Submit("SO", "C");

        var first = await PushAsync();
        Submit("SÄA", "D");
        var second = await PushAsync();

        await Assert.That(first!.Created).IsEqualTo(3);
        await Assert.That(second!.Created).IsEqualTo(1);
        await Assert.That(FakeOpenSlidesClient.Categories.Count).IsEqualTo(2);
        var sae = FakeOpenSlidesClient.Categories.Single(c => c.Prefix == "SÄA");
        await Assert.That(sae.Name).IsEqualTo("Satzungsänderungsantrag");
        await Assert.That(sae.MeetingId).IsEqualTo(7);
        await Assert.That(TypeOf("SÄA").OpenSlidesCategoryId).IsEqualTo(sae.Id);
        await Assert.That(FakeOpenSlidesClient.Motions.Count(m => m.CategoryId == sae.Id)).IsEqualTo(3);
    }

    [Test]
    public async Task Second_push_skips_unchanged() {
        var motion = Submit();
        await PushAsync();

        var second = await PushAsync();

        await Assert.That(second!.Created).IsEqualTo(0);
        await Assert.That(second.Updated).IsEqualTo(0);
        await Assert.That(second.Skipped).IsEqualTo(1);
        await Assert.That(FakeOpenSlidesClient.Motions.Count).IsEqualTo(1);
        var stored = Reload(motion.Id);
        await Assert.That(stored.OpenSlidesMotionId).IsEqualTo(FakeOpenSlidesClient.Motions[0].Id);
        await Assert.That(stored.OpenSlidesPushedRevision).IsEqualTo(stored.Revision);
        await Assert.That(stored.OpenSlidesPushedAt).IsEqualTo(_now);
        await Assert.That(stored.OpenSlidesLastError).IsNull();
    }

    [Test]
    public async Task Pushed_content_comes_from_the_motion() {
        var motion = Submit("SÄA", "Titel");

        await PushAsync();

        var sent = FakeOpenSlidesClient.Motions.Single();
        await Assert.That(sent.Title).IsEqualTo("Titel");
        await Assert.That(sent.Number).IsEqualTo(NumberOf(motion));
        await Assert.That(sent.TextHtml).IsEqualTo(motion.TextHtml);
        await Assert.That(sent.ReasonHtml).IsEqualTo(motion.ReasonHtml);
    }

    [Test]
    public async Task Commission_edit_triggers_update_on_next_push() {
        var motion = Submit();
        await PushAsync();
        Db.AssemblyMotions
            .Where(m => m.Id == motion.Id)
            .Set(m => m.Revision, motion.Revision + 1)
            .Set(m => m.TextHtml, "<p>Neu</p>")
            .Set(m => m.Title, "Neuer Titel")
            .Update();

        var second = await PushAsync(now: _now.AddHours(1));

        await Assert.That(second!.Updated).IsEqualTo(1);
        await Assert.That(second.Created).IsEqualTo(0);
        await Assert.That(FakeOpenSlidesClient.Motions.Single().TextHtml).IsEqualTo("<p>Neu</p>");
        await Assert.That(FakeOpenSlidesClient.Motions.Single().Title).IsEqualTo("Neuer Titel");
        await Assert.That(Reload(motion.Id).OpenSlidesPushedRevision).IsEqualTo(motion.Revision + 1);
        await Assert.That(Reload(motion.Id).OpenSlidesPushedAt).IsEqualTo(_now.AddHours(1));
    }

    [Test]
    public async Task Only_admitted_unless_submitted_included() {
        Submit("SÄA", "Zugelassen");
        Submit("SÄA", "Eingereicht", admit: false);

        var admittedOnly = await PushAsync();
        var withSubmitted = await PushAsync(includeSubmitted: true);

        await Assert.That(admittedOnly!.Created).IsEqualTo(1);
        await Assert.That(withSubmitted!.Created).IsEqualTo(1);
        await Assert.That(withSubmitted.Skipped).IsEqualTo(1);
        await Assert.That(FakeOpenSlidesClient.Motions.Select(m => m.Title)).IsEquivalentTo(["Zugelassen", "Eingereicht"]);
    }

    [Test]
    public async Task Withdrawn_never_pushed() {
        var motion = Submit();
        SetStatus(motion.Id, AssemblyMotionStatus.Withdrawn);

        var result = await PushAsync(includeSubmitted: true);

        await Assert.That(result!.Created + result.Updated + result.Skipped).IsEqualTo(0);
        await Assert.That(FakeOpenSlidesClient.Motions).IsEmpty();
        await Assert.That(FakeOpenSlidesClient.Categories).IsEmpty();
    }

    [Test]
    public async Task One_failure_does_not_stop_others() {
        var first = Submit("SÄA", "A");
        var second = Submit("SÄA", "B");
        var third = Submit("SÄA", "C");
        FakeOpenSlidesClient.FailCreateFor(NumberOf(second));

        var result = await PushAsync();

        await Assert.That(result!.Created).IsEqualTo(2);
        await Assert.That(result.Failures.Count).IsEqualTo(1);
        await Assert.That(result.Failures[0].DisplayNumber).IsEqualTo(NumberOf(second));
        await Assert.That(result.Failures[0].Error).Contains("Fake create failure");
        await Assert.That(Reload(second.Id).OpenSlidesLastError).Contains("Fake create failure");
        await Assert.That(Reload(second.Id).OpenSlidesMotionId).IsNull();
        await Assert.That(Reload(first.Id).OpenSlidesMotionId).IsNotNull();
        await Assert.That(Reload(third.Id).OpenSlidesMotionId).IsNotNull();
    }

    [Test]
    public async Task Repush_after_partial_failure_creates_no_duplicates() {
        var first = Submit("SÄA", "A");
        var second = Submit("SÄA", "B");
        var third = Submit("SÄA", "C");
        FakeOpenSlidesClient.FailCreateFor(NumberOf(second));
        await PushAsync();
        FakeOpenSlidesClient.ClearCreateFailures();

        var result = await PushAsync(now: _now.AddHours(1));

        await Assert.That(result!.Created).IsEqualTo(1);
        await Assert.That(result.Skipped).IsEqualTo(2);
        await Assert.That(result.Failures).IsEmpty();
        await Assert.That(FakeOpenSlidesClient.Motions.Count).IsEqualTo(3);
        await Assert.That(FakeOpenSlidesClient.Motions.Select(m => m.Number).Distinct().Count()).IsEqualTo(3);
        await Assert.That(Reload(second.Id).OpenSlidesLastError).IsNull();
        await Assert.That(Reload(second.Id).OpenSlidesMotionId).IsNotNull();
        await Assert.That(Reload(first.Id).OpenSlidesPushedAt).IsEqualTo(_now);
        await Assert.That(Reload(third.Id).OpenSlidesPushedAt).IsEqualTo(_now);
    }

    [Test]
    public async Task Motion_with_id_but_without_pushed_revision_is_updated_not_recreated() {
        var motion = Submit();
        await PushAsync();
        Db.AssemblyMotions
            .Where(m => m.Id == motion.Id)
            .Set(m => m.OpenSlidesPushedRevision, (int?)null)
            .Update();

        var result = await PushAsync();

        await Assert.That(result!.Updated).IsEqualTo(1);
        await Assert.That(result.Created).IsEqualTo(0);
        await Assert.That(FakeOpenSlidesClient.Motions.Count).IsEqualTo(1);
    }

    [Test]
    public async Task Category_failure_fails_only_that_type() {
        var failing = Submit("SÄA", "A");
        var failingToo = Submit("SÄA", "B");
        var other = Submit("SO", "C");
        FakeOpenSlidesClient.CategoryFailurePrefix = "SÄA";

        var result = await PushAsync();

        await Assert.That(result!.Created).IsEqualTo(1);
        await Assert.That(result.Failures.Count).IsEqualTo(2);
        await Assert.That(result.Failures.Select(f => f.DisplayNumber)).IsEquivalentTo([NumberOf(failing), NumberOf(failingToo)]);
        await Assert.That(Reload(failing.Id).OpenSlidesLastError).Contains("Fake category failure");
        await Assert.That(Reload(other.Id).OpenSlidesMotionId).IsNotNull();
        await Assert.That(TypeOf("SÄA").OpenSlidesCategoryId).IsNull();
        await Assert.That(TypeOf("SO").OpenSlidesCategoryId).IsNotNull();
    }

    [Test]
    public async Task Unexpected_exception_is_contained_logged_and_shown_generically() {
        var first = Submit("SÄA", "A");
        var second = Submit("SÄA", "B");
        FakeOpenSlidesClient.FailCreateFor(NumberOf(first), new InvalidOperationException($"internal detail {Password}"));

        var result = await PushAsync();

        await Assert.That(result!.Created).IsEqualTo(1);
        await Assert.That(result.Failures.Single().Error).IsEqualTo(OpenSlidesErrorText.Unexpected);
        await Assert.That(Reload(first.Id).OpenSlidesLastError).IsEqualTo(OpenSlidesErrorText.Unexpected);
        await Assert.That(Reload(second.Id).OpenSlidesMotionId).IsNotNull();
        await Assert.That(_logger.Entries.Any(e => e.Contains("InvalidOperationException") && e.Contains("internal detail"))).IsTrue();
    }

    [Test]
    public async Task Http_failure_is_shown_with_fixed_text() {
        var motion = Submit();
        FakeOpenSlidesClient.FailCreateFor(NumberOf(motion), new HttpRequestException("connect refused to 10.0.0.1"));

        var result = await PushAsync();

        await Assert.That(result!.Failures.Single().Error).IsEqualTo(OpenSlidesErrorText.Unreachable);
    }

    [Test]
    public async Task Cancellation_during_a_create_stores_the_id_and_a_repush_creates_nothing_twice() {
        var first = Submit("SÄA", "A");
        var second = Submit("SÄA", "B");
        using var cts = new CancellationTokenSource();
        FakeOpenSlidesClient.OnCreateMotion = _ => {
            cts.Cancel();
            return Task.CompletedTask;
        };

        var interrupted = (await _push.PushAsync(_portalRepo.Get(_portal.Id)!, false, _now, cts.Token)).Result;
        FakeOpenSlidesClient.OnCreateMotion = null;

        await Assert.That(interrupted!.Created).IsEqualTo(1);
        await Assert.That(Reload(first.Id).OpenSlidesMotionId).IsNotNull();
        await Assert.That(Reload(second.Id).OpenSlidesMotionId).IsNull();
        await Assert.That(Db.AuditLogs.Any(a => a.EntityId == _portal.Id && a.Action == "OpenSlidesPushed")).IsTrue();

        var repush = await PushAsync();

        await Assert.That(repush!.Created).IsEqualTo(1);
        await Assert.That(repush.Skipped).IsEqualTo(1);
        await Assert.That(FakeOpenSlidesClient.Motions.Count).IsEqualTo(2);
        await Assert.That(FakeOpenSlidesClient.Motions.Select(m => m.Number).Distinct().Count()).IsEqualTo(2);
    }

    [Test]
    public async Task Meeting_removed_since_the_caller_loaded_the_portal_fails_with_meeting_missing() {
        Submit();
        var stale = _portalRepo.Get(_portal.Id)!;
        Db.AssemblyMotionPortals
            .Where(p => p.Id == _portal.Id)
            .Set(p => p.OpenSlidesMeetingId, (int?)null)
            .Update();

        var outcome = await _push.PushAsync(stale, false, _now, CancellationToken.None);

        await Assert.That(outcome.Result).IsNull();
        await Assert.That(outcome.ErrorKey).IsEqualTo(I18nKey.Error.AssemblyMotion.OpenSlidesMeetingMissing);
        await Assert.That(FakeOpenSlidesClient.Motions).IsEmpty();
    }

    [Test]
    public async Task Unnumbered_admitted_motion_is_not_pushed_and_does_not_break_the_run() {
        var unnumbered = Submit("SÄA", "Ohne Nummer");
        Db.AssemblyMotions
            .Where(m => m.Id == unnumbered.Id)
            .Set(m => m.Number, (int?)null)
            .Update();
        var numbered = Submit("SÄA", "Mit Nummer");

        var result = await PushAsync(includeSubmitted: true);

        await Assert.That(result!.Created).IsEqualTo(1);
        await Assert.That(result.Failures).IsEmpty();
        await Assert.That(Reload(unnumbered.Id).OpenSlidesMotionId).IsNull();
        await Assert.That(Reload(numbered.Id).OpenSlidesMotionId).IsNotNull();
    }

    [Test]
    public async Task Response_body_excerpt_never_reaches_dto_or_column() {
        var motion = Submit();
        FakeOpenSlidesClient.FailCreateFor(NumberOf(motion), new OpenSlidesException("OpenSlides antwortete mit HTTP 500: SECRET-BODY-EXCERPT", "http_error"));

        var result = await PushAsync();

        await Assert.That(result!.Failures.Single().Error).DoesNotContain("SECRET-BODY-EXCERPT");
        await Assert.That(Reload(motion.Id).OpenSlidesLastError).DoesNotContain("SECRET-BODY-EXCERPT");
        await Assert.That(Reload(motion.Id).OpenSlidesLastError).IsNotNull();
    }

    [Test]
    public async Task Action_message_passes_through_capped() {
        var motion = Submit();
        FakeOpenSlidesClient.FailCreateFor(NumberOf(motion), new OpenSlidesException(new string('x', 900), "action_failed"));

        var result = await PushAsync();

        await Assert.That(result!.Failures.Single().Error.Length).IsEqualTo(OpenSlidesErrorText.MaxActionMessageLength);
        await Assert.That(Reload(motion.Id).OpenSlidesLastError!.Length).IsEqualTo(OpenSlidesErrorText.MaxActionMessageLength);
    }

    [Test]
    public async Task Portal_can_be_pushed_again_after_an_unexpected_exception() {
        var motion = Submit();
        FakeOpenSlidesClient.FailCreateFor(NumberOf(motion), new InvalidOperationException("boom"));
        await PushAsync();
        FakeOpenSlidesClient.ClearCreateFailures();

        var again = await PushAsync();

        await Assert.That(again).IsNotNull();
        await Assert.That(again!.Created).IsEqualTo(1);
    }

    [Test]
    public async Task Order_follows_ranking_after_window() {
        var first = Submit("SÄA", "Erster");
        var second = Submit("SÄA", "Zweiter");
        var third = Submit("SÄA", "Dritter");
        Db.AssemblyMotionPortals
            .Where(p => p.Id == _portal.Id)
            .Set(p => p.PrioritizationEnabled, true)
            .Set(p => p.PrioritizationEndsAt, _now.AddDays(-1))
            .Update();
        Upvote(third.Id, 2);
        Upvote(second.Id, 1);

        await PushAsync();

        await Assert.That(string.Join("|", FakeOpenSlidesClient.Motions.Select(m => m.Title))).IsEqualTo("Dritter|Zweiter|Erster");
    }

    [Test]
    public async Task Order_follows_type_and_number_before_ranking_is_public() {
        Submit("SO", "Sonstiger");
        Submit("SÄA", "Satzung 1");
        Submit("SÄA", "Satzung 2");
        Db.AssemblyMotionPortals
            .Where(p => p.Id == _portal.Id)
            .Set(p => p.PrioritizationEnabled, true)
            .Set(p => p.PrioritizationEndsAt, _now.AddDays(1))
            .Update();

        await PushAsync();

        await Assert.That(string.Join("|", FakeOpenSlidesClient.Motions.Select(m => m.Title))).IsEqualTo("Satzung 1|Satzung 2|Sonstiger");
    }

    [Test]
    public async Task Submitters_sent_as_names_author_first() {
        var author = _builder.SeedMember(chapterId: _chapter.Id, firstName: "Anna", lastName: "Autor");
        var motion = Submit("SÄA", "Gemeinsam", author);
        var coSubmitter = _builder.SeedMember(chapterId: _chapter.Id, firstName: "Carl", lastName: "Co");
        var former = _builder.SeedMember(chapterId: _chapter.Id, firstName: "Fritz", lastName: "Fort");
        AddSubmitter(motion.Id, coSubmitter.Id, DateTime.UtcNow.AddMinutes(1));
        AddSubmitter(motion.Id, former.Id, DateTime.UtcNow.AddMinutes(2));
        Db.Members
            .Where(m => m.Id == former.Id)
            .Set(m => m.AnonymizedAt, DateTime.UtcNow)
            .Update();

        await PushAsync();

        await Assert.That(FakeOpenSlidesClient.Motions.Single().AdditionalSubmitter).IsEqualTo("Anna Autor, Carl Co, Ehemaliges Mitglied");
    }

    [Test]
    public async Task Audit_entry_summarises_the_push() {
        Submit("SÄA", "A");
        var failing = Submit("SÄA", "B");
        FakeOpenSlidesClient.FailCreateFor(NumberOf(failing));

        await PushAsync();

        var entry = Db.AuditLogs.Single(a => a.EntityId == _portal.Id && a.Action == "OpenSlidesPushed");
        await Assert.That(entry.EntityType).IsEqualTo("AssemblyMotionPortal");
        await Assert.That(entry.NewValue).IsEqualTo("created=1 updated=0 skipped=0 failed=1");
    }

    [Test]
    public async Task Concurrent_push_for_the_same_portal_is_rejected() {
        Submit();
        var entered = new TaskCompletionSource();
        var release = new TaskCompletionSource();
        var blocked = 0;
        FakeOpenSlidesClient.OnCreateMotion = async _ => {
            if (Interlocked.Exchange(ref blocked, 1) != 0)
                return;

            entered.TrySetResult();
            await release.Task;
        };

        var running = PushAsync();
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(30));
        var concurrent = await PushAsync().WaitAsync(TimeSpan.FromSeconds(30));
        release.SetResult();
        var finished = await running.WaitAsync(TimeSpan.FromSeconds(30));

        await Assert.That(concurrent).IsNull();
        await Assert.That(finished!.Created).IsEqualTo(1);
        await Assert.That(FakeOpenSlidesClient.Motions.Count).IsEqualTo(1);
        await Assert.That(await PushAsync()).IsNotNull();
    }

    [Test]
    public async Task Validate_makes_one_login_and_creates_nothing() {
        Submit();

        var result = await _push.ValidateAsync(CancellationToken.None);

        await Assert.That(result.Ok).IsTrue();
        await Assert.That(result.Message).Contains("pusher");
        await Assert.That(result.Message).DoesNotContain(Password);
        await Assert.That(FakeOpenSlidesClient.LoginCount).IsEqualTo(1);
        await Assert.That(FakeOpenSlidesClient.Motions).IsEmpty();
        await Assert.That(FakeOpenSlidesClient.Categories).IsEmpty();
    }

    [Test]
    public async Task Validate_never_leaks_a_response_body() {
        FakeOpenSlidesClient.LoginFailure = new OpenSlidesException("OpenSlides antwortete mit HTTP 500: SECRET-BODY-EXCERPT", "http_error");

        var result = await _push.ValidateAsync(CancellationToken.None);

        await Assert.That(result.Ok).IsFalse();
        await Assert.That(result.Message).DoesNotContain("SECRET-BODY-EXCERPT");
        await Assert.That(result.Message).StartsWith("Anmeldung an OpenSlides fehlgeschlagen: ");
    }

    [Test]
    public async Task Validate_without_credentials_makes_no_request() {
        _options.SetValue("openslides.password", null, "");

        var result = await _push.ValidateAsync(CancellationToken.None);

        await Assert.That(result.Ok).IsFalse();
        await Assert.That(result.Message).IsEqualTo(AssemblyMotionOpenSlidesPush.NotConfiguredMessage);
        await Assert.That(FakeOpenSlidesClient.LoginCount).IsEqualTo(0);
    }

    [Test]
    public async Task Password_never_reaches_stored_errors_or_audit() {
        var motion = Submit();
        FakeOpenSlidesClient.FailCreateFor(NumberOf(motion), new InvalidOperationException($"boom {Password}"));

        var result = await PushAsync();

        await Assert.That(result!.Failures.Single().Error).DoesNotContain(Password);
        await Assert.That(Reload(motion.Id).OpenSlidesLastError).DoesNotContain(Password);
        await Assert.That(Db.AuditLogs.Any(a => a.NewValue != null && a.NewValue.Contains(Password))).IsFalse();
    }

    private void Upvote(Guid motionId, int count) {
        for (var i = 0; i < count; i++) {
            Db.Insert(new AssemblyMotionUpvote {
                MotionId = motionId,
                MemberId = _builder.SeedMember(chapterId: _chapter.Id).Id,
                CreatedAt = _now.AddDays(-2)
            });
        }
    }

    private void AddSubmitter(Guid motionId, Guid memberId, DateTime joinedAt)
        => Db.Insert(new AssemblyMotionSubmitter {
            MotionId = motionId,
            MemberId = memberId,
            IsAuthor = false,
            JoinedAt = joinedAt,
            JoinedRevision = 1
        });
}
