using LinqToDB;
using Quartermaster.Api.Options;
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
        _chapterRepo = new ChapterRepository(_context, new Quartermaster.Data.AuditLog.AuditLogRepository(_context));

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

    [Test]
    public async Task SetValue_SecretOption_RedactsAuditLogEntry() {
        _context.Insert(new OptionDefinition {
            Identifier = "test.secret",
            FriendlyName = "Test Secret",
            DataType = OptionDataType.String,
            IsOverridable = false,
            IsSecret = true
        });

        _repo.SetValue("test.secret", null, "plaintext-secret-value");
        var stored = _context.SystemOptions.Where(o => o.Identifier == "test.secret").First();
        _repo.SetValue("test.secret", null, "second-plaintext-value");

        var entries = _context.AuditLogs
            .Where(e => e.EntityType == "SystemOption" && e.EntityId == stored.Id)
            .ToList();

        // Stored value itself must be the real plaintext (the SMTP service still needs it).
        await Assert.That(_repo.ResolveValue("test.secret", null, _chapterRepo)).IsEqualTo("second-plaintext-value");
        // But every audit-log entry that touches the secret's value must hold the mask, never the plaintext.
        await Assert.That(entries.Count > 0).IsTrue();
        foreach (var entry in entries) {
            await Assert.That(entry.OldValue?.Contains("plaintext") ?? false).IsFalse();
            await Assert.That(entry.NewValue?.Contains("plaintext") ?? false).IsFalse();
        }
    }

    public void Dispose() {
        _context?.Dispose();
    }
}
