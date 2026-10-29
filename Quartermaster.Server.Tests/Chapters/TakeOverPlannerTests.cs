using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using LinqToDB;
using Microsoft.Extensions.DependencyInjection;
using Quartermaster.Api.Chapters;
using Quartermaster.Api.Forwarding;
using Quartermaster.Api.MembershipApplications;
using Quartermaster.Api.Motions;
using Quartermaster.Data;
using Quartermaster.Data.Chapters;
using Quartermaster.Data.Forwarding;
using Quartermaster.Data.MembershipApplications;
using Quartermaster.Data.Motions;
using Quartermaster.Data.Options;
using Quartermaster.Server.Chapters;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.Chapters;

public class TakeOverPlannerTests : IntegrationTestBase {
    private Chapter _root = null!;
    private Chapter _state = null!;
    private Chapter _district = null!;
    private Chapter _county = null!;

    public TakeOverPlannerTests() {
        DbContext.SupplementDefaults(Factory.Services);
        var chain = Builder.SeedChapterHierarchy("Bund", "Land", "Bezirk", "Kreis");
        (_root, _state, _district, _county) = (chain[0], chain[1], chain[2], chain[3]);
        SetLevel(_state, ChapterLevel.State);
    }

    private void SetLevel(Chapter chapter, ChapterLevel level) {
        Db.Chapters.Where(c => c.Id == chapter.Id).Set(c => c.Level, level).Update();
    }

    private void SetPeriod(Chapter chapter, int days) {
        Db.Chapters.Where(c => c.Id == chapter.Id).Set(c => c.TakeOverPeriodDays, days).Update();
    }

    private void Mark(Chapter chapter) {
        Db.Chapters.Where(c => c.Id == chapter.Id).Set(c => c.NoMemberAdministration, true).Update();
    }

    private void EnableApplicationForwarding() {
        using var scope = Factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<OptionRepository>().SetValue(ForwardingOptionKeys.MembershipApplicationEnabled, null, "true");
    }

    private void SetApplicationMode(Chapter chapter, ForwardingMode mode) {
        using var scope = Factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ForwardingSettingsRepository>()
            .SaveChapter(new ForwardingChapterSetting { ChapterId = chapter.Id, ApplicationMode = mode }, []);
    }

    private List<TakeOverCandidate> CandidatesAbove(Guid chapterId) {
        using var scope = Factory.Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<TakeOverPlanner>().CandidatesAbove(chapterId);
    }

    private Motion SeedMotion(Guid chapterId, Guid? applicationId) {
        var motion = Motion.Create(chapterId, "Anna Pirat", "anna@test.local", "Antrag", "Text", "<p>Text</p>", DateTime.UtcNow, applicationId, null);
        Db.Insert(motion);
        return motion;
    }

    private (TakeOverStep? Due, TakeOverStep? Next) Plan(Motion motion, MembershipApplication application, DateTime now) {
        using var scope = Factory.Services.CreateScope();
        var planner = scope.ServiceProvider.GetRequiredService<TakeOverPlanner>();
        return (planner.DueFor(motion, application, now), planner.NextFor(motion, application, now));
    }

    [Test]
    public async Task Candidates_skip_nothing_but_period_less_chapters() {
        var candidates = CandidatesAbove(_county.Id);

        await Assert.That(candidates.Select(c => c.ChapterId).ToList()).IsEquivalentTo([_district.Id, _state.Id, _root.Id]);
        await Assert.That(candidates[0].PeriodDays).IsNull();
        await Assert.That(candidates[1].PeriodDays).IsEqualTo(28);
        await Assert.That(candidates[2].PeriodDays).IsEqualTo(62);

        SetPeriod(_district, 14);

        await Assert.That(CandidatesAbove(_county.Id)[0].PeriodDays).IsEqualTo(14);
    }

