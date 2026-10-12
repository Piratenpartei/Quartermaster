using System;
using LinqToDB;
using Quartermaster.Api.AssemblyMotions;
using Quartermaster.Data;
using Quartermaster.Data.Chapters;
using Quartermaster.Data.Events;
using Quartermaster.Server.AssemblyMotions;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.AssemblyMotions;

public class AssemblyMotionEligibilityTests : RepositoryTestBase {
    private DbContext _context = default!;
    private TestDataBuilder _builder = default!;
    private ChapterRepository _chapterRepo = default!;
    private AssemblyMotionEligibility _eligibility = default!;
    private Chapter _bund = default!;
    private Chapter _land = default!;
    private Chapter _kreis = default!;
    private Event _landEvent = default!;

    [Before(Test)]
    public void Setup() {
        _context = Db;
        _builder = new TestDataBuilder(_context);
        _chapterRepo = new ChapterRepository(_context, AuditLog);
        _eligibility = new AssemblyMotionEligibility(_context, _chapterRepo);

        var chain = _builder.SeedChapterHierarchy("Bund", "Land", "Kreis");
        _bund = chain[0];
        _land = chain[1];
        _kreis = chain[2];
        _landEvent = _builder.SeedEvent(_land.Id);
    }

    [Test]
    public async Task Member_of_event_chapter_is_eligible() {
        var user = _builder.SeedUser();
        _builder.SeedMember(chapterId: _land.Id, userId: user.Id);

        var result = _eligibility.Check(user.Id, _landEvent, DateTime.UtcNow);

        await Assert.That(result.IsEligible).IsTrue();
        await Assert.That(result.Problem).IsEqualTo(EligibilityProblem.None);
    }

    [Test]
    public async Task Member_of_descendant_chapter_is_eligible() {
        var user = _builder.SeedUser();
        _builder.SeedMember(chapterId: _kreis.Id, userId: user.Id);

        var result = _eligibility.Check(user.Id, _landEvent, DateTime.UtcNow);

        await Assert.That(result.IsEligible).IsTrue();
    }

    [Test]
    public async Task Member_of_foreign_chapter_is_outside() {
        var foreignChapter = _builder.SeedChapter("Other Land", _bund.Id);
        var user = _builder.SeedUser();
        _builder.SeedMember(chapterId: foreignChapter.Id, userId: user.Id);

        var result = _eligibility.Check(user.Id, _landEvent, DateTime.UtcNow);

        await Assert.That(result.IsEligible).IsFalse();
        await Assert.That(result.Problem).IsEqualTo(EligibilityProblem.OutsideChapter);
    }

    [Test]
    public async Task Member_of_parent_chapter_is_outside() {
        var user = _builder.SeedUser();
        _builder.SeedMember(chapterId: _bund.Id, userId: user.Id);

        var result = _eligibility.Check(user.Id, _landEvent, DateTime.UtcNow);

        await Assert.That(result.IsEligible).IsFalse();
        await Assert.That(result.Problem).IsEqualTo(EligibilityProblem.OutsideChapter);
    }

    [Test]
    public async Task Exited_member_is_not_eligible() {
        var user = _builder.SeedUser();
        _builder.SeedMember(chapterId: _land.Id, userId: user.Id, exitDate: DateTime.UtcNow.Date.AddDays(-1));

        var result = _eligibility.Check(user.Id, _landEvent, DateTime.UtcNow);

        await Assert.That(result.IsEligible).IsFalse();
        await Assert.That(result.Problem).IsEqualTo(EligibilityProblem.Exited);
    }

    [Test]
    public async Task Exit_today_counts_as_exited() {
        var user = _builder.SeedUser();
        _builder.SeedMember(chapterId: _land.Id, userId: user.Id, exitDate: DateTime.UtcNow.Date);

        var result = _eligibility.Check(user.Id, _landEvent, DateTime.UtcNow);

        await Assert.That(result.IsEligible).IsFalse();
        await Assert.That(result.Problem).IsEqualTo(EligibilityProblem.Exited);
    }

    [Test]
    public async Task Pending_member_is_not_eligible() {
        var user = _builder.SeedUser();
        _builder.SeedMember(chapterId: _land.Id, userId: user.Id, isPending: true);

        var result = _eligibility.Check(user.Id, _landEvent, DateTime.UtcNow);

        await Assert.That(result.IsEligible).IsFalse();
        await Assert.That(result.Problem).IsEqualTo(EligibilityProblem.Pending);
    }

    [Test]
    public async Task Floating_member_is_not_eligible() {
        var user = _builder.SeedUser();
        var member = _builder.SeedMember(chapterId: _land.Id, userId: user.Id);
        _context.Members.Where(m => m.Id == member.Id).Set(m => m.MemberNumber, (int?)null).Update();

        var result = _eligibility.Check(user.Id, _landEvent, DateTime.UtcNow);

        await Assert.That(result.IsEligible).IsFalse();
        await Assert.That(result.Problem).IsEqualTo(EligibilityProblem.NoMemberNumber);
    }

    [Test]
    public async Task Anonymized_member_is_not_eligible() {
        var user = _builder.SeedUser();
        var member = _builder.SeedMember(chapterId: _land.Id, userId: user.Id);
        _context.Members.Where(m => m.Id == member.Id).Set(m => m.AnonymizedAt, DateTime.UtcNow).Update();

        var result = _eligibility.Check(user.Id, _landEvent, DateTime.UtcNow);

        await Assert.That(result.IsEligible).IsFalse();
        await Assert.That(result.Problem).IsEqualTo(EligibilityProblem.Anonymized);
    }

    [Test]
    public async Task User_without_member_is_not_eligible() {
        var user = _builder.SeedUser();

        var result = _eligibility.Check(user.Id, _landEvent, DateTime.UtcNow);

        await Assert.That(result.IsEligible).IsFalse();
        await Assert.That(result.Problem).IsEqualTo(EligibilityProblem.NoLinkedMember);
        await Assert.That(result.Member).IsNull();
    }

    [Test]
    public async Task Anonymous_is_not_signed_in() {
        var result = _eligibility.Check(null, _landEvent, DateTime.UtcNow);

        await Assert.That(result.IsEligible).IsFalse();
        await Assert.That(result.Problem).IsEqualTo(EligibilityProblem.NotSignedIn);
        await Assert.That(result.Member).IsNull();
    }

    [Test]
    public async Task Chapterless_member_is_eligible_for_federal_event() {
        var bundEvent = _builder.SeedEvent(_bund.Id);
        var user = _builder.SeedUser();
        _builder.SeedMember(chapterId: null, userId: user.Id);

        var result = _eligibility.Check(user.Id, bundEvent, DateTime.UtcNow);

        await Assert.That(result.IsEligible).IsTrue();
    }

    [Test]
    public async Task Chapterless_member_is_outside_for_state_event() {
        var user = _builder.SeedUser();
        _builder.SeedMember(chapterId: null, userId: user.Id);

        var result = _eligibility.Check(user.Id, _landEvent, DateTime.UtcNow);

        await Assert.That(result.IsEligible).IsFalse();
        await Assert.That(result.Problem).IsEqualTo(EligibilityProblem.OutsideChapter);
    }

    [Test]
    public async Task Negative_member_number_is_eligible() {
        var user = _builder.SeedUser();
        _builder.SeedMember(chapterId: _land.Id, userId: user.Id, memberNumber: -5);

        var result = _eligibility.Check(user.Id, _landEvent, DateTime.UtcNow);

        await Assert.That(result.IsEligible).IsTrue();
    }
}
