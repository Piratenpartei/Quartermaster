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

public class AssemblyMotionResultEndpointTests : IntegrationTestBase {
    private record Participant(Member Member, string Token);

    private (Chapter Chapter, AssemblyMotionPortal Portal, AssemblyMotionType Type) SeedPortal(Chapter? existingChapter = null) {
        var chapter = existingChapter ?? Builder.SeedChapter("Chapter");
        var ev = Builder.SeedEvent(chapter.Id, publicName: "BPT " + Guid.NewGuid().ToString("N")[..6],
            visibility: EventVisibility.Public, eventDate: BerlinTime.FromUtc(DateTime.UtcNow).Date);
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

    private Participant SeedParticipant(Chapter chapter) {
        var (user, token) = Builder.SeedAuthenticatedUser();
        var member = Builder.SeedMember(chapterId: chapter.Id, userId: user.Id);
        return new Participant(member, token);
    }

    private string SeedCommissionToken(Chapter chapter)
        => Builder.SeedAuthenticatedUser(chapterPermissions: new() {
            [chapter.Id] = [PermissionIdentifier.ManageAssemblyMotions]
        }).Token;

    private async Task<AssemblyMotionDetailDTO> CreateSubmitted(Participant author, AssemblyMotionPortal portal, AssemblyMotionType type) {
        using var client = await AuthenticatedClientWithCsrfAsync(author.Token);
        var response = await client.PostAsJsonAsync($"/api/assemblymotions/portals/{portal.Id}/motions", new AssemblyMotionSaveRequest {
            TypeId = type.Id,
            Title = "Antrag",
            TextMarkdown = "Text",
            ReasonMarkdown = "Grund"
        });
        var created = (await response.Content.ReadFromJsonAsync<AssemblyMotionDetailDTO>())!;
        await client.PostAsJsonAsync($"/api/assemblymotions/motions/{created.Id}/release", new { });
        return created;
    }

    private async Task<HttpResponseMessage> Put(string token, string url, object body) {
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        return await client.PutAsJsonAsync(url, body);
    }

    private AssemblyMotionResult StoredResult(Guid motionId)
        => Db.AssemblyMotions.First(m => m.Id == motionId).Result;

    [Test]
    public async Task Result_requires_commission() {
        var (chapter, portal, type) = SeedPortal();
        var author = SeedParticipant(chapter);
        var motion = await CreateSubmitted(author, portal, type);

        var asAuthor = await Put(author.Token, $"/api/assemblymotions/motions/{motion.Id}/result", new { Result = AssemblyMotionResult.Accepted });
        var asCommission = await Put(SeedCommissionToken(chapter), $"/api/assemblymotions/motions/{motion.Id}/result", new { Result = AssemblyMotionResult.Accepted });
        var detail = await asCommission.Content.ReadFromJsonAsync<AssemblyMotionDetailDTO>();

        await Assert.That(asAuthor.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
        await Assert.That(asCommission.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(detail!.Result).IsEqualTo(AssemblyMotionResult.Accepted);
    }

    [Test]
    public async Task Result_is_public_after_setting() {
        var (chapter, portal, type) = SeedPortal();
        var author = SeedParticipant(chapter);
        var motion = await CreateSubmitted(author, portal, type);
        await Put(SeedCommissionToken(chapter), $"/api/assemblymotions/motions/{motion.Id}/result", new { Result = AssemblyMotionResult.AcceptedModified });
        using var anonymous = CreateClient();

        var publicDetail = await anonymous.GetFromJsonAsync<AssemblyMotionPublicDTO>($"/api/assemblymotions/motions/{motion.Id}/public");
        var list = await anonymous.GetFromJsonAsync<AssemblyMotionPublicListResponse>($"/api/assemblymotions/portals/{portal.Id}/motions");

        await Assert.That(publicDetail!.Result).IsEqualTo(AssemblyMotionResult.AcceptedModified);
        await Assert.That(list!.Items.Single().Result).IsEqualTo(AssemblyMotionResult.AcceptedModified);
    }

    [Test]
    public async Task Result_before_event_date_is_conflict() {
        var (chapter, portal, type) = SeedPortal();
        var author = SeedParticipant(chapter);
        var motion = await CreateSubmitted(author, portal, type);
        Db.Events
            .Where(e => e.Id == portal.EventId)
            .Set(e => e.EventDate, BerlinTime.FromUtc(DateTime.UtcNow).Date.AddDays(5))
            .Update();

        var response = await Put(SeedCommissionToken(chapter), $"/api/assemblymotions/motions/{motion.Id}/result", new { Result = AssemblyMotionResult.Accepted });

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Conflict);
        await Assert.That(await response.Content.ReadAsStringAsync()).Contains("error.assembly_motion.event_not_reached");
    }

    [Test]
    public async Task Bulk_requires_commission() {
        var (chapter, portal, type) = SeedPortal();
        var author = SeedParticipant(chapter);
        var motion = await CreateSubmitted(author, portal, type);
        var url = $"/api/assemblymotions/portals/{portal.Id}/results";
        var body = new AssemblyMotionBulkResultRequest {
            Items = [new AssemblyMotionResultItem { MotionId = motion.Id, Result = AssemblyMotionResult.Rejected }]
        };

        var asAuthor = await Put(author.Token, url, body);
        var asCommission = await Put(SeedCommissionToken(chapter), url, body);
        var unknown = await Put(SeedCommissionToken(chapter), $"/api/assemblymotions/portals/{Guid.NewGuid()}/results", body);

        await Assert.That(asAuthor.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
        await Assert.That(asCommission.StatusCode).IsEqualTo(HttpStatusCode.NoContent);
        await Assert.That(unknown.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
        await Assert.That(StoredResult(motion.Id)).IsEqualTo(AssemblyMotionResult.Rejected);
    }

    [Test]
    public async Task Bulk_rejects_motion_of_other_portal() {
        var (chapter, portal, type) = SeedPortal();
        var (_, otherPortal, otherType) = SeedPortal(chapter);
        var author = SeedParticipant(chapter);
        var own = await CreateSubmitted(author, portal, type);
        var foreign = await CreateSubmitted(author, otherPortal, otherType);

        var response = await Put(SeedCommissionToken(chapter), $"/api/assemblymotions/portals/{portal.Id}/results", new AssemblyMotionBulkResultRequest {
            Items = [
                new AssemblyMotionResultItem { MotionId = own.Id, Result = AssemblyMotionResult.Accepted },
                new AssemblyMotionResultItem { MotionId = foreign.Id, Result = AssemblyMotionResult.Accepted }
            ]
        });

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
        await Assert.That(StoredResult(own.Id)).IsEqualTo(AssemblyMotionResult.None);
        await Assert.That(StoredResult(foreign.Id)).IsEqualTo(AssemblyMotionResult.None);
    }

    [Test]
    public async Task Bulk_rejects_invalid_item_lists_with_400() {
        var (chapter, portal, type) = SeedPortal();
        var author = SeedParticipant(chapter);
        var motion = await CreateSubmitted(author, portal, type);
        var commission = SeedCommissionToken(chapter);
        var url = $"/api/assemblymotions/portals/{portal.Id}/results";
        var item = new AssemblyMotionResultItem { MotionId = motion.Id, Result = AssemblyMotionResult.Accepted };

        var nullItems = await Put(commission, url, new { Items = (object?)null });
        var empty = await Put(commission, url, new AssemblyMotionBulkResultRequest());
        var duplicate = await Put(commission, url, new AssemblyMotionBulkResultRequest { Items = [item, item] });
        var tooMany = await Put(commission, url, new AssemblyMotionBulkResultRequest {
            Items = Enumerable.Range(0, 2001)
                .Select(_ => new AssemblyMotionResultItem { MotionId = Guid.NewGuid() })
                .ToList()
        });

        await Assert.That(nullItems.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
        await Assert.That(empty.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
        await Assert.That(duplicate.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
        await Assert.That(tooMany.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
        await Assert.That(StoredResult(motion.Id)).IsEqualTo(AssemblyMotionResult.None);
    }
}
