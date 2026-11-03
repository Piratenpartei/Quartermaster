using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Quartermaster.Data.AssemblyMotions;
using Quartermaster.Data.Events;
using Quartermaster.Server.Events.Subscriptions;

namespace Quartermaster.Server.Tests.Events;

public class EventReminderScheduleTests {
    private static readonly DateTime PortalCloses = new(2026, 11, 20, 22, 59, 0, DateTimeKind.Utc);

    private static Event NewEvent(DateTime? date = null, TimeSpan? startTime = null) => new() {
        PublicName = "Bundesparteitag",
        EventDate = date,
        StartTime = startTime
    };

    private static AssemblyMotionPortal NewPortal() => new() {
        OpensAt = PortalCloses.AddDays(-30),
        ClosesAt = PortalCloses
    };

    private static AssemblyMotionType NewType(string name, int sortOrder, DateTime? deadline = null, DateTime? competingDeadline = null) => new() {
        Name = name,
        SortOrder = sortOrder,
        Deadline = deadline,
        CompetingDeadline = competingDeadline
    };

    private static List<PlannedReminder> Deadlines(IReadOnlyList<PlannedReminder> reminders)
        => reminders.Where(r => r.Kind == EventReminderKind.Deadline).OrderBy(r => r.TargetAt).ToList();

    [Test]
    public async Task Deadline_reminder_is_36_hours_before() {
        var deadline = new DateTime(2026, 11, 10, 22, 59, 0, DateTimeKind.Utc);

        var reminders = EventReminderSchedule.For(NewEvent(), NewPortal(), [NewType("Satzungsänderungsantrag", 1, deadline)]);

        var single = Deadlines(reminders).Single();
        await Assert.That(single.TargetAt).IsEqualTo(deadline);
        await Assert.That(single.SendAt).IsEqualTo(deadline.AddHours(-36));
        await Assert.That(single.TypeNames).IsEqualTo("Satzungsänderungsantrag");
    }

    [Test]
    public async Task Shared_deadline_is_one_reminder_with_both_types() {
        var deadline = new DateTime(2026, 11, 10, 22, 59, 0, DateTimeKind.Utc);
        var types = new[] {
            NewType("Programmantrag", 2, deadline),
            NewType("Satzungsänderungsantrag", 1, deadline),
            NewType("Sonstiger Antrag", 3, null, deadline.AddDays(5))
        };

        var deadlines = Deadlines(EventReminderSchedule.For(NewEvent(), NewPortal(), types));

        await Assert.That(deadlines.Count).IsEqualTo(3);
        await Assert.That(deadlines[0].TargetAt).IsEqualTo(deadline);
        await Assert.That(deadlines[0].TypeNames).IsEqualTo("Satzungsänderungsantrag, Programmantrag");
    }

    [Test]
    public async Task Competing_deadline_is_named_separately() {
        var deadline = new DateTime(2026, 11, 10, 22, 59, 0, DateTimeKind.Utc);
        var competing = new DateTime(2026, 11, 15, 22, 59, 0, DateTimeKind.Utc);

        var deadlines = Deadlines(EventReminderSchedule.For(NewEvent(), NewPortal(), [NewType("Programmantrag", 1, deadline, competing)]));

        await Assert.That(deadlines.Count).IsEqualTo(2);
        await Assert.That(deadlines[1].TargetAt).IsEqualTo(competing);
        await Assert.That(deadlines[1].TypeNames).IsEqualTo("Programmantrag (Konkurrenzanträge)");
    }

    [Test]
    public async Task Deadline_is_capped_at_portal_close() {
        var types = new[] {
            NewType("Ohne Frist", 1),
            NewType("Zu spät", 2, PortalCloses.AddDays(3))
        };

        var single = Deadlines(EventReminderSchedule.For(NewEvent(), NewPortal(), types)).Single();

        await Assert.That(single.TargetAt).IsEqualTo(PortalCloses);
        await Assert.That(single.TypeNames).IsEqualTo("Ohne Frist, Zu spät");
    }

