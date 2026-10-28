using System;
using System.Linq;
using System.Threading.Tasks;
using Quartermaster.Api.ApplicantRegister;
using Quartermaster.Api.MembershipApplications;
using Quartermaster.Data.ApplicantRegister;
using Quartermaster.Data.MembershipApplications;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.MembershipApplications;

public class MembershipApplicationRepositoryTests : RepositoryTestBase {
    [Test]
    public async Task Federal_motion_claimed_once() {
        var applications = new MembershipApplicationRepository(Db, AuditLog);
        var application = new TestDataBuilder(Db).SeedMembershipApplication(status: ApplicationStatus.PendingFederalApproval);
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();

        var firstClaimed = applications.ClaimFederalApprovalMotion(application.Id, first);
        var secondClaimed = applications.ClaimFederalApprovalMotion(application.Id, second);

        await Assert.That(firstClaimed).IsTrue();
        await Assert.That(secondClaimed).IsFalse();
        await Assert.That(Db.MembershipApplications.Single(a => a.Id == application.Id).FederalApprovalMotionId).IsEqualTo(first);
    }

    [Test]
    public async Task Status_claimed_once() {
        var applications = new MembershipApplicationRepository(Db, AuditLog);
        var application = new TestDataBuilder(Db).SeedMembershipApplication(status: ApplicationStatus.PendingFederalApproval);

        var first = applications.ClaimStatus(application.Id, ApplicationStatus.PendingFederalApproval, ApplicationStatus.Approved, null);
        var second = applications.ClaimStatus(application.Id, ApplicationStatus.PendingFederalApproval, ApplicationStatus.Rejected, null);

        await Assert.That(first).IsTrue();
        await Assert.That(second).IsFalse();
        await Assert.That(Db.MembershipApplications.Single(a => a.Id == application.Id).Status).IsEqualTo(ApplicationStatus.Approved);
    }

    [Test]
    public async Task Status_not_claimed_when_no_longer_pending_federal() {
        var applications = new MembershipApplicationRepository(Db, AuditLog);
        var application = new TestDataBuilder(Db).SeedMembershipApplication(status: ApplicationStatus.Rejected);

        var claimed = applications.ClaimStatus(application.Id, ApplicationStatus.PendingFederalApproval, ApplicationStatus.Approved, null);

        await Assert.That(claimed).IsFalse();
        await Assert.That(Db.MembershipApplications.Single(a => a.Id == application.Id).Status).IsEqualTo(ApplicationStatus.Rejected);
    }

    [Test]
    public async Task Processed_at_stays_empty_while_pending_federal_approval() {
        var applications = new MembershipApplicationRepository(Db, AuditLog);
        var application = new TestDataBuilder(Db).SeedMembershipApplication();

        applications.UpdateStatus(application.Id, ApplicationStatus.PendingFederalApproval, null);

        var stored = Db.MembershipApplications.Single(a => a.Id == application.Id);
        await Assert.That(stored.Status).IsEqualTo(ApplicationStatus.PendingFederalApproval);
        await Assert.That(stored.ProcessedAt).IsNull();
    }

    [Test]
    public async Task Delete_removes_register_hits_and_audits_them() {
        var applications = new MembershipApplicationRepository(Db, AuditLog);
        var register = new ApplicantRegisterRepository(Db, AuditLog);
        var seed = new TestDataBuilder(Db);
        var application = seed.SeedMembershipApplication();
        var entry = seed.SeedRegisterEntry();
        register.AddHits(application.Id, [new ApplicationRegisterHit {
            EntryId = entry.Id,
            MatchedFields = RegisterMatchFields.FirstName,
            CreatedAt = DateTime.UtcNow
        }]);
        var hitId = register.HitsFor(application.Id).Single().Id;

        applications.Delete(application.Id);

        await Assert.That(Db.ApplicationRegisterHits.Count(h => h.ApplicationId == application.Id)).IsEqualTo(0);
        await Assert.That(Db.MembershipApplications.Count(a => a.Id == application.Id)).IsEqualTo(0);
        await Assert.That(AuditLog.GetForEntity("ApplicationRegisterHit", hitId).Count(a => a.Action == "Deleted")).IsEqualTo(1);
        await Assert.That(Db.ApplicantRegisterEntries.Count(e => e.Id == entry.Id)).IsEqualTo(1);
    }
}
