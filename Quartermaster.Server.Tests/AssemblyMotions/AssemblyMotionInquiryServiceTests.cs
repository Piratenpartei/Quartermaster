using System;
using System.Linq;
using System.Threading.Tasks;
using LinqToDB;
using Microsoft.Extensions.Logging.Abstractions;
using Quartermaster.Api.AssemblyMotions;
using Quartermaster.Api.I18n;
using Quartermaster.Data;
using Quartermaster.Data.AssemblyMotions;
using Quartermaster.Data.Chapters;
using Quartermaster.Data.Events;
using Quartermaster.Data.Members;
using Quartermaster.Server.AssemblyMotions;
using Quartermaster.Server.AssemblyMotions.Notifications;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.AssemblyMotions;

public class AssemblyMotionInquiryServiceTests : RepositoryTestBase {
    private readonly CapturingNotificationQueue _queue = new();
    private DbContext _context = default!;
    private TestDataBuilder _builder = default!;
    private AssemblyMotionPortalRepository _portalRepo = default!;
    private AssemblyMotionRepository _motionRepo = default!;
    private AssemblyMotionInquiryRepository _inquiryRepo = default!;
    private AssemblyMotionService _service = default!;
    private AssemblyMotionInquiryService _inquiries = default!;
    private Chapter _chapter = default!;
    private Event _event = default!;
    private readonly Guid _userId = Guid.NewGuid();

    [Before(Test)]
    public void Setup() {
        _context = Db;
        _builder = new TestDataBuilder(_context);
        _portalRepo = new AssemblyMotionPortalRepository(_context, AuditLog);
        _motionRepo = new AssemblyMotionRepository(_context, AuditLog);
        _inquiryRepo = new AssemblyMotionInquiryRepository(_context);
        _service = new AssemblyMotionService(_motionRepo, _portalRepo, AuditLog, _context, new AssemblyMotionNotifier(_queue, _context, NullLogger<AssemblyMotionNotifier>.Instance));
        _inquiries = new AssemblyMotionInquiryService(_motionRepo, _inquiryRepo, AuditLog, new AssemblyMotionNotifier(_queue, _context, NullLogger<AssemblyMotionNotifier>.Instance));
        _chapter = _builder.SeedChapter();
        _event = _builder.SeedEvent(_chapter.Id);
    }

    private static AssemblyMotionDraft NewDraft(Guid typeId)
        => new(typeId, "Testantrag", null, "Antragstext", "Begründung", null, null, null);

    private (AssemblyMotion Motion, Member Author) SubmittedMotion(int minSubmitters = 1) {
        var now = DateTime.UtcNow;
        var portal = _portalRepo.GetByEvent(_event.Id) ?? _portalRepo.Create(_event.Id, now.AddHours(-1), now.AddDays(7), now);
        _context.AssemblyMotionPortals
            .Where(p => p.Id == portal.Id)
            .Set(p => p.WikiMirrorEnabled, true)
            .Update();
        var type = _portalRepo.GetTypes(portal.Id).First(t => t.MinSubmitters == minSubmitters);
        var author = _builder.SeedMember(chapterId: _chapter.Id);
        var motion = _service.CreateDraft(portal, author, _userId, NewDraft(type.Id), now).Motion!;
        return (_service.Release(motion.Id, author, now).Motion!, author);
    }

    private AssemblyMotionInquiry AskedInquiry(AssemblyMotion motion) {
        var now = DateTime.UtcNow;
        _inquiries.Ask(motion.Id, "Frage?", now.AddDays(2), _userId, now);
        return _inquiryRepo.ListForMotion(motion.Id).Single();
    }

