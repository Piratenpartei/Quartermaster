using System;
using System.Collections.Generic;
using System.Linq;
using Quartermaster.Data;
using Quartermaster.Server.Notifications;

namespace Quartermaster.Server.Events.Subscriptions;

/// <summary>Resolves the subscribed users of an <see cref="EventReminderNotificationPayload"/>; the sender already filtered them by visibility.</summary>
public class EventReminderRecipientResolver : IRecipientResolver {
    public static IReadOnlyList<string> TriggerIds { get; } = [
        NotificationTriggers.EventDeadlineReminder,
        NotificationTriggers.EventReminder
    ];

    private readonly DbContext _db;

    public EventReminderRecipientResolver(string triggerId, DbContext db) {
        TriggerId = triggerId;
        _db = db;
    }

    public string TriggerId { get; }

    public IReadOnlyList<NotificationRecipient> Resolve(object payload) {
        if (payload is not EventReminderNotificationPayload typed)
            return Array.Empty<NotificationRecipient>();
        if (typed.UserIds.Count == 0)
            return Array.Empty<NotificationRecipient>();

        var userIds = typed.UserIds.Distinct().ToList();
        return _db.Users
            .Where(u => userIds.Contains(u.Id) && u.DeletedAt == null)
            .Select(u => new { u.Id, u.Email })
            .ToList()
            .Where(u => !string.IsNullOrEmpty(u.Email))
            .Select(u => new NotificationRecipient(u.Id, u.Email!))
            .ToList();
    }
}
