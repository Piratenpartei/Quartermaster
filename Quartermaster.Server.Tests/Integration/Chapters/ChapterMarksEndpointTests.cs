using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Quartermaster.Api;
using Quartermaster.Api.Chapters;
using Quartermaster.Data;
using Quartermaster.Data.Chapters;
using Quartermaster.Data.Roles;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.Integration.Chapters;

public class ChapterMarksEndpointTests : IntegrationTestBase {
    public ChapterMarksEndpointTests() {
        DbContext.SupplementDefaults(Factory.Services);
    }

    private static ChapterMarksRequest Marks(bool noMemberAdministration = false, bool unableToAct = false)
        => new() { NoMemberAdministration = noMemberAdministration, UnableToAct = unableToAct };

    private Task<HttpResponseMessage> PutMarks(HttpClient client, Guid chapterId, ChapterMarksRequest request)
        => client.PutAsJsonAsync($"/api/chapters/{chapterId}/marks", request);

    private List<Chapter> SeedTree()
        => Builder.SeedChapterHierarchy("Bund", "LV", "KV", "OV");

    [Test]
    public async Task Higher_officer_marks_chapter() {
        var tree = SeedTree();
        var (_, token) = Builder.SeedAuthenticatedUser(chapterPermissions: new() { [tree[1].Id] = [PermissionIdentifier.ManageChapterCapacity] });
        using var client = await AuthenticatedClientWithCsrfAsync(token);

        var response = await PutMarks(client, tree[3].Id, Marks(unableToAct: true));

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(Db.Chapters.Single(c => c.Id == tree[3].Id).UnableToAct).IsTrue();
    }

    [Test]
    public async Task Own_officer_cannot_mark_chapter() {
        var tree = SeedTree();
        var (_, token) = Builder.SeedAuthenticatedUser(chapterPermissions: new() { [tree[3].Id] = [PermissionIdentifier.ManageChapterCapacity] });
        using var client = await AuthenticatedClientWithCsrfAsync(token);

        var response = await PutMarks(client, tree[3].Id, Marks(unableToAct: true));

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
        await Assert.That(Db.Chapters.Single(c => c.Id == tree[3].Id).UnableToAct).IsFalse();
    }

