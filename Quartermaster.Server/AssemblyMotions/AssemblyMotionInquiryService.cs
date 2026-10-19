using System;
using System.Linq;
using Quartermaster.Api.AssemblyMotions;
using Quartermaster.Api.I18n;
using Quartermaster.Data.AssemblyMotions;
using Quartermaster.Data.AuditLog;
using Quartermaster.Data.Members;
using Quartermaster.Rendering;
using Quartermaster.Server.AssemblyMotions.Notifications;

namespace Quartermaster.Server.AssemblyMotions;

public class AssemblyMotionInquiryService {
    private const string EntityType = "AssemblyMotionInquiry";
    private const int MaxTextLength = 20_000;

    private readonly AssemblyMotionRepository _motions;
    private readonly AssemblyMotionInquiryRepository _inquiries;
    private readonly AuditLogRepository _auditLog;
    private readonly AssemblyMotionNotifier _notifier;

    public AssemblyMotionInquiryService(AssemblyMotionRepository motions, AssemblyMotionInquiryRepository inquiries, AuditLogRepository auditLog, AssemblyMotionNotifier notifier) {
        _motions = motions;
        _inquiries = inquiries;
        _auditLog = auditLog;
        _notifier = notifier;
    }

    public AssemblyMotionOutcome Ask(Guid motionId, string questionMarkdown, DateTime responseDueAt, Guid userId, DateTime nowUtc) {
        questionMarkdown = MotionMarkdown.Normalize(questionMarkdown);
        var textProblem = TextProblem(questionMarkdown, I18nKey.Error.AssemblyMotion.InquiryQuestionMaxLength);
        if (textProblem != null)
            return textProblem;
        if (responseDueAt < nowUtc.AddHours(24))
            return AssemblyMotionOutcome.Fail(422, I18nKey.Error.AssemblyMotion.InquiryDueTooSoon);

        using var tx = _motions.BeginTransaction();

        var motion = _motions.GetForUpdate(motionId);
        if (motion == null)
            return AssemblyMotionOutcome.Fail(404, I18nKey.Error.AssemblyMotion.NotFound);
        if (motion.Number == null || motion.Status == AssemblyMotionStatus.Withdrawn || motion.Status == AssemblyMotionStatus.Lapsed)
            return AssemblyMotionOutcome.Fail(409, I18nKey.Error.AssemblyMotion.InvalidState);

        var inquiry = new AssemblyMotionInquiry {
            MotionId = motion.Id,
            QuestionMarkdown = questionMarkdown,
            QuestionHtml = ToHtml(questionMarkdown),
            AskedByUserId = userId,
            AskedAt = nowUtc,
            ResponseDueAt = responseDueAt
        };
        _inquiries.Insert(inquiry);
        _motions.MarkMirrorsPending(motion.Id, nowUtc);

        _auditLog.Log(EntityType, inquiry.Id, "Asked", nameof(AssemblyMotionInquiry.QuestionMarkdown), null, questionMarkdown);

        var submitterIds = _motions.GetSubmitters(motion.Id)
            .Select(s => s.MemberId)
            .ToList();

        tx.Commit();
        _notifier.InquiryAsked(motion, inquiry, submitterIds);
        return AssemblyMotionOutcome.Ok(motion);
    }

    public AssemblyMotionOutcome Answer(Guid inquiryId, Member member, string answerMarkdown, DateTime nowUtc) {
        answerMarkdown = MotionMarkdown.Normalize(answerMarkdown);
        var textProblem = TextProblem(answerMarkdown, I18nKey.Error.AssemblyMotion.InquiryAnswerMaxLength);
        if (textProblem != null)
            return textProblem;

        using var tx = _motions.BeginTransaction();

        var inquiry = _inquiries.Get(inquiryId);
        if (inquiry == null)
            return AssemblyMotionOutcome.Fail(404, I18nKey.Error.AssemblyMotion.InquiryNotFound);

        var motion = _motions.GetForUpdate(inquiry.MotionId);
        if (motion == null)
            return AssemblyMotionOutcome.Fail(404, I18nKey.Error.AssemblyMotion.NotFound);

        inquiry = _inquiries.Get(inquiryId);
        if (inquiry == null)
            return AssemblyMotionOutcome.Fail(404, I18nKey.Error.AssemblyMotion.InquiryNotFound);
        if (!_motions.GetSubmitters(motion.Id).Any(s => s.MemberId == member.Id))
            return AssemblyMotionOutcome.Fail(403, I18nKey.Error.AssemblyMotion.NotSubmitter);
        if (inquiry.ClosedAt != null)
            return AssemblyMotionOutcome.Fail(409, I18nKey.Error.AssemblyMotion.InvalidState);

        var previousAnswer = inquiry.AnswerMarkdown;
        inquiry.AnswerMarkdown = answerMarkdown;
        inquiry.AnswerHtml = ToHtml(answerMarkdown);
        inquiry.AnsweredByMemberId = member.Id;
        inquiry.AnsweredAt = nowUtc;
        _inquiries.Update(inquiry);

        _auditLog.Log(EntityType, inquiry.Id, previousAnswer == null ? "Answered" : "AnswerReplaced", nameof(AssemblyMotionInquiry.AnswerMarkdown), previousAnswer, answerMarkdown);

        tx.Commit();
        _notifier.InquiryAnswered(motion, inquiry);
        return AssemblyMotionOutcome.Ok(motion);
    }

    public AssemblyMotionOutcome Close(Guid inquiryId, Guid userId, DateTime nowUtc) {
        using var tx = _motions.BeginTransaction();

        var inquiry = _inquiries.Get(inquiryId);
        if (inquiry == null)
            return AssemblyMotionOutcome.Fail(404, I18nKey.Error.AssemblyMotion.InquiryNotFound);

        var motion = _motions.GetForUpdate(inquiry.MotionId);
        if (motion == null)
            return AssemblyMotionOutcome.Fail(404, I18nKey.Error.AssemblyMotion.NotFound);

        inquiry = _inquiries.Get(inquiryId);
        if (inquiry == null)
            return AssemblyMotionOutcome.Fail(404, I18nKey.Error.AssemblyMotion.InquiryNotFound);
        if (inquiry.ClosedAt != null)
            return AssemblyMotionOutcome.Fail(409, I18nKey.Error.AssemblyMotion.InvalidState);

        inquiry.ClosedAt = nowUtc;
        inquiry.ClosedByUserId = userId;
        _inquiries.Update(inquiry);
        _motions.MarkMirrorsPending(motion.Id, nowUtc);

        _auditLog.Log(EntityType, inquiry.Id, "Closed");

        tx.Commit();
        return AssemblyMotionOutcome.Ok(motion);
    }

    private static AssemblyMotionOutcome? TextProblem(string normalizedMarkdown, string tooLongKey) {
        if (normalizedMarkdown.Length > MaxTextLength)
            return AssemblyMotionOutcome.Fail(422, tooLongKey);

        var key = AssemblyMotionContent.MarkdownProblemKey(normalizedMarkdown);
        return key == null ? null : AssemblyMotionOutcome.Fail(422, key);
    }

    private static string ToHtml(string markdown) {
        return MarkdownService.ToHtml(markdown, SanitizationProfile.Motion, MarkdownFlavor.Motion);
    }
}