    [Test]
    public async Task Due_less_than_24h_refused() {
        var (motion, _) = SubmittedMotion();
        var now = DateTime.UtcNow;

        var tooSoon = _inquiries.Ask(motion.Id, "Frage?", now.AddHours(23), _userId, now);
        var enough = _inquiries.Ask(motion.Id, "Frage?", now.AddHours(24), _userId, now);

        await Assert.That(tooSoon.Succeeded).IsFalse();
        await Assert.That(tooSoon.ErrorKey).IsEqualTo(I18nKey.Error.AssemblyMotion.InquiryDueTooSoon);
        await Assert.That(enough.Succeeded).IsTrue();
        await Assert.That(_inquiryRepo.ListForMotion(motion.Id).Count).IsEqualTo(1);
    }

    [Test]
    public async Task Ask_refused_without_number() {
        var (portalMotion, _) = SubmittedMotion(5);
        var now = DateTime.UtcNow;

        var outcome = _inquiries.Ask(portalMotion.Id, "Frage?", now.AddDays(2), _userId, now);

        await Assert.That(portalMotion.Number).IsNull();
        await Assert.That(outcome.Succeeded).IsFalse();
        await Assert.That(outcome.ErrorKey).IsEqualTo(I18nKey.Error.AssemblyMotion.InvalidState);
    }

    [Test]
    public async Task Ask_refused_for_withdrawn_motion() {
        var (motion, author) = SubmittedMotion();
        var now = DateTime.UtcNow;
        _service.Withdraw(motion.Id, author, now);

        var outcome = _inquiries.Ask(motion.Id, "Frage?", now.AddDays(2), _userId, now);

        await Assert.That(outcome.Succeeded).IsFalse();
        await Assert.That(outcome.ErrorKey).IsEqualTo(I18nKey.Error.AssemblyMotion.InvalidState);
    }

    [Test]
    public async Task Ask_queues_wiki_sync_and_renders_html() {
        var (motion, _) = SubmittedMotion();
        var now = DateTime.UtcNow;
        Db.AssemblyMotions
            .Where(m => m.Id == motion.Id)
            .Set(m => m.WikiSyncState, WikiSyncState.Synced)
            .Update();

        var outcome = _inquiries.Ask(motion.Id, "**Frage**", now.AddDays(2), _userId, now);
        var inquiry = _inquiryRepo.ListForMotion(motion.Id).Single();

        await Assert.That(outcome.Succeeded).IsTrue();
        await Assert.That(_motionRepo.Get(motion.Id)!.WikiSyncState).IsEqualTo(WikiSyncState.Pending);
        await Assert.That(inquiry.QuestionHtml).Contains("<strong>Frage</strong>");
        await Assert.That(inquiry.AskedByUserId).IsEqualTo(_userId);
        await Assert.That(_inquiryRepo.HasOpen(motion.Id)).IsTrue();
    }

    [Test]
    public async Task Any_submitter_can_answer() {
        var now = DateTime.UtcNow;
        var portal = _portalRepo.Create(_event.Id, now.AddHours(-1), now.AddDays(7), now);
        var type = _portalRepo.GetTypes(portal.Id).First(t => t.MinSubmitters == 5);
        Db.AssemblyMotionTypes
            .Where(t => t.Id == type.Id)
            .Set(t => t.MinSubmitters, 2)
            .Update();
        var author = _builder.SeedMember(chapterId: _chapter.Id);
        var second = _builder.SeedMember(chapterId: _chapter.Id);
        var created = _service.CreateDraft(portal, author, _userId, NewDraft(type.Id), now).Motion!;
        _service.Release(created.Id, author, now);
        _service.Join(created.Id, second, now);
        var motion = _motionRepo.Get(created.Id)!;
        var inquiry = AskedInquiry(motion);

        var outcome = _inquiries.Answer(inquiry.Id, second, "Antwort", DateTime.UtcNow);
        var stored = _inquiryRepo.Get(inquiry.Id)!;

        await Assert.That(motion.Number).IsNotNull();
        await Assert.That(outcome.Succeeded).IsTrue();
        await Assert.That(stored.AnswerMarkdown).IsEqualTo("Antwort");
        await Assert.That(stored.AnswerHtml).Contains("Antwort");
        await Assert.That(stored.AnsweredByMemberId).IsEqualTo(second.Id);
        await Assert.That(stored.AnsweredAt).IsNotNull();
    }

