using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Threading.Tasks;
using LinqToDB;
using Quartermaster.Api;
using Quartermaster.Api.I18n;
using Quartermaster.Api.SupportSignatures;
using Quartermaster.Data.SupportSignatures;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.Integration.SupportSignatures;

public class OfficeEndpointTests : IntegrationTestBase {
    private async Task<HttpClient> HolderAsync() {
        var chapter = Builder.SeedChapter("Niedersachsen");
        var (_, token) = Builder.SeedAuthenticatedUser(chapterPermissions: new Dictionary<Guid, string[]> {
            [chapter.Id] = [PermissionIdentifier.ManageSupportSignatures]
        });
        return await AuthenticatedClientWithCsrfAsync(token);
    }

    private CertificationOfficeSaveRequest Request(params Guid[] municipalityIds) {
        return new CertificationOfficeSaveRequest {
            Name = "Samtgemeinde Lachendorf",
            Street = "Oppershäuser Str. 1",
            PostCode = "29331",
            City = "Lachendorf",
            MunicipalityIds = [.. municipalityIds]
        };
    }

    [Test]
    public async Task Offices_need_the_permission_in_any_chapter() {
        using var anonymous = AnonymousClient();
        var (_, plainToken) = Builder.SeedAuthenticatedUser();
        using var plain = await AuthenticatedClientWithCsrfAsync(plainToken);
        using var holder = await HolderAsync();

        await Assert.That((await anonymous.GetAsync("/api/support-signatures/offices")).StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
        await Assert.That((await plain.GetAsync("/api/support-signatures/offices")).StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
        await Assert.That((await holder.GetAsync("/api/support-signatures/offices")).StatusCode).IsEqualTo(HttpStatusCode.OK);
    }

    [Test]
    public async Task Created_office_is_listed_with_its_municipalities() {
        using var holder = await HolderAsync();
        var ahnsbeck = Builder.SeedAdminDivision("Ahnsbeck", depth: 7);

        var created = await holder.PostAsJsonAsync("/api/support-signatures/offices", Request(ahnsbeck.Id));
        var listed = await holder.GetFromJsonAsync<List<CertificationOfficeDTO>>("/api/support-signatures/offices");

        await Assert.That(created.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(listed!.Count).IsEqualTo(1);
        await Assert.That(listed[0].Municipalities[0].Name).IsEqualTo("Ahnsbeck");
        await Assert.That(listed[0].HasShipments).IsFalse();
    }

    [Test]
    public async Task Municipality_can_belong_to_one_office_only() {
        using var holder = await HolderAsync();
        var ahnsbeck = Builder.SeedAdminDivision("Ahnsbeck", depth: 7);
        await holder.PostAsJsonAsync("/api/support-signatures/offices", Request(ahnsbeck.Id));

        var second = await holder.PostAsJsonAsync("/api/support-signatures/offices", Request(ahnsbeck.Id));

        await Assert.That(second.StatusCode).IsEqualTo(HttpStatusCode.Conflict);
        await Assert.That(await second.Content.ReadAsStringAsync()).Contains(I18nKey.Error.SupportSignatures.Office.MunicipalityTaken);
    }

    [Test]
    public async Task Only_municipalities_can_be_assigned() {
        using var holder = await HolderAsync();
        var county = Builder.SeedAdminDivision("Landkreis Celle", depth: 6);

        var response = await holder.PostAsJsonAsync("/api/support-signatures/offices", Request(county.Id));

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
        await Assert.That(await response.Content.ReadAsStringAsync()).Contains(I18nKey.Error.SupportSignatures.Office.NotAMunicipality);
    }

    [Test]
    public async Task Office_with_shipments_cannot_be_deleted() {
        using var holder = await HolderAsync();
        var created = await holder.PostAsJsonAsync("/api/support-signatures/offices", Request());
        var office = (await created.Content.ReadFromJsonAsync<CertificationOfficeDTO>())!;
        var chapter = Builder.SeedChapter("Celle");
        var campaign = new SignatureCampaign { ChapterId = chapter.Id, Name = "Landesliste", Target = 2000, Deadline = DateTime.UtcNow.AddDays(30), CreatedAt = DateTime.UtcNow };
        Db.Insert(campaign);
        Db.Insert(new SignatureShipment { CampaignId = campaign.Id, OfficeId = office.Id, CreatedAt = DateTime.UtcNow });

        var response = await holder.DeleteAsync($"/api/support-signatures/offices/{office.Id}");

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Conflict);
        await Assert.That(await response.Content.ReadAsStringAsync()).Contains(I18nKey.Error.SupportSignatures.Office.HasShipments);
    }

    [Test]
    public async Task Save_requires_name_and_address() {
        using var holder = await HolderAsync();
        var request = Request();
        request.Name = "";
        request.City = "";

        var response = await holder.PostAsJsonAsync("/api/support-signatures/offices", request);

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadAsStringAsync();
        await Assert.That(body).Contains(I18nKey.Error.SupportSignatures.Office.NameRequired);
        await Assert.That(body).Contains(I18nKey.Error.SupportSignatures.Office.AddressRequired);
    }

    [Test]
    public async Task Suggestion_answers_for_a_municipality() {
        using var holder = await HolderAsync();
        var ahnsbeck = Builder.SeedAdminDivision("Ahnsbeck", depth: 7);

        var suggestion = await holder.GetFromJsonAsync<OfficeSuggestionDTO>($"/api/support-signatures/offices/suggestion?municipalityId={ahnsbeck.Id}");

        await Assert.That(suggestion!.SuggestedName).IsEqualTo("Ahnsbeck");
    }

    private const string ImportFile =
        "Gemeinde,Gemeindeschluessel,Amt,Zusatz,Strasse_HausNr,PLZ,Ort\n" +
        "Salzgitter,03102000,Stadt Salzgitter,Wahlbüro,Joachim-Campe-Straße 6-8,38226,Salzgitter\n";

    private CertificationOffice OfficialOffice(params Guid[] municipalityIds) {
        var office = new CertificationOffice {
            Name = "Kreisfreie Stadt Salzgitter",
            Street = "Joachim-Campe-Straße 6 - 8",
            PostCode = "38226",
            City = "Salzgitter",
            OfficialKey = "03102|Kreisfreie Stadt Salzgitter"
        };
        Db.Insert(office);
        foreach (var municipalityId in municipalityIds) {
            Db.Insert(new OfficialOfficeMunicipality { AdministrativeDivisionId = municipalityId, OfficeId = office.Id });
        }
        return office;
    }

    [Test]
    public async Task Import_previews_first_and_stores_on_apply() {
        using var holder = await HolderAsync();
        Builder.SeedAdminDivision("Salzgitter", depth: 7, adminCode: "3102000");
        var content = Encoding.UTF8.GetBytes(ImportFile);

        var preview = await holder.PostAsJsonAsync("/api/support-signatures/offices/import", new OfficeImportRequest { Content = content });
        var afterPreview = await holder.GetFromJsonAsync<List<CertificationOfficeDTO>>("/api/support-signatures/offices");
        var applied = await holder.PostAsJsonAsync("/api/support-signatures/offices/import", new OfficeImportRequest { Content = content, Apply = true });
        var afterApply = await holder.GetFromJsonAsync<List<CertificationOfficeDTO>>("/api/support-signatures/offices");

        await Assert.That((await preview.Content.ReadFromJsonAsync<OfficeImportResultDTO>())!.Entries.Count).IsEqualTo(1);
        await Assert.That(afterPreview).IsEmpty();
        await Assert.That((await applied.Content.ReadFromJsonAsync<OfficeImportResultDTO>())!.Applied).IsTrue();
        await Assert.That(afterApply!.Single().AddressAddition).IsEqualTo("Wahlbüro");
    }

    [Test]
    public async Task Import_needs_the_permission_and_a_readable_file() {
        var (_, plainToken) = Builder.SeedAuthenticatedUser();
        using var plain = await AuthenticatedClientWithCsrfAsync(plainToken);
        using var holder = await HolderAsync();

        var forbidden = await plain.PostAsJsonAsync("/api/support-signatures/offices/import", new OfficeImportRequest { Content = Encoding.UTF8.GetBytes(ImportFile) });
        var noColumns = await holder.PostAsJsonAsync("/api/support-signatures/offices/import", new OfficeImportRequest { Content = Encoding.UTF8.GetBytes("a,b\n1,2\n") });
        var tooLarge = await holder.PostAsJsonAsync("/api/support-signatures/offices/import", new OfficeImportRequest { Content = new byte[OfficeImportRequest.MaxContentBytes + 1] });

        await Assert.That(forbidden.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
        await Assert.That(noColumns.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
        await Assert.That(await noColumns.Content.ReadAsStringAsync()).Contains(I18nKey.Error.SupportSignatures.OfficeImport.MissingColumns);
        await Assert.That(tooLarge.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
        await Assert.That(await tooLarge.Content.ReadAsStringAsync()).Contains(I18nKey.Error.SupportSignatures.OfficeImport.FileTooLarge);
    }

    [Test]
    public async Task Official_office_is_read_only_and_not_listed() {
        using var holder = await HolderAsync();
        var office = OfficialOffice();

        var update = await holder.PutAsJsonAsync($"/api/support-signatures/offices/{office.Id}", Request());
        var delete = await holder.DeleteAsync($"/api/support-signatures/offices/{office.Id}");
        var listed = await holder.GetFromJsonAsync<List<CertificationOfficeDTO>>("/api/support-signatures/offices");

        await Assert.That(update.StatusCode).IsEqualTo(HttpStatusCode.Conflict);
        await Assert.That(await update.Content.ReadAsStringAsync()).Contains(I18nKey.Error.SupportSignatures.Office.Official);
        await Assert.That(delete.StatusCode).IsEqualTo(HttpStatusCode.Conflict);
        await Assert.That(listed).IsEmpty();
    }

    [Test]
    public async Task Responsible_lookup_returns_the_office_forms_go_to() {
        using var holder = await HolderAsync();
        var salzgitter = Builder.SeedAdminDivision("Salzgitter", depth: 7);
        var elsewhere = Builder.SeedAdminDivision("Nirgendwo", depth: 7);
        OfficialOffice(salzgitter.Id);

        var found = await holder.GetFromJsonAsync<CertificationOfficeDTO>($"/api/support-signatures/offices/responsible?municipalityId={salzgitter.Id}");
        var missing = await holder.GetAsync($"/api/support-signatures/offices/responsible?municipalityId={elsewhere.Id}");

        await Assert.That(found!.IsOfficial).IsTrue();
        await Assert.That(found.Municipalities.Single().Name).IsEqualTo("Salzgitter");
        await Assert.That(missing.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }
}
