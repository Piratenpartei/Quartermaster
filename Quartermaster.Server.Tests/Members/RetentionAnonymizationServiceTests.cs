using System;
using System.Linq;
using System.Threading.Tasks;
using LinqToDB;
using Microsoft.Extensions.Logging.Abstractions;
using Quartermaster.Api.MembershipApplications;
using Quartermaster.Data;
using Quartermaster.Data.AuditLog;
using Quartermaster.Data.DueSelector;
using Quartermaster.Data.Members;
using Quartermaster.Data.MembershipApplications;
using Quartermaster.Server.Members;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.Members;

public class RetentionAnonymizationServiceTests : RepositoryTestBase {
    private DbContext _context = default!;
    private RetentionAnonymizationService _svc = default!;

    [Before(Test)]
    public void Setup() {
        _context = Db;
        var memberRepo = new MemberRepository(_context, AuditLog);
        var applicationRepo = new MembershipApplicationRepository(_context, AuditLog);
        var selectionRepo = new DueSelectionRepository(_context, AuditLog);
        _svc = new RetentionAnonymizationService(memberRepo, applicationRepo, selectionRepo, _context,
            NullLogger<RetentionAnonymizationService>.Instance);
    }

    [Test]
    public async Task Member_within_window_is_not_anonymized() {
        var id = SeedMember(exitDate: new DateTime(2024, 6, 15));
        var summary = _svc.RunOnce(new DateTime(2034, 12, 31));
        await Assert.That(summary.MembersAnonymized).IsEqualTo(0);
        var m = _context.Members.Where(x => x.Id == id).First();
        await Assert.That(m.AnonymizedAt).IsNull();
        await Assert.That(m.EMail).IsEqualTo("retained@example.com");
    }

    [Test]
    public async Task Member_past_window_is_anonymized_preserving_name_and_birthday() {
        var dob = new DateTime(1980, 4, 1);
        var id = SeedMember(exitDate: new DateTime(2024, 6, 15), firstName: "Jana", lastName: "Tester", dob: dob);
        var summary = _svc.RunOnce(new DateTime(2035, 1, 2));
        await Assert.That(summary.MembersAnonymized).IsEqualTo(1);
        var m = _context.Members.Where(x => x.Id == id).First();
        await Assert.That(m.AnonymizedAt).IsNotNull();
        await Assert.That(m.EMail).IsNull();
        await Assert.That(m.Street).IsNull();
        // Preserved for re-join detection
        await Assert.That(m.FirstName).IsEqualTo("Jana");
        await Assert.That(m.LastName).IsEqualTo("Tester");
        await Assert.That(m.DateOfBirth).IsEqualTo(dob);
    }

    [Test]
    public async Task Active_member_with_no_exit_is_never_anonymized() {
        SeedMember(exitDate: null);
        var summary = _svc.RunOnce(new DateTime(2099, 1, 1));
        await Assert.That(summary.MembersAnonymized).IsEqualTo(0);
    }

    [Test]
    public async Task Already_anonymized_member_is_skipped_on_subsequent_runs() {
        var id = SeedMember(exitDate: new DateTime(2010, 1, 1));
        _svc.RunOnce(new DateTime(2035, 1, 2));
        var firstStamp = _context.Members.Where(x => x.Id == id).First().AnonymizedAt;
        var second = _svc.RunOnce(new DateTime(2035, 6, 1));
        await Assert.That(second.MembersAnonymized).IsEqualTo(0);
        var laterStamp = _context.Members.Where(x => x.Id == id).First().AnonymizedAt;
        await Assert.That(laterStamp).IsEqualTo(firstStamp);
    }

    [Test]
    public async Task Application_deferred_while_matching_active_member_exists() {
        var dob = new DateTime(1985, 3, 14);
        // Active member matching the application by name+DOB
        SeedMember(exitDate: null, firstName: "Linked", lastName: "Person", dob: dob);
        var appId = SeedApplication(processedAt: new DateTime(2010, 1, 1), firstName: "Linked", lastName: "Person", dob: dob);

        var summary = _svc.RunOnce(new DateTime(2099, 1, 1));
        await Assert.That(summary.ApplicationsAnonymized).IsEqualTo(0);
        var app = _context.MembershipApplications.Where(a => a.Id == appId).First();
        await Assert.That(app.AnonymizedAt).IsNull();
        await Assert.That(app.EMail).IsEqualTo("retained@app.example");
    }

    [Test]
    public async Task Application_anonymized_when_no_matching_member_exists() {
        var appId = SeedApplication(processedAt: new DateTime(2010, 1, 1), firstName: "Orphan", lastName: "Application", dob: new DateTime(1990, 1, 1));
        var summary = _svc.RunOnce(new DateTime(2099, 1, 1));
        await Assert.That(summary.ApplicationsAnonymized).IsEqualTo(1);
        var app = _context.MembershipApplications.Where(a => a.Id == appId).First();
        await Assert.That(app.EMail).IsEqualTo("");
        await Assert.That(app.AnonymizedAt).IsNotNull();
        // Preserved
        await Assert.That(app.FirstName).IsEqualTo("Orphan");
        await Assert.That(app.LastName).IsEqualTo("Application");
    }

    [Test]
    public async Task Application_anonymized_when_linked_member_is_also_past_window() {
        var dob = new DateTime(1985, 3, 14);
        SeedMember(exitDate: new DateTime(2010, 1, 1), firstName: "Old", lastName: "Member", dob: dob);
        var appId = SeedApplication(processedAt: new DateTime(2010, 1, 1), firstName: "Old", lastName: "Member", dob: dob);

        // Same run: member crosses threshold and gets anonymized; application should follow on the same run
        // because the matching Member is now anonymized → LinkedMemberStillInRetention sees no live match.
        var summary = _svc.RunOnce(new DateTime(2035, 1, 2));
        await Assert.That(summary.MembersAnonymized).IsEqualTo(1);
        await Assert.That(summary.ApplicationsAnonymized).IsEqualTo(1);
        var app = _context.MembershipApplications.Where(a => a.Id == appId).First();
        await Assert.That(app.AnonymizedAt).IsNotNull();
    }

    private Guid SeedMember(
        DateTime? exitDate,
        string firstName = "Test",
        string lastName = "Member",
        DateTime? dob = null) {
        var id = Guid.NewGuid();
        _context.Insert(new Member {
            Id = id,
            MemberNumber = Random.Shared.Next(100000, 999999),
            FirstName = firstName,
            LastName = lastName,
            DateOfBirth = dob ?? new DateTime(1980, 1, 1),
            ExitDate = exitDate,
            EMail = "retained@example.com",
            Street = "Teststr. 1",
            City = "Berlin",
            LastImportedAt = DateTime.UtcNow
        });
        return id;
    }

    private Guid SeedApplication(
        DateTime processedAt,
        string firstName = "Test",
        string lastName = "Member",
        DateTime? dob = null) {
        var id = Guid.NewGuid();
        _context.Insert(new MembershipApplication {
            Id = id,
            FirstName = firstName,
            LastName = lastName,
            DateOfBirth = dob ?? new DateTime(1980, 1, 1),
            Citizenship = "DE",
            EMail = "retained@app.example",
            PhoneNumber = "+49 30 1234567",
            AddressStreet = "Beispielweg",
            AddressHouseNbr = "7",
            AddressPostCode = "10115",
            AddressCity = "Berlin",
            EntryDate = processedAt.AddYears(-1),
            SubmittedAt = processedAt.AddDays(-7),
            ProcessedAt = processedAt,
            Status = ApplicationStatus.Approved
        });
        return id;
    }

}
