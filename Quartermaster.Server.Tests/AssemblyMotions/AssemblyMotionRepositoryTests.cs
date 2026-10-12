using LinqToDB;
using Quartermaster.Api.AssemblyMotions;
using Quartermaster.Data;
using Quartermaster.Data.AssemblyMotions;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.AssemblyMotions;

public class AssemblyMotionRepositoryTests : RepositoryTestBase {
    private DbContext _context = default!;
    private TestDataBuilder _builder = default!;
    private AssemblyMotionPortalRepository _portalRepo = default!;
    private AssemblyMotionRepository _motionRepo = default!;

    [Before(Test)]
    public void Setup() {
        _context = Db;
        _builder = new TestDataBuilder(_context);
        _portalRepo = new AssemblyMotionPortalRepository(_context, AuditLog);
        _motionRepo = new AssemblyMotionRepository(_context, AuditLog);
    }

    private AssemblyMotionPortal SeedPortal() {
        var chapter = _builder.SeedChapter();
        var evt = _builder.SeedEvent(chapter.Id);
        return _portalRepo.Create(evt.Id, DateTime.UtcNow, DateTime.UtcNow.AddDays(7), DateTime.UtcNow);
    }

    [Test]
    public async Task Create_seeds_wiki_template_per_type_code() {
        var chapter = _builder.SeedChapter();
        var evt = _builder.SeedEvent(chapter.Id);

        var portal = _portalRepo.Create(evt.Id, DateTime.UtcNow, DateTime.UtcNow.AddDays(7), DateTime.UtcNow, code => "T-" + code);

        var templates = _context.AssemblyMotionTypes
            .Where(t => t.PortalId == portal.Id)
            .ToList()
            .ToDictionary(t => t.Code, t => t.WikiTemplate);
        await Assert.That(templates.Count).IsEqualTo(6);
        await Assert.That(templates.All(pair => pair.Value == "T-" + pair.Key)).IsTrue();
    }

    private AssemblyMotion InsertMotion(Guid portalId, Guid typeId, Guid memberId, int? number,
        AssemblyMotionStatus status = AssemblyMotionStatus.Submitted, DateTime? deletedAt = null) {
        var motion = new AssemblyMotion {
            PortalId = portalId,
            TypeId = typeId,
            Number = number,
            Title = "Testantrag",
            Status = status,
            CreatedByMemberId = memberId,
            CreatedAt = DateTime.UtcNow,
            DeletedAt = deletedAt
        };
        _context.Insert(motion);
        return motion;
    }

    [Test]
    public async Task Create_seeds_six_default_types() {
        var portal = SeedPortal();

        var types = _portalRepo.GetTypes(portal.Id);

        var codes = string.Join(",", types.Select(t => t.Code));
        var minSubmitters = string.Join(",", types.Select(t => t.MinSubmitters));

        await Assert.That(codes).IsEqualTo("GP,WP,SÄA,SBA,PP,SO");
        await Assert.That(minSubmitters).IsEqualTo("5,5,5,5,1,1");
    }

    [Test]
    public async Task NextNumber_counts_withdrawn_and_deleted() {
        var portal = SeedPortal();
        var member = _builder.SeedMember();
        var types = _portalRepo.GetTypes(portal.Id);
        var typeA = types[0];
        var typeB = types[1];

        InsertMotion(portal.Id, typeA.Id, member.Id, number: 1, status: AssemblyMotionStatus.Withdrawn);
        InsertMotion(portal.Id, typeA.Id, member.Id, number: 2, deletedAt: DateTime.UtcNow);

        await Assert.That(_motionRepo.NextNumber(portal.Id, typeA.Id)).IsEqualTo(3);
        await Assert.That(_motionRepo.NextNumber(portal.Id, typeB.Id)).IsEqualTo(1);
    }

    [Test]
    public async Task RemoveCoSubmitters_keeps_author() {
        var portal = SeedPortal();
        var author = _builder.SeedMember();
        var coSubmitter1 = _builder.SeedMember();
        var coSubmitter2 = _builder.SeedMember();
        var type = _portalRepo.GetTypes(portal.Id)[0];
        var motion = InsertMotion(portal.Id, type.Id, author.Id, number: null, status: AssemblyMotionStatus.CollectingCoSubmitters);

        _motionRepo.AddSubmitter(new AssemblyMotionSubmitter { MotionId = motion.Id, MemberId = author.Id, IsAuthor = true, JoinedAt = DateTime.UtcNow, JoinedRevision = 1 });
        _motionRepo.AddSubmitter(new AssemblyMotionSubmitter { MotionId = motion.Id, MemberId = coSubmitter1.Id, IsAuthor = false, JoinedAt = DateTime.UtcNow, JoinedRevision = 1 });
        _motionRepo.AddSubmitter(new AssemblyMotionSubmitter { MotionId = motion.Id, MemberId = coSubmitter2.Id, IsAuthor = false, JoinedAt = DateTime.UtcNow, JoinedRevision = 1 });

        var removedMemberIds = _motionRepo.RemoveCoSubmitters(motion.Id);

        await Assert.That(removedMemberIds).IsEquivalentTo([coSubmitter1.Id, coSubmitter2.Id]);

        var remaining = _motionRepo.GetSubmitters(motion.Id);
        await Assert.That(remaining.Count).IsEqualTo(1);
        await Assert.That(remaining[0].MemberId).IsEqualTo(author.Id);
    }

    [Test]
    public async Task GetSubmitters_orders_author_first() {
        var portal = SeedPortal();
        var author = _builder.SeedMember();
        var coSubmitter = _builder.SeedMember();
        var type = _portalRepo.GetTypes(portal.Id)[0];
        var motion = InsertMotion(portal.Id, type.Id, author.Id, number: null, status: AssemblyMotionStatus.CollectingCoSubmitters);

        var now = DateTime.UtcNow;
        _motionRepo.AddSubmitter(new AssemblyMotionSubmitter { MotionId = motion.Id, MemberId = coSubmitter.Id, IsAuthor = false, JoinedAt = now.AddMinutes(-5), JoinedRevision = 1 });
        _motionRepo.AddSubmitter(new AssemblyMotionSubmitter { MotionId = motion.Id, MemberId = author.Id, IsAuthor = true, JoinedAt = now, JoinedRevision = 1 });

        var submitters = _motionRepo.GetSubmitters(motion.Id);

        await Assert.That(submitters[0].MemberId).IsEqualTo(author.Id);
        await Assert.That(submitters[1].MemberId).IsEqualTo(coSubmitter.Id);
    }

    [Test]
    public async Task TypeHasMotions_ignores_soft_deleted_drafts() {
        var portal = SeedPortal();
        var member = _builder.SeedMember();
        var types = _portalRepo.GetTypes(portal.Id);
        var typeWithDeletedDraft = types[0];
        var typeWithLiveDraft = types[1];

        InsertMotion(portal.Id, typeWithDeletedDraft.Id, member.Id, number: null, status: AssemblyMotionStatus.Draft, deletedAt: DateTime.UtcNow);
        InsertMotion(portal.Id, typeWithLiveDraft.Id, member.Id, number: null, status: AssemblyMotionStatus.Draft);

        await Assert.That(_portalRepo.TypeHasMotions(typeWithDeletedDraft.Id)).IsFalse();
        await Assert.That(_portalRepo.TypeHasMotions(typeWithLiveDraft.Id)).IsTrue();
    }
}
