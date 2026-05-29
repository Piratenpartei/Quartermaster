using LinqToDB;
using Quartermaster.Api.ChapterAssociates;
using Quartermaster.Api.DueSelector;
using Quartermaster.Api.MembershipApplications;
using Quartermaster.Api.Motions;
using Quartermaster.Data;
using Quartermaster.Data.AdministrativeDivisions;
using Quartermaster.Data.AuditLog;
using Quartermaster.Data.ChapterAssociates;
using Quartermaster.Data.Chapters;
using Quartermaster.Data.DueSelector;
using Quartermaster.Data.Members;
using Quartermaster.Data.MembershipApplications;
using Quartermaster.Data.Motions;
using Quartermaster.Data.Roles;
using Quartermaster.Data.Users;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.Motions;

public class MotionRepositoryTests : RepositoryTestBase {
    private DbContext _context = default!;
    private MotionRepository _motionRepo = default!;
    private ChapterOfficerRepository _officerRepo = default!;

    private Guid _chapterId;
    private Guid _adminDivId;

    [Before(Test)]
    public void Setup() {
        _context = Db;
        _motionRepo = new MotionRepository(_context, AuditLog);
        var roleRepo = new RoleRepository(_context);
        _officerRepo = new ChapterOfficerRepository(_context, AuditLog, roleRepo);

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
    /// Seeds a Member, User, and ChapterOfficer. Returns the member id (the vote target) and
    /// the linked user id (the recorder for self-votes).
    /// </summary>
    private (Guid MemberId, Guid UserId) SeedOfficer() {
        var memberId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        _context.Insert(new User {
            Id = userId,
            Username = $"user-{userId.ToString()[..8]}",
            CitizenshipAdministrativeDivisionId = _adminDivId,
            AddressAdministrativeDivisionId = _adminDivId
        });
        _context.Insert(new Member {
            Id = memberId,
            MemberNumber = Random.Shared.Next(100000, 999999),
            FirstName = "Officer",
            LastName = memberId.ToString()[..8],
            UserId = userId,
            LastImportedAt = DateTime.UtcNow
        });
        _context.Insert(new ChapterOfficer {
            MemberId = memberId,
            ChapterId = _chapterId,
            AssociateType = ChapterOfficerType.Member
        });
        return (memberId, userId);
    }

    private List<(Guid MemberId, Guid UserId)> SeedOfficers(int count) {
        var ids = new List<(Guid, Guid)>();
        for (int i = 0; i < count; i++)
            ids.Add(SeedOfficer());
        return ids;
    }

    private Guid SeedMotion(MotionApprovalStatus status = MotionApprovalStatus.Pending,
        Guid? linkedAppId = null, Guid? linkedDueSelectionId = null) {

        var motion = new Motion {
            ChapterId = _chapterId,
            AuthorName = "Test Author",
            AuthorEmail = "test@example.com",
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

    private void CastVote(Guid motionId, (Guid MemberId, Guid UserId) officer, VoteType voteType) {
        _motionRepo.CastVote(new MotionVote {
            MotionId = motionId,
            MemberId = officer.MemberId,
            CastByUserId = officer.UserId,
            Vote = voteType,
            VotedAt = DateTime.UtcNow
        });
    }

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
            Email = "test@example.com",
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

}