    [Test]
    public async Task Non_submitter_cannot_answer() {
        var (motion, _) = SubmittedMotion();
        var inquiry = AskedInquiry(motion);
        var stranger = _builder.SeedMember(chapterId: _chapter.Id);

        var outcome = _inquiries.Answer(inquiry.Id, stranger, "Antwort", DateTime.UtcNow);

        await Assert.That(outcome.Succeeded).IsFalse();
        await Assert.That(outcome.StatusCode).IsEqualTo(403);
        await Assert.That(outcome.ErrorKey).IsEqualTo(I18nKey.Error.AssemblyMotion.NotSubmitter);
        await Assert.That(_inquiryRepo.Get(inquiry.Id)!.AnswerMarkdown).IsNull();
    }

    [Test]
    public async Task Second_answer_replaces_first() {
        var (motion, author) = SubmittedMotion();
        var inquiry = AskedInquiry(motion);

        _inquiries.Answer(inquiry.Id, author, "Erste", DateTime.UtcNow);
        var second = _inquiries.Answer(inquiry.Id, author, "Zweite", DateTime.UtcNow);
        var stored = _inquiryRepo.Get(inquiry.Id)!;
        var audit = AuditLog.GetForEntity("AssemblyMotionInquiry", inquiry.Id);

        await Assert.That(second.Succeeded).IsTrue();
        await Assert.That(stored.AnswerMarkdown).IsEqualTo("Zweite");
        await Assert.That(audit.Any(a => a.Action == "AnswerReplaced" && a.OldValue == "Erste" && a.NewValue == "Zweite")).IsTrue();
    }

    [Test]
    public async Task Answer_after_close_refused() {
        var (motion, author) = SubmittedMotion();
        var inquiry = AskedInquiry(motion);
        _inquiries.Close(inquiry.Id, _userId, DateTime.UtcNow);

        var outcome = _inquiries.Answer(inquiry.Id, author, "Zu spät", DateTime.UtcNow);

        await Assert.That(outcome.Succeeded).IsFalse();
        await Assert.That(outcome.ErrorKey).IsEqualTo(I18nKey.Error.AssemblyMotion.InvalidState);
        await Assert.That(_inquiryRepo.Get(inquiry.Id)!.AnswerMarkdown).IsNull();
    }

    [Test]
    public async Task Close_sets_fields_queues_wiki_and_cannot_repeat() {
        var (motion, _) = SubmittedMotion();
        var inquiry = AskedInquiry(motion);
        Db.AssemblyMotions
            .Where(m => m.Id == motion.Id)
            .Set(m => m.WikiSyncState, WikiSyncState.Synced)
            .Update();

        var closed = _inquiries.Close(inquiry.Id, _userId, DateTime.UtcNow);
        var again = _inquiries.Close(inquiry.Id, _userId, DateTime.UtcNow);
        var stored = _inquiryRepo.Get(inquiry.Id)!;

        await Assert.That(closed.Succeeded).IsTrue();
        await Assert.That(again.Succeeded).IsFalse();
        await Assert.That(stored.ClosedAt).IsNotNull();
        await Assert.That(stored.ClosedByUserId).IsEqualTo(_userId);
        await Assert.That(_motionRepo.Get(motion.Id)!.WikiSyncState).IsEqualTo(WikiSyncState.Pending);
        await Assert.That(_inquiryRepo.HasOpen(motion.Id)).IsFalse();
    }

