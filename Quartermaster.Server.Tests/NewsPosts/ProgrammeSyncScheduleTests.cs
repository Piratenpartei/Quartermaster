using Quartermaster.Data.NewsPosts;
using Quartermaster.Server.NewsPosts.Programme;

namespace Quartermaster.Server.Tests.NewsPosts;

public class ProgrammeSyncScheduleTests {
    private static readonly DateTime Now = new(2026, 9, 23, 12, 0, 0, DateTimeKind.Utc);

    [Test]
    public async Task No_sources_is_not_due() {
        await Assert.That(ProgrammeSyncHostedService.IsSyncDue([], Now)).IsFalse();
    }

    [Test]
    public async Task All_sources_never_synced_is_due() {
        var sources = new List<ProgrammeSource> {
            new() { WikiPageTitle = "A" },
            new() { WikiPageTitle = "B" }
        };

        await Assert.That(ProgrammeSyncHostedService.IsSyncDue(sources, Now)).IsTrue();
    }

    [Test]
    public async Task One_source_synced_recently_and_another_never_synced_is_not_due() {
        var sources = new List<ProgrammeSource> {
            new() { WikiPageTitle = "A", LastSyncedAt = Now.AddDays(-1) },
            new() { WikiPageTitle = "B" }
        };

        await Assert.That(ProgrammeSyncHostedService.IsSyncDue(sources, Now)).IsFalse();
    }

    [Test]
    public async Task Most_recent_success_over_a_week_ago_is_due() {
        var sources = new List<ProgrammeSource> {
            new() { WikiPageTitle = "A", LastSyncedAt = Now.AddDays(-8) },
            new() { WikiPageTitle = "B", LastSyncedAt = Now.AddDays(-10) }
        };

        await Assert.That(ProgrammeSyncHostedService.IsSyncDue(sources, Now)).IsTrue();
    }
}
