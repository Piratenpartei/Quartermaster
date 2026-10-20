using Quartermaster.Api.Forwarding;
using Quartermaster.Data.Chapters;
using Quartermaster.Data.Forwarding;
using Quartermaster.Server.Forwarding;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.Forwarding;

public class ForwardingSettingsResolverTests : RepositoryTestBase {
    private TestDataBuilder _builder = default!;
    private ForwardingSettingsRepository _settings = default!;
    private RedmineConnectionRepository _connections = default!;
    private ForwardingSettingsResolver _resolver = default!;
    private Chapter _federal = default!;
    private Chapter _state = default!;
    private Chapter _district = default!;

    [Before(Test)]
    public void Setup() {
        _builder = new TestDataBuilder(Db);
        _settings = new ForwardingSettingsRepository(Db);
        _connections = new RedmineConnectionRepository(Db);
        _resolver = new ForwardingSettingsResolver(new ChapterRepository(Db, AuditLog), _settings, _connections);
        _federal = _builder.SeedChapter("Bund " + Guid.NewGuid().ToString("N")[..6]);
        _state = _builder.SeedChapter("Land", parentChapterId: _federal.Id);
        _district = _builder.SeedChapter("Kreis", parentChapterId: _state.Id);
    }

    [Test]
    public async Task State_chapter_is_child_of_root() {
        await Assert.That(_resolver.StateChapterOf(_district.Id)).IsEqualTo(_state.Id);
        await Assert.That(_resolver.StateChapterOf(_state.Id)).IsEqualTo(_state.Id);
        await Assert.That(_resolver.StateChapterOf(_federal.Id)).IsNull();
        await Assert.That(_resolver.StateChapterOf(null)).IsNull();
    }

    [Test]
    public async Task Handling_chapter_skips_root() {
        _settings.SaveChapter(new ForwardingChapterSetting { ChapterId = _federal.Id, ApplicationMode = ForwardingMode.Quartermaster }, []);

        var none = _resolver.HandlingChapterFor(_district.Id);
        _settings.SaveChapter(new ForwardingChapterSetting { ChapterId = _state.Id, ApplicationMode = ForwardingMode.Redmine }, []);
        var state = _resolver.HandlingChapterFor(_district.Id);

        await Assert.That(none.ChapterId).IsNull();
        await Assert.That(state.ChapterId).IsEqualTo(_state.Id);
        await Assert.That(state.Mode).IsEqualTo(ForwardingMode.Redmine);
    }

    [Test]
    public async Task Connection_inherits_up_to_root() {
        _connections.Upsert(_federal.Id, "https://bund.invalid", "k", DateTime.UtcNow);

        var inherited = _resolver.ConnectionFor(_district.Id);
        _connections.Upsert(_state.Id, "https://land.invalid", "k", DateTime.UtcNow);
        var own = _resolver.ConnectionFor(_district.Id);

        await Assert.That(inherited!.BaseUrl).IsEqualTo("https://bund.invalid");
        await Assert.That(own!.BaseUrl).IsEqualTo("https://land.invalid");
    }

    [Test]
    public async Task Failure_email_inherits() {
        _settings.SaveChapter(new ForwardingChapterSetting { ChapterId = _federal.Id, FailureEmail = "bund@example.org" }, []);
        _settings.SaveChapter(new ForwardingChapterSetting { ChapterId = _state.Id, ApplicationMode = ForwardingMode.Redmine }, []);

        await Assert.That(_resolver.FailureEmailFor(_district.Id)).IsEqualTo("bund@example.org");
    }

    [Test]
    public async Task Build_collects_modes_for_member_chapter() {
        _settings.SaveChapter(new ForwardingChapterSetting { ChapterId = _federal.Id, ReductionMode = ForwardingMode.Redmine, ApplicationMode = ForwardingMode.Quartermaster }, []);
        _settings.SaveChapter(new ForwardingChapterSetting { ChapterId = _state.Id, ReductionMode = ForwardingMode.Quartermaster }, []);

        var ctx = _resolver.Build(_district.Id);

        await Assert.That(ctx.FederalId).IsEqualTo(_federal.Id);
        await Assert.That(ctx.StateId).IsEqualTo(_state.Id);
        await Assert.That(ctx.FederalReductionMode).IsEqualTo(ForwardingMode.Redmine);
        await Assert.That(ctx.FederalApplicationMode).IsEqualTo(ForwardingMode.Quartermaster);
        await Assert.That(ctx.StateReductionMode).IsEqualTo(ForwardingMode.Quartermaster);
    }
    [Test]
    public async Task Deleted_chapter_still_resolves_connection_and_failure_email() {
        _connections.Upsert(_federal.Id, "https://bund.invalid", "k", DateTime.UtcNow);
        _settings.SaveChapter(new ForwardingChapterSetting { ChapterId = _state.Id, FailureEmail = "land@example.org" }, []);
        _connections.Upsert(_district.Id, "https://kreis.invalid", "k", DateTime.UtcNow);
        var chapters = new ChapterRepository(Db, AuditLog);
        chapters.SoftDelete(_district.Id);

        await Assert.That(_resolver.ConnectionFor(_district.Id)!.BaseUrl).IsEqualTo("https://kreis.invalid");
        await Assert.That(_resolver.FailureEmailFor(_district.Id)).IsEqualTo("land@example.org");
    }
}
