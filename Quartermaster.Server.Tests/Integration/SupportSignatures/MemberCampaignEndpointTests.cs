using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using LinqToDB;
using Microsoft.Extensions.DependencyInjection;
using Quartermaster.Api;
using Quartermaster.Api.Dashboard;
using Quartermaster.Api.SupportSignatures;
using Quartermaster.Data.Chapters;
using Quartermaster.Data.SupportSignatures;
using Quartermaster.Server.SupportSignatures;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.Integration.SupportSignatures;

public class MemberCampaignEndpointTests : IntegrationTestBase {
    private Chapter _state = default!;
    private Chapter _celle = default!;
    private Chapter _hannover = default!;

    [Before(Test)]
    public void Setup() {
        _state = Builder.SeedChapter("Niedersachsen");
        _celle = Builder.SeedChapter("Celle", parentChapterId: _state.Id);
        _hannover = Builder.SeedChapter("Hannover", parentChapterId: _state.Id);
    }

    private SignatureCampaign Campaign(Chapter chapter, string name, SignatureCampaignVisibility visibility = SignatureCampaignVisibility.MembersOnly,
        DateTime? deadline = null, DateTime? archivedAt = null) {
        var campaign = new SignatureCampaign {
            ChapterId = chapter.Id,
            Name = name,
            Target = 100,
            MarginPercent = 20,
            Deadline = deadline ?? DateTime.UtcNow.AddDays(30),
            Visibility = visibility,
            ArchivedAt = archivedAt,
            CreatedAt = DateTime.UtcNow
        };
        Db.Insert(campaign);
        return campaign;
    }

    private HttpClient MemberClient(Chapter chapter) {
        var (user, token) = Builder.SeedAuthenticatedUser();
        Builder.SeedMember(chapterId: chapter.Id, userId: user.Id);
        return AuthenticatedClient(token);
    }

    [Test]
    public async Task Every_signed_in_user_gets_all_visible_cards() {
        Campaign(_state, "Landesliste");
        Campaign(_celle, "Direktkandidatur Celle");
        Campaign(_hannover, "Direktkandidatur Hannover");
        using var memberClient = MemberClient(_celle);
        var (_, token) = Builder.SeedAuthenticatedUser();
        using var userClient = AuthenticatedClient(token);

        var memberDashboard = await memberClient.GetFromJsonAsync<DashboardDTO>("/api/dashboard");
        var userDashboard = await userClient.GetFromJsonAsync<DashboardDTO>("/api/dashboard");

        await Assert.That(memberDashboard!.SupportSignatureCampaigns!.Select(c => c.Name).ToList())
            .IsEquivalentTo(["Landesliste", "Direktkandidatur Celle", "Direktkandidatur Hannover"]);
        await Assert.That(userDashboard!.SupportSignatureCampaigns!.Select(c => c.Name).ToList())
            .IsEquivalentTo(["Landesliste", "Direktkandidatur Celle", "Direktkandidatur Hannover"]);
    }

    [Test]
    public async Task Hidden_archived_and_long_past_campaigns_are_left_out() {
        Campaign(_celle, "Versteckt", SignatureCampaignVisibility.Private);
        Campaign(_celle, "Archiviert", archivedAt: DateTime.UtcNow);
        Campaign(_celle, "Lange vorbei", deadline: DateTime.UtcNow.AddDays(-31));
        Campaign(_celle, "Gerade vorbei", deadline: DateTime.UtcNow.AddDays(-29));
        using var client = MemberClient(_celle);

        var dashboard = await client.GetFromJsonAsync<DashboardDTO>("/api/dashboard");

        await Assert.That(dashboard!.SupportSignatureCampaigns!.Select(c => c.Name).ToList()).IsEquivalentTo(["Gerade vorbei"]);
    }

