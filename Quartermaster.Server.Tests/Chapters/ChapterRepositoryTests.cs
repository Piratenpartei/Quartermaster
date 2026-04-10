using LinqToDB;
using Quartermaster.Data;
using Quartermaster.Data.Chapters;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.Chapters;

public class ChapterRepositoryTests : IDisposable {
    private DbContext _context = default!;
    private ChapterRepository _repo = default!;

    // Seeded chapter IDs for a 4-level hierarchy: Bund -> LV -> Bezirk -> Kreis
    private Guid _bundId;
    private Guid _lvId;
    private Guid _bezirkId;
    private Guid _kreisId;

    [Before(Test)]
    public void Setup() {
        TestDatabaseFixture.CleanAllTables();
        _context = TestDatabaseFixture.CreateDbContext();
        _repo = new ChapterRepository(_context);

        _bundId = Guid.NewGuid();
        _lvId = Guid.NewGuid();
        _bezirkId = Guid.NewGuid();
        _kreisId = Guid.NewGuid();

        _context.Insert(new Chapter {
            Id = _bundId,
            Name = "Bundesverband",
            ShortCode = "bund",
            ExternalCode = "BV"
        });
        _context.Insert(new Chapter {
            Id = _lvId,
            Name = "LV Niedersachsen",
            ShortCode = "nds",
            ExternalCode = "NI",
            ParentChapterId = _bundId
        });
        _context.Insert(new Chapter {
            Id = _bezirkId,
            Name = "Bezirk Hannover",
            ExternalCode = "H",
            ParentChapterId = _lvId
        });
        _context.Insert(new Chapter {
            Id = _kreisId,
            Name = "Kreis Hildesheim",
            ExternalCode = "HI",
            ParentChapterId = _bezirkId
        });
    }

    [Test]
    public async Task GetAncestorChain_LeafNode_ReturnsFullChain() {
        var chain = _repo.GetAncestorChain(_kreisId);

        await Assert.That(chain.Count).IsEqualTo(4);
        await Assert.That(chain[0].Id).IsEqualTo(_kreisId);
        await Assert.That(chain[1].Id).IsEqualTo(_bezirkId);
        await Assert.That(chain[2].Id).IsEqualTo(_lvId);
        await Assert.That(chain[3].Id).IsEqualTo(_bundId);
    }

    [Test]
    public async Task GetAncestorChain_RootNode_ReturnsSingleElement() {
        var chain = _repo.GetAncestorChain(_bundId);

        await Assert.That(chain.Count).IsEqualTo(1);
        await Assert.That(chain[0].Id).IsEqualTo(_bundId);
    }

    [Test]
    public async Task GetAncestorChain_MidLevelNode_ReturnsPartialChain() {
        var chain = _repo.GetAncestorChain(_lvId);

        await Assert.That(chain.Count).IsEqualTo(2);
        await Assert.That(chain[0].Id).IsEqualTo(_lvId);
        await Assert.That(chain[1].Id).IsEqualTo(_bundId);
    }

    [Test]
    public async Task GetAncestorChain_NonExistentId_ReturnsEmptyList() {
        var chain = _repo.GetAncestorChain(Guid.NewGuid());

        await Assert.That(chain.Count).IsEqualTo(0);
    }

    [Test]
    public async Task GetDescendantIds_Root_ReturnsAllDescendants() {
        var ids = _repo.GetDescendantIds(_bundId);

        await Assert.That(ids.Count).IsEqualTo(4);
        await Assert.That(ids).Contains(_bundId);
        await Assert.That(ids).Contains(_lvId);
        await Assert.That(ids).Contains(_bezirkId);
        await Assert.That(ids).Contains(_kreisId);
    }

    [Test]
    public async Task GetDescendantIds_MidLevel_ReturnsSelfAndBelow() {
        var ids = _repo.GetDescendantIds(_lvId);

        await Assert.That(ids.Count).IsEqualTo(3);
        await Assert.That(ids).Contains(_lvId);
        await Assert.That(ids).Contains(_bezirkId);
        await Assert.That(ids).Contains(_kreisId);
    }

    [Test]
    public async Task GetDescendantIds_LeafNode_ReturnsSelfOnly() {
        var ids = _repo.GetDescendantIds(_kreisId);

        await Assert.That(ids.Count).IsEqualTo(1);
        await Assert.That(ids).Contains(_kreisId);
    }

    [Test]
    public async Task GetDescendantIds_NonExistentId_ReturnsSingleId() {
        var nonExistent = Guid.NewGuid();
        var ids = _repo.GetDescendantIds(nonExistent);

        // GetDescendantIds always starts with the given ID in its result
        await Assert.That(ids.Count).IsEqualTo(1);
        await Assert.That(ids).Contains(nonExistent);
    }

    [Test]
    public async Task FindByExternalCodeAndParent_ExactMatch_ReturnsChapter() {
        var result = _repo.FindByExternalCodeAndParent("NI", _bundId);

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Id).IsEqualTo(_lvId);
    }

    [Test]
    public async Task FindByExternalCodeAndParent_CodeMatchWrongParent_ReturnsNull() {
        var result = _repo.FindByExternalCodeAndParent("NI", _lvId);

        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task FindByExternalCodeAndParent_WrongCode_ReturnsNull() {
        var result = _repo.FindByExternalCodeAndParent("XX", _bundId);

        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task FindByExternalCodeAndParent_NullParentForRoot_ReturnsRoot() {
        var result = _repo.FindByExternalCodeAndParent("BV", null);

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Id).IsEqualTo(_bundId);
    }

    [Test]
    public async Task FindByExternalCodeAndParent_NonExistentCode_ReturnsNull() {
        var result = _repo.FindByExternalCodeAndParent("NONEXISTENT", null);

        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task FindByExternalCodeAndParent_NullParentButNotRoot_ReturnsNull() {
        // "NI" exists but its parent is _bundId, not null
        var result = _repo.FindByExternalCodeAndParent("NI", null);

        await Assert.That(result).IsNull();
    }

    public void Dispose() {
        _context?.Dispose();
    }
}
