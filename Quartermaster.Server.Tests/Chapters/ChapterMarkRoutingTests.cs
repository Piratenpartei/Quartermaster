using Quartermaster.Api.Chapters;
using Quartermaster.Data.Chapters;

namespace Quartermaster.Server.Tests.Chapters;

public class ChapterMarkRoutingTests {
    private static Chapter Create(bool noMemberAdministration = false, bool unableToAct = false) => new() {
        Id = Guid.NewGuid(),
        NoMemberAdministration = noMemberAdministration,
        UnableToAct = unableToAct
    };

    [Test]
    public async Task Unmarked_chapter_decides_itself() {
        var chain = new List<Chapter> { Create(), Create() };

        var route = ChapterMarkRouting.Resolve(chain, ChapterMarkPurpose.MemberAdministration);

        await Assert.That(route.DecidingChapterId).IsEqualTo(chain[0].Id);
        await Assert.That(route.Skip).IsNull();
    }

    [Test]
    public async Task Marked_chapter_goes_to_parent() {
        var chain = new List<Chapter> { Create(unableToAct: true), Create(), Create() };

        var route = ChapterMarkRouting.Resolve(chain, ChapterMarkPurpose.Motion);

        await Assert.That(route.DecidingChapterId).IsEqualTo(chain[1].Id);
        await Assert.That(route.Skip).IsEqualTo(new ChapterMarkSkip(chain[0].Id, ChapterMark.UnableToAct));
    }

    [Test]
    public async Task Consecutive_marks_skip_to_first_unmarked() {
        var chain = new List<Chapter> { Create(noMemberAdministration: true), Create(unableToAct: true), Create(), Create() };

        var route = ChapterMarkRouting.Resolve(chain, ChapterMarkPurpose.MemberAdministration);

        await Assert.That(route.DecidingChapterId).IsEqualTo(chain[2].Id);
        await Assert.That(route.Skip).IsEqualTo(new ChapterMarkSkip(chain[0].Id, ChapterMark.NoMemberAdministration));
    }

    [Test]
    public async Task No_member_administration_is_ignored_for_motions() {
        var chain = new List<Chapter> { Create(noMemberAdministration: true), Create() };

        var route = ChapterMarkRouting.Resolve(chain, ChapterMarkPurpose.Motion);

        await Assert.That(route.DecidingChapterId).IsEqualTo(chain[0].Id);
        await Assert.That(route.Skip).IsNull();
    }

    [Test]
    public async Task Unable_to_act_wins_over_no_member_administration() {
        var chain = new List<Chapter> { Create(noMemberAdministration: true, unableToAct: true), Create() };

        var route = ChapterMarkRouting.Resolve(chain, ChapterMarkPurpose.MemberAdministration);

        await Assert.That(route.Skip!.Reason).IsEqualTo(ChapterMark.UnableToAct);
    }

    [Test]
    public async Task Root_decides_even_when_marked() {
        var chain = new List<Chapter> { Create(unableToAct: true), Create(unableToAct: true) };

        var route = ChapterMarkRouting.Resolve(chain, ChapterMarkPurpose.Motion);

        await Assert.That(route.DecidingChapterId).IsEqualTo(chain[1].Id);
        await Assert.That(route.Skip).IsEqualTo(new ChapterMarkSkip(chain[0].Id, ChapterMark.UnableToAct));
    }

    [Test]
    public async Task Empty_chain_throws() {
        await Assert.That(() => ChapterMarkRouting.Resolve([], ChapterMarkPurpose.Motion)).Throws<ArgumentException>();
    }
}
