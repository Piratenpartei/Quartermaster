using Microsoft.Extensions.Logging.Abstractions;
using Quartermaster.Data.NewsPosts;
using Quartermaster.Server.NewsPosts.Programme;
using Quartermaster.Server.Tests.Infrastructure;
using Quartermaster.Server.Tests.NewsPosts.Fakes;

namespace Quartermaster.Server.Tests.NewsPosts;

// SyncAllAsync/TrySyncAllAsync share a static lock across every ProgrammeSyncService instance in the
// process; running this class's tests one at a time avoids one test's held lock delaying (or, with
// worker-DB timing, corrupting the row count of) an unrelated test in the same run.
[NotInParallel("ProgrammeSyncLock")]
public class ProgrammeSyncServiceTests : RepositoryTestBase {
    private ProgrammeRepository _repo = default!;
    private FakeWikiClient _wiki = default!;
    private FakeMistralClient _mistral = default!;
    private ProgrammeIndex _index = default!;
    private ProgrammeSyncService _service = default!;

    [Before(Test)]
    public void Setup() {
        _repo = new ProgrammeRepository(Db);
        _wiki = new FakeWikiClient();
        _mistral = new FakeMistralClient();
        _index = new ProgrammeIndex();
        _service = new ProgrammeSyncService(_repo, _wiki, _mistral, _index, NullLogger<ProgrammeSyncService>.Instance);
    }

    [Test]
    public async Task First_sync_stores_sections_embeds_them_and_loads_index() {
        _repo.CreateSource(new ProgrammeSource { WikiPageTitle = "Parteiprogramm" });
        _wiki.Pages["Parteiprogramm"] = new WikiPage("Parteiprogramm", 10, "==Jugendschutz==\nJugendschutz durch Aufklärung.\n==Verkehr==\nÖPNV fahrscheinfrei.");

        await _service.SyncAllAsync(CancellationToken.None);

        await Assert.That(_repo.GetAllSections().Count).IsEqualTo(2);
        await Assert.That(_mistral.EmbeddedInputs.Count).IsEqualTo(2);
        await Assert.That(_index.Count).IsEqualTo(2);
        await Assert.That(_repo.GetSources()[0].LastRevisionId).IsEqualTo(10);
    }

    [Test]
    public async Task Unchanged_revision_is_skipped() {
        _repo.CreateSource(new ProgrammeSource { WikiPageTitle = "Parteiprogramm" });
        _wiki.Pages["Parteiprogramm"] = new WikiPage("Parteiprogramm", 10, "==A==\nText");
        await _service.SyncAllAsync(CancellationToken.None);
        var callsAfterFirst = _mistral.EmbedCalls;

        await _service.SyncAllAsync(CancellationToken.None);

        await Assert.That(_mistral.EmbedCalls).IsEqualTo(callsAfterFirst);
    }

    [Test]
    public async Task Only_changed_sections_are_re_embedded_and_removed_ones_deleted() {
        _repo.CreateSource(new ProgrammeSource { WikiPageTitle = "P" });
        _wiki.Pages["P"] = new WikiPage("P", 1, "==A==\nAlt A\n==B==\nText B\n==C==\nText C");
        await _service.SyncAllAsync(CancellationToken.None);
        _mistral.EmbeddedInputs.Clear();

        _wiki.Pages["P"] = new WikiPage("P", 2, "==A==\nNeu A\n==B==\nText B");
        await _service.SyncAllAsync(CancellationToken.None);

        await Assert.That(_mistral.EmbeddedInputs.Count).IsEqualTo(1);
        await Assert.That(_repo.GetAllSections().Select(s => s.Heading).OrderBy(h => h).ToList())
            .IsEquivalentTo(new List<string> { "A", "B" });
    }

    [Test]
    public async Task Prefix_source_syncs_every_subpage() {
        _repo.CreateSource(new ProgrammeSource { WikiPageTitle = "Positionspapiere/" });
        _wiki.Pages["Positionspapiere/Eins"] = new WikiPage("Positionspapiere/Eins", 1, "==A==\nText");
        _wiki.Pages["Positionspapiere/Zwei"] = new WikiPage("Positionspapiere/Zwei", 1, "==B==\nText");

        await _service.SyncAllAsync(CancellationToken.None);

        await Assert.That(_repo.GetAllSections().Select(s => s.PageTitle).Distinct().Count()).IsEqualTo(2);
    }

    [Test]
    public async Task Page_with_duplicate_stored_heading_paths_heals_on_sync() {
        var source = new ProgrammeSource { WikiPageTitle = "P" };
        _repo.CreateSource(source);
        _repo.ApplyPageDiff([
            new ProgrammeSection { SourceId = source.Id, PageTitle = "P", Heading = "A", HeadingPath = "P > A", Anchor = "A", Body = "Alt 1", ContentHash = "h1", UpdatedAt = DateTime.UtcNow },
            new ProgrammeSection { SourceId = source.Id, PageTitle = "P", Heading = "A", HeadingPath = "P > A", Anchor = "A", Body = "Alt 2", ContentHash = "h2", UpdatedAt = DateTime.UtcNow }
        ], [], []);
        _wiki.Pages["P"] = new WikiPage("P", 1, "==A==\nNeuer Text");

        await _service.SyncAllAsync(CancellationToken.None);

        var sections = _repo.GetAllSections();
        await Assert.That(sections.Count).IsEqualTo(1);
        await Assert.That(sections[0].Body).IsEqualTo("Neuer Text");
    }

    [Test]
    public async Task Concurrent_SyncAllAsync_calls_do_not_duplicate_sections() {
        _repo.CreateSource(new ProgrammeSource { WikiPageTitle = "P" });
        _wiki.Pages["P"] = new WikiPage("P", 1, "==A==\nText");
        _wiki.Gate = new TaskCompletionSource();
        _wiki.Started = new TaskCompletionSource();

        var first = _service.SyncAllAsync(CancellationToken.None);
        await _wiki.Started.Task;
        var second = _service.SyncAllAsync(CancellationToken.None);
        _wiki.Gate.SetResult();
        await Task.WhenAll(first, second);

        await Assert.That(_repo.GetAllSections().Count).IsEqualTo(1);
    }

    [Test]
    public async Task TrySyncAllAsync_returns_false_while_a_sync_is_in_progress() {
        _repo.CreateSource(new ProgrammeSource { WikiPageTitle = "P" });
        _wiki.Pages["P"] = new WikiPage("P", 1, "==A==\nText");
        _wiki.Gate = new TaskCompletionSource();
        _wiki.Started = new TaskCompletionSource();

        var running = _service.SyncAllAsync(CancellationToken.None);
        await _wiki.Started.Task;
        var startedSecond = await _service.TrySyncAllAsync(CancellationToken.None);
        _wiki.Gate.SetResult();
        await running;

        await Assert.That(startedSecond).IsFalse();
    }

    [Test]
    public async Task Failing_page_keeps_old_sections_and_records_error() {
        _repo.CreateSource(new ProgrammeSource { WikiPageTitle = "P" });
        _wiki.Pages["P"] = new WikiPage("P", 1, "==A==\nText");
        await _service.SyncAllAsync(CancellationToken.None);

        _wiki.Failing.Add("P");
        await _service.SyncAllAsync(CancellationToken.None);

        await Assert.That(_repo.GetAllSections().Count).IsEqualTo(1);
        await Assert.That(_repo.GetSources()[0].LastError).Contains("wiki down");
    }
}
