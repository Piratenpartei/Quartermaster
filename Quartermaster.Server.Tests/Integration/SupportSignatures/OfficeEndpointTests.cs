using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
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
}
