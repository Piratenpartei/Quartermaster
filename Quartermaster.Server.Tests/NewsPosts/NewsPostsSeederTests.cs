using System.Linq;
using Quartermaster.Data.NewsPosts;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.NewsPosts;

public class NewsPostsSeederTests : RepositoryTestBase {
    private NewsFeedRepository _feedRepo = default!;
    private ProgrammeRepository _programmeRepo = default!;

    [Before(Test)]
    public void Setup() {
        _feedRepo = new NewsFeedRepository(Db);
        _programmeRepo = new ProgrammeRepository(Db);
    }

    [Test]
    public async Task First_install_seeds_default_feeds_and_sources() {
        NewsPostsSeeder.Seed(_feedRepo, _programmeRepo);

        await Assert.That(_feedRepo.GetAll().Count).IsEqualTo(3);
        await Assert.That(_programmeRepo.GetSources().Count).IsEqualTo(3);
    }

    [Test]
    public async Task Second_seed_after_admin_edits_leaves_exactly_the_admins_state() {
        NewsPostsSeeder.Seed(_feedRepo, _programmeRepo);

        var netzpolitik = _feedRepo.GetAll().Single(f => f.Name == "netzpolitik.org");
        netzpolitik.Url = "https://netzpolitik.org/custom-feed/";
        _feedRepo.Update(netzpolitik);
        var bundestagSource = _programmeRepo.GetSources().Single(s => s.WikiPageTitle == "Bundestagswahl 2025/Wahlprogramm");
        _programmeRepo.DeleteSource(bundestagSource.Id);

        NewsPostsSeeder.Seed(_feedRepo, _programmeRepo);

        var feeds = _feedRepo.GetAll();
        await Assert.That(feeds.Count).IsEqualTo(3);
        await Assert.That(feeds.Single(f => f.Name == "netzpolitik.org").Url).IsEqualTo("https://netzpolitik.org/custom-feed/");
        var sources = _programmeRepo.GetSources();
        await Assert.That(sources.Count).IsEqualTo(2);
        await Assert.That(sources.Any(s => s.WikiPageTitle == "Bundestagswahl 2025/Wahlprogramm")).IsFalse();
    }
}
