using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using LinqToDB;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Quartermaster.Api;
using Quartermaster.Api.AssemblyMotions;
using Quartermaster.Api.Events;
using Quartermaster.Api.I18n;
using Quartermaster.Data.AssemblyMotions;
using Quartermaster.Data.Chapters;
using Quartermaster.Data.Members;
using Quartermaster.Data.Options;
using Quartermaster.Server.AssemblyMotions.OpenSlides;
using Quartermaster.Server.Tests.AssemblyMotions.Fakes;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.Integration.AssemblyMotions;

[NotInParallel("FakeOpenSlidesClient")]
public class AssemblyMotionOpenSlidesEndpointTests : IntegrationTestBase {
    private const string OpenSlidesPassword = "os-secret-pw";

    private WebApplicationFactory<Program> _host = default!;

    [Before(Test)]
    public void Setup() {
        FakeOpenSlidesClient.Reset();
        _host = Factory.WithWebHostBuilder(b => b.ConfigureServices(services => {
            services.RemoveAll<IOpenSlidesClient>();
            services.AddSingleton<IOpenSlidesClient, FakeOpenSlidesClient>();
        }));
    }

    [After(Test)]
    public void TearDown() {
        _host.Dispose();
    }

    private void SeedOptions() {
        using var scope = _host.Services.CreateScope();
        var options = scope.ServiceProvider.GetRequiredService<OptionRepository>();
        options.SetValue("openslides.url", null, "https://os.test");
        options.SetValue("openslides.username", null, "pusher");
        options.SetValue("openslides.password", null, OpenSlidesPassword);
    }

    private async Task<HttpClient> ClientAsync(string token) {
        var client = _host.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        await AttachAntiforgeryTokenAsync(client);
        return client;
    }

