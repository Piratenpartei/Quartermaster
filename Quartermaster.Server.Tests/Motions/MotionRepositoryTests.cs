using LinqToDB;
using Quartermaster.Data;
using Quartermaster.Data.AdministrativeDivisions;
using Quartermaster.Data.AuditLog;
using Quartermaster.Data.ChapterAssociates;
using Quartermaster.Data.Chapters;
using Quartermaster.Data.DueSelector;
using Quartermaster.Data.Members;
using Quartermaster.Data.MembershipApplications;
using Quartermaster.Data.Motions;
using Quartermaster.Data.Users;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.Motions;

[NotInParallel]
public class MotionRepositoryTests : IDisposable {
    private DbContext _context = default!;
    private MotionRepository _motionRepo = default!;
    private ChapterOfficerRepository _officerRepo = default!;

    private Guid _chapterId;
    private Guid _adminDivId;

    [Before(Test)]
    public void Setup() {
        TestDatabaseFixture.CleanAllTables();
        _context = TestDatabaseFixture.CreateDbContext();
        var auditLog = new AuditLogRepository(_context);
        _motionRepo = new MotionRepository(_context, auditLog);
        var roleRepo = new Quartermaster.Data.Roles.RoleRepository(_context);
        _officerRepo = new ChapterOfficerRepository(_context, auditLog, roleRepo);

        // Seed an AdministrativeDivision for User FK constraints
        _adminDivId = Guid.NewGuid();
        _context.Insert(new AdministrativeDivision {
            Id = _adminDivId,
            Name = "Test Division",
            Depth = 0
        });

        _chapterId = Guid.NewGuid();
        _context.Insert(new Chapter {
            Id = _chapterId,
            Name = "Test Chapter",
            ShortCode = "tst",
            ExternalCode = "TST"
        });
    }

    /// <summary>
    /// Seeds a Member, User, and ChapterOfficer. Returns the User ID (for casting votes).
    /// </summary>
    private Guid SeedOfficer() {
        var memberId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        _context.Insert(new Member {
            Id = memberId,
            MemberNumber = Random.Shared.Next(100000, 999999),
            FirstName = "Officer",
            LastName = memberId.ToString()[..8],
            LastImportedAt = DateTime.UtcNow
        });
        _context.Insert(new User {
            Id = userId,
            Username = $"user-{userId.ToString()[..8]}",
            CitizenshipAdministrativeDivisionId = _adminDivId,
            AddressAdministrativeDivisionId = _adminDivId
        });
        _context.Insert(new ChapterOfficer {
            MemberId = memberId,
            ChapterId = _chapterId,
            AssociateType = ChapterOfficerType.Member
        });
        return userId;
    }

    private List<Guid> SeedOfficers(int count) {
        var ids = new List<Guid>();
        for (int i = 0; i < count; i++)
            ids.Add(SeedOfficer());
        return ids;
    }

    private Guid SeedMotion(MotionApprovalStatus status = MotionApprovalStatus.Pending,
        Guid? linkedAppId = null, Guid? linkedDueSelectionId = null) {

        var motion = new Motion {
            ChapterId = _chapterId,
            AuthorName = "Test Author",
            AuthorEMail = "test@example.com",
            Title = "Test Motion",
            Text = "Test text",
            IsPublic = true,
            ApprovalStatus = status,
            CreatedAt = DateTime.UtcNow,
            LinkedMembershipApplicationId = linkedAppId,
            LinkedDueSelectionId = linkedDueSelectionId
        };
        _motionRepo.Create(motion);
        return motion.Id;
    }

    private void CastVote(Guid motionId, Guid userId, VoteType voteType) {
        _motionRepo.CastVote(new MotionVote {
            MotionId = motionId,
            UserId = userId,
            Vote = voteType,
            VotedAt = DateTime.UtcNow
        });
    }

    // --- TryAutoResolve ---

