using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using LinqToDB;
using Quartermaster.Api.AssemblyMotions;
using Quartermaster.Data;
using Quartermaster.Data.AssemblyMotions;
using Quartermaster.Data.Chapters;
using Quartermaster.Data.Events;
using Quartermaster.Data.Members;
using Quartermaster.Server.AssemblyMotions;
using Quartermaster.Server.AssemblyMotions.Notifications;
using Quartermaster.Server.Notifications;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.AssemblyMotions;

public class AssemblyMotionNotificationTests : RepositoryTestBase {
    private readonly CapturingNotificationQueue _queue = new();
    private readonly Guid _userId = Guid.NewGuid();
    private DbContext _context = default!;
    private TestDataBuilder _builder = default!;
    private AssemblyMotionPortalRepository _portalRepo = default!;
    private AssemblyMotionRepository _motionRepo = default!;
    private AssemblyMotionService _service = default!;
    private AssemblyMotionCommissionService _commission = default!;
    private AssemblyMotionInquiryService _inquiries = default!;
    private Chapter _chapter = default!;
    private Event _event = default!;

    [Before(Test)]
    public void Setup() {
        _context = Db;
        _builder = new TestDataBuilder(_context);
        _portalRepo = new AssemblyMotionPortalRepository(_context, AuditLog);
        _motionRepo = new AssemblyMotionRepository(_context, AuditLog);
        var notifier = new AssemblyMotionNotifier(_queue, _context, NullLogger<AssemblyMotionNotifier>.Instance);
        _service = new AssemblyMotionService(_motionRepo, _portalRepo, AuditLog, _context, notifier);
        _commission = new AssemblyMotionCommissionService(_motionRepo, AuditLog, notifier);
        _inquiries = new AssemblyMotionInquiryService(_motionRepo, new AssemblyMotionInquiryRepository(_context), AuditLog, notifier);
        _chapter = _builder.SeedChapter();
        _event = _builder.SeedEvent(_chapter.Id);
    }

    private static AssemblyMotionDraft NewDraft(Guid typeId, string title = "Testantrag")
        => new(typeId, title, null, "Antragstext", "Begründung", null, null, null);

    private (AssemblyMotion Motion, Member Author, AssemblyMotionType Type) Released(int minSubmitters, TimeSpan closesIn) {
        var now = DateTime.UtcNow;
        var portal = _portalRepo.GetByEvent(_event.Id) ?? _portalRepo.Create(_event.Id, now.AddHours(-1), now.Add(closesIn), now);
        var type = _portalRepo.GetTypes(portal.Id).First(t => t.MinSubmitters == minSubmitters);
        var author = _builder.SeedMember(chapterId: _chapter.Id);
        var motion = _service.CreateDraft(portal, author, _userId, NewDraft(type.Id), now).Motion!;
        motion = _service.Release(motion.Id, author, now).Motion!;
        return (motion, author, type);
    }

    private Member JoinNew(AssemblyMotion motion) {
        var member = _builder.SeedMember(chapterId: _chapter.Id);
        _service.Join(motion.Id, member, DateTime.UtcNow);
        return member;
    }

    private static AssemblyMotionNotificationPayload PayloadOf(NotificationDispatchRequest request)
        => (AssemblyMotionNotificationPayload)request.Payload;

    private static AssemblyMotionNotificationModel ModelFor(NotificationDispatchRequest request, Guid? userId = null)
        => (AssemblyMotionNotificationModel)request.ModelFactory(new NotificationRecipient(userId, "x@test.local"))["motion"];

    [Test]
    public async Task Fifth_join_enqueues_submitted_for_commission() {
        var (motion, _, _) = Released(5, TimeSpan.FromDays(7));
        JoinNew(motion);
        JoinNew(motion);
        JoinNew(motion);
        await Assert.That(_queue.Requests.Count).IsEqualTo(0);

        JoinNew(motion);

        await Assert.That(_queue.Requests.Count).IsEqualTo(1);
        var request = _queue.Requests[0];
        await Assert.That(request.TriggerId).IsEqualTo(NotificationTriggers.AssemblyMotionSubmitted);
        await Assert.That(PayloadOf(request).IncludeCommission).IsTrue();
        await Assert.That(PayloadOf(request).MemberIds.Count).IsEqualTo(0);
        await Assert.That(PayloadOf(request).EventChapterId).IsEqualTo(_chapter.Id);
        await Assert.That(ModelFor(request).Path).IsEqualTo($"/Administration/AssemblyMotions/{motion.Id}");
        await Assert.That(ModelFor(request).DisplayNumber).IsEqualTo("GP001");
    }

