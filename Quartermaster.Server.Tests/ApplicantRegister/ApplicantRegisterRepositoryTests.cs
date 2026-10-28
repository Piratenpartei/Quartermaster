using System.Linq;
using Quartermaster.Api.ApplicantRegister;
using Quartermaster.Data.ApplicantRegister;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.ApplicantRegister;

public class ApplicantRegisterRepositoryTests : RepositoryTestBase {
    private ApplicantRegisterRepository _repo = default!;
    private TestDataBuilder _seed = default!;

    [Before(Test)]
    public void Setup() {
        _repo = new ApplicantRegisterRepository(Db, AuditLog);
        _seed = new TestDataBuilder(Db);
    }

    private ApplicationRegisterHit NewHit(Guid entryId)
        => new() {
            EntryId = entryId,
            MatchedFields = RegisterMatchFields.FirstName,
            CreatedAt = DateTime.UtcNow
        };

    [Test]
    public async Task Search_finds_by_last_name() {
        _seed.SeedRegisterEntry(lastName: "Schneider");
        _seed.SeedRegisterEntry(lastName: "Becker");

        var (items, totalCount) = _repo.Search("schneid", 1, 10);

        await Assert.That(totalCount).IsEqualTo(1);
        await Assert.That(items[0].LastName).IsEqualTo("Schneider");
    }

    [Test]
    public async Task Delete_removes_hits() {
        var entry = _seed.SeedRegisterEntry();
        var application = _seed.SeedMembershipApplication();
        _repo.AddHits(application.Id, [NewHit(entry.Id)]);

        _repo.Delete(entry.Id);

        await Assert.That(_repo.HitsFor(application.Id)).IsEmpty();
        await Assert.That(_repo.Get(entry.Id)).IsNull();
    }

    [Test]
    public async Task Dismiss_only_once() {
        var entry = _seed.SeedRegisterEntry();
        var application = _seed.SeedMembershipApplication();
        _repo.AddHits(application.Id, [NewHit(entry.Id)]);
        var hit = _repo.HitsFor(application.Id)[0];

        var first = _repo.Dismiss(hit.Id, Guid.NewGuid(), DateTime.UtcNow);
        var second = _repo.Dismiss(hit.Id, Guid.NewGuid(), DateTime.UtcNow);

        await Assert.That(first).IsTrue();
        await Assert.That(second).IsFalse();
    }

    [Test]
    public async Task Open_hits_ignore_dismissed() {
        var entry = _seed.SeedRegisterEntry();
        var application = _seed.SeedMembershipApplication();
        _repo.AddHits(application.Id, [NewHit(entry.Id)]);
        await Assert.That(_repo.HasOpenHits(application.Id)).IsTrue();

        _repo.Dismiss(_repo.HitsFor(application.Id)[0].Id, Guid.NewGuid(), DateTime.UtcNow);

        await Assert.That(_repo.HasOpenHits(application.Id)).IsFalse();
    }

    [Test]
    public async Task Update_leaves_immutable_columns_alone() {
        var application = _seed.SeedMembershipApplication();
        var entry = _seed.SeedRegisterEntry(sourceApplicationId: application.Id);
        var before = _repo.Get(entry.Id)!;
        var changed = _repo.Get(entry.Id)!;
        changed.City = "Elsewhere";
        changed.SourceApplicationId = null;
        changed.CreatedAt = new DateTime(2000, 1, 1);
        changed.CreatedByUserId = Guid.NewGuid();

        _repo.Update(changed);

        var after = _repo.Get(entry.Id)!;
        await Assert.That(after.City).IsEqualTo("Elsewhere");
        await Assert.That(after.SourceApplicationId).IsEqualTo(application.Id);
        await Assert.That(after.CreatedAt).IsEqualTo(before.CreatedAt);
        await Assert.That(after.CreatedByUserId).IsEqualTo(before.CreatedByUserId);
    }

    [Test]
    public async Task Search_matches_percent_in_a_name_literally() {
        _seed.SeedRegisterEntry(firstName: "Anna", lastName: "Schmidt");
        _seed.SeedRegisterEntry(firstName: "Max", lastName: "100%Sure");

        var (items, totalCount) = _repo.Search("100%S", 1, 10);

        await Assert.That(totalCount).IsEqualTo(1);
        await Assert.That(items[0].LastName).IsEqualTo("100%Sure");
    }

    [Test]
    public async Task Search_matches_backslash_literally() {
        _seed.SeedRegisterEntry(lastName: "A\\b");
        _seed.SeedRegisterEntry(lastName: "Ab");

        var (items, _) = _repo.Search("A\\b", 1, 10);

        await Assert.That(items.Select(i => i.LastName)).IsEquivalentTo(new[] { "A\\b" });
    }

    [Test]
    public async Task Search_with_bare_percent_does_not_match_everything() {
        _seed.SeedRegisterEntry(firstName: "Anna", lastName: "Schmidt");
        _seed.SeedRegisterEntry(firstName: "100%", lastName: "Sure");

        var (items, totalCount) = _repo.Search("%", 1, 10);

        await Assert.That(totalCount).IsEqualTo(1);
        await Assert.That(items[0].LastName).IsEqualTo("Sure");
    }

    [Test]
    public async Task Search_underscore_is_not_a_single_character_wildcard() {
        _seed.SeedRegisterEntry(lastName: "Abc");
        _seed.SeedRegisterEntry(lastName: "A_c");

        var (items, _) = _repo.Search("A_c", 1, 10);

        await Assert.That(items.Select(i => i.LastName)).IsEquivalentTo(new[] { "A_c" });
    }

    [Test]
    public async Task Search_pages_through_the_results() {
        for (var i = 0; i < 5; i++)
            _seed.SeedRegisterEntry(lastName: "Entry" + i, occurredOn: new DateTime(2025, 1, 1).AddDays(i));

        var (second, totalCount) = _repo.Search(null, 2, 2);

        await Assert.That(totalCount).IsEqualTo(5);
        await Assert.That(second.Select(e => e.LastName)).IsEquivalentTo(new[] { "Entry2", "Entry1" });
    }
}
