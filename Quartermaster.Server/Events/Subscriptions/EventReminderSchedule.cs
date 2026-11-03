using System;
using System.Collections.Generic;
using System.Linq;
using Quartermaster.Api.AssemblyMotions;
using Quartermaster.Data.AssemblyMotions;
using Quartermaster.Data.Events;
using Quartermaster.Server.AssemblyMotions;

namespace Quartermaster.Server.Events.Subscriptions;

/// <summary>The reminders an event's subscribers get, from its motion deadlines and its start.</summary>
public static class EventReminderSchedule {
    public const string CompetingSuffix = " (Konkurrenzanträge)";
    private static readonly TimeSpan DeadlineLead = TimeSpan.FromHours(36);
    private static readonly TimeSpan DayBeforeLead = TimeSpan.FromHours(24);
    private static readonly TimeSpan SoonLead = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan DayBeforeSendTimeWithoutStart = TimeSpan.FromHours(9);

    public static IReadOnlyList<PlannedReminder> For(Event ev, AssemblyMotionPortal? portal, IReadOnlyList<AssemblyMotionType> types) {
        var reminders = new List<PlannedReminder>();
        if (portal != null)
            reminders.AddRange(DeadlineReminders(portal, types));
        reminders.AddRange(StartReminders(ev));
        return reminders;
    }

    /// <summary>The start as UTC instant; null without a date or without a start time.</summary>
    public static DateTime? StartUtc(Event ev) {
        if (ev.EventDate == null || ev.StartTime == null)
            return null;
        return BerlinTime.ToUtcLenient(ev.EventDate.Value.Date + ev.StartTime.Value);
    }

    /// <summary>Until when the event counts as upcoming: its start, or without a start time the end of its day (Berlin).</summary>
    public static DateTime? UpcomingUntilUtc(Event ev) {
        if (ev.EventDate == null)
            return null;
        return StartUtc(ev) ?? BerlinTime.ToUtcLenient(ev.EventDate.Value.Date.AddDays(1));
    }

    private static IEnumerable<PlannedReminder> DeadlineReminders(AssemblyMotionPortal portal, IReadOnlyList<AssemblyMotionType> types) {
        var entries = new List<(DateTime Deadline, string Name)>();
        foreach (var type in types.OrderBy(t => t.SortOrder)) {
            entries.Add((AssemblyMotionDeadlines.Effective(portal, type, false), type.Name));
            if (type.CompetingDeadline != null)
                entries.Add((AssemblyMotionDeadlines.Effective(portal, type, true), type.Name + CompetingSuffix));
        }

        return entries
            .GroupBy(e => e.Deadline)
            .OrderBy(g => g.Key)
            .Select(g => new PlannedReminder(
                EventReminderKind.Deadline,
                g.Key,
                g.Key - DeadlineLead,
                string.Join(", ", g.Select(e => e.Name)),
                g.Key));
    }

    private static IEnumerable<PlannedReminder> StartReminders(Event ev) {
        if (ev.EventDate == null)
            yield break;

        var start = StartUtc(ev);
        if (start == null) {
            var date = ev.EventDate.Value.Date;
            var midnight = BerlinTime.ToUtcLenient(date);
            yield return new PlannedReminder(
                EventReminderKind.StartDayBefore,
                midnight,
                BerlinTime.ToUtcLenient(date.AddDays(-1) + DayBeforeSendTimeWithoutStart),
                "",
                midnight);
            yield break;
        }

        var soonFrom = start.Value - SoonLead;
        yield return new PlannedReminder(EventReminderKind.StartDayBefore, start.Value, start.Value - DayBeforeLead, "", soonFrom);
        yield return new PlannedReminder(EventReminderKind.StartSoon, start.Value, soonFrom, "", start.Value);
    }
}