    [Test]
    public async Task Release_with_single_submitter_enqueues_submitted() {
        Released(1, TimeSpan.FromDays(7));

        await Assert.That(_queue.Requests.Count).IsEqualTo(1);
        await Assert.That(_queue.Requests[0].TriggerId).IsEqualTo(NotificationTriggers.AssemblyMotionSubmitted);
    }

    [Test]
    public async Task Editing_a_single_submitter_motion_enqueues_nothing() {
        var (motion, author, type) = Released(1, TimeSpan.FromDays(7));
        _queue.Requests.Clear();

        var outcome = _service.UpdateByAuthor(motion, author, _userId, NewDraft(type.Id, "Neuer Titel"), motion.Revision, DateTime.UtcNow);

        await Assert.That(outcome.Succeeded).IsTrue();
        await Assert.That(_queue.Requests.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Author_edit_enqueues_reset_for_removed_only() {
        var (motion, author, type) = Released(5, TimeSpan.FromDays(7));
        var first = JoinNew(motion);
        var second = JoinNew(motion);

        var outcome = _service.UpdateByAuthor(motion, author, _userId, NewDraft(type.Id, "Neuer Titel"), motion.Revision, DateTime.UtcNow);

        await Assert.That(outcome.Succeeded).IsTrue();
        await Assert.That(_queue.Requests.Count).IsEqualTo(1);
        var request = _queue.Requests[0];
        await Assert.That(request.TriggerId).IsEqualTo(NotificationTriggers.AssemblyMotionCoSubmitterReset);
        await Assert.That(PayloadOf(request).MemberIds.Order().ToList()).IsEquivalentTo(new[] { first.Id, second.Id }.Order().ToList());
        await Assert.That(PayloadOf(request).MemberIds.Contains(author.Id)).IsFalse();
        await Assert.That(PayloadOf(request).IncludeCommission).IsFalse();
        await Assert.That(ModelFor(request, Guid.NewGuid()).Title).IsEqualTo("Neuer Titel");
    }

    [Test]
    public async Task Type_change_to_single_submitter_type_while_collecting_enqueues_submitted() {
        var (motion, author, _) = Released(5, TimeSpan.FromDays(7));
        var singleType = _portalRepo.GetTypes(motion.PortalId).First(t => t.MinSubmitters == 1);
        _queue.Requests.Clear();

        var outcome = _service.UpdateByAuthor(motion, author, _userId, NewDraft(singleType.Id), motion.Revision, DateTime.UtcNow);

        await Assert.That(outcome.Succeeded).IsTrue();
        await Assert.That(outcome.Motion!.Status).IsEqualTo(AssemblyMotionStatus.Submitted);
        await Assert.That(_queue.Requests.Select(r => r.TriggerId).ToList()).IsEquivalentTo(new[] { NotificationTriggers.AssemblyMotionSubmitted });
    }

    [Test]
    public async Task Editing_a_submitted_multi_submitter_motion_enqueues_only_the_reset() {
        var (motion, author, type) = Released(5, TimeSpan.FromDays(7));
        var joiners = new[] { JoinNew(motion), JoinNew(motion), JoinNew(motion), JoinNew(motion) };
        _queue.Requests.Clear();

        var outcome = _service.UpdateByAuthor(motion, author, _userId, NewDraft(type.Id, "Neuer Titel"), motion.Revision, DateTime.UtcNow);

        await Assert.That(outcome.Succeeded).IsTrue();
        await Assert.That(outcome.Motion!.Status).IsEqualTo(AssemblyMotionStatus.CollectingCoSubmitters);
        var request = _queue.Requests.Single();
        await Assert.That(request.TriggerId).IsEqualTo(NotificationTriggers.AssemblyMotionCoSubmitterReset);
        await Assert.That(PayloadOf(request).MemberIds.Count).IsEqualTo(joiners.Length);
    }

    [Test]
    public async Task Reminder_sent_once_within_48h() {
        var (motion, author, _) = Released(5, TimeSpan.FromHours(40));

        var first = _service.SendCollectingReminders(DateTime.UtcNow);
        var second = _service.SendCollectingReminders(DateTime.UtcNow);

        await Assert.That(first).IsEqualTo(1);
        await Assert.That(second).IsEqualTo(0);
        await Assert.That(_queue.Requests.Count).IsEqualTo(1);
        var request = _queue.Requests[0];
        await Assert.That(request.TriggerId).IsEqualTo(NotificationTriggers.AssemblyMotionCollectingReminder);
        await Assert.That(PayloadOf(request).MemberIds.Single()).IsEqualTo(author.Id);
        await Assert.That(_motionRepo.Get(motion.Id)!.CollectingReminderSentAt).IsNotNull();
    }

    [Test]
    public async Task Reminder_not_sent_earlier_than_48h() {
        var (motion, _, _) = Released(5, TimeSpan.FromDays(3));

        var sent = _service.SendCollectingReminders(DateTime.UtcNow);

        await Assert.That(sent).IsEqualTo(0);
        await Assert.That(_queue.Requests.Count).IsEqualTo(0);
        await Assert.That(_motionRepo.Get(motion.Id)!.CollectingReminderSentAt).IsNull();
    }

    [Test]
    public async Task Reminder_is_sent_again_after_an_author_edit() {
        var (motion, author, type) = Released(5, TimeSpan.FromHours(40));
        _service.SendCollectingReminders(DateTime.UtcNow);

        _service.UpdateByAuthor(motion, author, _userId, NewDraft(type.Id, "Neuer Titel"), motion.Revision, DateTime.UtcNow);
        _service.SendCollectingReminders(DateTime.UtcNow);

        await Assert.That(_queue.Requests.Count(r => r.TriggerId == NotificationTriggers.AssemblyMotionCollectingReminder)).IsEqualTo(2);
    }

    [Test]
    public async Task Lapsed_notifies_all_submitter_rows() {
        var (motion, author, _) = Released(5, TimeSpan.FromHours(1));
        var joiner = JoinNew(motion);
        _queue.Requests.Clear();

        var lapsed = _service.LapseOverdue(DateTime.UtcNow.AddHours(2));

        await Assert.That(lapsed).IsEqualTo(1);
        await Assert.That(_queue.Requests.Count).IsEqualTo(1);
        var request = _queue.Requests[0];
        await Assert.That(request.TriggerId).IsEqualTo(NotificationTriggers.AssemblyMotionLapsed);
        await Assert.That(PayloadOf(request).MemberIds.Order().ToList()).IsEquivalentTo(new[] { author.Id, joiner.Id }.Order().ToList());
        await Assert.That(ModelFor(request).StatusLabel).IsEqualTo("Verfallen");
    }

    [Test]
    public async Task Withdraw_notifies_cosubmitters_and_commission_not_author() {
        var (motion, author, _) = Released(5, TimeSpan.FromDays(7));
        var joiners = new[] { JoinNew(motion), JoinNew(motion), JoinNew(motion), JoinNew(motion) };
        _queue.Requests.Clear();

        var outcome = _service.Withdraw(motion.Id, author, DateTime.UtcNow);

        await Assert.That(outcome.Succeeded).IsTrue();
        await Assert.That(_queue.Requests.Count).IsEqualTo(1);
        var request = _queue.Requests[0];
        await Assert.That(request.TriggerId).IsEqualTo(NotificationTriggers.AssemblyMotionWithdrawn);
        await Assert.That(PayloadOf(request).MemberIds.Order().ToList()).IsEquivalentTo(joiners.Select(j => j.Id).Order().ToList());
        await Assert.That(PayloadOf(request).MemberIds.Contains(author.Id)).IsFalse();
        await Assert.That(PayloadOf(request).IncludeCommission).IsTrue();
    }

    [Test]
    public async Task Withdraw_of_never_submitted_motion_does_not_notify_commission() {
        var (motion, author, _) = Released(5, TimeSpan.FromDays(7));
        var joiner = JoinNew(motion);
        _queue.Requests.Clear();

        var outcome = _service.Withdraw(motion.Id, author, DateTime.UtcNow);

        await Assert.That(outcome.Succeeded).IsTrue();
        var request = _queue.Requests.Single();
        await Assert.That(PayloadOf(request).MemberIds.Single()).IsEqualTo(joiner.Id);
        await Assert.That(PayloadOf(request).IncludeCommission).IsFalse();
    }

    [Test]
    public async Task Failing_queue_does_not_change_the_outcome() {
        var queue = new ThrowingNotificationQueue();
        var notifier = new AssemblyMotionNotifier(queue, _context, NullLogger<AssemblyMotionNotifier>.Instance);
        var service = new AssemblyMotionService(_motionRepo, _portalRepo, AuditLog, _context, notifier);
        var commission = new AssemblyMotionCommissionService(_motionRepo, AuditLog, notifier);
        var now = DateTime.UtcNow;
        var portal = _portalRepo.Create(_event.Id, now.AddHours(-1), now.AddDays(7), now);
        var type = _portalRepo.GetTypes(portal.Id).First(t => t.MinSubmitters == 1);
        var author = _builder.SeedMember(chapterId: _chapter.Id);
        var draft = service.CreateDraft(portal, author, _userId, NewDraft(type.Id), now).Motion!;

        var released = service.Release(draft.Id, author, now);
        var checkedMotion = commission.Check(draft.Id, AssemblyMotionStatus.Admitted, null, _userId, now);

        await Assert.That(released.Succeeded).IsTrue();
        await Assert.That(checkedMotion.Succeeded).IsTrue();
        await Assert.That(queue.Attempts).IsEqualTo(2);
        await Assert.That(_motionRepo.Get(draft.Id)!.Status).IsEqualTo(AssemblyMotionStatus.Admitted);
    }

    [Test]
    public async Task Withdrawing_a_draft_is_refused_and_enqueues_nothing() {
        var now = DateTime.UtcNow;
        var portal = _portalRepo.Create(_event.Id, now.AddHours(-1), now.AddDays(7), now);
        var type = _portalRepo.GetTypes(portal.Id).First(t => t.MinSubmitters == 5);
        var author = _builder.SeedMember(chapterId: _chapter.Id);
        var draft = _service.CreateDraft(portal, author, _userId, NewDraft(type.Id), now).Motion!;

        var outcome = _service.Withdraw(draft.Id, author, now);

        await Assert.That(outcome.Succeeded).IsFalse();
        await Assert.That(_queue.Requests.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Commission_withdraw_notifies_all_submitters_including_author_without_commission() {
        var (motion, author, _) = Released(5, TimeSpan.FromDays(7));
        var joiner = JoinNew(motion);
        _queue.Requests.Clear();

        var outcome = _commission.Withdraw(motion.Id, _userId, DateTime.UtcNow);

        await Assert.That(outcome.Succeeded).IsTrue();
        await Assert.That(_queue.Requests.Count).IsEqualTo(1);
        var request = _queue.Requests[0];
        await Assert.That(PayloadOf(request).MemberIds.Order().ToList()).IsEquivalentTo(new[] { author.Id, joiner.Id }.Order().ToList());
        await Assert.That(PayloadOf(request).IncludeCommission).IsFalse();
    }

    [Test]
    public async Task Withdrawn_model_links_submitters_to_portal_and_commission_to_admin_page() {
        var (motion, author, _) = Released(5, TimeSpan.FromDays(7));
        var joinerUser = _builder.SeedUser();
        var joiner = _builder.SeedMember(chapterId: _chapter.Id, userId: joinerUser.Id);
        _service.Join(motion.Id, joiner, DateTime.UtcNow);
        _queue.Requests.Clear();

        _service.Withdraw(motion.Id, author, DateTime.UtcNow);

        var request = _queue.Requests.Single();
        await Assert.That(ModelFor(request, joinerUser.Id).Path).IsEqualTo($"/AssemblyMotions/{motion.Id}");
        await Assert.That(ModelFor(request, Guid.NewGuid()).Path).IsEqualTo($"/Administration/AssemblyMotions/{motion.Id}");
    }

    [Test]
    public async Task Check_notifies_all_submitters_with_decision_and_note() {
        var (motion, author, _) = Released(1, TimeSpan.FromDays(7));
        var authorUser = _builder.SeedUser();
        _context.Members.Where(m => m.Id == author.Id).Set(m => m.UserId, authorUser.Id).Update();
        _queue.Requests.Clear();

        var outcome = _commission.Check(motion.Id, AssemblyMotionStatus.FormallyInsufficient, "Frist verpasst", _userId, DateTime.UtcNow);

        await Assert.That(outcome.Succeeded).IsTrue();
        var request = _queue.Requests.Single();
        await Assert.That(request.TriggerId).IsEqualTo(NotificationTriggers.AssemblyMotionChecked);
        await Assert.That(PayloadOf(request).MemberIds.Single()).IsEqualTo(author.Id);
        await Assert.That(ModelFor(request).CheckNote).IsEqualTo("Frist verpasst");
        await Assert.That(ModelFor(request).StatusLabel).IsEqualTo("Formal unzureichend");
        await Assert.That(ModelFor(request, authorUser.Id).Path).IsEqualTo($"/AssemblyMotions/{motion.Id}");
    }

    [Test]
    public async Task Commission_edit_notifies_only_when_a_revision_was_created() {
        var (motion, _, type) = Released(1, TimeSpan.FromDays(7));
        _queue.Requests.Clear();

        var unchanged = _commission.Edit(motion.Id, _userId, NewDraft(type.Id), motion.Revision, "Nichts geaendert", DateTime.UtcNow);
        await Assert.That(unchanged.Succeeded).IsTrue();
        await Assert.That(_queue.Requests.Count).IsEqualTo(0);

        var changed = _commission.Edit(motion.Id, _userId, NewDraft(type.Id, "Korrigierter Titel"), motion.Revision, "Titel korrigiert", DateTime.UtcNow);

        await Assert.That(changed.Succeeded).IsTrue();
        var request = _queue.Requests.Single();
        await Assert.That(request.TriggerId).IsEqualTo(NotificationTriggers.AssemblyMotionEditedByCommission);
        await Assert.That(ModelFor(request).ChangeNote).IsEqualTo("Titel korrigiert");
        await Assert.That(ModelFor(request).Title).IsEqualTo("Korrigierter Titel");
    }

    [Test]
    public async Task Inquiry_asked_goes_to_submitters_with_question() {
        var (motion, author, _) = Released(1, TimeSpan.FromDays(7));
        _queue.Requests.Clear();

        var outcome = _inquiries.Ask(motion.Id, "Warum diese Frist?", DateTime.UtcNow.AddDays(3), _userId, DateTime.UtcNow);

        await Assert.That(outcome.Succeeded).IsTrue();
        var request = _queue.Requests.Single();
        await Assert.That(request.TriggerId).IsEqualTo(NotificationTriggers.AssemblyMotionInquiryAsked);
        await Assert.That(PayloadOf(request).MemberIds.Single()).IsEqualTo(author.Id);
        await Assert.That(ModelFor(request).InquiryQuestion).IsEqualTo("Warum diese Frist?");
        await Assert.That(ModelFor(request).InquiryDueAt).IsNotEqualTo("");
    }

    [Test]
    public async Task Inquiry_answer_notifies_commission_each_time_it_is_replaced() {
        var (motion, author, _) = Released(1, TimeSpan.FromDays(7));
        _inquiries.Ask(motion.Id, "Frage?", DateTime.UtcNow.AddDays(3), _userId, DateTime.UtcNow);
        var inquiryId = _context.AssemblyMotionInquiries.Single().Id;
        _queue.Requests.Clear();

        _inquiries.Answer(inquiryId, author, "Antwort eins", DateTime.UtcNow);
        _inquiries.Answer(inquiryId, author, "Antwort zwei", DateTime.UtcNow);

        await Assert.That(_queue.Requests.Count).IsEqualTo(2);
        var request = _queue.Requests[0];
        await Assert.That(request.TriggerId).IsEqualTo(NotificationTriggers.AssemblyMotionInquiryAnswered);
        await Assert.That(PayloadOf(request).IncludeCommission).IsTrue();
        await Assert.That(PayloadOf(request).MemberIds.Count).IsEqualTo(0);
        await Assert.That(ModelFor(request).InquiryQuestion).IsEqualTo("");
    }

    [Test]
    public async Task Rolled_back_action_enqueues_nothing() {
        var (motion, author, type) = Released(5, TimeSpan.FromDays(7));
        JoinNew(motion);
        _queue.Requests.Clear();

        var conflict = _service.UpdateByAuthor(motion, author, _userId, NewDraft(type.Id, "Neuer Titel"), motion.Revision + 1, DateTime.UtcNow);
        var alreadyJoined = _service.Join(motion.Id, author, DateTime.UtcNow);
        var missing = _commission.Check(Guid.NewGuid(), AssemblyMotionStatus.Admitted, null, _userId, DateTime.UtcNow);

        await Assert.That(conflict.Succeeded).IsFalse();
        await Assert.That(alreadyJoined.Succeeded).IsFalse();
        await Assert.That(missing.Succeeded).IsFalse();
        await Assert.That(_queue.Requests.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Competition_declared_describes_the_recipients_own_motion() {
        var (declaring, _, _) = Released(1, TimeSpan.FromDays(7));
        var (own, ownAuthor, _) = Released(1, TimeSpan.FromDays(7));
        _queue.Requests.Clear();

        new AssemblyMotionNotifier(_queue, _context, NullLogger<AssemblyMotionNotifier>.Instance).CompetitionDeclared(declaring, own, [ownAuthor.Id]);

        var request = _queue.Requests.Single();
        await Assert.That(request.TriggerId).IsEqualTo(NotificationTriggers.AssemblyMotionCompetitionDeclared);
        await Assert.That(PayloadOf(request).MemberIds.Single()).IsEqualTo(ownAuthor.Id);
        await Assert.That(ModelFor(request, null).Id).IsEqualTo(own.Id);
        await Assert.That(ModelFor(request).OtherTitle).IsEqualTo("Testantrag");
    }
}