    [Test]
    public async Task No_portal_no_deadline_reminders() {
        var reminders = EventReminderSchedule.For(NewEvent(), null, []);

        await Assert.That(reminders.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Start_reminders_with_start_time() {
        var ev = NewEvent(new DateTime(2026, 11, 28), new TimeSpan(19, 0, 0));
        var start = new DateTime(2026, 11, 28, 18, 0, 0, DateTimeKind.Utc);

        var reminders = EventReminderSchedule.For(ev, null, []);

        var dayBefore = reminders.Single(r => r.Kind == EventReminderKind.StartDayBefore);
        var soon = reminders.Single(r => r.Kind == EventReminderKind.StartSoon);
        await Assert.That(dayBefore.TargetAt).IsEqualTo(start);
        await Assert.That(dayBefore.SendAt).IsEqualTo(start.AddHours(-24));
        await Assert.That(soon.TargetAt).IsEqualTo(start);
        await Assert.That(soon.SendAt).IsEqualTo(start.AddMinutes(-30));
        await Assert.That(EventReminderSchedule.StartUtc(ev)).IsEqualTo(start);
    }

    [Test]
    public async Task Day_before_without_start_time_is_nine_berlin_across_dst() {
        var ev = NewEvent(new DateTime(2026, 10, 26));

        var reminders = EventReminderSchedule.For(ev, null, []);

        var dayBefore = reminders.Single();
        await Assert.That(dayBefore.Kind).IsEqualTo(EventReminderKind.StartDayBefore);
        await Assert.That(dayBefore.SendAt).IsEqualTo(new DateTime(2026, 10, 25, 8, 0, 0, DateTimeKind.Utc));
        await Assert.That(dayBefore.TargetAt).IsEqualTo(new DateTime(2026, 10, 25, 23, 0, 0, DateTimeKind.Utc));
        await Assert.That(EventReminderSchedule.StartUtc(ev)).IsNull();
    }

    [Test]
    public async Task Day_before_with_start_time_is_24_real_hours_across_dst() {
        var ev = NewEvent(new DateTime(2026, 10, 25), new TimeSpan(19, 0, 0));
        var start = new DateTime(2026, 10, 25, 18, 0, 0, DateTimeKind.Utc);

        var dayBefore = EventReminderSchedule.For(ev, null, []).Single(r => r.Kind == EventReminderKind.StartDayBefore);

        await Assert.That(dayBefore.SendAt).IsEqualTo(start.AddHours(-24));
    }

    [Test]
    public async Task No_date_no_start_reminders() {
        var reminders = EventReminderSchedule.For(NewEvent(null, new TimeSpan(19, 0, 0)), null, []);

        await Assert.That(reminders.Count).IsEqualTo(0);
        await Assert.That(EventReminderSchedule.UpcomingUntilUtc(NewEvent())).IsNull();
    }

    [Test]
    public async Task UpcomingUntil_without_start_time_is_next_midnight() {
        var ev = NewEvent(new DateTime(2026, 11, 28));

        await Assert.That(EventReminderSchedule.UpcomingUntilUtc(ev)).IsEqualTo(new DateTime(2026, 11, 28, 23, 0, 0, DateTimeKind.Utc));
    }

    [Test]
    public async Task UpcomingUntil_with_start_time_is_start() {
        var ev = NewEvent(new DateTime(2026, 11, 28), new TimeSpan(19, 0, 0));

        await Assert.That(EventReminderSchedule.UpcomingUntilUtc(ev)).IsEqualTo(new DateTime(2026, 11, 28, 18, 0, 0, DateTimeKind.Utc));
    }

    [Test]
    public async Task Start_in_spring_forward_gap_moves_one_hour() {
        var ev = NewEvent(new DateTime(2027, 3, 28), new TimeSpan(2, 30, 0));

        var start = EventReminderSchedule.StartUtc(ev);

        await Assert.That(start).IsEqualTo(new DateTime(2027, 3, 28, 1, 30, 0, DateTimeKind.Utc));
        await Assert.That(EventReminderSchedule.For(ev, null, []).Count).IsEqualTo(2);
    }
}
