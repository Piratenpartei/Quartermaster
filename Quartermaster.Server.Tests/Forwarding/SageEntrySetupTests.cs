using Quartermaster.Api.Forwarding;
using Quartermaster.Data.Chapters;
using Quartermaster.Data.Forwarding;
using Quartermaster.Data.Options;
using Quartermaster.Server.Forwarding;
using Quartermaster.Server.Forwarding.SageEntry;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.Forwarding;

public class SageEntrySetupTests : RepositoryTestBase {
    private OptionRepository _options = default!;
    private ForwardingSettingsRepository _settings = default!;
    private RedmineConnectionRepository _connections = default!;
    private SageEntrySetup _setup = default!;
    private Chapter _federal = default!;

    [Before(Test)]
    public void Setup() {
        var builder = new TestDataBuilder(Db);
        _options = new OptionRepository(Db, AuditLog);
        _settings = new ForwardingSettingsRepository(Db);
        _connections = new RedmineConnectionRepository(Db);
        var chapters = new ChapterRepository(Db, AuditLog);
        _setup = new SageEntrySetup(_options, chapters, _settings, new ForwardingSettingsResolver(chapters, _settings, _connections));
        _federal = builder.SeedChapter("Bund " + Guid.NewGuid().ToString("N")[..6]);
    }

    private void SetUp(int? assignee, int? doneStatus, bool awaitReassignment, bool withTarget = true) {
        _options.SetValue(ForwardingOptionKeys.MembershipApplicationEnabled, null, "true");
        _connections.Upsert(_federal.Id, "https://redmine.invalid", "secret", DateTime.UtcNow);
        _settings.SaveChapter(new ForwardingChapterSetting { ChapterId = _federal.Id, ApplicationMode = ForwardingMode.Redmine }, withTarget ? [
            new ForwardingTarget {
                ChapterId = _federal.Id, Purpose = ForwardingPurpose.Application, ProjectId = 1, TrackerId = 2,
                SageAssigneeId = assignee, SageDoneStatusId = doneStatus, SageAwaitReassignment = awaitReassignment
            }
        ] : []);
    }

    [Test]
    public async Task Nothing_set_up_lists_every_gap() {
        await Assert.That(_setup.Missing()).IsEquivalentTo([
            SageSetupGap.ForwardingSwitch, SageSetupGap.FederalConnection, SageSetupGap.FederalTarget, SageSetupGap.SageGroup, SageSetupGap.DoneSignal
        ]);
        await Assert.That(_setup.IsComplete()).IsFalse();
    }

    [Test]
    public async Task Complete_with_status_only() {
        SetUp(12, 5, false);

        await Assert.That(_setup.Missing()).IsEmpty();
        await Assert.That(_setup.IsComplete()).IsTrue();
    }

    [Test]
    public async Task Complete_with_reassignment_only() {
        SetUp(12, null, true);

        await Assert.That(_setup.Missing()).IsEmpty();
        await Assert.That(_setup.IsComplete()).IsTrue();
    }

    [Test]
    public async Task Group_without_signal_is_incomplete() {
        SetUp(12, null, false);

        await Assert.That(_setup.Missing()).IsEquivalentTo([SageSetupGap.DoneSignal]);
    }

    [Test]
    public async Task Check_interval_defaults_and_clamps() {
        await Assert.That(_setup.CheckInterval()).IsEqualTo(TimeSpan.FromMinutes(15));

        _options.SetValue(ForwardingOptionKeys.SageCheckMinutes, null, "2");
        await Assert.That(_setup.CheckInterval()).IsEqualTo(TimeSpan.FromMinutes(5));

        _options.SetValue(ForwardingOptionKeys.SageCheckMinutes, null, "30");
        await Assert.That(_setup.CheckInterval()).IsEqualTo(TimeSpan.FromMinutes(30));
    }
}