    private (Chapter Chapter, AssemblyMotionPortal Portal, AssemblyMotionType Type) SeedPortal(int? meetingId = 5, EventVisibility visibility = EventVisibility.Public) {
        var chapter = Builder.SeedChapter("Chapter");
        var ev = Builder.SeedEvent(chapter.Id, publicName: "BPT " + Guid.NewGuid().ToString("N")[..6], visibility: visibility);
        var portal = new AssemblyMotionPortal {
            EventId = ev.Id,
            OpensAt = DateTime.UtcNow.AddDays(-1),
            ClosesAt = DateTime.UtcNow.AddDays(30),
            CreatedAt = DateTime.UtcNow,
            OpenSlidesMeetingId = meetingId
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

    private AssemblyMotion SeedAdmittedMotion(AssemblyMotionPortal portal, AssemblyMotionType type, int number) {
        var author = Builder.SeedMember(firstName: "Anna", lastName: "Autor");
        var motion = new AssemblyMotion {
            PortalId = portal.Id,
            TypeId = type.Id,
            Number = number,
            Title = "Antrag " + number,
            TextHtml = "<p>Text</p>",
            ReasonHtml = "<p>Grund</p>",
            Status = AssemblyMotionStatus.Admitted,
            CreatedByMemberId = author.Id,
            CreatedAt = DateTime.UtcNow,
            SubmittedAt = DateTime.UtcNow
        };
        Db.Insert(motion);
        Db.Insert(new AssemblyMotionSubmitter { MotionId = motion.Id, MemberId = author.Id, IsAuthor = true, JoinedAt = DateTime.UtcNow, JoinedRevision = 1 });
        return motion;
    }

    private string SeedCommissionToken(Chapter chapter)
        => Builder.SeedAuthenticatedUser(chapterPermissions: new() {
            [chapter.Id] = [PermissionIdentifier.ManageAssemblyMotions]
        }).Token;

    private string SeedEditorToken(Chapter chapter)
        => Builder.SeedAuthenticatedUser(chapterPermissions: new() {
            [chapter.Id] = [PermissionIdentifier.EditEvents]
        }).Token;

    private async Task<HttpResponseMessage> Post(string token, string url) {
        using var client = await ClientAsync(token);
        return await client.PostAsJsonAsync(url, new { });
    }

    [Test]
    public async Task Push_requires_commission() {
        SeedOptions();
        var (chapter, portal, type) = SeedPortal();
        SeedAdmittedMotion(portal, type, 1);

        var editor = await Post(SeedEditorToken(chapter), $"/api/assemblymotions/portals/{portal.Id}/openslides/push");
        var body = await editor.Content.ReadAsStringAsync();

        await Assert.That(editor.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
        await Assert.That(body).Contains(I18nKey.Error.AssemblyMotion.NotCommission);
        await Assert.That(FakeOpenSlidesClient.Motions).IsEmpty();
    }

    [Test]
    public async Task Push_on_hidden_event_is_not_found_for_outsiders() {
        SeedOptions();
        var (_, portal, _) = SeedPortal(visibility: EventVisibility.Private);
        var outsider = Builder.SeedAuthenticatedUser().Token;

        var response = await Post(outsider, $"/api/assemblymotions/portals/{portal.Id}/openslides/push");

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task Push_without_meeting_id_conflicts() {
        SeedOptions();
        var (chapter, portal, type) = SeedPortal(meetingId: null);
        SeedAdmittedMotion(portal, type, 1);

        var response = await Post(SeedCommissionToken(chapter), $"/api/assemblymotions/portals/{portal.Id}/openslides/push");

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Conflict);
        await Assert.That(await response.Content.ReadAsStringAsync()).Contains(I18nKey.Error.AssemblyMotion.OpenSlidesMeetingMissing);
        await Assert.That(FakeOpenSlidesClient.Motions).IsEmpty();
    }

    [Test]
    public async Task Push_unconfigured_conflicts() {
        var (chapter, portal, type) = SeedPortal();
        SeedAdmittedMotion(portal, type, 1);

        var response = await Post(SeedCommissionToken(chapter), $"/api/assemblymotions/portals/{portal.Id}/openslides/push");

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Conflict);
        await Assert.That(await response.Content.ReadAsStringAsync()).Contains(I18nKey.Error.AssemblyMotion.OpenSlidesNotConfigured);
        await Assert.That(FakeOpenSlidesClient.Motions).IsEmpty();
    }

    [Test]
    public async Task Push_creates_motions_and_returns_result() {
        SeedOptions();
        var (chapter, portal, type) = SeedPortal();
        SeedAdmittedMotion(portal, type, 1);
        SeedAdmittedMotion(portal, type, 2);

        var response = await Post(SeedCommissionToken(chapter), $"/api/assemblymotions/portals/{portal.Id}/openslides/push");
        var result = await response.Content.ReadFromJsonAsync<OpenSlidesPushResultDTO>();

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(result!.Created).IsEqualTo(2);
        await Assert.That(result.Failures).IsEmpty();
        await Assert.That(string.Join("|", FakeOpenSlidesClient.Motions.Select(m => m.Number))).IsEqualTo("SÄA001|SÄA002");
        await Assert.That(FakeOpenSlidesClient.Motions.All(m => m.MeetingId == 5)).IsTrue();
        await Assert.That(Db.AuditLogs.Count(a => a.EntityId == portal.Id && a.Action == "OpenSlidesPushed")).IsEqualTo(1);
    }

    [Test]
    public async Task Concurrent_push_answers_conflict() {
        SeedOptions();
        var (chapter, portal, type) = SeedPortal();
        SeedAdmittedMotion(portal, type, 1);
        var token = SeedCommissionToken(chapter);
        var entered = new TaskCompletionSource();
        var release = new TaskCompletionSource();
        var blocked = 0;
        FakeOpenSlidesClient.OnCreateMotion = async _ => {
            if (Interlocked.Exchange(ref blocked, 1) != 0)
                return;

            entered.TrySetResult();
            await release.Task;
        };

        var running = Post(token, $"/api/assemblymotions/portals/{portal.Id}/openslides/push");
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(30));
        var concurrent = await Post(token, $"/api/assemblymotions/portals/{portal.Id}/openslides/push").WaitAsync(TimeSpan.FromSeconds(30));
        release.SetResult();
        var finished = await running.WaitAsync(TimeSpan.FromSeconds(30));

        await Assert.That(concurrent.StatusCode).IsEqualTo(HttpStatusCode.Conflict);
        await Assert.That(await concurrent.Content.ReadAsStringAsync()).Contains(I18nKey.Error.AssemblyMotion.OpenSlidesPushRunning);
        await Assert.That(finished.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(FakeOpenSlidesClient.Motions.Count).IsEqualTo(1);
    }

    [Test]
    public async Task Validate_reports_unconfigured() {
        var (chapter, portal, _) = SeedPortal();

        var response = await Post(SeedEditorToken(chapter), $"/api/assemblymotions/portals/{portal.Id}/openslides/validate");
        var result = await response.Content.ReadFromJsonAsync<AssemblyMotionConnectionCheckDTO>();

        await Assert.That(result!.Ok).IsFalse();
        await Assert.That(FakeOpenSlidesClient.LoginCount).IsEqualTo(0);
    }

    [Test]
    public async Task Validate_reports_login_failure() {
        SeedOptions();
        FakeOpenSlidesClient.LoginFailure = new OpenSlidesException("Anmeldung an OpenSlides fehlgeschlagen.", "login_failed");
        var (chapter, portal, _) = SeedPortal();

        var response = await Post(SeedEditorToken(chapter), $"/api/assemblymotions/portals/{portal.Id}/openslides/validate");
        var result = await response.Content.ReadFromJsonAsync<AssemblyMotionConnectionCheckDTO>();

        await Assert.That(result!.Ok).IsFalse();
        await Assert.That(result.Message).StartsWith("Anmeldung an OpenSlides fehlgeschlagen: ");
        await Assert.That(result.Message).DoesNotContain(OpenSlidesPassword);
    }

    [Test]
    public async Task Validate_reports_success_with_one_login_and_creates_nothing() {
        SeedOptions();
        var (chapter, portal, _) = SeedPortal();

        var response = await Post(SeedEditorToken(chapter), $"/api/assemblymotions/portals/{portal.Id}/openslides/validate");
        var result = await response.Content.ReadFromJsonAsync<AssemblyMotionConnectionCheckDTO>();

        await Assert.That(result!.Ok).IsTrue();
        await Assert.That(result.Message).Contains("pusher");
        await Assert.That(FakeOpenSlidesClient.LoginCount).IsEqualTo(1);
        await Assert.That(FakeOpenSlidesClient.Motions).IsEmpty();
        await Assert.That(FakeOpenSlidesClient.Categories).IsEmpty();
    }

    [Test]
    public async Task Push_by_commission_of_another_chapter_is_forbidden() {
        SeedOptions();
        var (_, portal, type) = SeedPortal();
        SeedAdmittedMotion(portal, type, 1);
        var otherChapter = Builder.SeedChapter("Other");

        var response = await Post(SeedCommissionToken(otherChapter), $"/api/assemblymotions/portals/{portal.Id}/openslides/push");

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
        await Assert.That(FakeOpenSlidesClient.Motions).IsEmpty();
    }

    [Test]
    public async Task Validate_by_editor_of_another_chapter_is_forbidden() {
        SeedOptions();
        var (_, portal, _) = SeedPortal();
        var otherChapter = Builder.SeedChapter("Other");

        var response = await Post(SeedEditorToken(otherChapter), $"/api/assemblymotions/portals/{portal.Id}/openslides/validate");

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
        await Assert.That(FakeOpenSlidesClient.LoginCount).IsEqualTo(0);
    }

    [Test]
    public async Task Validate_requires_event_edit_permission() {
        SeedOptions();
        var (chapter, portal, _) = SeedPortal();

        var response = await Post(SeedCommissionToken(chapter), $"/api/assemblymotions/portals/{portal.Id}/openslides/validate");

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
        await Assert.That(FakeOpenSlidesClient.LoginCount).IsEqualTo(0);
    }
}