    [Test]
    public async Task TryAutoResolve_MotionNotFound_ReturnsFalse() {
        var result = _motionRepo.TryAutoResolve(Guid.NewGuid(), _officerRepo);

        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task TryAutoResolve_MotionNotPending_ReturnsFalse() {
        var motionId = SeedMotion(MotionApprovalStatus.Approved);

        var result = _motionRepo.TryAutoResolve(motionId, _officerRepo);

        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task TryAutoResolve_NoOfficers_ReturnsFalse() {
        var motionId = SeedMotion();

        var result = _motionRepo.TryAutoResolve(motionId, _officerRepo);

        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task TryAutoResolve_3Of5Approve_Approved() {
        var officers = SeedOfficers(5);
        var motionId = SeedMotion();
        CastVote(motionId, officers[0], VoteType.Approve);
        CastVote(motionId, officers[1], VoteType.Approve);
        CastVote(motionId, officers[2], VoteType.Approve);

        var result = _motionRepo.TryAutoResolve(motionId, _officerRepo);

        await Assert.That(result).IsTrue();
        var motion = _motionRepo.Get(motionId);
        await Assert.That(motion!.ApprovalStatus).IsEqualTo(MotionApprovalStatus.Approved);
    }

    [Test]
    public async Task TryAutoResolve_3Of5Deny_Rejected() {
        var officers = SeedOfficers(5);
        var motionId = SeedMotion();
        CastVote(motionId, officers[0], VoteType.Deny);
        CastVote(motionId, officers[1], VoteType.Deny);
        CastVote(motionId, officers[2], VoteType.Deny);

        var result = _motionRepo.TryAutoResolve(motionId, _officerRepo);

        await Assert.That(result).IsTrue();
        var motion = _motionRepo.Get(motionId);
        await Assert.That(motion!.ApprovalStatus).IsEqualTo(MotionApprovalStatus.Rejected);
    }

    [Test]
    public async Task TryAutoResolve_2Approve2Deny1Abstain_NotResolved() {
        var officers = SeedOfficers(5);
        var motionId = SeedMotion();
        CastVote(motionId, officers[0], VoteType.Approve);
        CastVote(motionId, officers[1], VoteType.Approve);
        CastVote(motionId, officers[2], VoteType.Deny);
        CastVote(motionId, officers[3], VoteType.Deny);
        CastVote(motionId, officers[4], VoteType.Abstain);

        var result = _motionRepo.TryAutoResolve(motionId, _officerRepo);

        await Assert.That(result).IsFalse();
        var motion = _motionRepo.Get(motionId);
        await Assert.That(motion!.ApprovalStatus).IsEqualTo(MotionApprovalStatus.Pending);
    }

    [Test]
    public async Task TryAutoResolve_2Of4Approve_NotEnough() {
        var officers = SeedOfficers(4);
        var motionId = SeedMotion();
        CastVote(motionId, officers[0], VoteType.Approve);
        CastVote(motionId, officers[1], VoteType.Approve);

        var result = _motionRepo.TryAutoResolve(motionId, _officerRepo);

        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task TryAutoResolve_3Of4Approve_Approved() {
        var officers = SeedOfficers(4);
        var motionId = SeedMotion();
        CastVote(motionId, officers[0], VoteType.Approve);
        CastVote(motionId, officers[1], VoteType.Approve);
        CastVote(motionId, officers[2], VoteType.Approve);

        var result = _motionRepo.TryAutoResolve(motionId, _officerRepo);

        await Assert.That(result).IsTrue();
        var motion = _motionRepo.Get(motionId);
        await Assert.That(motion!.ApprovalStatus).IsEqualTo(MotionApprovalStatus.Approved);
    }

    [Test]
    public async Task TryAutoResolve_LinkedApplicationUpdatedOnApproval() {
        var appId = Guid.NewGuid();
        _context.Insert(new MembershipApplication {
            Id = appId,
            FirstName = "Test",
            LastName = "Applicant",
            EMail = "test@example.com",
            Status = ApplicationStatus.Pending,
            SubmittedAt = DateTime.UtcNow,
            EntryDate = DateTime.UtcNow
        });

        var officers = SeedOfficers(3);
        var motionId = SeedMotion(linkedAppId: appId);
        CastVote(motionId, officers[0], VoteType.Approve);
        CastVote(motionId, officers[1], VoteType.Approve);

        var result = _motionRepo.TryAutoResolve(motionId, _officerRepo);

        await Assert.That(result).IsTrue();
        var app = _context.MembershipApplications
            .Where(a => a.Id == appId).First();
        await Assert.That(app.Status).IsEqualTo(ApplicationStatus.Approved);
        await Assert.That(app.ProcessedAt).IsNotNull();
    }

    [Test]
    public async Task TryAutoResolve_LinkedDueSelectionUpdatedOnRejection() {
        var dsId = Guid.NewGuid();
        _context.Insert(new DueSelection {
            Id = dsId,
            FirstName = "Test",
            LastName = "Member",
            Status = DueSelectionStatus.Pending
        });

        var officers = SeedOfficers(3);
        var motionId = SeedMotion(linkedDueSelectionId: dsId);
        CastVote(motionId, officers[0], VoteType.Deny);
        CastVote(motionId, officers[1], VoteType.Deny);

        var result = _motionRepo.TryAutoResolve(motionId, _officerRepo);

        await Assert.That(result).IsTrue();
        var ds = _context.DueSelections
            .Where(d => d.Id == dsId).First();
        await Assert.That(ds.Status).IsEqualTo(DueSelectionStatus.Rejected);
        await Assert.That(ds.ProcessedAt).IsNotNull();
    }

    [Test]
    public async Task TryAutoResolve_SingleOfficerApproves_Approved() {
        var officers = SeedOfficers(1);
        var motionId = SeedMotion();
        CastVote(motionId, officers[0], VoteType.Approve);

        var result = _motionRepo.TryAutoResolve(motionId, _officerRepo);

        await Assert.That(result).IsTrue();
        var motion = _motionRepo.Get(motionId);
        await Assert.That(motion!.ApprovalStatus).IsEqualTo(MotionApprovalStatus.Approved);
    }

    public void Dispose() {
        _context?.Dispose();
    }
}
