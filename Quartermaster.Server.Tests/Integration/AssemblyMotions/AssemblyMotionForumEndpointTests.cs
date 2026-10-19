using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading.Tasks;
using LinqToDB;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Quartermaster.Api;
using Quartermaster.Api.AssemblyMotions;
using Quartermaster.Api.Events;
using Quartermaster.Data.AssemblyMotions;
using Quartermaster.Data.Chapters;
using Quartermaster.Data.Members;
using Quartermaster.Data.Options;
using Quartermaster.Server.AssemblyMotions.Forum;
using Quartermaster.Server.Tests.AssemblyMotions.Fakes;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.Integration.AssemblyMotions;

[NotInParallel("FakeDiscourseClient")]
public class AssemblyMotionForumEndpointTests : IntegrationTestBase {
    private record Participant(Member Member, string Token);

    private WebApplicationFactory<Program> _host = default!;

    [Before(Test)]
    public void Setup() {
        FakeDiscourseClient.Reset();
        FakeDiscourseClient.Categories[7] = new DiscourseCategory(7, "Anträge", true, true);
        _host = Factory.WithWebHostBuilder(b => b.ConfigureServices(services => {
            services.RemoveAll<IDiscourseClient>();
            services.AddSingleton<IDiscourseClient, FakeDiscourseClient>();
        }));
    }

    [After(Test)]
    public void TearDown() {
        _host.Dispose();
    }

    private void SeedForumOptions() {
        using var scope = _host.Services.CreateScope();
        var options = scope.ServiceProvider.GetRequiredService<OptionRepository>();
        options.SetValue("discourse.base_url", null, "https://forum.test");
        options.SetValue("discourse.api_username", null, "antragsbot");
        options.SetValue("discourse.api_key", null, "forum-secret-key");
    }

    private async Task<HttpClient> ClientAsync(string? token) {
        var client = _host.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        if (token != null)
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        await AttachAntiforgeryTokenAsync(client);
        return client;
    }