    [Test]
    public async Task Delegate_of_higher_chapter_is_refused_but_delegate_of_parent_is_allowed() {
        var tree = SeedTree();
        var delegateRole = RoleRepo().GetByIdentifier(PermissionIdentifier.SystemRole.GeneralChapterDelegate)!;
        var (user, token) = Builder.SeedAuthenticatedUser();
        Builder.AssignRoleToUser(user.Id, delegateRole.Id, tree[1].Id);
        using var client = await AuthenticatedClientWithCsrfAsync(token);

        var twoLevelsDown = await PutMarks(client, tree[3].Id, Marks(unableToAct: true));
        var directlyBelow = await PutMarks(client, tree[2].Id, Marks(unableToAct: true));

        await Assert.That(twoLevelsDown.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
        await Assert.That(Db.Chapters.Single(c => c.Id == tree[3].Id).UnableToAct).IsFalse();
        await Assert.That(directlyBelow.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }

    [Test]
    public async Task Officer_role_on_higher_chapter_marks_chapter() {
        var tree = SeedTree();
        var officerRole = RoleRepo().GetByIdentifier(PermissionIdentifier.SystemRole.ChapterOfficer)!;
        var (user, token) = Builder.SeedAuthenticatedUser();
        Builder.AssignRoleToUser(user.Id, officerRole.Id, tree[1].Id);
        using var client = await AuthenticatedClientWithCsrfAsync(token);

        var response = await PutMarks(client, tree[3].Id, Marks(unableToAct: true));

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }

    private RoleRepository RoleRepo()
        => new(Db);

    [Test]
    public async Task Global_admin_marks_chapter() {
        var tree = SeedTree();
        var (_, token) = Builder.SeedAuthenticatedUser(globalPermissions: [PermissionIdentifier.ManageChapterCapacity]);
        using var client = await AuthenticatedClientWithCsrfAsync(token);

        var response = await PutMarks(client, tree[2].Id, Marks(noMemberAdministration: true));

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(Db.Chapters.Single(c => c.Id == tree[2].Id).NoMemberAdministration).IsTrue();
    }

    [Test]
    public async Task Preview_is_unauthorized_for_anonymous_and_put_not_found_for_unknown_chapter() {
        var tree = SeedTree();
        var (_, token) = Builder.SeedAuthenticatedUser(globalPermissions: [PermissionIdentifier.ManageChapterCapacity]);
        using var visitor = AnonymousClient();
        using var client = await AuthenticatedClientWithCsrfAsync(token);

        var anonymous = await visitor.GetAsync($"/api/chapters/{tree[3].Id}/marks/preview?UnableToAct=true");
        var unknown = await PutMarks(client, Guid.NewGuid(), Marks(unableToAct: true));

        await Assert.That(anonymous.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
        await Assert.That(unknown.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task Root_cannot_be_marked() {
        var tree = SeedTree();
        var (_, token) = Builder.SeedAuthenticatedUser(globalPermissions: [PermissionIdentifier.ManageChapterCapacity]);
        using var client = await AuthenticatedClientWithCsrfAsync(token);

        var response = await PutMarks(client, tree[0].Id, Marks(unableToAct: true));

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
        await Assert.That(await response.Content.ReadAsStringAsync()).Contains("error.chapter.marks.root");
        await Assert.That(Db.Chapters.Single(c => c.Id == tree[0].Id).UnableToAct).IsFalse();
    }

    [Test]
    public async Task Marking_hands_over_and_returns_count() {
        var tree = SeedTree();
        var first = Builder.SeedMotion(tree[3].Id, title: "Eins");
        var second = Builder.SeedMotion(tree[3].Id, title: "Zwei");
        var (_, token) = Builder.SeedAuthenticatedUser(globalPermissions: [PermissionIdentifier.ManageChapterCapacity]);
        using var client = await AuthenticatedClientWithCsrfAsync(token);

        var response = await PutMarks(client, tree[3].Id, Marks(unableToAct: true));

        var body = await response.Content.ReadFromJsonAsync<ChapterMarksResponse>();
        await Assert.That(body!.HandedOver).IsEqualTo(2);
        var closed = Db.Motions.Single(m => m.Id == first.Id);
        await Assert.That(closed.HandedOverToMotionId).IsNotNull();
        await Assert.That(Db.Motions.Count(m => m.ChapterId == tree[2].Id && m.OnBehalfOfChapterId == tree[3].Id)).IsEqualTo(2);
        await Assert.That(Db.Motions.Single(m => m.Id == second.Id).HandedOverToMotionId).IsNotNull();
    }

    [Test]
    public async Task Lifting_moves_nothing_back() {
        var tree = SeedTree();
        Builder.SeedMotion(tree[3].Id);
        var (_, token) = Builder.SeedAuthenticatedUser(globalPermissions: [PermissionIdentifier.ManageChapterCapacity]);
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        await PutMarks(client, tree[3].Id, Marks(unableToAct: true));

        var response = await PutMarks(client, tree[3].Id, Marks());

        var body = await response.Content.ReadFromJsonAsync<ChapterMarksResponse>();
        await Assert.That(body!.HandedOver).IsEqualTo(0);
        await Assert.That(Db.Chapters.Single(c => c.Id == tree[3].Id).UnableToAct).IsFalse();
        await Assert.That(Db.Motions.Count(m => m.ChapterId == tree[3].Id)).IsEqualTo(1);
        await Assert.That(Db.Motions.Count(m => m.ChapterId == tree[2].Id)).IsEqualTo(1);
    }

    [Test]
    public async Task Preview_counts_without_changing() {
        var tree = SeedTree();
        var motion = Builder.SeedMotion(tree[3].Id);
        var (_, token) = Builder.SeedAuthenticatedUser(globalPermissions: [PermissionIdentifier.ManageChapterCapacity]);
        using var client = AuthenticatedClient(token);

        var response = await client.GetAsync($"/api/chapters/{tree[3].Id}/marks/preview?NoMemberAdministration=false&UnableToAct=true");

        var preview = await response.Content.ReadFromJsonAsync<List<ChapterMarksPreviewDTO>>();
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(preview!.Single().TargetChapterName).IsEqualTo("KV");
        await Assert.That(preview.Single().Count).IsEqualTo(1);
        await Assert.That(Db.Chapters.Single(c => c.Id == tree[3].Id).UnableToAct).IsFalse();
        await Assert.That(Db.Motions.Single(m => m.Id == motion.Id).HandedOverToMotionId).IsNull();
        await Assert.That(Db.Motions.Count()).IsEqualTo(1);
    }

    [Test]
    public async Task Preview_refuses_own_officer_and_root() {
        var tree = SeedTree();
        var (_, ownToken) = Builder.SeedAuthenticatedUser(chapterPermissions: new() { [tree[3].Id] = [PermissionIdentifier.ManageChapterCapacity] });
        var (_, adminToken) = Builder.SeedAuthenticatedUser(globalPermissions: [PermissionIdentifier.ManageChapterCapacity]);
        using var own = AuthenticatedClient(ownToken);
        using var admin = AuthenticatedClient(adminToken);

        var ownResponse = await own.GetAsync($"/api/chapters/{tree[3].Id}/marks/preview?UnableToAct=true");
        var rootResponse = await admin.GetAsync($"/api/chapters/{tree[0].Id}/marks/preview?UnableToAct=true");

        await Assert.That(ownResponse.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
        await Assert.That(rootResponse.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task Tree_hides_marks_from_anonymous() {
        var tree = Builder.SeedChapterHierarchy("Bund", "LV");
        var marked = Builder.SeedChapter("OV", tree[1].Id, noMemberAdministration: true, unableToAct: true);
        var markedRoot = Builder.SeedChapter("Zweiter Bund", noMemberAdministration: true, unableToAct: true);
        var (_, token) = Builder.SeedAuthenticatedUser();
        using var visitor = AnonymousClient();
        using var member = AuthenticatedClient(token);
        var searchUrl = "/api/chapters/search?query=OV&page=1&pageSize=25";

        var visitorChildren = await visitor.GetFromJsonAsync<List<ChapterDTO>>($"/api/chapters/{tree[1].Id}/children");
        var visitorRoots = await visitor.GetFromJsonAsync<List<ChapterDTO>>("/api/chapters/roots");
        var visitorList = await visitor.GetFromJsonAsync<List<ChapterDTO>>("/api/chapters");
        var visitorSearch = await visitor.GetFromJsonAsync<ChapterSearchResponse>(searchUrl);
        var visitorDetail = await visitor.GetFromJsonAsync<ChapterDetailBody>($"/api/chapters/{marked.Id}");
        var visitorParent = await visitor.GetFromJsonAsync<ChapterDetailBody>($"/api/chapters/{tree[1].Id}");

        var memberChildren = await member.GetFromJsonAsync<List<ChapterDTO>>($"/api/chapters/{tree[1].Id}/children");
        var memberRoots = await member.GetFromJsonAsync<List<ChapterDTO>>("/api/chapters/roots");
        var memberList = await member.GetFromJsonAsync<List<ChapterDTO>>("/api/chapters");
        var memberSearch = await member.GetFromJsonAsync<ChapterSearchResponse>(searchUrl);
        var memberDetail = await member.GetFromJsonAsync<ChapterDetailBody>($"/api/chapters/{marked.Id}");
        var memberParent = await member.GetFromJsonAsync<ChapterDetailBody>($"/api/chapters/{tree[1].Id}");

        await Assert.That(AnyMarked(visitorChildren!)).IsFalse();
        await Assert.That(visitorRoots!.Any(c => c.Id == markedRoot.Id)).IsTrue();
        await Assert.That(AnyMarked(visitorRoots!)).IsFalse();
        await Assert.That(AnyMarked(visitorList!)).IsFalse();
        await Assert.That(AnyMarked(visitorSearch!.Items)).IsFalse();
        await Assert.That(AnyMarked([visitorDetail!.Chapter])).IsFalse();
        await Assert.That(AnyMarked(visitorParent!.Children)).IsFalse();
        await Assert.That(AllMarked(memberChildren!)).IsTrue();
        await Assert.That(AllMarked(memberRoots!.Where(c => c.Id == markedRoot.Id).ToList())).IsTrue();
        await Assert.That(AllMarked(memberList!.Where(c => c.Id == marked.Id).ToList())).IsTrue();
        await Assert.That(AllMarked(memberSearch!.Items)).IsTrue();
        await Assert.That(AllMarked([memberDetail!.Chapter])).IsTrue();
        await Assert.That(AllMarked(memberParent!.Children)).IsTrue();
    }

    [Test]
    public async Task Detail_tells_higher_officer_they_can_mark() {
        var tree = SeedTree();
        var (_, token) = Builder.SeedAuthenticatedUser(chapterPermissions: new() { [tree[1].Id] = [PermissionIdentifier.ManageChapterCapacity] });
        using var officer = await AuthenticatedClientWithCsrfAsync(token);
        using var visitor = AnonymousClient();

        var twoLevelsDown = await officer.GetFromJsonAsync<ChapterDetailBody>($"/api/chapters/{tree[3].Id}");
        var ownChapter = await officer.GetFromJsonAsync<ChapterDetailBody>($"/api/chapters/{tree[1].Id}");
        var anonymous = await visitor.GetFromJsonAsync<ChapterDetailBody>($"/api/chapters/{tree[3].Id}");

        await Assert.That(twoLevelsDown!.CanManageMarks).IsTrue();
        await Assert.That(ownChapter!.CanManageMarks).IsFalse();
        await Assert.That(anonymous!.CanManageMarks).IsFalse();
    }

    private static bool AnyMarked(IEnumerable<ChapterDTO> chapters)
        => chapters.Any(c => c.NoMemberAdministration || c.UnableToAct);

    private static bool AllMarked(IReadOnlyCollection<ChapterDTO> chapters)
        => chapters.Count > 0 && chapters.All(c => c.NoMemberAdministration && c.UnableToAct);

    private class ChapterDetailBody {
        public ChapterDTO Chapter { get; set; } = new();
        public List<ChapterDTO> Children { get; set; } = [];
        public bool CanManageMarks { get; set; }
    }
}