    [Test]
    public async Task Marked_lv_does_not_handle() {
        Mark(_state);

        var candidates = CandidatesAbove(_county.Id);

        await Assert.That(candidates.Single(c => c.ChapterId == _state.Id).HandlesApplications).IsFalse();
        await Assert.That(candidates.Single(c => c.ChapterId == _district.Id).HandlesApplications).IsTrue();
    }

    [Test]
    public async Task Redmine_lv_does_not_handle_with_forwarding() {
        EnableApplicationForwarding();
        SetApplicationMode(_state, ForwardingMode.Redmine);

        var candidates = CandidatesAbove(_county.Id);

        await Assert.That(candidates.Single(c => c.ChapterId == _state.Id).HandlesApplications).IsFalse();
    }

    [Test]
    public async Task Quartermaster_lv_handles_with_forwarding() {
        EnableApplicationForwarding();
        SetApplicationMode(_state, ForwardingMode.Quartermaster);

        var candidates = CandidatesAbove(_county.Id);

        await Assert.That(candidates.Single(c => c.ChapterId == _state.Id).HandlesApplications).IsTrue();
    }

    [Test]
    public async Task Root_handles_with_forwarding_to_redmine() {
        EnableApplicationForwarding();
        SetApplicationMode(_root, ForwardingMode.Redmine);

        var candidates = CandidatesAbove(_county.Id);

        await Assert.That(candidates.Single(c => c.ChapterId == _root.Id).HandlesApplications).IsTrue();
    }

    [Test]
    public async Task Motion_at_root_has_no_candidates() {
        await Assert.That(CandidatesAbove(_root.Id)).IsEmpty();
    }

    [Test]
    public async Task Due_and_next_follow_the_application_start() {
        var application = Builder.SeedMembershipApplication(_county.Id);
        var motion = SeedMotion(_county.Id, application.Id);
        var start = application.SubmittedAt;

        var early = Plan(motion, application, start.AddDays(10));
        var late = Plan(motion, application, start.AddDays(30));

        await Assert.That(early.Due).IsNull();
        await Assert.That(early.Next!.ChapterId).IsEqualTo(_state.Id);
        await Assert.That(late.Due!.ChapterId).IsEqualTo(_state.Id);
        await Assert.That(late.Next!.ChapterId).IsEqualTo(_root.Id);
    }

    [Test]
    public async Task Not_pending_application_has_no_step() {
        var application = Builder.SeedMembershipApplication(_county.Id, status: ApplicationStatus.Approved);
        var motion = SeedMotion(_county.Id, application.Id);

        var plan = Plan(motion, application, application.SubmittedAt.AddDays(500));

        await Assert.That(plan.Due).IsNull();
        await Assert.That(plan.Next).IsNull();
    }

    [Test]
    public async Task Reduction_motion_has_no_step() {
        var application = Builder.SeedMembershipApplication(_county.Id);
        var motion = SeedMotion(_county.Id, null);

        var plan = Plan(motion, application, application.SubmittedAt.AddDays(500));

        await Assert.That(plan.Due).IsNull();
        await Assert.That(plan.Next).IsNull();
    }

    [Test]
    public async Task Handed_over_motion_has_no_step() {
        var application = Builder.SeedMembershipApplication(_county.Id);
        var motion = SeedMotion(_county.Id, application.Id);
        motion.HandedOverToMotionId = Guid.NewGuid();

        var plan = Plan(motion, application, application.SubmittedAt.AddDays(500));

        await Assert.That(plan.Due).IsNull();
        await Assert.That(plan.Next).IsNull();
    }

    [Test]
    public async Task Decided_motion_has_no_step() {
        var application = Builder.SeedMembershipApplication(_county.Id);
        var motion = SeedMotion(_county.Id, application.Id);
        motion.ApprovalStatus = MotionApprovalStatus.ClosedWithoutAction;

        var plan = Plan(motion, application, application.SubmittedAt.AddDays(500));

        await Assert.That(plan.Due).IsNull();
        await Assert.That(plan.Next).IsNull();
    }
}
