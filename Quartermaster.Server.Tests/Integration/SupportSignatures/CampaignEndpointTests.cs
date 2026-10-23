using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading.Tasks;
using LinqToDB;
using Quartermaster.Api;
using Quartermaster.Api.I18n;
using Quartermaster.Api.SupportSignatures;
using Quartermaster.Data.Chapters;
using Quartermaster.Data.SupportSignatures;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.Integration.SupportSignatures;

public class CampaignEndpointTests : IntegrationTestBase {
    private static readonly byte[] PdfBytes = "%PDF-1.4\n%test\n"u8.ToArray();
    private static readonly byte[] PngBytes = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0];

    private async Task<HttpClient> HolderAsync(Chapter chapter) {
        var (_, token) = Builder.SeedAuthenticatedUser(chapterPermissions: new Dictionary<Guid, string[]> {
            [chapter.Id] = [PermissionIdentifier.ManageSupportSignatures]
        });
        return await AuthenticatedClientWithCsrfAsync(token);
    }

    private static SignatureCampaignSaveRequest Request(Guid chapterId, string name = "Direktkandidatur Celle") {
        return new SignatureCampaignSaveRequest {
            ChapterId = chapterId,
            Name = name,
            Target = 100,
            MarginPercent = 20,
            Deadline = DateTimeOffset.UtcNow.AddDays(60),
            OverdueDays = 21
        };
    }

    private async Task<SignatureCampaignDTO> CreateAsync(HttpClient client, Guid chapterId, string name = "Direktkandidatur Celle") {
        var response = await client.PostAsJsonAsync("/api/support-signatures/campaigns", Request(chapterId, name));
        return (await response.Content.ReadFromJsonAsync<SignatureCampaignDTO>())!;
    }

    private static MultipartFormDataContent FormFile(byte[] bytes, string fileName) {
        var content = new ByteArrayContent(bytes);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        return new MultipartFormDataContent { { content, "File", fileName } };
    }

    [Test]
    public async Task Kreisverband_holder_sees_only_own_campaigns() {
        var state = Builder.SeedChapter("Niedersachsen");
        var celle = Builder.SeedChapter("Celle", parentChapterId: state.Id);
        var hannover = Builder.SeedChapter("Hannover", parentChapterId: state.Id);
        using var celleClient = await HolderAsync(celle);
        using var hannoverClient = await HolderAsync(hannover);
        await CreateAsync(celleClient, celle.Id, "Celle");
        var foreign = await CreateAsync(hannoverClient, hannover.Id, "Hannover");

        var listed = await celleClient.GetFromJsonAsync<List<SignatureCampaignDTO>>("/api/support-signatures/campaigns");
        var get = await celleClient.GetAsync($"/api/support-signatures/campaigns/{foreign.Id}");

        await Assert.That(listed!.Select(c => c.Name).ToList()).IsEquivalentTo(["Celle"]);
        await Assert.That(get.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task Landesverband_holder_sees_campaigns_of_sub_chapters() {
        var state = Builder.SeedChapter("Niedersachsen");
        var celle = Builder.SeedChapter("Celle", parentChapterId: state.Id);
        using var celleClient = await HolderAsync(celle);
        using var stateClient = await HolderAsync(state);
        await CreateAsync(celleClient, celle.Id, "Celle");
        await CreateAsync(stateClient, state.Id, "Landesliste");

        var listed = await stateClient.GetFromJsonAsync<List<SignatureCampaignDTO>>("/api/support-signatures/campaigns");

        await Assert.That(listed!.Select(c => c.Name).ToList()).IsEquivalentTo(["Celle", "Landesliste"]);
    }

    [Test]
    public async Task Landesverband_holder_manages_campaigns_of_sub_chapters() {
        var state = Builder.SeedChapter("Niedersachsen");
        var celle = Builder.SeedChapter("Celle", parentChapterId: state.Id);
        using var celleClient = await HolderAsync(celle);
        using var stateClient = await HolderAsync(state);
        var campaign = await CreateAsync(celleClient, celle.Id, "Celle");

        var opened = await stateClient.GetAsync($"/api/support-signatures/campaigns/{campaign.Id}");
        var updated = await stateClient.PutAsJsonAsync($"/api/support-signatures/campaigns/{campaign.Id}", Request(celle.Id, "Celle 2027"));

        await Assert.That(opened.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(updated.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }

    [Test]
    public async Task Create_needs_permission_on_the_chosen_chapter() {
        var state = Builder.SeedChapter("Niedersachsen");
        var celle = Builder.SeedChapter("Celle", parentChapterId: state.Id);
        using var celleClient = await HolderAsync(celle);

        var response = await celleClient.PostAsJsonAsync("/api/support-signatures/campaigns", Request(state.Id));

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task Update_changes_the_settings() {
        var state = Builder.SeedChapter("Niedersachsen");
        using var client = await HolderAsync(state);
        var campaign = await CreateAsync(client, state.Id);
        var update = Request(state.Id, "Landesliste 2027");
        update.Target = 2000;
        update.Visibility = SignatureCampaignVisibility.Public;
        update.MemberNotice = "Infostand jeden Samstag am Kröpcke.";
        update.PostalAddress = "Piratenpartei Niedersachsen\nPostfach 1234\n30001 Hannover";
        update.ContactEmail = " sammeln@piraten-nds.de ";

        var response = await client.PutAsJsonAsync($"/api/support-signatures/campaigns/{campaign.Id}", update);
        var reloaded = await client.GetFromJsonAsync<SignatureCampaignDTO>($"/api/support-signatures/campaigns/{campaign.Id}");

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(reloaded!.Name).IsEqualTo("Landesliste 2027");
        await Assert.That(reloaded.Target).IsEqualTo(2000);
        await Assert.That(reloaded.Visibility).IsEqualTo(SignatureCampaignVisibility.Public);
        await Assert.That(reloaded.MemberNotice).IsEqualTo("Infostand jeden Samstag am Kröpcke.");
        await Assert.That(reloaded.PostalAddress).IsEqualTo("Piratenpartei Niedersachsen\nPostfach 1234\n30001 Hannover");
        await Assert.That(reloaded.ContactEmail).IsEqualTo("sammeln@piraten-nds.de");
    }

    [Test]
    public async Task Archive_hides_from_default_list_and_shows_with_filter() {
        var state = Builder.SeedChapter("Niedersachsen");
        using var client = await HolderAsync(state);
        var campaign = await CreateAsync(client, state.Id);

        var archive = await client.PostAsJsonAsync($"/api/support-signatures/campaigns/{campaign.Id}/archive", new { });
        var active = await client.GetFromJsonAsync<List<SignatureCampaignDTO>>("/api/support-signatures/campaigns");
        var archived = await client.GetFromJsonAsync<List<SignatureCampaignDTO>>("/api/support-signatures/campaigns?archived=true");

        await Assert.That(archive.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(active!.Count).IsEqualTo(0);
        await Assert.That(archived!.Single().ArchivedAt).IsNotNull();
    }

    [Test]
    public async Task Form_upload_accepts_pdf_and_rejects_png_and_oversize() {
        var state = Builder.SeedChapter("Niedersachsen");
        using var client = await HolderAsync(state);
        var campaign = await CreateAsync(client, state.Id);
        Db.SystemOptions
            .Where(o => o.Identifier == SupportSignatureOptions.FormMaxMegabytes && o.ChapterId == null)
            .Set(o => o.Value, "1")
            .Update();

        var png = await client.PutAsync($"/api/support-signatures/campaigns/{campaign.Id}/form", FormFile(PngBytes, "form.png"));
        var oversize = await client.PutAsync($"/api/support-signatures/campaigns/{campaign.Id}/form",
            FormFile([.. PdfBytes, .. new byte[1024 * 1024]], "big.pdf"));
        var pdf = await client.PutAsync($"/api/support-signatures/campaigns/{campaign.Id}/form", FormFile(PdfBytes, "Formblatt Landesliste.pdf"));
        var reloaded = await client.GetFromJsonAsync<SignatureCampaignDTO>($"/api/support-signatures/campaigns/{campaign.Id}");

        await Assert.That(png.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
        await Assert.That(await png.Content.ReadAsStringAsync()).Contains(I18nKey.Error.SupportSignatures.Form.NotPdf);
        await Assert.That(oversize.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
        await Assert.That(await oversize.Content.ReadAsStringAsync()).Contains(I18nKey.Error.SupportSignatures.Form.TooLarge);
        await Assert.That(pdf.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(reloaded!.HasForm).IsTrue();
        await Assert.That(reloaded.FormFileName).IsEqualTo("Formblatt Landesliste.pdf");
    }

    [Test]
    public async Task Form_upload_replaces_and_delete_removes_the_file() {
        var state = Builder.SeedChapter("Niedersachsen");
        using var client = await HolderAsync(state);
        var campaign = await CreateAsync(client, state.Id);
        await client.PutAsync($"/api/support-signatures/campaigns/{campaign.Id}/form", FormFile(PdfBytes, "alt.pdf"));

        var replace = await client.PutAsync($"/api/support-signatures/campaigns/{campaign.Id}/form", FormFile([.. PdfBytes, 1, 2, 3], "neu.pdf"));
        var afterReplace = await client.GetFromJsonAsync<SignatureCampaignDTO>($"/api/support-signatures/campaigns/{campaign.Id}");
        var delete = await client.DeleteAsync($"/api/support-signatures/campaigns/{campaign.Id}/form");
        var afterDelete = await client.GetFromJsonAsync<SignatureCampaignDTO>($"/api/support-signatures/campaigns/{campaign.Id}");

        await Assert.That(replace.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(afterReplace!.FormFileName).IsEqualTo("neu.pdf");
        await Assert.That(delete.StatusCode).IsEqualTo(HttpStatusCode.NoContent);
        await Assert.That(afterDelete!.HasForm).IsFalse();
    }

    [Test]
    public async Task Holder_downloads_the_form_and_other_chapters_cannot() {
        var state = Builder.SeedChapter("Niedersachsen");
        var celle = Builder.SeedChapter("Celle", parentChapterId: state.Id);
        var hannover = Builder.SeedChapter("Hannover", parentChapterId: state.Id);
        using var celleClient = await HolderAsync(celle);
        using var hannoverClient = await HolderAsync(hannover);
        var campaign = await CreateAsync(celleClient, celle.Id);
        await celleClient.PutAsync($"/api/support-signatures/campaigns/{campaign.Id}/form", FormFile(PdfBytes, "Formblatt Celle.pdf"));

        var own = await celleClient.GetAsync($"/api/support-signatures/campaigns/{campaign.Id}/form");
        var foreign = await hannoverClient.GetAsync($"/api/support-signatures/campaigns/{campaign.Id}/form");

        await Assert.That(own.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(await own.Content.ReadAsByteArrayAsync()).IsEquivalentTo(PdfBytes);
        await Assert.That(foreign.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
    }
}
