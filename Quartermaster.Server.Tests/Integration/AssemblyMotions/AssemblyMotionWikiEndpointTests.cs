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
using Quartermaster.Server.AssemblyMotions.Wiki;
using Quartermaster.Server.Tests.AssemblyMotions.Fakes;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.Integration.AssemblyMotions;

[NotInParallel("FakeWikiBotClient")]
public class AssemblyMotionWikiEndpointTests : IntegrationTestBase {
    private const string BotPassword = "bot-secret-pw";

    private record Participant(Member Member, string Token);

    private WebApplicationFactory<Program> _host = default!;

    [Before(Test)]
    public void Setup() {
        FakeWikiBotClient.Reset();
        _host = Factory.WithWebHostBuilder(b => b.ConfigureServices(services => {
            services.RemoveAll<IWikiBotClient>();
            services.AddSingleton<IWikiBotClient, FakeWikiBotClient>();
        }));
    }

    [After(Test)]
    public void TearDown() {
        _host.Dispose();
    }

    private void SeedWikiOptions() {
        using var scope = _host.Services.CreateScope();
        var options = scope.ServiceProvider.GetRequiredService<OptionRepository>();
        options.SetValue("wiki.api_url", null, "https://wiki.test/api.php");
        options.SetValue("wiki.bot_username", null, "Portalbot");
        options.SetValue("wiki.bot_password", null, BotPassword);
    }

    private async Task<HttpClient> ClientAsync(string token) {
        var client = _host.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        await AttachAntiforgeryTokenAsync(client);
        return client;
    }

