using System;
using System.Threading.Tasks;
using Quartermaster.Blazor.Components.AssemblyMotions;

namespace Quartermaster.Server.Tests.AssemblyMotions;

public class AssemblyMotionDeadlineDisplayTests {
    [Test]
    public async Task Midnight_deadline_names_last_second_of_previous_day() {
        // 28.11.2026 00:00 Berlin is winter time: 27.11.2026 23:00 UTC.
        var deadline = new DateTimeOffset(2026, 11, 27, 23, 0, 0, TimeSpan.Zero);

        await Assert.That(AssemblyMotionDisplay.Berlin(deadline)).IsEqualTo("28.11.2026 00:00");
        await Assert.That(AssemblyMotionDisplay.LastSecondBefore(deadline)).IsEqualTo("27.11.2026 23:59:59");
    }

    [Test]
    public async Task Midnight_in_summer_time_is_detected_too() {
        // 01.07.2026 00:00 Berlin is summer time: 30.06.2026 22:00 UTC.
        var deadline = new DateTimeOffset(2026, 6, 30, 22, 0, 0, TimeSpan.Zero);

        await Assert.That(AssemblyMotionDisplay.LastSecondBefore(deadline)).IsEqualTo("30.06.2026 23:59:59");
    }

    [Test]
    public async Task Other_deadlines_get_no_hint() {
        var deadline = new DateTimeOffset(2026, 11, 27, 22, 59, 0, TimeSpan.Zero);

        await Assert.That(AssemblyMotionDisplay.LastSecondBefore(deadline)).IsNull();
    }
}
