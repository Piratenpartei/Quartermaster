using System;
using System.Collections.Generic;
using System.Linq;
using LinqToDB;
using Quartermaster.Data;
using Quartermaster.Server.Events;
using Quartermaster.Server.Notifications;

namespace Quartermaster.Server.AssemblyMotions.Notifications;

/// <summary>
/// Resolves the members of an <see cref="AssemblyMotionNotificationPayload"/> to their linked users and, when
/// requested, adds the motion commission of the event's chapter (<see cref="AssemblyMotionCommissionUsers"/>)
/// and the event's subscribers who can see it.
/// </summary>
public class AssemblyMotionRecipientResolver : IRecipientResolver {
    public static IReadOnlyList<string> TriggerIds { get; } = [
        NotificationTriggers.AssemblyMotionCoSubmitterReset,
        NotificationTriggers.AssemblyMotionCoSubmitterJoined,
        NotificationTriggers.AssemblyMotionCoSubmitterLeft,
        NotificationTriggers.AssemblyMotionForumReply,
        NotificationTriggers.AssemblyMotionSubscribedForumReply,
        NotificationTriggers.AssemblyMotionCollectingReminder,
        NotificationTriggers.AssemblyMotionSubmitted,
        NotificationTriggers.AssemblyMotionLapsed,
        NotificationTriggers.AssemblyMotionChecked,
        NotificationTriggers.AssemblyMotionInquiryAsked,
        NotificationTriggers.AssemblyMotionInquiryAnswered,
        NotificationTriggers.AssemblyMotionEditedByCommission,
        NotificationTriggers.AssemblyMotionCompetitionDeclared,
        NotificationTriggers.AssemblyMotionWithdrawn
    ];

    private readonly DbContext _db;
    private readonly EventUserVisibility _visibility;

    public AssemblyMotionRecipientResolver(string triggerId, DbContext db, EventUserVisibility visibility) {
        TriggerId = triggerId;
        _db = db;
        _visibility = visibility;
    }

    public string TriggerId { get; }

    public IReadOnlyList<NotificationRecipient> Resolve(object payload) {
        if (payload is not AssemblyMotionNotificationPayload typed)
            return Array.Empty<NotificationRecipient>();

        var recipients = ResolveMembers(typed.MemberIds);
        if (typed.IncludeCommission)
            recipients.AddRange(ResolveCommission(typed.EventChapterId));
        if (typed.SubscriberEventId != null)
            recipients.AddRange(ResolveSubscribers(typed.SubscriberEventId.Value));
        if (typed.UserIds != null)
            recipients.AddRange(ResolveUsers(typed.UserIds));

        return recipients
            .GroupBy(r => r.UserId)
            .Select(g => g.First())
            .ToList();
    }

    private List<NotificationRecipient> ResolveMembers(IReadOnlyList<Guid> memberIds) {
        if (memberIds.Count == 0)
            return [];

        var userIds = _db.Members
            .Where(m => memberIds.Contains(m.Id) && m.UserId != null)
            .Select(m => m.UserId!.Value)
            .Distinct()
            .ToList();
        return ResolveUsers(userIds);
    }

    private List<NotificationRecipient> ResolveUsers(IReadOnlyList<Guid> userIds) {
        if (userIds.Count == 0)
            return [];

        return _db.Users
            .Where(u => userIds.Contains(u.Id) && u.DeletedAt == null)
            .Select(u => new { u.Id, u.Email })
            .ToList()
            .Where(u => !string.IsNullOrEmpty(u.Email))
            .Select(u => new NotificationRecipient(u.Id, u.Email!))
            .ToList();
    }

    private List<NotificationRecipient> ResolveSubscribers(Guid eventId) {
        var ev = _db.Events.FirstOrDefault(e => e.Id == eventId && e.DeletedAt == null);
        if (ev == null)
            return [];

        var userIds = _db.EventSubscriptions
            .Where(s => s.EventId == eventId)
            .Select(s => s.UserId)
            .ToList()
            .Where(userId => _visibility.IsVisibleTo(ev, userId))
            .ToList();
        if (userIds.Count == 0)
            return [];

        return _db.Users
            .Where(u => userIds.Contains(u.Id) && u.DeletedAt == null)
            .Select(u => new { u.Id, u.Email })
            .ToList()
            .Where(u => !string.IsNullOrEmpty(u.Email))
            .Select(u => new NotificationRecipient(u.Id, u.Email!))
            .ToList();
    }

    private List<NotificationRecipient> ResolveCommission(Guid chapterId) {
        var userIds = AssemblyMotionCommissionUsers.UserIds(_db, chapterId);
        if (userIds.Count == 0)
            return [];

        return _db.Users
            .Where(u => userIds.Contains(u.Id) && u.DeletedAt == null)
            .Select(u => new { u.Id, u.Email })
            .ToList()
            .Select(u => new NotificationRecipient(u.Id, u.Email ?? ""))
            .ToList();
    }
}