    private (Chapter Chapter, AssemblyMotionPortal Portal, AssemblyMotionType Type) SeedPortal(bool mirror, int? categoryId = 7) {
        var chapter = Builder.SeedChapter("Chapter");
        var ev = Builder.SeedEvent(chapter.Id, publicName: "BPT " + Guid.NewGuid().ToString("N")[..6], visibility: EventVisibility.Public);
        var portal = new AssemblyMotionPortal {
            EventId = ev.Id,
            OpensAt = DateTime.UtcNow.AddDays(-1),
            ClosesAt = DateTime.UtcNow.AddDays(30),
            CreatedAt = DateTime.UtcNow,
            ForumMirrorEnabled = mirror,
            ForumCategoryId = categoryId
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
        var member = Builder.SeedMember(chapterId: chapter.Id, firstName: "Anna", lastName: "Autor", userId: user.Id);
        return new Participant(member, token);
    }

    private string SeedCommissionToken(Chapter chapter)
        => Builder.SeedAuthenticatedUser(chapterPermissions: new() {
            [chapter.Id] = [PermissionIdentifier.ManageAssemblyMotions]
        }).Token;

    private string SeedEditorToken(Chapter chapter)
        => Builder.SeedAuthenticatedUser(chapterPermissions: new() {
            [chapter.Id] = [PermissionIdentifier.EditEvents]
        }).Token;

    private async Task<AssemblyMotionDetailDTO> CreateMotion(Participant author, AssemblyMotionPortal portal, AssemblyMotionType type) {
        using var client = await ClientAsync(author.Token);
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

    private static AssemblyMotionPortalUpdateRequest UpdateRequest(AssemblyMotionPortal portal, bool mirror, int? categoryId = 7) => new() {
        Id = portal.Id,
        OpensAt = new DateTimeOffset(DateTime.SpecifyKind(portal.OpensAt, DateTimeKind.Utc)),
        ClosesAt = new DateTimeOffset(DateTime.SpecifyKind(portal.ClosesAt, DateTimeKind.Utc)),
        IntroMarkdown = "",
        ForumMirrorEnabled = mirror,
        ForumCategoryId = categoryId
    };

    private void SetForumState(Guid motionId, ForumSyncState state, int? topicId = null) {
        Db.AssemblyMotions
            .Where(m => m.Id == motionId)
            .Set(m => m.ForumSyncState, state)
            .Set(m => m.ForumTopicId, topicId)
            .Update();
    }

    private AssemblyMotion Reload(Guid motionId) => Db.AssemblyMotions.First(m => m.Id == motionId);

    [Test]
    public async Task Enabling_forum_mirror_queues_numbered_motions() {
        SeedForumOptions();
        var (chapter, portal, type) = SeedPortal(mirror: false);
        var numbered = await CreateMotion(SeedParticipant(chapter), portal, type);
        using var client = await ClientAsync(SeedEditorToken(chapter));

        var response = await client.PutAsJsonAsync($"/api/assemblymotions/portals/{portal.Id}", UpdateRequest(portal, mirror: true));

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(Db.AssemblyMotionPortals.First(p => p.Id == portal.Id).ForumCategoryId).IsEqualTo(7);
        await Assert.That(Reload(numbered.Id).ForumSyncState).IsEqualTo(ForumSyncState.Pending);
    }

    [Test]
    public async Task Enabling_with_failing_check_is_refused_and_nothing_saved() {
        SeedForumOptions();
        var (chapter, portal, _) = SeedPortal(mirror: false);
        using var client = await ClientAsync(SeedEditorToken(chapter));

        var response = await client.PutAsJsonAsync($"/api/assemblymotions/portals/{portal.Id}", UpdateRequest(portal, mirror: true, categoryId: 8));
        var body = await response.Content.ReadAsStringAsync();

        await Assert.That(response.StatusCode).IsEqualTo((HttpStatusCode)422);
        await Assert.That(body).Contains("error.assembly_motion.forum_check_failed");
        await Assert.That(Db.AssemblyMotionPortals.First(p => p.Id == portal.Id).ForumMirrorEnabled).IsFalse();
    }

    [Test]
    public async Task Enabling_without_settings_is_refused() {
        var (chapter, portal, _) = SeedPortal(mirror: false);
        using var client = await ClientAsync(SeedEditorToken(chapter));

        var response = await client.PutAsJsonAsync($"/api/assemblymotions/portals/{portal.Id}", UpdateRequest(portal, mirror: true));
        var body = await response.Content.ReadAsStringAsync();

        await Assert.That(response.StatusCode).IsEqualTo((HttpStatusCode)422);
        await Assert.That(body).Contains("error.assembly_motion.forum_not_configured");
    }

    [Test]
    public async Task Disabling_resets_pending_motions() {
        SeedForumOptions();
        var (chapter, portal, type) = SeedPortal(mirror: true);
        var motion = await CreateMotion(SeedParticipant(chapter), portal, type);
        await Assert.That(Reload(motion.Id).ForumSyncState).IsEqualTo(ForumSyncState.Pending);
        using var client = await ClientAsync(SeedEditorToken(chapter));

        var response = await client.PutAsJsonAsync($"/api/assemblymotions/portals/{portal.Id}", UpdateRequest(portal, mirror: false));

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(Reload(motion.Id).ForumSyncState).IsEqualTo(ForumSyncState.NotMirrored);
    }

    [Test]
    public async Task Validate_endpoint_requires_portal_edit_permission() {
        SeedForumOptions();
        var (chapter, portal, _) = SeedPortal(mirror: false);
        using var client = await ClientAsync(SeedParticipant(chapter).Token);

        var response = await client.PostAsJsonAsync($"/api/assemblymotions/portals/{portal.Id}/forum/validate", new AssemblyMotionForumValidateRequest { PortalId = portal.Id, CategoryId = 7 });

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
        await Assert.That(FakeDiscourseClient.Calls).IsEmpty();
    }

    [Test]
    public async Task Validate_endpoint_returns_warnings() {
        SeedForumOptions();
        FakeDiscourseClient.Categories[7] = new DiscourseCategory(7, "Anträge", false, true);
        var (chapter, portal, _) = SeedPortal(mirror: false);
        using var client = await ClientAsync(SeedEditorToken(chapter));

        var response = await client.PostAsJsonAsync($"/api/assemblymotions/portals/{portal.Id}/forum/validate", new AssemblyMotionForumValidateRequest { PortalId = portal.Id, CategoryId = 7 });
        var result = await response.Content.ReadFromJsonAsync<AssemblyMotionConnectionCheckDTO>();

        await Assert.That(result!.Ok).IsTrue();
        await Assert.That(result.Warnings.Single()).Contains("öffentlich lesbar");
        await Assert.That(result.Message).DoesNotContain("forum-secret-key");
    }
    private async Task<HttpResponseMessage> Post(string token, string url) {
        using var client = await ClientAsync(token);
        return await client.PostAsJsonAsync(url, new { });
    }

    [Test]
    public async Task Resync_one_requeues_failed_motion() {
        var (chapter, portal, type) = SeedPortal(mirror: true);
        var motion = await CreateMotion(SeedParticipant(chapter), portal, type);
        SetForumState(motion.Id, ForumSyncState.Failed, topicId: 5);

        var response = await Post(SeedCommissionToken(chapter), $"/api/assemblymotions/motions/{motion.Id}/forum/resync");

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(Reload(motion.Id).ForumSyncState).IsEqualTo(ForumSyncState.Pending);
    }

    [Test]
    public async Task Resync_one_refuses_deleted_topic() {
        var (chapter, portal, type) = SeedPortal(mirror: true);
        var motion = await CreateMotion(SeedParticipant(chapter), portal, type);
        SetForumState(motion.Id, ForumSyncState.Deleted, topicId: 5);

        var response = await Post(SeedCommissionToken(chapter), $"/api/assemblymotions/motions/{motion.Id}/forum/resync");

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Conflict);
        await Assert.That(Reload(motion.Id).ForumSyncState).IsEqualTo(ForumSyncState.Deleted);
    }

    [Test]
    public async Task Resync_one_requires_commission() {
        var (chapter, portal, type) = SeedPortal(mirror: true);
        var author = SeedParticipant(chapter);
        var motion = await CreateMotion(author, portal, type);
        SetForumState(motion.Id, ForumSyncState.Failed);

        var response = await Post(author.Token, $"/api/assemblymotions/motions/{motion.Id}/forum/resync");

        await Assert.That((int)response.StatusCode).IsGreaterThanOrEqualTo(400);
        await Assert.That(Reload(motion.Id).ForumSyncState).IsEqualTo(ForumSyncState.Failed);
    }

    [Test]
    public async Task Resync_all_counts_numbered_motions_except_deleted() {
        var (chapter, portal, type) = SeedPortal(mirror: true);
        var author = SeedParticipant(chapter);
        var synced = await CreateMotion(author, portal, type);
        var deleted = await CreateMotion(author, portal, type);
        SetForumState(synced.Id, ForumSyncState.Synced, topicId: 5);
        SetForumState(deleted.Id, ForumSyncState.Deleted, topicId: 6);

        var response = await Post(SeedCommissionToken(chapter), $"/api/assemblymotions/portals/{portal.Id}/forum/resync");
        var result = await response.Content.ReadFromJsonAsync<AssemblyMotionForumResyncAllDTO>();

        await Assert.That(result!.Queued).IsEqualTo(1);
        await Assert.That(Reload(synced.Id).ForumSyncState).IsEqualTo(ForumSyncState.Pending);
        await Assert.That(Reload(deleted.Id).ForumSyncState).IsEqualTo(ForumSyncState.Deleted);
    }

    [Test]
    public async Task Resync_all_refuses_portal_without_forum_mirror() {
        var (chapter, portal, _) = SeedPortal(mirror: false);

        var response = await Post(SeedCommissionToken(chapter), $"/api/assemblymotions/portals/{portal.Id}/forum/resync");

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Conflict);
    }

