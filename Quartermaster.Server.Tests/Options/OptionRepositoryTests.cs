using LinqToDB;
using Quartermaster.Data;
using Quartermaster.Data.AuditLog;
using Quartermaster.Data.Chapters;
using Quartermaster.Data.Options;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.Options;

public class OptionRepositoryTests : IDisposable {
    private DbContext _context = default!;
    private OptionRepository _repo = default!;
    private ChapterRepository _chapterRepo = default!;

    // 3-level hierarchy: Bund -> LV -> Kreis
    private Guid _bundId;
    private Guid _lvId;
    private Guid _kreisId;

    [Before(Test)]
    public void Setup() {
        TestDatabaseFixture.CleanAllTables();
        _context = TestDatabaseFixture.CreateDbContext();
        var auditLog = new AuditLogRepository(_context);
        _repo = new OptionRepository(_context, auditLog);
        _chapterRepo = new ChapterRepository(_context);

        _bundId = Guid.NewGuid();
        _lvId = Guid.NewGuid();
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
            Id = _kreisId,
            Name = "Kreis Hildesheim",
            ExternalCode = "HI",
            ParentChapterId = _lvId
        });

        // Seed an option definition
        _context.Insert(new OptionDefinition {
            Identifier = "test.option",
            FriendlyName = "Test Option",
            DataType = OptionDataType.String,
            IsOverridable = true
        });
    }

    [Test]
    public async Task ResolveValue_NoChapterId_ReturnsGlobalValue() {
        _repo.SetValue("test.option", null, "global-value");

        var result = _repo.ResolveValue("test.option", null, _chapterRepo);

        await Assert.That(result).IsEqualTo("global-value");
    }

    [Test]
    public async Task ResolveValue_NoGlobalNoChapterValue_ReturnsNull() {
        var result = _repo.ResolveValue("test.option", _kreisId, _chapterRepo);

        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task ResolveValue_ChapterLevelValueAtTarget_ReturnsIt() {
        _repo.SetValue("test.option", _kreisId, "kreis-value");

        var result = _repo.ResolveValue("test.option", _kreisId, _chapterRepo);

        await Assert.That(result).IsEqualTo("kreis-value");
    }

    [Test]
    public async Task ResolveValue_ValueAtParent_ChildInherits() {
        _repo.SetValue("test.option", _lvId, "lv-value");

        var result = _repo.ResolveValue("test.option", _kreisId, _chapterRepo);

        await Assert.That(result).IsEqualTo("lv-value");
    }

    [Test]
    public async Task ResolveValue_ChildOverridesParent() {
        _repo.SetValue("test.option", _lvId, "lv-value");
        _repo.SetValue("test.option", _kreisId, "kreis-value");

        var result = _repo.ResolveValue("test.option", _kreisId, _chapterRepo);

        await Assert.That(result).IsEqualTo("kreis-value");
    }

    [Test]
    public async Task ResolveValue_GlobalFallbackWhenNoChapterMatch() {
        _repo.SetValue("test.option", null, "global-fallback");

        var result = _repo.ResolveValue("test.option", _kreisId, _chapterRepo);

        await Assert.That(result).IsEqualTo("global-fallback");
    }

    [Test]
    public async Task ResolveValue_DeepestAncestorMatchWins() {
        _repo.SetValue("test.option", _bundId, "bund-value");
        _repo.SetValue("test.option", _lvId, "lv-value");

        var result = _repo.ResolveValue("test.option", _kreisId, _chapterRepo);

        await Assert.That(result).IsEqualTo("lv-value");
    }

    [Test]
    public async Task ResolveValue_IdentifierNotDefined_ReturnsNull() {
        var result = _repo.ResolveValue("nonexistent.option", _kreisId, _chapterRepo);

        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task ResolveValue_ChapterIdProvidedButNoChapterValues_FallsBackToGlobal() {
        _repo.SetValue("test.option", null, "global-only");

        var result = _repo.ResolveValue("test.option", _lvId, _chapterRepo);

        await Assert.That(result).IsEqualTo("global-only");
    }

    public void Dispose() {
        _context?.Dispose();
    }
}
