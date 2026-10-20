using Quartermaster.Api.Forwarding;
using Quartermaster.Data.Forwarding;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.Forwarding;

public class ForwardingRepositoryTests : RepositoryTestBase {
    private TestDataBuilder _builder = default!;
    private RedmineConnectionRepository _connections = default!;
    private ForwardingSettingsRepository _settings = default!;
    private ForwardingCaseRepository _cases = default!;

    [Before(Test)]
    public void Setup() {
        _builder = new TestDataBuilder(Db);
        _connections = new RedmineConnectionRepository(Db);
        _settings = new ForwardingSettingsRepository(Db);
        _cases = new ForwardingCaseRepository(Db);
    }

    private static ForwardingStep Step(Guid caseId, int order, ForwardingStepState state, Guid chapterId, DateTime? nextAttempt = null) => new() {
        CaseId = caseId,
        Order = order,
        Kind = ForwardingStepKind.CreateTicket,
        TargetChapterId = chapterId,
        Purpose = ForwardingPurpose.Application,
        State = state,
        NextAttemptAt = nextAttempt,
        UpdatedAt = DateTime.UtcNow
    };

    [Test]
    public async Task Upsert_keeps_key_when_null() {
        var chapter = _builder.SeedChapter();
        _connections.Upsert(chapter.Id, "https://redmine.example.org", "secret", DateTime.UtcNow);

        _connections.Upsert(chapter.Id, "https://redmine2.example.org", null, DateTime.UtcNow);

        var connection = _connections.Get(chapter.Id)!;
        await Assert.That(connection.BaseUrl).IsEqualTo("https://redmine2.example.org");
        await Assert.That(connection.ApiKey).IsEqualTo("secret");
    }

    [Test]
    public async Task SaveChapter_replaces_targets() {
        var chapter = _builder.SeedChapter();
        var setting = new ForwardingChapterSetting { ChapterId = chapter.Id, ApplicationMode = ForwardingMode.Redmine };
        _settings.SaveChapter(setting, [
            new ForwardingTarget { ChapterId = chapter.Id, Purpose = ForwardingPurpose.Application, ProjectId = 1, TrackerId = 2 },
            new ForwardingTarget { ChapterId = chapter.Id, Purpose = ForwardingPurpose.Reduction, ProjectId = 3, TrackerId = 4 }
        ]);

        _settings.SaveChapter(setting, [
            new ForwardingTarget { ChapterId = chapter.Id, Purpose = ForwardingPurpose.Application, ProjectId = 5, TrackerId = 6 }
        ]);

        var targets = _settings.ListTargets(chapter.Id);
        await Assert.That(targets.Count).IsEqualTo(1);
        await Assert.That(targets[0].ProjectId).IsEqualTo(5);
        await Assert.That(_settings.GetChapter(chapter.Id)!.ApplicationMode).IsEqualTo(ForwardingMode.Redmine);
    }

    [Test]
    public async Task ListCasesWithDueSteps_includes_pending_due_and_in_flight() {
        var chapter = _builder.SeedChapter();
        var now = DateTime.UtcNow;
        var due = new ForwardingCase { Kind = ForwardingCaseKind.Application, CreatedAt = now };
        var later = new ForwardingCase { Kind = ForwardingCaseKind.Application, CreatedAt = now };
        var inFlight = new ForwardingCase { Kind = ForwardingCaseKind.Application, CreatedAt = now };
        var waiting = new ForwardingCase { Kind = ForwardingCaseKind.Application, CreatedAt = now };
        _cases.Create(due, [Step(due.Id, 1, ForwardingStepState.Pending, chapter.Id)]);
        _cases.Create(later, [Step(later.Id, 1, ForwardingStepState.Pending, chapter.Id, now.AddMinutes(5))]);
        _cases.Create(inFlight, [Step(inFlight.Id, 1, ForwardingStepState.InFlight, chapter.Id)]);
        _cases.Create(waiting, [Step(waiting.Id, 1, ForwardingStepState.Waiting, chapter.Id)]);

        // Tests on other threads can share this worker database, so only this test's cases are judged.
        var mine = new[] { due.Id, later.Id, inFlight.Id, waiting.Id };
        var ids = _cases.ListCasesWithDueSteps(now, 20).Where(mine.Contains);

        await Assert.That(ids.ToArray()).IsEquivalentTo(new[] { due.Id, inFlight.Id });
    }

    [Test]
    public async Task Reference_is_upper_short_id() {
        var forwardingCase = new ForwardingCase { Id = Guid.Parse("0a1b2c3d-0000-0000-0000-000000000000") };

        await Assert.That(forwardingCase.Reference).IsEqualTo("QM-0A1B2C3D");
    }
}