    [Test]
    public async Task Dashboard_filters_by_forum_state_and_counts_failures() {
        var (chapter, portal, type) = SeedPortal(mirror: true);
        var author = SeedParticipant(chapter);
        var failed = await CreateMotion(author, portal, type);
        var synced = await CreateMotion(author, portal, type);
        SetForumState(failed.Id, ForumSyncState.Failed);
        SetForumState(synced.Id, ForumSyncState.Synced, topicId: 5);
        using var client = await ClientAsync(SeedCommissionToken(chapter));

        var failedOnly = await client.GetFromJsonAsync<AssemblyMotionDashboardResponse>($"/api/assemblymotions/portals/{portal.Id}/dashboard?ForumFailed=true");
        var syncedOnly = await client.GetFromJsonAsync<AssemblyMotionDashboardResponse>($"/api/assemblymotions/portals/{portal.Id}/dashboard?ForumSyncState={(int)ForumSyncState.Synced}");

        await Assert.That(failedOnly!.Items.Select(i => i.Id).ToArray()).IsEquivalentTo(new[] { failed.Id });
        await Assert.That(failedOnly.FailedForumSyncCount).IsEqualTo(1);
        await Assert.That(failedOnly.ForumMirrorEnabled).IsTrue();
        await Assert.That(syncedOnly!.Items.Single().ForumSyncState).IsEqualTo(ForumSyncState.Synced);
    }

