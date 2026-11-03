using System;
using Quartermaster.Data.Events;

namespace Quartermaster.Server.Events.Subscriptions;

/// <param name="TargetAt">The deadline or start the reminder is about; a moved time makes a new reminder.</param>
/// <param name="TypeNames">The motion types whose deadline it is; empty for start reminders.</param>
/// <param name="DueUntil">The reminder is due while now lies in [<paramref name="SendAt"/>, DueUntil); the day-before reminder ends where the 30-minute one begins.</param>
public record PlannedReminder(EventReminderKind Kind, DateTime TargetAt, DateTime SendAt, string TypeNames, DateTime DueUntil) {
    public bool IsDue(DateTime nowUtc) {
        return SendAt <= nowUtc && nowUtc < DueUntil;
    }
}
