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

public class AssemblyMotionCommissionEndpointTests : IntegrationTestBase {
    private record Participant(Member Member, string Token);

    private (Chapter Chapter, AssemblyMotionPortal Portal, AssemblyMotionType Type) SeedPortal(Chapter? existingChapter = null, int minSubmitters = 1) {
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
            MinSubmitters = minSubmitters
        };
        Db.Insert(type);
        return (chapter, portal, type);
    }

    private Participant SeedParticipant(Chapter chapter, string firstName, string lastName, int memberNumber = 0) {
        var (user, token) = Builder.SeedAuthenticatedUser();
        var member = Builder.SeedMember(chapterId: chapter.Id, memberNumber: memberNumber, firstName: firstName, lastName: lastName, userId: user.Id);
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

    private async Task<HttpResponseMessage> Get(string token, string url) {
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        return await client.GetAsync(url);
    }

    [Test]
    public async Task Dashboard_requires_commission_on_event_chapter() {
        var parent = Builder.SeedChapter("Parent");
        var (_, portal, _) = SeedPortal(parent);
        var child = Builder.SeedChapter("Child", parentChapterId: parent.Id);
        var onChild = SeedCommissionToken(child);
        var onParent = SeedCommissionToken(parent);
        var plainUser = Builder.SeedAuthenticatedUser().Token;
        var url = $"/api/assemblymotions/portals/{portal.Id}/dashboard";

        var childResponse = await Get(onChild, url);
        var plainResponse = await Get(plainUser, url);
        var parentResponse = await Get(onParent, url);
        using var anonymous = CreateClient();
        var anonymousResponse = await anonymous.GetAsync(url);
        var unknown = await Get(onParent, $"/api/assemblymotions/portals/{Guid.NewGuid()}/dashboard");

        await Assert.That(childResponse.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
        await Assert.That(plainResponse.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
        await Assert.That(parentResponse.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(anonymousResponse.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
        await Assert.That(unknown.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task Dashboard_excludes_drafts() {
        var (chapter, portal, type) = SeedPortal();
        var author = SeedParticipant(chapter, "Anna", "Autor");
        await CreateMotion(author, portal, type, release: false, title: "Entwurf");
        await CreateMotion(author, portal, type, release: true, title: "Eingereicht");
        var commission = SeedCommissionToken(chapter);

        var response = await Get(commission, $"/api/assemblymotions/portals/{portal.Id}/dashboard");
        var list = await response.Content.ReadFromJsonAsync<AssemblyMotionDashboardResponse>();
        var search = await Get(commission, $"/api/assemblymotions/portals/{portal.Id}/dashboard?search=Eingereicht");
        var searchList = await search.Content.ReadFromJsonAsync<AssemblyMotionDashboardResponse>();

        await Assert.That(list!.Items.Select(i => i.Title).ToArray()).IsEquivalentTo(new[] { "Eingereicht" });
        await Assert.That(list.Items[0].DisplayNumber).IsEqualTo("SÄA001");
        await Assert.That(list.Items[0].SubmitterCount).IsEqualTo(1);
        await Assert.That(list.Items[0].IsInRevision).IsFalse();
        await Assert.That(list.FailedWikiSyncCount).IsEqualTo(0);
        await Assert.That(searchList!.TotalCount).IsEqualTo(1);
    }

    [Test]
    public async Task Dashboard_lists_unnumbered_collecting_motions_and_counts_failed_sync() {
        var (chapter, portal, type) = SeedPortal(minSubmitters: 5);
        var author = SeedParticipant(chapter, "Anna", "Autor");
        var created = await CreateMotion(author, portal, type, release: true, title: "Sammelnd");
        Db.AssemblyMotions
            .Where(m => m.Id == created.Id)
            .Set(m => m.WikiSyncState, WikiSyncState.Failed)
            .Update();
        var commission = SeedCommissionToken(chapter);

        var response = await Get(commission, $"/api/assemblymotions/portals/{portal.Id}/dashboard?search=nichts");
        var empty = await response.Content.ReadFromJsonAsync<AssemblyMotionDashboardResponse>();
        var all = await (await Get(commission, $"/api/assemblymotions/portals/{portal.Id}/dashboard")).Content.ReadFromJsonAsync<AssemblyMotionDashboardResponse>();

        await Assert.That(empty!.TotalCount).IsEqualTo(0);
        await Assert.That(all!.Items.Count).IsEqualTo(1);
        await Assert.That(all.Items[0].DisplayNumber).IsNull();
        await Assert.That(all.Items[0].MinSubmitters).IsEqualTo(5);
        await Assert.That(all.FailedWikiSyncCount).IsEqualTo(1);
    }

    [Test]
    public async Task Managed_lists_only_own_chapters() {
        var own = Builder.SeedChapter("Own");
        var foreign = Builder.SeedChapter("Foreign");
        var (_, ownPortal, _) = SeedPortal(own);
        SeedPortal(foreign);
        var commission = SeedCommissionToken(own);

        var response = await Get(commission, "/api/assemblymotions/portals/managed");
        var list = await response.Content.ReadFromJsonAsync<AssemblyMotionManagedPortalDTO[]>();
        using var anonymous = CreateClient();
        var anonymousResponse = await anonymous.GetAsync("/api/assemblymotions/portals/managed");

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(list!.Select(p => p.PortalId).ToArray()).IsEquivalentTo(new[] { ownPortal.Id });
        await Assert.That(list[0].ChapterName).IsEqualTo("Own");
        await Assert.That(anonymousResponse.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Commission_detail_has_member_numbers() {
        var (chapter, portal, type) = SeedPortal();
        var author = SeedParticipant(chapter, "Anna", "Autor", memberNumber: 111111);
        var created = await CreateMotion(author, portal, type, release: true);
        var commission = SeedCommissionToken(chapter);

        var asCommission = await (await Get(commission, $"/api/assemblymotions/motions/{created.Id}")).Content.ReadFromJsonAsync<AssemblyMotionDetailDTO>();
        var asAuthor = await (await Get(author.Token, $"/api/assemblymotions/motions/{created.Id}")).Content.ReadFromJsonAsync<AssemblyMotionDetailDTO>();

        await Assert.That(asCommission!.Submitters[0].MemberNumber).IsEqualTo(111111);
        await Assert.That(asCommission.Commission).IsNotNull();
        await Assert.That(asCommission.Commission!.WikiSyncState).IsEqualTo(WikiSyncState.Pending);
        await Assert.That(asAuthor!.Commission).IsNull();
    }

    [Test]
    public async Task Commission_cannot_see_draft() {
        var (chapter, portal, type) = SeedPortal();
        var author = SeedParticipant(chapter, "Anna", "Autor");
        var draft = await CreateMotion(author, portal, type, release: false);
        var commission = SeedCommissionToken(chapter);
        using var client = await AuthenticatedClientWithCsrfAsync(commission);

        var detail = await client.GetAsync($"/api/assemblymotions/motions/{draft.Id}");
        var check = await client.PostAsJsonAsync($"/api/assemblymotions/motions/{draft.Id}/check", new { Status = AssemblyMotionStatus.Admitted });

        await Assert.That(detail.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
        await Assert.That(check.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task Check_edit_and_withdraw_round_trip() {
        var (chapter, portal, type) = SeedPortal();
        var author = SeedParticipant(chapter, "Anna", "Autor");
        var created = await CreateMotion(author, portal, type, release: true);
        var commission = SeedCommissionToken(chapter);
        using var client = await AuthenticatedClientWithCsrfAsync(commission);
        using var authorClient = await AuthenticatedClientWithCsrfAsync(author.Token);

        var denied = await authorClient.PostAsJsonAsync($"/api/assemblymotions/motions/{created.Id}/check", new { Status = AssemblyMotionStatus.Admitted });
        var missingNote = await client.PostAsJsonAsync($"/api/assemblymotions/motions/{created.Id}/check", new { Status = AssemblyMotionStatus.FormallyInsufficient });
        var checkResponse = await client.PostAsJsonAsync($"/api/assemblymotions/motions/{created.Id}/check", new { Status = AssemblyMotionStatus.FormallyInsufficient, CheckNote = "Zu kurz" });
        var checkedDetail = await checkResponse.Content.ReadFromJsonAsync<AssemblyMotionDetailDTO>();
        var editResponse = await client.PutAsJsonAsync($"/api/assemblymotions/motions/{created.Id}/commission-edit", new AssemblyMotionCommissionEditRequest {
            TypeId = type.Id,
            Title = "Korrigiert",
            TextMarkdown = "Text",
            ReasonMarkdown = "Grund",
            BaseRevision = 1,
            ChangeNote = "Titel"
        });
        var editedDetail = await editResponse.Content.ReadFromJsonAsync<AssemblyMotionDetailDTO>();
        var htmlEdit = await client.PutAsJsonAsync($"/api/assemblymotions/motions/{created.Id}/commission-edit", new AssemblyMotionCommissionEditRequest {
            TypeId = type.Id,
            Title = "Korrigiert",
            TextMarkdown = "<b>x</b>",
            ReasonMarkdown = "Grund",
            BaseRevision = 2,
            ChangeNote = "Html"
        });
        var withdraw = await client.PostAsJsonAsync($"/api/assemblymotions/motions/{created.Id}/commission-withdraw", new { });
        var withdrawn = await withdraw.Content.ReadFromJsonAsync<AssemblyMotionDetailDTO>();

        await Assert.That(denied.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
        await Assert.That(missingNote.StatusCode).IsEqualTo((HttpStatusCode)422);
        await Assert.That(checkedDetail!.Status).IsEqualTo(AssemblyMotionStatus.FormallyInsufficient);
        await Assert.That(checkedDetail.CheckNote).IsEqualTo("Zu kurz");
        await Assert.That(checkedDetail.Commission!.CheckedByName).IsEqualTo("Test User");
        await Assert.That(editedDetail!.Revision).IsEqualTo(2);
        await Assert.That(editedDetail.Title).IsEqualTo("Korrigiert");
        await Assert.That(htmlEdit.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
        await Assert.That(withdrawn!.Status).IsEqualTo(AssemblyMotionStatus.Withdrawn);
    }

    [Test]
    public async Task Dashboard_refused_with_permission_on_parent_chapter_only() {
        var parent = Builder.SeedChapter("Parent");
        var child = Builder.SeedChapter("Child", parentChapterId: parent.Id);
        var (_, portal, _) = SeedPortal(child);
        var onParent = SeedCommissionToken(parent);

        var response = await Get(onParent, $"/api/assemblymotions/portals/{portal.Id}/dashboard");

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task Managed_ignores_sub_chapter_permission() {
        var parent = Builder.SeedChapter("Parent");
        var child = Builder.SeedChapter("Child", parentChapterId: parent.Id);
        SeedPortal(parent);
        var onChild = SeedCommissionToken(child);

        var response = await Get(onChild, "/api/assemblymotions/portals/managed");
        var list = await response.Content.ReadFromJsonAsync<AssemblyMotionManagedPortalDTO[]>();

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(list!.Length).IsEqualTo(0);
    }
}
