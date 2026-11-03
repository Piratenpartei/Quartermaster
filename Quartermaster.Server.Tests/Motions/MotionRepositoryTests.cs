using LinqToDB;
using LinqToDB.Data;
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
            ShortCode = "tst"
        });
        _context.Insert(new ChapterExternalCode { ChapterId = _chapterId, Code = "TST" });
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

    /// <summary>A hand-over can close the motion between the read and the vote's update; the closing must win.</summary>
    [Test]
    public async Task TryAutoResolve_keeps_a_status_set_while_it_counted() {
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
        using var officerContext = Worker.CreateDbContext();
        officerContext.AddInterceptor(new OnFirstCommandInterceptor(() => _context.Motions
            .Where(m => m.Id == motionId)
            .Set(m => m.ApprovalStatus, MotionApprovalStatus.ClosedWithoutAction)
            .Update()));
        var officerRepo = new ChapterOfficerRepository(officerContext, AuditLog, new RoleRepository(officerContext));

        var result = _motionRepo.TryAutoResolve(motionId, officerRepo);

        await Assert.That(result).IsFalse();
        await Assert.That(_motionRepo.Get(motionId)!.ApprovalStatus).IsEqualTo(MotionApprovalStatus.ClosedWithoutAction);
        await Assert.That(_context.MembershipApplications.Single(a => a.Id == appId).Status).IsEqualTo(ApplicationStatus.Pending);
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

    private Guid SeedStateChapter() {
        var stateChapterId = Guid.NewGuid();
        _context.Insert(new Chapter {
            Id = stateChapterId,
            Name = "State Chapter",
            ShortCode = "stt",
            ParentChapterId = _chapterId
        });
        return stateChapterId;
    }

    private Guid SeedApplication(Guid? chapterId, ApplicationStatus status, bool requiresFederalApproval) {
        var appId = Guid.NewGuid();
        _context.Insert(new MembershipApplication {
            Id = appId,
            FirstName = "Test",
            LastName = "Applicant",
            Email = "test@example.com",
            ChapterId = chapterId,
            Status = status,
            RequiresFederalApproval = requiresFederalApproval,
            SubmittedAt = DateTime.UtcNow,
            EntryDate = DateTime.UtcNow
        });
        return appId;
    }

    private Guid SeedFederalMotion(Guid appId) {
        var motionId = SeedMotion(linkedAppId: appId);
        _context.MembershipApplications
            .Where(a => a.Id == appId)
            .Set(a => a.FederalApprovalMotionId, (Guid?)motionId)
            .Update();
        return motionId;
    }

    private ApplicationStatus StatusOf(Guid appId)
        => _context.MembershipApplications.Where(a => a.Id == appId).First().Status;

    [Test]
    public async Task Approval_waits_for_federal_when_required() {
        var appId = SeedApplication(SeedStateChapter(), ApplicationStatus.Pending, true);
        var motionId = SeedMotion(linkedAppId: appId);

        _motionRepo.UpdatePendingApprovalStatus(motionId, MotionApprovalStatus.Approved);

        await Assert.That(StatusOf(appId)).IsEqualTo(ApplicationStatus.PendingFederalApproval);
    }

    [Test]
    public async Task Approval_of_federal_step_approves() {
        var appId = SeedApplication(SeedStateChapter(), ApplicationStatus.PendingFederalApproval, true);
        var motionId = SeedFederalMotion(appId);

        _motionRepo.UpdatePendingApprovalStatus(motionId, MotionApprovalStatus.Approved);

        await Assert.That(StatusOf(appId)).IsEqualTo(ApplicationStatus.Approved);
    }

    [Test]
    public async Task Motion_leaves_decided_application_alone() {
        var appId = SeedApplication(SeedStateChapter(), ApplicationStatus.Rejected, false);
        var motionId = SeedMotion(linkedAppId: appId);

        _motionRepo.UpdatePendingApprovalStatus(motionId, MotionApprovalStatus.Approved);

        await Assert.That(StatusOf(appId)).IsEqualTo(ApplicationStatus.Rejected);
    }

    [Test]
    public async Task Late_federal_motion_leaves_approved_application_alone() {
        var appId = SeedApplication(SeedStateChapter(), ApplicationStatus.Approved, true);
        var federalMotionId = SeedFederalMotion(appId);

        _motionRepo.UpdatePendingApprovalStatus(federalMotionId, MotionApprovalStatus.Rejected);

        await Assert.That(StatusOf(appId)).IsEqualTo(ApplicationStatus.Approved);
    }

    [Test]
    public async Task Chapter_motion_leaves_federal_step_open() {
        var appId = SeedApplication(SeedStateChapter(), ApplicationStatus.PendingFederalApproval, true);
        var chapterMotionId = SeedMotion(linkedAppId: appId);
        SeedFederalMotion(appId);

        _motionRepo.UpdatePendingApprovalStatus(chapterMotionId, MotionApprovalStatus.Rejected);

        await Assert.That(StatusOf(appId)).IsEqualTo(ApplicationStatus.PendingFederalApproval);
    }

    [Test]
    public async Task Root_chapter_application_approves_directly() {
        var appId = SeedApplication(_chapterId, ApplicationStatus.Pending, true);
        var motionId = SeedMotion(linkedAppId: appId);

        _motionRepo.UpdatePendingApprovalStatus(motionId, MotionApprovalStatus.Approved);

        await Assert.That(StatusOf(appId)).IsEqualTo(ApplicationStatus.Approved);
    }

    [Test]
    public async Task Rejection_rejects_in_both_steps() {
        var stateChapterId = SeedStateChapter();
        var pendingId = SeedApplication(stateChapterId, ApplicationStatus.Pending, true);
        var federalId = SeedApplication(stateChapterId, ApplicationStatus.PendingFederalApproval, true);

        _motionRepo.UpdatePendingApprovalStatus(SeedMotion(linkedAppId: pendingId), MotionApprovalStatus.Rejected);
        _motionRepo.UpdatePendingApprovalStatus(SeedFederalMotion(federalId), MotionApprovalStatus.Rejected);

        await Assert.That(StatusOf(pendingId)).IsEqualTo(ApplicationStatus.Rejected);
        await Assert.That(StatusOf(federalId)).IsEqualTo(ApplicationStatus.Rejected);
    }

    [Test]
    public async Task Division_linking_application_waits_for_federal() {
        var appId = SeedApplication(null, ApplicationStatus.PendingDivisionLinking, true);
        var motionId = SeedMotion(linkedAppId: appId);

        _motionRepo.UpdatePendingApprovalStatus(motionId, MotionApprovalStatus.Approved);

        await Assert.That(StatusOf(appId)).IsEqualTo(ApplicationStatus.PendingFederalApproval);
    }

    [Test]
    public async Task Pending_close_skips_decided_motion() {
        var pendingId = SeedMotion();
        var decidedId = SeedMotion(MotionApprovalStatus.Approved);

        var pendingClosed = _motionRepo.UpdatePendingApprovalStatus(pendingId, MotionApprovalStatus.ClosedWithoutAction);
        var decidedClosed = _motionRepo.UpdatePendingApprovalStatus(decidedId, MotionApprovalStatus.ClosedWithoutAction);

        await Assert.That(pendingClosed).IsTrue();
        await Assert.That(_motionRepo.Get(pendingId)!.ApprovalStatus).IsEqualTo(MotionApprovalStatus.ClosedWithoutAction);
        await Assert.That(decidedClosed).IsFalse();
        await Assert.That(_motionRepo.Get(decidedId)!.ApprovalStatus).IsEqualTo(MotionApprovalStatus.Approved);
    }

    [Test]
    public async Task Cascade_keeps_status_changed_after_read() {
        var appId = SeedApplication(SeedStateChapter(), ApplicationStatus.Pending, false);
        var motionId = SeedMotion(linkedAppId: appId);
        // MariaDB's snapshot isolation would abort the stale update on its own; MySQL and older MariaDB servers let it through.
        _context.Execute("SET SESSION innodb_snapshot_isolation = OFF");
        _context.AddInterceptor(new BeforeUpdateInterceptor(MembershipApplication.TableName, () => {
            using var other = Worker.CreateDbContext();
            other.MembershipApplications.Where(a => a.Id == appId).Set(a => a.Status, ApplicationStatus.Rejected).Update();
        }));

        _motionRepo.UpdatePendingApprovalStatus(motionId, MotionApprovalStatus.Approved);

        await Assert.That(StatusOf(appId)).IsEqualTo(ApplicationStatus.Rejected);
    }

    [Test]
    public async Task GetByLinkedApplicationId_returns_the_oldest_linked_motion() {
        var seed = new TestDataBuilder(_context);
        var application = seed.SeedMembershipApplication();
        var newer = seed.SeedMotion(_chapterId, title: "Newer");
        var older = seed.SeedMotion(_chapterId, title: "Older");
        _context.Motions.Where(m => m.Id == newer.Id)
            .Set(m => m.LinkedMembershipApplicationId, application.Id)
            .Set(m => m.CreatedAt, new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc))
            .Update();
        _context.Motions.Where(m => m.Id == older.Id)
            .Set(m => m.LinkedMembershipApplicationId, application.Id)
            .Set(m => m.CreatedAt, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc))
            .Update();

        var found = _motionRepo.GetByLinkedApplicationId(application.Id);

        await Assert.That(found!.Id).IsEqualTo(older.Id);
    }
}
