using System;
using System.Linq;
using Quartermaster.Api.Events;
using Quartermaster.Data.Events;

namespace Quartermaster.Server.Events.Subscriptions;

/// <summary>Tells the subscribers of an upcoming event that its date or start time changed.</summary>
public class EventScheduleChangeNotice {
    private readonly EventSubscriptionRepository _subscriptions;
    private readonly EventUserVisibility _visibility;
    private readonly EventSubscriptionNotifier _notifier;

    public EventScheduleChangeNotice(EventSubscriptionRepository subscriptions, EventUserVisibility visibility, EventSubscriptionNotifier notifier) {
        _subscriptions = subscriptions;
        _visibility = visibility;
        _notifier = notifier;
    }

    public void NotifyIfChanged(Event before, Event after, DateTime nowUtc) {
        if (!IsNoticeDue(before, after, nowUtc))
            return;

        var userIds = _subscriptions.ListForEvent(after.Id)
            .Where(s => _visibility.IsVisibleTo(after, s.UserId))
            .Select(s => s.UserId)
            .ToList();
        _notifier.ScheduleChanged(after, userIds);
    }

    private static bool IsNoticeDue(Event before, Event after, DateTime nowUtc) {
        if (before.EventDate == after.EventDate && before.StartTime == after.StartTime)
            return false;
        if (after.DeletedAt != null)
            return false;
        if (after.Status == EventStatus.Completed || after.Status == EventStatus.Archived)
            return false;

        var upcomingUntil = EventReminderSchedule.UpcomingUntilUtc(after.EventDate == null ? before : after);
        return upcomingUntil != null && upcomingUntil.Value > nowUtc;
    }
}