    [Test]
    public async Task Card_shows_certified_against_target_and_goal() {
        var campaign = Campaign(_celle, "Direktkandidatur Celle");
        var municipality = Builder.SeedAdminDivision("Celle", depth: 7);
        Db.Insert(new SignatureIntake { CampaignId = campaign.Id, AdministrativeDivisionId = municipality.Id, Count = 9, AlreadyCertified = true, ReceivedOn = DateTime.UtcNow.Date, CreatedAt = DateTime.UtcNow });
        using var client = MemberClient(_celle);

        var page = await client.GetFromJsonAsync<MemberSignatureCampaignDTO>($"/api/support-signatures/member-campaigns/{campaign.Id}");

        await Assert.That(page!.CertifiedTotal).IsEqualTo(9);
        await Assert.That(page.CollectedTotal).IsEqualTo(9);
        await Assert.That(page.Target).IsEqualTo(100);
        await Assert.That(page.Goal).IsEqualTo(120);
        await Assert.That(page.ChapterName).IsEqualTo("Celle");
    }

    [Test]
    public async Task Collected_total_counts_everything_received_except_rejections() {
        var campaign = Campaign(_celle, "Direktkandidatur Celle");
        var municipality = Builder.SeedAdminDivision("Celle", depth: 7);
        var office = new CertificationOffice { Name = "Stadt Celle", Street = "Am Markt 14", PostCode = "29221", City = "Celle" };
        new CertificationOfficeRepository(Db).Create(office, [municipality.Id]);
        Db.Insert(new SignatureIntake { CampaignId = campaign.Id, AdministrativeDivisionId = municipality.Id, Count = 10, ReceivedOn = DateTime.UtcNow.Date, CreatedAt = DateTime.UtcNow });
        var shipment = new SignatureShipment { CampaignId = campaign.Id, OfficeId = office.Id, SentOn = DateTime.UtcNow.Date, CreatedAt = DateTime.UtcNow };
        new SignatureShipmentRepository(Db).Create(shipment, [new SignatureShipmentLine { AdministrativeDivisionId = municipality.Id, Count = 8 }]);
        new SignatureShipmentRepository(Db).CreateReturn(new SignatureReturn { ShipmentId = shipment.Id, ReturnedOn = DateTime.UtcNow.Date, CreatedAt = DateTime.UtcNow },
            [new SignatureReturnLine { AdministrativeDivisionId = municipality.Id, Certified = 5, Rejected = 2 }]);
        using var client = MemberClient(_celle);

        var page = await client.GetFromJsonAsync<MemberSignatureCampaignDTO>($"/api/support-signatures/member-campaigns/{campaign.Id}");

        await Assert.That(page!.CertifiedTotal).IsEqualTo(5);
        await Assert.That(page.CollectedTotal).IsEqualTo(8);
    }

