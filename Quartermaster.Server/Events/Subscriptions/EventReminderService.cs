using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using Quartermaster.Data;
using Quartermaster.Data.AssemblyMotions;
using Quartermaster.Data.Events;

namespace Quartermaster.Server.Events.Subscriptions;

/// <summary>Sends the due reminders of all active subscriptions, each once per (subscription, kind, target).</summary>
public class EventReminderService {
    private readonly EventSubscriptionRepository _subscriptions;
    private readonly DbContext _db;
    private readonly EventUserVisibility _visibility;
    private readonly EventSubscriptionNotifier _notifier;
    private readonly ILogger<EventReminderService> _logger;

    public EventReminderService(
        EventSubscriptionRepository subscriptions,
        DbContext db,
        EventUserVisibility visibility,
        EventSubscriptionNotifier notifier,
        ILogger<EventReminderService> logger) {
        _subscriptions = subscriptions;
        _db = db;
        _visibility = visibility;
        _notifier = notifier;
        _logger = logger;
    }

    /// <summary>Returns the number of reminders enqueued, one per user and reminder. A failing event is logged and skipped.</summary>
    public int SendDue(DateTime nowUtc) {
        var sent = 0;
        foreach (var group in _subscriptions.ListActive().GroupBy(s => s.EventId)) {
            try {
                sent += SendDueForEvent(group.Key, group.ToList(), nowUtc);
            } catch (Exception ex) {
                _logger.LogError(ex, "Event reminders for event {EventId} failed: {ex}", group.Key, ex);
            }
        }
        return sent;
    }

    private int SendDueForEvent(Guid eventId, IReadOnlyList<EventSubscription> subscriptions, DateTime nowUtc) {
        var ev = _db.Events.FirstOrDefault(e => e.Id == eventId);
        if (ev == null)
            return 0;

        var portal = _db.AssemblyMotionPortals.FirstOrDefault(p => p.EventId == ev.Id);
        var types = portal == null
            ? new List<AssemblyMotionType>()
            : _db.AssemblyMotionTypes.Where(t => t.PortalId == portal.Id).ToList();
        var due = EventReminderSchedule.For(ev, portal, types)
            .Where(r => r.IsDue(nowUtc))
            .ToList();
        if (due.Count == 0)
            return 0;

        var visible = subscriptions
            .Where(s => _visibility.IsVisibleTo(ev, s.UserId))
            .ToList();
        var delivered = _subscriptions.ListDeliveries(visible.Select(s => s.Id).ToList());

        var sent = 0;
        foreach (var reminder in due) {
            var userIds = Deliver(reminder, visible, delivered, nowUtc);
            Notify(ev, reminder, userIds, nowUtc);
            sent += userIds.Count;
        }
        return sent;
    }

    private List<Guid> Deliver(
        PlannedReminder reminder,
        IReadOnlyList<EventSubscription> subscriptions,
        HashSet<(Guid SubscriptionId, EventReminderKind Kind, DateTime TargetAt)> delivered,
        DateTime nowUtc) {
        var userIds = new List<Guid>();
        foreach (var subscription in subscriptions) {
            if (reminder.SendAt < subscription.CreatedAt)
                continue;
            if (delivered.Contains((subscription.Id, reminder.Kind, reminder.TargetAt)))
                continue;
            if (_subscriptions.TryRecordDelivery(subscription.Id, reminder.Kind, reminder.TargetAt, nowUtc))
                userIds.Add(subscription.UserId);
        }
        return userIds;
    }

    private void Notify(Event ev, PlannedReminder reminder, IReadOnlyList<Guid> userIds, DateTime nowUtc) {
        if (userIds.Count == 0)
            return;

        if (reminder.Kind == EventReminderKind.Deadline) {
            _notifier.DeadlineReminder(ev, userIds, reminder.TargetAt, reminder.TypeNames);
        } else {
            _notifier.StartReminder(ev, userIds, reminder.Kind, nowUtc);
        }
    }
}
