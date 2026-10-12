using System;
using Quartermaster.Api.AssemblyMotions;
using Quartermaster.Data.AssemblyMotions;
using Quartermaster.Server.AssemblyMotions;

namespace Quartermaster.Server.Tests.AssemblyMotions;

public class AssemblyMotionDeadlinesTests {
    [Test]
    public async Task BerlinTime_round_trips_across_dst() {
        var winterUtc = BerlinTime.ToUtc(new DateTime(2027, 3, 12, 23, 59, 0));
        await Assert.That(winterUtc).IsEqualTo(new DateTime(2027, 3, 12, 22, 59, 0));
        await Assert.That(winterUtc.Kind).IsEqualTo(DateTimeKind.Utc);

        var summerUtc = BerlinTime.ToUtc(new DateTime(2027, 7, 12, 23, 59, 0));
        await Assert.That(summerUtc).IsEqualTo(new DateTime(2027, 7, 12, 21, 59, 0));
        await Assert.That(summerUtc.Kind).IsEqualTo(DateTimeKind.Utc);

        var winterBerlin = BerlinTime.FromUtc(new DateTime(2027, 3, 12, 22, 59, 0, DateTimeKind.Utc));
        await Assert.That(winterBerlin).IsEqualTo(new DateTime(2027, 3, 12, 23, 59, 0));
        await Assert.That(winterBerlin.Kind).IsEqualTo(DateTimeKind.Unspecified);

        var summerBerlin = BerlinTime.FromUtc(new DateTime(2027, 7, 12, 21, 59, 0, DateTimeKind.Utc));
        await Assert.That(summerBerlin).IsEqualTo(new DateTime(2027, 7, 12, 23, 59, 0));
        await Assert.That(summerBerlin.Kind).IsEqualTo(DateTimeKind.Unspecified);
    }

    [Test]
    public async Task FormatDeadline_uses_berlin_time() {
        var formatted = BerlinTime.FormatDeadline(new DateTime(2027, 7, 12, 21, 59, 0, DateTimeKind.Utc));
        await Assert.That(formatted).IsEqualTo("12.07. um 23:59 Uhr");
    }

    [Test]
    [Arguments("SÄA", 7, "SÄA007")]
    [Arguments("SO", 123, "SO123")]
    public async Task Format_pads_to_three_digits(string typeCode, int number, string expected) {
        await Assert.That(AssemblyMotionNumber.Format(typeCode, number)).IsEqualTo(expected);
    }

    [Test]
    public async Task Effective_prefers_competing_deadline_only_when_flagged() {
        var portal = BuildPortal(new DateTime(2027, 5, 1, 0, 0, 0, DateTimeKind.Utc));
        var type = BuildType(
            deadline: new DateTime(2027, 4, 1, 0, 0, 0, DateTimeKind.Utc),
            competingDeadline: new DateTime(2027, 4, 10, 0, 0, 0, DateTimeKind.Utc));

        await Assert.That(AssemblyMotionDeadlines.Effective(portal, type, isCompetingMotion: true)).IsEqualTo(type.CompetingDeadline!.Value);
        await Assert.That(AssemblyMotionDeadlines.Effective(portal, type, isCompetingMotion: false)).IsEqualTo(type.Deadline!.Value);
    }

    [Test]
    public async Task Effective_falls_back_to_portal_close() {
        var portal = BuildPortal(new DateTime(2027, 5, 1, 0, 0, 0, DateTimeKind.Utc));
        var type = BuildType(deadline: null, competingDeadline: null);

        await Assert.That(AssemblyMotionDeadlines.Effective(portal, type, isCompetingMotion: false)).IsEqualTo(portal.ClosesAt);
        await Assert.That(AssemblyMotionDeadlines.Effective(portal, type, isCompetingMotion: true)).IsEqualTo(portal.ClosesAt);
    }

    [Test]
    public async Task Effective_never_after_portal_close() {
        var portal = BuildPortal(new DateTime(2027, 5, 1, 0, 0, 0, DateTimeKind.Utc));
        var type = BuildType(
            deadline: new DateTime(2027, 5, 10, 0, 0, 0, DateTimeKind.Utc),
            competingDeadline: null);

        await Assert.That(AssemblyMotionDeadlines.Effective(portal, type, isCompetingMotion: false)).IsEqualTo(portal.ClosesAt);
    }

    [Test]
    public async Task IsPast_is_exclusive_at_the_deadline() {
        var deadline = new DateTime(2027, 5, 1, 0, 0, 0, DateTimeKind.Utc);

        await Assert.That(AssemblyMotionDeadlines.IsPast(deadline, deadline)).IsFalse();
        await Assert.That(AssemblyMotionDeadlines.IsPast(deadline, deadline.AddSeconds(1))).IsTrue();
    }

    private static AssemblyMotionPortal BuildPortal(DateTime closesAt) {
        return new AssemblyMotionPortal {
            ClosesAt = closesAt
        };
    }

    private static AssemblyMotionType BuildType(DateTime? deadline, DateTime? competingDeadline) {
        return new AssemblyMotionType {
            Deadline = deadline,
            CompetingDeadline = competingDeadline
        };
    }
}
