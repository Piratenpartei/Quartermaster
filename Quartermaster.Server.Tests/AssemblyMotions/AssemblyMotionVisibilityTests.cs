using System;
using Quartermaster.Api.AssemblyMotions;
using Quartermaster.Data.AssemblyMotions;
using Quartermaster.Server.AssemblyMotions;

namespace Quartermaster.Server.Tests.AssemblyMotions;

public class AssemblyMotionVisibilityTests {
    private static readonly AssemblyMotionViewer Anonymous = new(null, IsEligible: false, IsCommission: false);
    private static readonly AssemblyMotionViewer Eligible = new(Guid.NewGuid(), IsEligible: true, IsCommission: false);
    private static readonly AssemblyMotionViewer Commission = new(Guid.NewGuid(), IsEligible: false, IsCommission: true);

    [Test]
    [Arguments(AssemblyMotionStatus.CollectingCoSubmitters, null)]
    [Arguments(AssemblyMotionStatus.CollectingCoSubmitters, 7)]
    [Arguments(AssemblyMotionStatus.Submitted, 5)]
    [Arguments(AssemblyMotionStatus.Admitted, 5)]
    [Arguments(AssemblyMotionStatus.FormallyInsufficient, 5)]
    [Arguments(AssemblyMotionStatus.Withdrawn, null)]
    [Arguments(AssemblyMotionStatus.Withdrawn, 5)]
    [Arguments(AssemblyMotionStatus.Lapsed, null)]
    [Arguments(AssemblyMotionStatus.Lapsed, 5)]
    public async Task Released_motion_visible_to_everyone(AssemblyMotionStatus status, int? number) {
        var motion = BuildMotion(status, number);

        await Assert.That(AssemblyMotionVisibility.IsPublic(motion)).IsTrue();
        await Assert.That(AssemblyMotionVisibility.CanSee(motion, Anonymous)).IsTrue();
        await Assert.That(AssemblyMotionVisibility.CanSee(motion, Eligible)).IsTrue();
        await Assert.That(AssemblyMotionVisibility.CanSee(motion, Commission)).IsTrue();
    }

    [Test]
    public async Task Draft_visible_to_author_only() {
        var authorId = Guid.NewGuid();
        var motion = BuildMotion(AssemblyMotionStatus.Draft, number: null, createdByMemberId: authorId);
        var author = new AssemblyMotionViewer(authorId, IsEligible: false, IsCommission: false);
        var otherEligibleMember = new AssemblyMotionViewer(Guid.NewGuid(), IsEligible: true, IsCommission: false);

        await Assert.That(AssemblyMotionVisibility.IsPublic(motion)).IsFalse();
        await Assert.That(AssemblyMotionVisibility.CanSee(motion, author)).IsTrue();
        await Assert.That(AssemblyMotionVisibility.CanSee(motion, Anonymous)).IsFalse();
        await Assert.That(AssemblyMotionVisibility.CanSee(motion, otherEligibleMember)).IsFalse();
        await Assert.That(AssemblyMotionVisibility.CanSee(motion, Commission)).IsFalse();
    }

    [Test]
    public async Task Names_hidden_from_anonymous_only() {
        await Assert.That(AssemblyMotionVisibility.CanSeeNames(Anonymous, viewerIsSubmitter: false)).IsFalse();
        await Assert.That(AssemblyMotionVisibility.CanSeeNames(Eligible, viewerIsSubmitter: false)).IsTrue();
        await Assert.That(AssemblyMotionVisibility.CanSeeNames(Commission, viewerIsSubmitter: false)).IsTrue();
        await Assert.That(AssemblyMotionVisibility.CanSeeNames(Anonymous, viewerIsSubmitter: true)).IsTrue();
    }

    private static AssemblyMotion BuildMotion(AssemblyMotionStatus status, int? number, Guid? createdByMemberId = null) {
        return new AssemblyMotion {
            Status = status,
            Number = number,
            CreatedByMemberId = createdByMemberId ?? Guid.NewGuid()
        };
    }
}
