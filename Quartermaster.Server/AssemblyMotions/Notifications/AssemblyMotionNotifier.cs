using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using LinqToDB;
using Microsoft.Extensions.Logging;
using Quartermaster.Api.AssemblyMotions;
using Quartermaster.Data;
using Quartermaster.Data.AssemblyMotions;
using Quartermaster.Server.Notifications;

namespace Quartermaster.Server.AssemblyMotions.Notifications;

/// <summary>
/// Builds and enqueues the assembly motion notifications. Callers invoke it only after the
/// originating transaction has committed.
/// </summary>
public class AssemblyMotionNotifier {
    private const string EscapedCharacters = "\\`*_[]()<>#|~";
    private const string EntityType = "AssemblyMotion";
    private const string MemberPathPrefix = "/AssemblyMotions/";
    private const string CommissionPathPrefix = "/Administration/AssemblyMotions/";

    private readonly INotificationDispatchQueue _queue;
    private readonly DbContext _db;
    private readonly ILogger<AssemblyMotionNotifier> _logger;

    public AssemblyMotionNotifier(INotificationDispatchQueue queue, DbContext db, ILogger<AssemblyMotionNotifier> logger) {
        _queue = queue;
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// Escapes Markdown-significant characters so member-typed text renders literally inside a template body,
    /// and flattens line breaks to spaces. Valid for the email Markdown renderer and for Telegram legacy Markdown.
    /// </summary>
    public static string EscapeMarkdown(string text) {
        var builder = new StringBuilder(text.Length);
        foreach (var character in text) {
            if (character == '\r' || character == '\n') {
                builder.Append(' ');
                continue;
            }
            if (EscapedCharacters.Contains(character))
                builder.Append('\\');
            builder.Append(character);
        }
        return builder.ToString();
    }

    public void CoSubmittersReset(AssemblyMotion motion, IReadOnlyList<Guid> removedMemberIds) {
        Safely(nameof(CoSubmittersReset), () => {
            var context = BuildContext(motion);
            Enqueue(NotificationTriggers.AssemblyMotionCoSubmitterReset, context, removedMemberIds, false);
        });
    }

    public void CollectingReminder(AssemblyMotion motion, DateTime effectiveDeadlineUtc) {
        Safely(nameof(CollectingReminder), () => {
            var context = BuildContext(motion);
            context.Model = context.Model with { Deadline = BerlinTime.FormatDeadline(effectiveDeadlineUtc) };
            Enqueue(NotificationTriggers.AssemblyMotionCollectingReminder, context, [motion.CreatedByMemberId], false);
        });
    }

    public void Submitted(AssemblyMotion motion) {
        Safely(nameof(Submitted), () => {
            var context = BuildContext(motion);
            Enqueue(NotificationTriggers.AssemblyMotionSubmitted, context, [], true);
        });
    }

    public void Lapsed(AssemblyMotion motion, IReadOnlyList<Guid> submitterMemberIds) {
        Safely(nameof(Lapsed), () => {
            var context = BuildContext(motion);
            Enqueue(NotificationTriggers.AssemblyMotionLapsed, context, submitterMemberIds, false);
        });
    }

    public void Checked(AssemblyMotion motion, IReadOnlyList<Guid> submitterMemberIds) {
        Safely(nameof(Checked), () => {
            var context = BuildContext(motion);
            context.Model = context.Model with { CheckNote = motion.CheckNote ?? "" };
            Enqueue(NotificationTriggers.AssemblyMotionChecked, context, submitterMemberIds, false);
        });
    }

    public void InquiryAsked(AssemblyMotion motion, AssemblyMotionInquiry inquiry, IReadOnlyList<Guid> submitterMemberIds) {
        Safely(nameof(InquiryAsked), () => {
            var context = BuildContext(motion);
            context.Model = context.Model with {
                InquiryQuestion = inquiry.QuestionMarkdown,
                InquiryDueAt = BerlinTime.FormatDeadline(inquiry.ResponseDueAt)
            };
            Enqueue(NotificationTriggers.AssemblyMotionInquiryAsked, context, submitterMemberIds, false);
        });
    }

    public void InquiryAnswered(AssemblyMotion motion, AssemblyMotionInquiry inquiry) {
        Safely(nameof(InquiryAnswered), () => {
            var context = BuildContext(motion);
            context.Model = context.Model with { InquiryDueAt = BerlinTime.FormatDeadline(inquiry.ResponseDueAt) };
            Enqueue(NotificationTriggers.AssemblyMotionInquiryAnswered, context, [], true);
        });
    }

    public void EditedByCommission(AssemblyMotion motion, string? changeNote, IReadOnlyList<Guid> submitterMemberIds) {
        Safely(nameof(EditedByCommission), () => {
            var context = BuildContext(motion);
            context.Model = context.Model with { ChangeNote = changeNote ?? "" };
            Enqueue(NotificationTriggers.AssemblyMotionEditedByCommission, context, submitterMemberIds, false);
        });
    }

    /// <summary>
    /// Notifies the submitters of <paramref name="otherMotion"/> that <paramref name="motion"/> was declared its
    /// competitor. The model describes <paramref name="otherMotion"/> as the main motion (the recipients' own motion)
    /// and <paramref name="motion"/> through the <c>Other*</c> fields.
    /// </summary>
    public void CompetitionDeclared(AssemblyMotion motion, AssemblyMotion otherMotion, IReadOnlyList<Guid> otherSubmitterMemberIds) {
        Safely(nameof(CompetitionDeclared), () => {
            var declaring = BuildContext(motion);
            var context = BuildContext(otherMotion);
            context.Model = context.Model with {
                OtherDisplayNumber = declaring.Model.DisplayNumber,
                OtherTitle = declaring.Model.Title,
                OtherTitleEscaped = declaring.Model.TitleEscaped
            };
            Enqueue(NotificationTriggers.AssemblyMotionCompetitionDeclared, context, otherSubmitterMemberIds, false);
        });
    }

    /// <summary>
    /// Notifies <paramref name="memberIds"/> and, when <paramref name="includeCommission"/> is set, the commission.
    /// The caller decides who is notified, because an author's own withdrawal does not notify the author while a
    /// withdrawal by the commission does.
    /// </summary>
    public void Withdrawn(AssemblyMotion motion, IReadOnlyList<Guid> memberIds, bool includeCommission) {
        Safely(nameof(Withdrawn), () => {
            var context = BuildContext(motion);
            Enqueue(NotificationTriggers.AssemblyMotionWithdrawn, context, memberIds, includeCommission);
        });
    }

    private void Safely(string notification, Action action) {
        try {
            action();
        } catch (Exception ex) {
            _logger.LogError(ex, "Assembly motion notification {Notification} failed: {ex}", notification, ex);
        }
    }

    private void Enqueue(string triggerId, NotificationContext context, IReadOnlyList<Guid> memberIds, bool includeCommission) {
        if (memberIds.Count == 0 && !includeCommission)
            return;

        var model = context.Model;
        var memberUserIds = MemberUserIds(memberIds);
        var payload = new AssemblyMotionNotificationPayload(model.Id, context.EventChapterId, memberIds, includeCommission);

        _queue.Enqueue(new NotificationDispatchRequest(
            triggerId,
            payload,
            recipient => new Dictionary<string, object> {
                ["motion"] = model with { Path = PathFor(model.Id, recipient, memberUserIds) }
            },
            SourceEntityType: EntityType,
            SourceEntityId: model.Id));
    }

    private static string PathFor(Guid motionId, NotificationRecipient recipient, HashSet<Guid> memberUserIds) {
        if (recipient.UserId != null && memberUserIds.Contains(recipient.UserId.Value))
            return MemberPathPrefix + motionId;

        return CommissionPathPrefix + motionId;
    }

    private HashSet<Guid> MemberUserIds(IReadOnlyList<Guid> memberIds) {
        if (memberIds.Count == 0)
            return [];

        return _db.Members
            .Where(m => memberIds.Contains(m.Id) && m.UserId != null)
            .Select(m => m.UserId!.Value)
            .ToHashSet();
    }

    private NotificationContext BuildContext(AssemblyMotion motion) {
        var type = _db.AssemblyMotionTypes.FirstOrDefault(t => t.Id == motion.TypeId)
            ?? throw new InvalidOperationException($"Type {motion.TypeId} not found for motion {motion.Id}.");
        var portal = _db.AssemblyMotionPortals.FirstOrDefault(p => p.Id == motion.PortalId)
            ?? throw new InvalidOperationException($"Portal {motion.PortalId} not found for motion {motion.Id}.");
        var assembly = _db.Events.FirstOrDefault(e => e.Id == portal.EventId)
            ?? throw new InvalidOperationException($"Event {portal.EventId} not found for portal {portal.Id}.");

        var effectiveDeadline = AssemblyMotionDeadlines.Effective(portal, type, motion.IsCompetingMotion);
        var model = new AssemblyMotionNotificationModel {
            Id = motion.Id,
            DisplayNumber = DisplayNumber(motion, type),
            Title = motion.Title,
            TitleEscaped = EscapeMarkdown(motion.Title),
            TypeName = type.Name,
            EventName = assembly.PublicName,
            StatusLabel = StatusLabel(motion),
            Deadline = BerlinTime.FormatDeadline(effectiveDeadline)
        };
        return new NotificationContext(model, assembly.ChapterId);
    }

    private static string DisplayNumber(AssemblyMotion motion, AssemblyMotionType type) {
        if (motion.Number == null)
            return "Entwurf";

        return AssemblyMotionNumber.Format(type.Code, motion.Number.Value);
    }

    private static string StatusLabel(AssemblyMotion motion) {
        return motion.Status switch {
            AssemblyMotionStatus.Draft => "Entwurf",
            AssemblyMotionStatus.CollectingCoSubmitters => motion.IsInRevision ? "In Überarbeitung" : "Sucht Mitantragsteller",
            AssemblyMotionStatus.Submitted => "Eingereicht",
            AssemblyMotionStatus.Admitted => "Zugelassen",
            AssemblyMotionStatus.FormallyInsufficient => "Formal unzureichend",
            AssemblyMotionStatus.Withdrawn => "Zurückgezogen",
            AssemblyMotionStatus.Lapsed => "Verfallen",
            _ => throw new ArgumentOutOfRangeException(nameof(motion), motion.Status, null)
        };
    }

    private class NotificationContext {
        public NotificationContext(AssemblyMotionNotificationModel model, Guid eventChapterId) {
            Model = model;
            EventChapterId = eventChapterId;
        }

        public AssemblyMotionNotificationModel Model { get; set; }
        public Guid EventChapterId { get; }
    }
}
