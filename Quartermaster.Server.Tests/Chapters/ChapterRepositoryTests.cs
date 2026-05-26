using LinqToDB;
using Quartermaster.Api.ChapterAssociates;
using Quartermaster.Data;
using Quartermaster.Data.ChapterAssociates;
using Quartermaster.Data.Chapters;
using Quartermaster.Data.DueSelector;
using Quartermaster.Data.Events;
using Quartermaster.Data.Members;
using Quartermaster.Data.Meetings;
using Quartermaster.Data.MembershipApplications;
using Quartermaster.Data.Motions;
using Quartermaster.Data.UserChapterPermissions;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.Chapters;

public class ChapterRepositoryTests : RepositoryTestBase {
    private DbContext _context = default!;
    private ChapterRepository _repo = default!;

    // Seeded chapter IDs for a 4-level hierarchy: Bund -> LV -> Bezirk -> Kreis
    private Guid _bundId;
    private Guid _lvId;
    private Guid _bezirkId;
    private Guid _kreisId;

    [Before(Test)]
    public void Setup() {
        _context = Db;
        _repo = new ChapterRepository(_context, AuditLog);

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
    public async Task GetDescendantIds_NonExistentId_ReturnsEmpty() {
        var nonExistent = Guid.NewGuid();
        var ids = _repo.GetDescendantIds(nonExistent);

        await Assert.That(ids).IsEmpty();
    }

    [Test]
    public async Task GetByExternalCodeAndParent_ExactMatch_ReturnsChapter() {
        var result = _repo.GetByExternalCodeAndParent("NI", _bundId);

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Id).IsEqualTo(_lvId);
    }

    [Test]
    public async Task GetByExternalCodeAndParent_CodeMatchWrongParent_ReturnsNull() {
        var result = _repo.GetByExternalCodeAndParent("NI", _lvId);

        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task GetByExternalCodeAndParent_WrongCode_ReturnsNull() {
        var result = _repo.GetByExternalCodeAndParent("XX", _bundId);

        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task GetByExternalCodeAndParent_NullParentForRoot_ReturnsRoot() {
        var result = _repo.GetByExternalCodeAndParent("BV", null);

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Id).IsEqualTo(_bundId);
    }

    [Test]
    public async Task GetByExternalCodeAndParent_NonExistentCode_ReturnsNull() {
        var result = _repo.GetByExternalCodeAndParent("NONEXISTENT", null);

        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task GetByExternalCodeAndParent_NullParentButNotRoot_ReturnsNull() {
        // "NI" exists but its parent is _bundId, not null
        var result = _repo.GetByExternalCodeAndParent("NI", null);

        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task SoftDeleteWithCascade_RootChapter_Blocked() {
        var result = _repo.SoftDeleteWithCascade(_bundId);
        await Assert.That(result).IsEqualTo(ChapterRepository.ChapterDeleteResult.IsRoot);
        await Assert.That(_repo.Get(_bundId)).IsNotNull();
    }

    [Test]
    public async Task SoftDeleteWithCascade_NonExistent_NotFound() {
        var result = _repo.SoftDeleteWithCascade(Guid.NewGuid());
        await Assert.That(result).IsEqualTo(ChapterRepository.ChapterDeleteResult.NotFound);
    }

    [Test]
    public async Task SoftDeleteWithCascade_NonRoot_HidesChapterFromQueries() {
        var result = _repo.SoftDeleteWithCascade(_lvId);
        await Assert.That(result).IsEqualTo(ChapterRepository.ChapterDeleteResult.Success);

        await Assert.That(_repo.Get(_lvId)).IsNull();
        await Assert.That(_repo.GetAll().Any(c => c.Id == _lvId)).IsFalse();
        // Row still in DB with DeletedAt set.
        var raw = _context.Chapters.Where(c => c.Id == _lvId).First();
        await Assert.That(raw.DeletedAt).IsNotNull();
    }

    [Test]
    public async Task SoftDeleteWithCascade_ReassignsMembersToParent() {
        var memberInLv = new Member {
            Id = Guid.NewGuid(),
            MemberNumber = 999001,
            FirstName = "Maja",
            LastName = "Müller",
            ChapterId = _lvId,
            LastImportedAt = DateTime.UtcNow
        };
        _context.Insert(memberInLv);

        _repo.SoftDeleteWithCascade(_lvId);

        var reassigned = _context.Members.Where(m => m.Id == memberInLv.Id).First();
        await Assert.That(reassigned.ChapterId).IsEqualTo(_bundId);
    }

    [Test]
    public async Task SoftDeleteWithCascade_CascadesEventsMeetingsMotionsApplications() {
        var eventId = Guid.NewGuid();
        _context.Insert(new Event {
            Id = eventId, ChapterId = _lvId, InternalName = "Stammtisch", PublicName = "Stammtisch", CreatedAt = DateTime.UtcNow
        });
        var meetingId = Guid.NewGuid();
        _context.Insert(new Meeting {
            Id = meetingId, ChapterId = _lvId, Title = "Vorstand Mai", CreatedAt = DateTime.UtcNow
        });
        var motionId = Guid.NewGuid();
        _context.Insert(new Motion {
            Id = motionId, ChapterId = _lvId, AuthorName = "Anon", AuthorEmail = "a@x", Title = "Test", Text = "", IsPublic = true, CreatedAt = DateTime.UtcNow
        });
        var appId = Guid.NewGuid();
        _context.Insert(new MembershipApplication {
            Id = appId, ChapterId = _lvId, FirstName = "Pa", LastName = "Sa", DateOfBirth = new DateTime(1990, 1, 1),
            Citizenship = "DE", Email = "p@x", PhoneNumber = "", AddressStreet = "", AddressHouseNbr = "", AddressPostCode = "", AddressCity = "",
            EntryDate = DateTime.UtcNow, SubmittedAt = DateTime.UtcNow
        });

        _repo.SoftDeleteWithCascade(_lvId);

        await Assert.That(_context.Events.Where(e => e.Id == eventId).First().DeletedAt).IsNotNull();
        await Assert.That(_context.Meetings.Where(m => m.Id == meetingId).First().DeletedAt).IsNotNull();
        await Assert.That(_context.Motions.Where(m => m.Id == motionId).First().DeletedAt).IsNotNull();
        await Assert.That(_context.MembershipApplications.Where(a => a.Id == appId).First().DeletedAt).IsNotNull();
    }

    [Test]
    public async Task SoftDeleteWithCascade_HardDeletesJunctions() {
        var memberId = Guid.NewGuid();
        _context.Insert(new Member {
            Id = memberId, MemberNumber = 999002, FirstName = "M", LastName = "M", ChapterId = _lvId, LastImportedAt = DateTime.UtcNow
        });
        _context.Insert(new ChapterOfficer {
            MemberId = memberId, ChapterId = _lvId, AssociateType = ChapterOfficerType.Captain
        });

        _repo.SoftDeleteWithCascade(_lvId);

        await Assert.That(_context.ChapterOfficers.Any(o => o.ChapterId == _lvId)).IsFalse();
    }

}