    [Test]
    public async Task Commission_detail_carries_forum_state_and_topic_url() {
        SeedForumOptions();
        var (chapter, portal, type) = SeedPortal(mirror: true);
        var motion = await CreateMotion(SeedParticipant(chapter), portal, type);
        SetForumState(motion.Id, ForumSyncState.Synced, topicId: 42);
        using var client = await ClientAsync(SeedCommissionToken(chapter));

        var detail = await client.GetFromJsonAsync<AssemblyMotionDetailDTO>($"/api/assemblymotions/motions/{motion.Id}");

        await Assert.That(detail!.Commission!.ForumMirrorEnabled).IsTrue();
        await Assert.That(detail.Commission.ForumSyncState).IsEqualTo(ForumSyncState.Synced);
        await Assert.That(detail.Commission.ForumTopicUrl).IsEqualTo("https://forum.test/t/42");
    }

    [Test]
    public async Task Forum_link_is_hidden_from_anonymous_viewers() {
        SeedForumOptions();
        var (chapter, portal, type) = SeedPortal(mirror: true);
        var motion = await CreateMotion(SeedParticipant(chapter), portal, type);
        SetForumState(motion.Id, ForumSyncState.Synced, topicId: 42);
        using var client = await ClientAsync(null);

        var publicView = await client.GetAsync($"/api/assemblymotions/motions/{motion.Id}/public");
        var memberView = await client.GetAsync($"/api/assemblymotions/motions/{motion.Id}");

        await Assert.That(publicView.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(await publicView.Content.ReadAsStringAsync()).DoesNotContain("forum.test");
        await Assert.That(memberView.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Forum_link_is_shown_to_logged_in_members() {
        SeedForumOptions();
        var (chapter, portal, type) = SeedPortal(mirror: true);
        var motion = await CreateMotion(SeedParticipant(chapter), portal, type);
        SetForumState(motion.Id, ForumSyncState.Synced, topicId: 42);
        using var client = await ClientAsync(SeedParticipant(chapter).Token);

        var detail = await client.GetFromJsonAsync<AssemblyMotionDetailDTO>($"/api/assemblymotions/motions/{motion.Id}");

        await Assert.That(detail!.ForumTopicUrl).IsEqualTo("https://forum.test/t/42");
    }

    [Test]
    public async Task Forum_link_is_hidden_for_deleted_topics() {
        SeedForumOptions();
        var (chapter, portal, type) = SeedPortal(mirror: true);
        var motion = await CreateMotion(SeedParticipant(chapter), portal, type);
        SetForumState(motion.Id, ForumSyncState.Deleted, topicId: 42);
        using var client = await ClientAsync(SeedParticipant(chapter).Token);

        var detail = await client.GetFromJsonAsync<AssemblyMotionDetailDTO>($"/api/assemblymotions/motions/{motion.Id}");

        await Assert.That(detail!.ForumTopicUrl).IsNull();
    }
    [Test]
    public async Task Saving_into_a_public_category_returns_the_warning() {
        SeedForumOptions();
        FakeDiscourseClient.Categories[7] = new DiscourseCategory(7, "Anträge", false, true);
        var (chapter, portal, _) = SeedPortal(mirror: false);
        using var client = await ClientAsync(SeedEditorToken(chapter));

        var response = await client.PutAsJsonAsync($"/api/assemblymotions/portals/{portal.Id}", UpdateRequest(portal, mirror: true));
        var settings = await response.Content.ReadFromJsonAsync<AssemblyMotionPortalSettingsDTO>();

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(settings!.ForumWarnings.Single()).Contains("öffentlich lesbar");
    }
}