    private (Chapter Chapter, AssemblyMotionPortal Portal, AssemblyMotionType Type) SeedPortal(bool mirror, int minSubmitters = 1) {
        var chapter = Builder.SeedChapter("Chapter");
        var ev = Builder.SeedEvent(chapter.Id, publicName: "BPT " + Guid.NewGuid().ToString("N")[..6], visibility: EventVisibility.Public);
        var portal = new AssemblyMotionPortal {
            EventId = ev.Id,
            OpensAt = DateTime.UtcNow.AddDays(-1),
            ClosesAt = DateTime.UtcNow.AddDays(30),
            CreatedAt = DateTime.UtcNow,
            WikiMirrorEnabled = mirror,
            WikiPagePrefix = "Antrag:Test",
            WikiAssemblyLabel = "BPT Test"
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

    private async Task<AssemblyMotionDetailDTO> CreateMotion(Participant author, AssemblyMotionPortal portal, AssemblyMotionType type, bool release) {
        using var client = await ClientAsync(author.Token);
        var response = await client.PostAsJsonAsync($"/api/assemblymotions/portals/{portal.Id}/motions", new AssemblyMotionSaveRequest {
            TypeId = type.Id,
            Title = "Antrag",
            TextMarkdown = "Text",
            ReasonMarkdown = "Grund"
        });
        var created = (await response.Content.ReadFromJsonAsync<AssemblyMotionDetailDTO>())!;
        if (release)
            await client.PostAsJsonAsync($"/api/assemblymotions/motions/{created.Id}/release", new { });
        return created;
    }

    private static AssemblyMotionPortalUpdateRequest UpdateRequest(AssemblyMotionPortal portal, bool mirror) => new() {
        Id = portal.Id,
        OpensAt = new DateTimeOffset(DateTime.SpecifyKind(portal.OpensAt, DateTimeKind.Utc)),
        ClosesAt = new DateTimeOffset(DateTime.SpecifyKind(portal.ClosesAt, DateTimeKind.Utc)),
        IntroMarkdown = "",
        WikiMirrorEnabled = mirror,
        WikiPagePrefix = "Antrag:Test",
        WikiAssemblyLabel = "BPT Test"
    };

    private async Task<HttpResponseMessage> Post(string token, string url) {
        using var client = await ClientAsync(token);
        return await client.PostAsJsonAsync(url, new { });
    }

    private void SetSyncState(Guid motionId, WikiSyncState state, int failures = 0, DateTime? nextAttempt = null) {
        Db.AssemblyMotions
            .Where(m => m.Id == motionId)
            .Set(m => m.WikiSyncState, state)
            .Set(m => m.WikiFailureCount, failures)
            .Set(m => m.WikiNextAttemptAt, nextAttempt)
            .Update();
    }

    private AssemblyMotion Reload(Guid motionId) => Db.AssemblyMotions.First(m => m.Id == motionId);

    [Test]
    public async Task Validate_reports_unconfigured() {
        var (chapter, portal, _) = SeedPortal(mirror: false);
        var token = SeedEditorToken(chapter);

        var response = await Post(token, $"/api/assemblymotions/portals/{portal.Id}/wiki/validate");
        var result = await response.Content.ReadFromJsonAsync<AssemblyMotionConnectionCheckDTO>();

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(result!.Ok).IsFalse();
        await Assert.That(result.Message).IsEqualTo("Die Wiki-Zugangsdaten sind nicht hinterlegt.");
        await Assert.That(FakeWikiBotClient.LoginCount).IsEqualTo(0);
    }

    [Test]
    public async Task Validate_reports_login_failure() {
        SeedWikiOptions();
        FakeWikiBotClient.ThrowOn["Login"] = new WikiBotException("Anmeldung am Wiki fehlgeschlagen: Falsches Passwort", "login_failed");
        var (chapter, portal, _) = SeedPortal(mirror: false);

        var response = await Post(SeedEditorToken(chapter), $"/api/assemblymotions/portals/{portal.Id}/wiki/validate");
        var result = await response.Content.ReadFromJsonAsync<AssemblyMotionConnectionCheckDTO>();

        await Assert.That(result!.Ok).IsFalse();
        await Assert.That(result.Message).IsEqualTo("Anmeldung am Wiki fehlgeschlagen: Falsches Passwort");
        await Assert.That(result.Message).DoesNotContain(BotPassword);
    }

    [Test]
    public async Task Validate_reports_success_with_bot_name_and_one_login() {
        SeedWikiOptions();
        var (chapter, portal, _) = SeedPortal(mirror: false);

        var response = await Post(SeedEditorToken(chapter), $"/api/assemblymotions/portals/{portal.Id}/wiki/validate");
        var result = await response.Content.ReadFromJsonAsync<AssemblyMotionConnectionCheckDTO>();

        await Assert.That(result!.Ok).IsTrue();
        await Assert.That(result.Message).Contains("Portalbot");
        await Assert.That(FakeWikiBotClient.LoginCount).IsEqualTo(1);
    }

    [Test]
    public async Task Validate_reports_insecure_url() {
        SeedWikiOptions();
        FakeWikiBotClient.ThrowOn["Login"] = new WikiBotException("Die Wiki-API-URL muss https verwenden.", "insecure_url");
        var (chapter, portal, _) = SeedPortal(mirror: false);

        var response = await Post(SeedEditorToken(chapter), $"/api/assemblymotions/portals/{portal.Id}/wiki/validate");
        var result = await response.Content.ReadFromJsonAsync<AssemblyMotionConnectionCheckDTO>();

        await Assert.That(result!.Ok).IsFalse();
        await Assert.That(result.Message).Contains("https");
    }

    [Test]
    public async Task Validate_never_leaks_a_response_body() {
        SeedWikiOptions();
        FakeWikiBotClient.ThrowOn["Login"] = new WikiBotException("Das Wiki antwortete mit HTTP 500: SECRET-BODY-EXCERPT", "http_error");
        var (chapter, portal, _) = SeedPortal(mirror: false);

        var response = await Post(SeedEditorToken(chapter), $"/api/assemblymotions/portals/{portal.Id}/wiki/validate");
        var result = await response.Content.ReadFromJsonAsync<AssemblyMotionConnectionCheckDTO>();

        await Assert.That(result!.Ok).IsFalse();
        await Assert.That(result.Message).DoesNotContain("SECRET-BODY-EXCERPT");
    }

    [Test]
    public async Task Validate_never_reads_or_writes_pages() {
        SeedWikiOptions();
        var (chapter, portal, _) = SeedPortal(mirror: true);

        await Post(SeedEditorToken(chapter), $"/api/assemblymotions/portals/{portal.Id}/wiki/validate");

        await Assert.That(FakeWikiBotClient.LoginCount).IsEqualTo(1);
        await Assert.That(FakeWikiBotClient.GetCount).IsEqualTo(0);
        await Assert.That(FakeWikiBotClient.Edits.Count).IsEqualTo(0);
        await Assert.That(FakeWikiBotClient.Protected.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Validate_requires_events_edit() {
        SeedWikiOptions();
        var (chapter, portal, _) = SeedPortal(mirror: false);
        var url = $"/api/assemblymotions/portals/{portal.Id}/wiki/validate";

        var commissionOnly = await Post(SeedCommissionToken(chapter), url);
        var plain = await Post(Builder.SeedAuthenticatedUser().Token, url);
        var unknown = await Post(SeedEditorToken(chapter), $"/api/assemblymotions/portals/{Guid.NewGuid()}/wiki/validate");

        await Assert.That(commissionOnly.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
        await Assert.That(plain.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
        await Assert.That(unknown.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
        await Assert.That(FakeWikiBotClient.LoginCount).IsEqualTo(0);
    }

    [Test]
    public async Task Enabling_mirror_with_bad_credentials_refused() {
        SeedWikiOptions();
        FakeWikiBotClient.ThrowOn["Login"] = new WikiBotException("Anmeldung am Wiki fehlgeschlagen: Falsches Passwort", "login_failed");
        var (chapter, portal, type) = SeedPortal(mirror: false);
        var motion = await CreateMotion(SeedParticipant(chapter), portal, type, release: true);
        SetSyncState(motion.Id, WikiSyncState.NotMirrored);
        using var client = await ClientAsync(SeedEditorToken(chapter));

        var response = await client.PutAsJsonAsync($"/api/assemblymotions/portals/{portal.Id}", UpdateRequest(portal, mirror: true));
        var body = await response.Content.ReadAsStringAsync();

        await Assert.That(response.StatusCode).IsEqualTo((HttpStatusCode)422);
        await Assert.That(body).Contains("error.assembly_motion.wiki_login_failed");
        await Assert.That(body).Contains("Falsches");
        await Assert.That(Db.AssemblyMotionPortals.First(p => p.Id == portal.Id).WikiMirrorEnabled).IsFalse();
        await Assert.That(Reload(motion.Id).WikiSyncState).IsEqualTo(WikiSyncState.NotMirrored);
    }

    [Test]
    public async Task Enabling_mirror_when_unconfigured_refused() {
        var (chapter, portal, _) = SeedPortal(mirror: false);
        using var client = await ClientAsync(SeedEditorToken(chapter));

        var response = await client.PutAsJsonAsync($"/api/assemblymotions/portals/{portal.Id}", UpdateRequest(portal, mirror: true));
        var body = await response.Content.ReadAsStringAsync();

        await Assert.That(response.StatusCode).IsEqualTo((HttpStatusCode)422);
        await Assert.That(body).Contains("error.assembly_motion.wiki_not_configured");
        await Assert.That(Db.AssemblyMotionPortals.First(p => p.Id == portal.Id).WikiMirrorEnabled).IsFalse();
        await Assert.That(FakeWikiBotClient.LoginCount).IsEqualTo(0);
    }

    [Test]
    public async Task Enabling_mirror_queues_numbered_motions() {
        SeedWikiOptions();
        var (chapter, portal, type) = SeedPortal(mirror: false);
        var collectingType = new AssemblyMotionType { PortalId = portal.Id, Code = "SA", Name = "Sonstiger", MinSubmitters = 3 };
        Db.Insert(collectingType);
        var author = SeedParticipant(chapter);
        var numbered = await CreateMotion(author, portal, type, release: true);
        var collecting = await CreateMotion(author, portal, collectingType, release: true);
        var draft = await CreateMotion(author, portal, type, release: false);
        using var client = await ClientAsync(SeedEditorToken(chapter));

        var before = Reload(numbered.Id).WikiSyncState;
        var response = await client.PutAsJsonAsync($"/api/assemblymotions/portals/{portal.Id}", UpdateRequest(portal, mirror: true));

        await Assert.That(before).IsEqualTo(WikiSyncState.NotMirrored);
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(Db.AssemblyMotionPortals.First(p => p.Id == portal.Id).WikiMirrorEnabled).IsTrue();
        await Assert.That(Reload(numbered.Id).WikiSyncState).IsEqualTo(WikiSyncState.Pending);
        await Assert.That(Reload(collecting.Id).WikiSyncState).IsEqualTo(WikiSyncState.NotMirrored);
        await Assert.That(Reload(draft.Id).WikiSyncState).IsEqualTo(WikiSyncState.NotMirrored);
        await Assert.That(FakeWikiBotClient.LoginCount).IsEqualTo(1);
        await Assert.That(FakeWikiBotClient.Edits.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Submitted_motion_on_unmirrored_portal_stays_not_mirrored() {
        var (chapter, portal, type) = SeedPortal(mirror: false);

        var motion = await CreateMotion(SeedParticipant(chapter), portal, type, release: true);

        await Assert.That(Reload(motion.Id).Number).IsNotNull();
        await Assert.That(Reload(motion.Id).WikiSyncState).IsEqualTo(WikiSyncState.NotMirrored);
    }

    [Test]
    public async Task Disabling_mirror_resets_pending_to_not_mirrored() {
        var (chapter, portal, type) = SeedPortal(mirror: true);
        var author = SeedParticipant(chapter);
        var pending = await CreateMotion(author, portal, type, release: true);
        var synced = await CreateMotion(author, portal, type, release: true);
        var failed = await CreateMotion(author, portal, type, release: true);
        SetSyncState(synced.Id, WikiSyncState.Synced);
        SetSyncState(failed.Id, WikiSyncState.Failed, failures: 2);
        using var client = await ClientAsync(SeedEditorToken(chapter));

        var before = Reload(pending.Id).WikiSyncState;
        var response = await client.PutAsJsonAsync($"/api/assemblymotions/portals/{portal.Id}", UpdateRequest(portal, mirror: false));

        await Assert.That(before).IsEqualTo(WikiSyncState.Pending);
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(Reload(pending.Id).WikiSyncState).IsEqualTo(WikiSyncState.NotMirrored);
        await Assert.That(Reload(synced.Id).WikiSyncState).IsEqualTo(WikiSyncState.Synced);
        await Assert.That(Reload(failed.Id).WikiSyncState).IsEqualTo(WikiSyncState.Failed);
    }

    [Test]
    public async Task Changing_prefix_on_enabled_mirror_requeues_numbered_without_login() {
        SeedWikiOptions();
        var (chapter, portal, type) = SeedPortal(mirror: true);
        var author = SeedParticipant(chapter);
        var numbered = await CreateMotion(author, portal, type, release: true);
        var draft = await CreateMotion(author, portal, type, release: false);
        SetSyncState(numbered.Id, WikiSyncState.Synced);
        var request = UpdateRequest(portal, mirror: true);
        request.WikiPagePrefix = "Antrag:Echt";
        using var client = await ClientAsync(SeedEditorToken(chapter));

        var response = await client.PutAsJsonAsync($"/api/assemblymotions/portals/{portal.Id}", request);

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(Reload(numbered.Id).WikiSyncState).IsEqualTo(WikiSyncState.Pending);
        await Assert.That(Reload(draft.Id).WikiSyncState).IsEqualTo(WikiSyncState.NotMirrored);
        await Assert.That(FakeWikiBotClient.LoginCount).IsEqualTo(0);
        await Assert.That(FakeWikiBotClient.Edits.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Dashboard_wiki_failed_filter_covers_both_failure_states() {
        var (chapter, portal, type) = SeedPortal(mirror: true);
        var author = SeedParticipant(chapter);
        var failed = await CreateMotion(author, portal, type, release: true);
        var protectionFailed = await CreateMotion(author, portal, type, release: true);
        var synced = await CreateMotion(author, portal, type, release: true);
        SetSyncState(failed.Id, WikiSyncState.Failed);
        SetSyncState(protectionFailed.Id, WikiSyncState.ProtectionFailed);
        SetSyncState(synced.Id, WikiSyncState.Synced);
        using var client = await ClientAsync(SeedCommissionToken(chapter));

        var response = await client.GetAsync($"/api/assemblymotions/portals/{portal.Id}/dashboard?WikiFailed=true");
        var list = await response.Content.ReadFromJsonAsync<AssemblyMotionDashboardResponse>();

        await Assert.That(list!.Items.Select(i => i.Id).ToArray()).IsEquivalentTo(new[] { failed.Id, protectionFailed.Id });
        await Assert.That(list.FailedWikiSyncCount).IsEqualTo(2);
        await Assert.That(list.WikiMirrorEnabled).IsTrue();
    }

    [Test]
    public async Task Saving_an_already_enabled_mirror_does_not_log_in_or_requeue() {
        SeedWikiOptions();
        var (chapter, portal, type) = SeedPortal(mirror: true);
        var motion = await CreateMotion(SeedParticipant(chapter), portal, type, release: true);
        SetSyncState(motion.Id, WikiSyncState.Synced);
        using var client = await ClientAsync(SeedEditorToken(chapter));

        var response = await client.PutAsJsonAsync($"/api/assemblymotions/portals/{portal.Id}", UpdateRequest(portal, mirror: true));

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(FakeWikiBotClient.LoginCount).IsEqualTo(0);
        await Assert.That(Reload(motion.Id).WikiSyncState).IsEqualTo(WikiSyncState.Synced);
    }

    [Test]
    public async Task Disabling_mirror_writes_nothing() {
        SeedWikiOptions();
        var (chapter, portal, type) = SeedPortal(mirror: true);
        var motion = await CreateMotion(SeedParticipant(chapter), portal, type, release: true);
        SetSyncState(motion.Id, WikiSyncState.Synced);
        using var client = await ClientAsync(SeedEditorToken(chapter));

        var response = await client.PutAsJsonAsync($"/api/assemblymotions/portals/{portal.Id}", UpdateRequest(portal, mirror: false));

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(Db.AssemblyMotionPortals.First(p => p.Id == portal.Id).WikiMirrorEnabled).IsFalse();
        await Assert.That(Reload(motion.Id).WikiSyncState).IsEqualTo(WikiSyncState.Synced);
        await Assert.That(FakeWikiBotClient.LoginCount).IsEqualTo(0);
        await Assert.That(FakeWikiBotClient.GetCount).IsEqualTo(0);
        await Assert.That(FakeWikiBotClient.Edits.Count).IsEqualTo(0);
        await Assert.That(FakeWikiBotClient.Protected.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Resync_requires_commission() {
        var (chapter, portal, type) = SeedPortal(mirror: true);
        var motion = await CreateMotion(SeedParticipant(chapter), portal, type, release: true);
        SetSyncState(motion.Id, WikiSyncState.Synced);
        var url = $"/api/assemblymotions/motions/{motion.Id}/wiki/resync";

        var plain = await Post(Builder.SeedAuthenticatedUser().Token, url);
        var editorOnly = await Post(SeedEditorToken(chapter), url);
        var commission = await Post(SeedCommissionToken(chapter), url);

        await Assert.That(plain.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
        await Assert.That(editorOnly.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
        await Assert.That(commission.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }

    [Test]
    public async Task Resync_one_queues_and_resets_backoff() {
        var (chapter, portal, type) = SeedPortal(mirror: true);
        var motion = await CreateMotion(SeedParticipant(chapter), portal, type, release: true);
        SetSyncState(motion.Id, WikiSyncState.Failed, failures: 7, nextAttempt: DateTime.UtcNow.AddHours(6));

        var response = await Post(SeedCommissionToken(chapter), $"/api/assemblymotions/motions/{motion.Id}/wiki/resync");
        var detail = await response.Content.ReadFromJsonAsync<AssemblyMotionDetailDTO>();
        var reloaded = Reload(motion.Id);

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(detail!.Commission!.WikiSyncState).IsEqualTo(WikiSyncState.Pending);
        await Assert.That(detail.Commission.WikiPageTitle).IsEqualTo("Antrag:Test/SÄA001");
        await Assert.That(reloaded.WikiSyncState).IsEqualTo(WikiSyncState.Pending);
        await Assert.That(reloaded.WikiFailureCount).IsEqualTo(0);
        await Assert.That(reloaded.WikiNextAttemptAt).IsNull();
        await Assert.That(Db.AuditLogs.Count(a => a.EntityId == motion.Id && a.Action == "WikiResyncRequested")).IsEqualTo(1);
        await Assert.That(FakeWikiBotClient.LoginCount).IsEqualTo(0);
        await Assert.That(FakeWikiBotClient.Edits.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Resync_one_for_draft_is_404() {
        var (chapter, portal, type) = SeedPortal(mirror: true);
        var draft = await CreateMotion(SeedParticipant(chapter), portal, type, release: false);

        var response = await Post(SeedCommissionToken(chapter), $"/api/assemblymotions/motions/{draft.Id}/wiki/resync");

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task Resync_one_for_unnumbered_motion_conflicts() {
        var (chapter, portal, type) = SeedPortal(mirror: true, minSubmitters: 3);
        var collecting = await CreateMotion(SeedParticipant(chapter), portal, type, release: true);

        var response = await Post(SeedCommissionToken(chapter), $"/api/assemblymotions/motions/{collecting.Id}/wiki/resync");

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Conflict);
    }

    [Test]
    public async Task Resync_all_requires_commission() {
        var (chapter, portal, _) = SeedPortal(mirror: true);
        var url = $"/api/assemblymotions/portals/{portal.Id}/wiki/resync";

        var plain = await Post(Builder.SeedAuthenticatedUser().Token, url);
        var editorOnly = await Post(SeedEditorToken(chapter), url);
        var unknown = await Post(SeedCommissionToken(chapter), $"/api/assemblymotions/portals/{Guid.NewGuid()}/wiki/resync");
        var commission = await Post(SeedCommissionToken(chapter), url);

        await Assert.That(plain.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
        await Assert.That(editorOnly.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
        await Assert.That(unknown.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
        await Assert.That(commission.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }

    [Test]
    public async Task Resync_all_counts_only_numbered() {
        var (chapter, portal, type) = SeedPortal(mirror: true);
        var collectingType = new AssemblyMotionType { PortalId = portal.Id, Code = "SA", Name = "Sonstiger", MinSubmitters = 3 };
        Db.Insert(collectingType);
        var author = SeedParticipant(chapter);
        var numberedA = await CreateMotion(author, portal, type, release: true);
        var numberedB = await CreateMotion(author, portal, type, release: true);
        var collecting = await CreateMotion(author, portal, collectingType, release: true);
        var draft = await CreateMotion(author, portal, type, release: false);
        SetSyncState(numberedA.Id, WikiSyncState.Failed, failures: 4, nextAttempt: DateTime.UtcNow.AddHours(3));
        SetSyncState(numberedB.Id, WikiSyncState.Synced);

        var response = await Post(SeedCommissionToken(chapter), $"/api/assemblymotions/portals/{portal.Id}/wiki/resync");
        var result = await response.Content.ReadFromJsonAsync<AssemblyMotionWikiResyncAllDTO>();

        await Assert.That(result!.Queued).IsEqualTo(2);
        await Assert.That(Reload(numberedA.Id).WikiSyncState).IsEqualTo(WikiSyncState.Pending);
        await Assert.That(Reload(numberedA.Id).WikiFailureCount).IsEqualTo(0);
        await Assert.That(Reload(numberedA.Id).WikiNextAttemptAt).IsNull();
        await Assert.That(Reload(numberedB.Id).WikiSyncState).IsEqualTo(WikiSyncState.Pending);
        await Assert.That(Reload(collecting.Id).WikiSyncState).IsEqualTo(WikiSyncState.NotMirrored);
        await Assert.That(Reload(draft.Id).WikiSyncState).IsEqualTo(WikiSyncState.NotMirrored);

        var audit = Db.AuditLogs.Where(a => a.EntityId == portal.Id && a.Action == "WikiResyncRequested").ToList();
        await Assert.That(audit.Count).IsEqualTo(1);
        await Assert.That(audit[0].NewValue).IsEqualTo("2");
        await Assert.That(FakeWikiBotClient.Edits.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Resync_on_unmirrored_portal_conflicts() {
        var (chapter, portal, type) = SeedPortal(mirror: false);
        var motion = await CreateMotion(SeedParticipant(chapter), portal, type, release: true);
        SetSyncState(motion.Id, WikiSyncState.Synced);
        var token = SeedCommissionToken(chapter);

        var one = await Post(token, $"/api/assemblymotions/motions/{motion.Id}/wiki/resync");
        var all = await Post(token, $"/api/assemblymotions/portals/{portal.Id}/wiki/resync");

        await Assert.That(one.StatusCode).IsEqualTo(HttpStatusCode.Conflict);
        await Assert.That(all.StatusCode).IsEqualTo(HttpStatusCode.Conflict);
        await Assert.That(Reload(motion.Id).WikiSyncState).IsEqualTo(WikiSyncState.Synced);
    }
}