    [Test]
    public async Task CountOpen_counts_only_open_inquiries() {
        var (first, _) = SubmittedMotion();
        var (second, _) = SubmittedMotion();
        var now = DateTime.UtcNow;
        _inquiries.Ask(first.Id, "A?", now.AddDays(2), _userId, now);
        _inquiries.Ask(first.Id, "B?", now.AddDays(2), _userId, now);
        _inquiries.Ask(second.Id, "C?", now.AddDays(2), _userId, now);
        _inquiries.Close(_inquiryRepo.ListForMotion(second.Id).Single().Id, _userId, now);

        var counts = _inquiryRepo.CountOpen([first.Id, second.Id]);

        await Assert.That(counts[first.Id]).IsEqualTo(2);
        await Assert.That(counts.ContainsKey(second.Id)).IsFalse();
    }

    [Test]
    [MethodDataSource(typeof(MotionMarkdownSafetyTests), nameof(MotionMarkdownSafetyTests.ShapeShiftingPayloads))]
    public async Task Ask_and_answer_refuse_text_that_becomes_html_when_normalized(string markdown) {
        var (motion, author) = SubmittedMotion();
        var now = DateTime.UtcNow;
        var inquiry = AskedInquiry(motion);

        var asked = _inquiries.Ask(motion.Id, markdown, now.AddDays(2), _userId, now);
        var answered = _inquiries.Answer(inquiry.Id, author, markdown, now);
        var stored = _inquiryRepo.ListForMotion(motion.Id);

        await Assert.That(asked.StatusCode).IsEqualTo(422);
        await Assert.That(asked.ErrorKey).IsEqualTo(I18nKey.Error.AssemblyMotion.MarkdownHtmlNotAllowed);
        await Assert.That(answered.StatusCode).IsEqualTo(422);
        await Assert.That(answered.ErrorKey).IsEqualTo(I18nKey.Error.AssemblyMotion.MarkdownHtmlNotAllowed);
        await Assert.That(stored.Count).IsEqualTo(1);
        await Assert.That(stored[0].AnswerMarkdown).IsNull();
        await Assert.That(MotionMarkdownSafetyTests.ForeignMarkup(stored[0].QuestionHtml + stored[0].AnswerHtml)).IsEmpty();
    }

    [Test]
    public async Task Ask_and_answer_store_the_normalized_text() {
        var (motion, author) = SubmittedMotion();
        var now = DateTime.UtcNow;

        _inquiries.Ask(motion.Id, "    Fräge\r\nzwei \r\n", now.AddDays(2), _userId, now);
        var inquiry = _inquiryRepo.ListForMotion(motion.Id).Single();
        _inquiries.Answer(inquiry.Id, author, "*Ja*" + MotionMarkdownSafetyTests.GreekQuestionMark + "\n\n", now);
        var answered = _inquiryRepo.ListForMotion(motion.Id).Single();

        await Assert.That(inquiry.QuestionMarkdown).IsEqualTo("    Fräge\nzwei");
        await Assert.That(inquiry.QuestionHtml).IsEqualTo("<pre><code>Fräge\n</code></pre>\n<p>zwei</p>\n");
        await Assert.That(answered.AnswerMarkdown).IsEqualTo("*Ja*;");
        await Assert.That(answered.AnswerHtml).IsEqualTo("<p><em>Ja</em>;</p>\n");
    }

    [Test]
    public async Task Length_limit_applies_to_the_normalized_inquiry_text() {
        var (motion, author) = SubmittedMotion();
        var now = DateTime.UtcNow;
        var inquiry = AskedInquiry(motion);
        var expanding = new string('שּׁ', 7000);

        var asked = _inquiries.Ask(motion.Id, expanding, now.AddDays(2), _userId, now);
        var answered = _inquiries.Answer(inquiry.Id, author, expanding, now);

        await Assert.That(asked.StatusCode).IsEqualTo(422);
        await Assert.That(asked.ErrorKey).IsEqualTo(I18nKey.Error.AssemblyMotion.InquiryQuestionMaxLength);
        await Assert.That(answered.StatusCode).IsEqualTo(422);
        await Assert.That(answered.ErrorKey).IsEqualTo(I18nKey.Error.AssemblyMotion.InquiryAnswerMaxLength);
    }
}
