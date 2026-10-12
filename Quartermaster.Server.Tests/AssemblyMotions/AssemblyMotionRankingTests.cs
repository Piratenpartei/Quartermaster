using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Quartermaster.Api.AssemblyMotions;
using Quartermaster.Data.AssemblyMotions;
using Quartermaster.Server.AssemblyMotions;

namespace Quartermaster.Server.Tests.AssemblyMotions;

public class AssemblyMotionRankingTests {
    private static readonly DateTime Base = new(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);

    private static AssemblyMotionType Type(string code, int sortOrder) => new() {
        Id = Guid.NewGuid(),
        Code = code,
        Name = code,
        SortOrder = sortOrder
    };

    private static AssemblyMotion Motion(AssemblyMotionType type, int number, int submittedMinutes, AssemblyMotionStatus status = AssemblyMotionStatus.Submitted) => new() {
        Id = Guid.NewGuid(),
        TypeId = type.Id,
        Number = number,
        Status = status,
        SubmittedAt = Base.AddMinutes(submittedMinutes)
    };

    private static Dictionary<Guid, AssemblyMotionType> Types(params AssemblyMotionType[] types)
        => types.ToDictionary(t => t.Id);

    [Test]
    public async Task Ranking_order_and_tie_breaks() {
        var early = Type("A", 2);
        var late = Type("B", 1);
        var top = Motion(early, 5, 30);
        var tieEarlier = Motion(early, 3, 10);
        var tieOtherType = Motion(late, 9, 20);
        var tieNumber2 = Motion(early, 2, 20);
        var tieNumber1 = Motion(early, 1, 20);
        var counts = new Dictionary<Guid, int> {
            [top.Id] = 7,
            [tieEarlier.Id] = 4,
            [tieOtherType.Id] = 4,
            [tieNumber2.Id] = 4,
            [tieNumber1.Id] = 4
        };

        var ranking = AssemblyMotionRanking.Compute(
            [top, tieNumber2, tieOtherType, tieEarlier, tieNumber1], Types(early, late), counts);

        await Assert.That(ranking.Select(r => r.Motion.Id).SequenceEqual([
            top.Id, tieEarlier.Id, tieOtherType.Id, tieNumber1.Id, tieNumber2.Id
        ])).IsTrue();
        await Assert.That(ranking.Select(r => r.Rank).SequenceEqual([1, 2, 3, 4, 5])).IsTrue();
        await Assert.That(ranking[0].UpvoteCount).IsEqualTo(7);
    }

    [Test]
    public async Task Motions_without_upvotes_rank_last_with_zero() {
        var type = Type("A", 1);
        var voted = Motion(type, 2, 10);
        var unvoted = Motion(type, 1, 0);

        var ranking = AssemblyMotionRanking.Compute([unvoted, voted], Types(type), new Dictionary<Guid, int> { [voted.Id] = 1 });

        await Assert.That(ranking.Select(r => r.Motion.Id).SequenceEqual([voted.Id, unvoted.Id])).IsTrue();
        await Assert.That(ranking[1].UpvoteCount).IsEqualTo(0);
    }

    [Test]
    public async Task Insufficient_motion_drops_out_of_ranking() {
        var type = Type("A", 1);
        var admitted = Motion(type, 1, 0, AssemblyMotionStatus.Admitted);
        var submitted = Motion(type, 2, 1);
        var insufficient = Motion(type, 3, 2, AssemblyMotionStatus.FormallyInsufficient);
        var withdrawn = Motion(type, 4, 3, AssemblyMotionStatus.Withdrawn);
        var collecting = Motion(type, 5, 4, AssemblyMotionStatus.CollectingCoSubmitters);
        var counts = new Dictionary<Guid, int> { [insufficient.Id] = 99, [withdrawn.Id] = 99 };

        var ranking = AssemblyMotionRanking.Compute([admitted, submitted, insufficient, withdrawn, collecting], Types(type), counts);

        await Assert.That(ranking.Select(r => r.Motion.Id).SequenceEqual([admitted.Id, submitted.Id])).IsTrue();
    }

    [Test]
    public async Task Full_ties_are_ordered_by_motion_id() {
        var type = Type("A", 1);
        var first = Motion(type, 1, 0);
        var second = Motion(type, 1, 0);
        var ordered = new[] { first, second }
            .OrderBy(m => m.Id)
            .Select(m => m.Id)
            .ToArray();

        var forward = AssemblyMotionRanking.Compute([first, second], Types(type), new Dictionary<Guid, int>());
        var backward = AssemblyMotionRanking.Compute([second, first], Types(type), new Dictionary<Guid, int>());

        await Assert.That(forward.Select(r => r.Motion.Id).SequenceEqual(ordered)).IsTrue();
        await Assert.That(backward.Select(r => r.Motion.Id).SequenceEqual(ordered)).IsTrue();
    }

    [Test]
    public async Task Unknown_type_throws() {
        var known = Type("A", 1);
        var motion = Motion(Type("B", 2), 1, 0);

        await Assert.That(() => AssemblyMotionRanking.Compute([motion], Types(known), new Dictionary<Guid, int>()))
            .Throws<InvalidOperationException>();
    }
}
