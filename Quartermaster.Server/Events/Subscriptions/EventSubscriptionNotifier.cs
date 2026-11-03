using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Quartermaster.Api.AssemblyMotions;
using Quartermaster.Api.Events;
using Quartermaster.Data.Events;
using Quartermaster.Server.AssemblyMotions.Notifications;
using Quartermaster.Server.Notifications;

namespace Quartermaster.Server.Events.Subscriptions;

/// <summary>Builds and enqueues the notifications for event subscribers; carries the event, never names of people.</summary>
public class EventSubscriptionNotifier {
    public const string OpenDate = "noch offen";
    private const string EntityType = "Event";

    private readonly INotificationDispatchQueue _queue;
    private readonly ILogger<EventSubscriptionNotifier> _logger;

    public EventSubscriptionNotifier(INotificationDispatchQueue queue, ILogger<EventSubscriptionNotifier> logger) {
        _queue = queue;
        _logger = logger;
    }

    public void DeadlineReminder(Event ev, IReadOnlyList<Guid> userIds, DateTime deadlineUtc, string typeNames) {
        Safely(nameof(DeadlineReminder), () => {
            var berlin = BerlinTime.FromUtc(deadlineUtc);
            var model = BaseModel(ev, $"/Events/{ev.Id}/Motions") with {
                Date = berlin.ToString("dd.MM.yyyy"),
                Time = berlin.ToString("HH:mm"),
                TypeNames = AssemblyMotionNotifier.EscapeMarkdown(typeNames)
            };
            Enqueue(NotificationTriggers.EventDeadlineReminder, ev, userIds, model);
        });
    }

    public void StartReminder(Event ev, IReadOnlyList<Guid> userIds, EventReminderKind kind, DateTime nowUtc) {
        Safely(nameof(StartReminder), () => {
            var soon = kind == EventReminderKind.StartSoon;
            var model = WithSchedule(BaseModel(ev, $"/Events/{ev.Id}"), ev) with {
                Kind = soon ? EventReminderNotificationModel.KindSoon : DayBeforeKind(ev, nowUtc),
                MeetingUrl = soon ? ev.MeetingUrl ?? "" : ""
            };
            Enqueue(NotificationTriggers.EventReminder, ev, userIds, model);
        });
    }

    public void ScheduleChanged(Event ev, IReadOnlyList<Guid> userIds) {
        Safely(nameof(ScheduleChanged), () => {
            var model = WithSchedule(BaseModel(ev, $"/Events/{ev.Id}"), ev) with {
                Kind = EventReminderNotificationModel.KindChanged
            };
            Enqueue(NotificationTriggers.EventReminder, ev, userIds, model);
        });
    }

    /// <summary>A day-before reminder sent late, after the start moved to today, must not say "morgen".</summary>
    private static string DayBeforeKind(Event ev, DateTime nowUtc) {
        if (ev.EventDate != null && ev.EventDate.Value.Date == BerlinTime.FromUtc(nowUtc).Date)
            return EventReminderNotificationModel.KindToday;
        return EventReminderNotificationModel.KindDayBefore;
    }

    private static EventReminderNotificationModel BaseModel(Event ev, string path) {
        return new EventReminderNotificationModel {
            Name = ev.PublicName,
            NameEscaped = AssemblyMotionNotifier.EscapeMarkdown(ev.PublicName),
            Path = path
        };
    }

    private static EventReminderNotificationModel WithSchedule(EventReminderNotificationModel model, Event ev) {
        if (ev.EventDate == null)
            return model with { Date = OpenDate, Time = "" };

        return model with {
            Date = ev.EventDate.Value.ToString("dd.MM.yyyy"),
            Time = ev.StartTime == null ? "" : ev.StartTime.Value.ToString(@"hh\:mm")
        };
    }

    private void Enqueue(string triggerId, Event ev, IReadOnlyList<Guid> userIds, EventReminderNotificationModel model) {
        if (userIds.Count == 0)
            return;

        _queue.Enqueue(new NotificationDispatchRequest(
            triggerId,
            new EventReminderNotificationPayload(ev.Id, userIds),
            _ => new Dictionary<string, object> { ["event"] = model },
            SourceEntityType: EntityType,
            SourceEntityId: ev.Id));
    }

    private void Safely(string notification, Action action) {
        try {
            action();
        } catch (Exception ex) {
            _logger.LogError(ex, "Event subscription notification {Notification} failed: {ex}", notification, ex);
        }
    }
}
