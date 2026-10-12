using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using LinqToDB;
using Quartermaster.Api;
using Quartermaster.Api.AssemblyMotions;
using Quartermaster.Api.Events;
using Quartermaster.Data.AssemblyMotions;
using Quartermaster.Data.Chapters;
using Quartermaster.Data.Members;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.Integration.AssemblyMotions;

public class AssemblyMotionCompetitionEndpointTests : IntegrationTestBase {
    private record Participant(Member Member, string Token);

    private (Chapter Chapter, AssemblyMotionPortal Portal, AssemblyMotionType Type) SeedPortal(Chapter? existingChapter = null) {
        var chapter = existingChapter ?? Builder.SeedChapter("Chapter");
        var ev = Builder.SeedEvent(chapter.Id, publicName: "BPT " + Guid.NewGuid().ToString("N")[..6], visibility: EventVisibility.Public);
        var portal = new AssemblyMotionPortal {
            EventId = ev.Id,
            OpensAt = DateTime.UtcNow.AddDays(-1),
            ClosesAt = DateTime.UtcNow.AddDays(30),
            CreatedAt = DateTime.UtcNow
        };
        Db.Insert(portal);
        var type = new AssemblyMotionType {
            PortalId = portal.Id,
            Code = "SÄA",
            Name = "Satzungsänderungsantrag",
            MinSubmitters = 1
        };
        Db.Insert(type);
        return (chapter, portal, type);
    }

    private Participant SeedParticipant(Chapter chapter, string firstName, string lastName) {
        var (user, token) = Builder.SeedAuthenticatedUser();
        var member = Builder.SeedMember(chapterId: chapter.Id, firstName: firstName, lastName: lastName, userId: user.Id);
        return new Participant(member, token);
    }

    private string SeedCommissionToken(Chapter chapter)
        => Builder.SeedAuthenticatedUser(chapterPermissions: new() {
            [chapter.Id] = [PermissionIdentifier.ManageAssemblyMotions]
        }).Token;

    private async Task<AssemblyMotionDetailDTO> CreateMotion(Participant author, AssemblyMotionPortal portal, AssemblyMotionType type, bool release, string title = "Antrag") {
        using var client = await AuthenticatedClientWithCsrfAsync(author.Token);
        var response = await client.PostAsJsonAsync($"/api/assemblymotions/portals/{portal.Id}/motions", new AssemblyMotionSaveRequest {
            TypeId = type.Id,
            Title = title,
            TextMarkdown = "Text",
            ReasonMarkdown = "Grund"
        });
        var created = (await response.Content.ReadFromJsonAsync<AssemblyMotionDetailDTO>())!;
        if (release)
            await client.PostAsJsonAsync($"/api/assemblymotions/motions/{created.Id}/release", new { });
        return created;
    }

    private async Task<HttpResponseMessage> Declare(string token, Guid motionId, Guid otherMotionId) {
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        return await client.PostAsJsonAsync($"/api/assemblymotions/motions/{motionId}/competitions", new AssemblyMotionCompetitionDeclareRequest {
            OtherMotionId = otherMotionId
        });
    }

    private async Task<HttpResponseMessage> Remove(string token, Guid competitionId) {
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        return await client.DeleteAsync($"/api/assemblymotions/competitions/{competitionId}");
    }

    private async Task<HttpResponseMessage> SetFlag(string token, Guid motionId, bool value) {
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        return await client.PutAsJsonAsync($"/api/assemblymotions/motions/{motionId}/competing-flag", new AssemblyMotionCompetingFlagRequest {
            Value = value
        });
    }