    [Test]
    public async Task Anonymous_gets_only_public_campaigns() {
        var membersOnly = Campaign(_celle, "Direktkandidatur Celle");
        var open = Campaign(_hannover, "Direktkandidatur Hannover", SignatureCampaignVisibility.Public);
        Campaign(_state, "Intern", SignatureCampaignVisibility.Private);
        using var client = AnonymousClient();

        var dashboard = await client.GetFromJsonAsync<DashboardDTO>("/api/dashboard");
        var hiddenPage = await client.GetAsync($"/api/support-signatures/member-campaigns/{membersOnly.Id}");
        var hiddenForm = await client.GetAsync($"/api/support-signatures/member-campaigns/{membersOnly.Id}/form");
        var publicPage = await client.GetFromJsonAsync<MemberSignatureCampaignDTO>($"/api/support-signatures/member-campaigns/{open.Id}");

        await Assert.That(dashboard!.SupportSignatureCampaigns!.Select(c => c.Name).ToList()).IsEquivalentTo(["Direktkandidatur Hannover"]);
        await Assert.That(hiddenPage.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
        await Assert.That(hiddenForm.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
        await Assert.That(publicPage!.Name).IsEqualTo("Direktkandidatur Hannover");
    }

    [Test]
    public async Task Signed_in_user_gets_public_and_members_only_campaigns() {
        Campaign(_celle, "Direktkandidatur Celle");
        Campaign(_hannover, "Direktkandidatur Hannover", SignatureCampaignVisibility.Public);
        Campaign(_state, "Intern", SignatureCampaignVisibility.Private);
        var (_, token) = Builder.SeedAuthenticatedUser();
        using var client = AuthenticatedClient(token);

        var dashboard = await client.GetFromJsonAsync<DashboardDTO>("/api/dashboard");

        await Assert.That(dashboard!.SupportSignatureCampaigns!.Select(c => c.Name).ToList())
            .IsEquivalentTo(["Direktkandidatur Celle", "Direktkandidatur Hannover"]);
    }

    [Test]
    public async Task Card_carries_address_and_contact() {
        var campaign = Campaign(_celle, "Direktkandidatur Celle", SignatureCampaignVisibility.Public);
        Db.SignatureCampaigns
            .Where(c => c.Id == campaign.Id)
            .Set(c => c.PostalAddress, "Postfach 1234\n29221 Celle")
            .Set(c => c.ContactEmail, "celle@piraten-nds.de")
            .Update();
        using var client = AnonymousClient();

        var page = await client.GetFromJsonAsync<MemberSignatureCampaignDTO>($"/api/support-signatures/member-campaigns/{campaign.Id}");

        await Assert.That(page!.PostalAddress).IsEqualTo("Postfach 1234\n29221 Celle");
        await Assert.That(page.ContactEmail).IsEqualTo("celle@piraten-nds.de");
    }

    [Test]
    public async Task Detail_says_whether_the_caller_can_manage_the_campaign() {
        var campaign = Campaign(_celle, "Direktkandidatur Celle", SignatureCampaignVisibility.Public);
        var (_, stateToken) = Builder.SeedAuthenticatedUser(chapterPermissions: new Dictionary<Guid, string[]> {
            [_state.Id] = [PermissionIdentifier.ManageSupportSignatures]
        });
        var (_, hannoverToken) = Builder.SeedAuthenticatedUser(chapterPermissions: new Dictionary<Guid, string[]> {
            [_hannover.Id] = [PermissionIdentifier.ManageSupportSignatures]
        });
        using var stateClient = AuthenticatedClient(stateToken);
        using var hannoverClient = AuthenticatedClient(hannoverToken);
        using var anonymous = AnonymousClient();
        var url = $"/api/support-signatures/member-campaigns/{campaign.Id}";

        var state = await stateClient.GetFromJsonAsync<MemberSignatureCampaignDTO>(url);
        var hannover = await hannoverClient.GetFromJsonAsync<MemberSignatureCampaignDTO>(url);
        var visitor = await anonymous.GetFromJsonAsync<MemberSignatureCampaignDTO>(url);

        await Assert.That(state!.CanManage).IsTrue();
        await Assert.That(hannover!.CanManage).IsFalse();
        await Assert.That(visitor!.CanManage).IsFalse();
    }

    [Test]
    public async Task Hidden_campaign_gives_404() {
        var campaign = Campaign(_hannover, "Direktkandidatur Hannover", SignatureCampaignVisibility.Private);
        using var client = MemberClient(_celle);

        var page = await client.GetAsync($"/api/support-signatures/member-campaigns/{campaign.Id}");
        var form = await client.GetAsync($"/api/support-signatures/member-campaigns/{campaign.Id}/form");

        await Assert.That(page.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
        await Assert.That(form.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task Form_download_sends_the_original_file_name() {
        var campaign = Campaign(_celle, "Direktkandidatur Celle");
        var storage = Factory.Services.GetRequiredService<SupportSignatureFormStorage>();
        await storage.SaveAsync(campaign.Id, new MemoryStream("%PDF-1.4 test"u8.ToArray()), CancellationToken.None);
        Db.SignatureCampaigns
            .Where(c => c.Id == campaign.Id)
            .Set(c => c.HasForm, true)
            .Set(c => c.FormFileName, "Formblatt Celle.pdf")
            .Update();
        using var client = MemberClient(_celle);

        var form = await client.GetAsync($"/api/support-signatures/member-campaigns/{campaign.Id}/form");

        await Assert.That(form.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(form.Content.Headers.ContentType!.MediaType).IsEqualTo("application/pdf");
        await Assert.That(form.Content.Headers.ContentDisposition!.FileNameStar ?? form.Content.Headers.ContentDisposition.FileName).Contains("Formblatt Celle.pdf");
    }
}