    [Test]
    public async Task Declare_requires_submitter_or_commission() {
        var (chapter, portal, type) = SeedPortal();
        var author = SeedParticipant(chapter, "Anna", "Autor");
        var other = SeedParticipant(chapter, "Bernd", "Anderer");
        var stranger = SeedParticipant(chapter, "Carla", "Fremd");
        var first = await CreateMotion(author, portal, type, release: true);
        var second = await CreateMotion(other, portal, type, release: true);
        var commission = SeedCommissionToken(chapter);

        var byStranger = await Declare(stranger.Token, first.Id, second.Id);
        var byAuthor = await Declare(author.Token, first.Id, second.Id);
        var detail = await byAuthor.Content.ReadFromJsonAsync<AssemblyMotionDetailDTO>();
        var duplicate = await Declare(commission, second.Id, first.Id);

        await Assert.That(byStranger.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
        await Assert.That(byAuthor.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(detail!.CompetitionLinks.Single().OtherMotionId).IsEqualTo(second.Id);
        await Assert.That(detail.CompetitionLinks.Single().CanRemove).IsTrue();
        await Assert.That(duplicate.StatusCode).IsEqualTo(HttpStatusCode.Conflict);
    }

    [Test]
    public async Task Declare_for_foreign_draft_is_404() {
        var (chapter, portal, type) = SeedPortal();
        var author = SeedParticipant(chapter, "Anna", "Autor");
        var other = SeedParticipant(chapter, "Bernd", "Anderer");
        var draft = await CreateMotion(author, portal, type, release: false);
        var submitted = await CreateMotion(other, portal, type, release: true);
        var commission = SeedCommissionToken(chapter);

        var byOther = await Declare(other.Token, draft.Id, submitted.Id);
        var byCommission = await Declare(commission, draft.Id, submitted.Id);
        var byAuthor = await Declare(author.Token, draft.Id, submitted.Id);

        await Assert.That(byOther.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
        await Assert.That(byCommission.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
        await Assert.That(byAuthor.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }

    [Test]
    public async Task Declare_across_portals_refused() {
        var (chapter, portal, type) = SeedPortal();
        var (_, otherPortal, otherType) = SeedPortal(chapter);
        var author = SeedParticipant(chapter, "Anna", "Autor");
        var first = await CreateMotion(author, portal, type, release: true);
        var foreign = await CreateMotion(author, otherPortal, otherType, release: true);

        var response = await Declare(author.Token, first.Id, foreign.Id);

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
        await Assert.That(Db.AssemblyMotionCompetitions.Count()).IsEqualTo(0);
    }

    [Test]
    public async Task Remove_by_other_side_submitter_refused() {
        var (chapter, portal, type) = SeedPortal();
        var author = SeedParticipant(chapter, "Anna", "Autor");
        var other = SeedParticipant(chapter, "Bernd", "Anderer");
        var first = await CreateMotion(author, portal, type, release: true);
        var second = await CreateMotion(other, portal, type, release: true);
        await Declare(author.Token, first.Id, second.Id);
        var link = Db.AssemblyMotionCompetitions.Single();

        var byOtherSide = await Remove(other.Token, link.Id);
        var byDeclaring = await Remove(author.Token, link.Id);

        await Assert.That(byOtherSide.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
        await Assert.That(byDeclaring.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(Db.AssemblyMotionCompetitions.Count()).IsEqualTo(0);
    }

    [Test]
    public async Task Commission_can_only_clear_flag() {
        var (chapter, portal, type) = SeedPortal();
        var earlierAuthor = SeedParticipant(chapter, "Anna", "Autor");
        var laterAuthor = SeedParticipant(chapter, "Bernd", "Spaeter");
        var earlier = await CreateMotion(earlierAuthor, portal, type, release: true);
        var later = await CreateMotion(laterAuthor, portal, type, release: true);
        var commission = SeedCommissionToken(chapter);
        await Declare(laterAuthor.Token, later.Id, earlier.Id);

        var commissionSets = await SetFlag(commission, later.Id, true);
        var authorSets = await SetFlag(laterAuthor.Token, later.Id, true);
        var commissionClears = await SetFlag(commission, later.Id, false);
        var cleared = await commissionClears.Content.ReadFromJsonAsync<AssemblyMotionDetailDTO>();

        await Assert.That(commissionSets.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
        await Assert.That(authorSets.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(commissionClears.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(cleared!.IsCompetingMotion).IsFalse();
    }

    private void MoveOutsideChapter(Participant participant) {
        var elsewhere = Builder.SeedChapter("Elsewhere " + Guid.NewGuid().ToString("N")[..6]);
        Db.Members
            .Where(m => m.Id == participant.Member.Id)
            .Set(m => m.ChapterId, elsewhere.Id)
            .Update();
    }

    [Test]
    public async Task Declare_and_flag_true_need_eligibility_but_remove_and_flag_false_do_not() {
        var (chapter, portal, type) = SeedPortal();
        var earlierAuthor = SeedParticipant(chapter, "Anna", "Autor");
        var laterAuthor = SeedParticipant(chapter, "Bernd", "Spaeter");
        var earlier = await CreateMotion(earlierAuthor, portal, type, release: true);
        var later = await CreateMotion(laterAuthor, portal, type, release: true);
        await Declare(laterAuthor.Token, later.Id, earlier.Id);
        await SetFlag(laterAuthor.Token, later.Id, true);
        MoveOutsideChapter(laterAuthor);
        var link = Db.AssemblyMotionCompetitions.Single();

        var flagFalse = await SetFlag(laterAuthor.Token, later.Id, false);
        var flagTrue = await SetFlag(laterAuthor.Token, later.Id, true);
        var declareAgain = await Declare(laterAuthor.Token, later.Id, earlier.Id);
        var remove = await Remove(laterAuthor.Token, link.Id);

        await Assert.That(flagFalse.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(flagTrue.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
        await Assert.That(declareAgain.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
        await Assert.That(remove.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }

    [Test]
    public async Task Draft_link_is_listed_for_draft_author_only() {
        var (chapter, portal, type) = SeedPortal();
        var author = SeedParticipant(chapter, "Anna", "Autor");
        var other = SeedParticipant(chapter, "Bernd", "Anderer");
        var draft = await CreateMotion(author, portal, type, release: false);
        var submitted = await CreateMotion(other, portal, type, release: true);
        await Declare(author.Token, draft.Id, submitted.Id);

        using var authorClient = await AuthenticatedClientWithCsrfAsync(author.Token);
        using var otherClient = await AuthenticatedClientWithCsrfAsync(other.Token);
        var authorView = await authorClient.GetFromJsonAsync<AssemblyMotionDetailDTO>($"/api/assemblymotions/motions/{draft.Id}");
        var otherView = await otherClient.GetFromJsonAsync<AssemblyMotionDetailDTO>($"/api/assemblymotions/motions/{submitted.Id}");

        await Assert.That(authorView!.CompetitionLinks.Single().OtherMotionId).IsEqualTo(submitted.Id);
        await Assert.That(otherView!.CompetitionLinks.Count).IsEqualTo(0);
        await Assert.That(otherView.Competitions.Count).IsEqualTo(0);
    }
}
