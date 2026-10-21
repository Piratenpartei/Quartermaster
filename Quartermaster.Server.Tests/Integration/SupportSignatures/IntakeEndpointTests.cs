using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using LinqToDB;
using Quartermaster.Api;
using Quartermaster.Api.I18n;
using Quartermaster.Api.SupportSignatures;
using Quartermaster.Data.AdministrativeDivisions;
using Quartermaster.Data.Chapters;
using Quartermaster.Data.SupportSignatures;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.Integration.SupportSignatures;

public class IntakeEndpointTests : IntegrationTestBase {
    private Chapter _state = default!;
    private AdministrativeDivision _county = default!;
    private AdministrativeDivision _ahnsbeck = default!;
    private AdministrativeDivision _eldingen = default!;
    private SignatureCampaign _campaign = default!;

    [Before(Test)]
    public void Setup() {
        _state = Builder.SeedChapter("Niedersachsen");
        _county = Builder.SeedAdminDivision("Landkreis Celle", depth: 6);
        _ahnsbeck = Builder.SeedAdminDivision("Ahnsbeck", depth: 7, parentId: _county.Id);
        _eldingen = Builder.SeedAdminDivision("Eldingen", depth: 7, parentId: _county.Id);
        _campaign = new SignatureCampaign { ChapterId = _state.Id, Name = "Landesliste", Target = 2000, Deadline = DateTime.UtcNow.AddDays(30), CreatedAt = DateTime.UtcNow };
        Db.Insert(_campaign);
    }

    private async Task<HttpClient> HolderAsync(Chapter chapter) {
        var (_, token) = Builder.SeedAuthenticatedUser(chapterPermissions: new Dictionary<Guid, string[]> {
            [chapter.Id] = [PermissionIdentifier.ManageSupportSignatures]
        });
        return await AuthenticatedClientWithCsrfAsync(token);
    }

    private static SignatureIntakeSaveRequest Intake(Guid municipalityId, int count, bool alreadyCertified = false) {
        return new SignatureIntakeSaveRequest {
            AdministrativeDivisionId = municipalityId,
            Count = count,
            AlreadyCertified = alreadyCertified,
            ReceivedOn = DateOnly.FromDateTime(DateTime.UtcNow)
        };
    }

    private async Task<SignatureIntakeDTO> AddAsync(HttpClient client, Guid municipalityId, int count) {
        var response = await client.PostAsJsonAsync($"/api/support-signatures/campaigns/{_campaign.Id}/intakes", Intake(municipalityId, count));
        return (await response.Content.ReadFromJsonAsync<SignatureIntakeDTO>())!;
    }

    private void SendAll(Guid municipalityId, int count) {
        var office = new CertificationOffice { Name = "Samtgemeinde Lachendorf" };
        Db.Insert(office);
        var shipment = new SignatureShipment { CampaignId = _campaign.Id, OfficeId = office.Id, SentOn = DateTime.UtcNow.Date, CreatedAt = DateTime.UtcNow };
        Db.Insert(shipment);
        Db.Insert(new SignatureShipmentLine { ShipmentId = shipment.Id, AdministrativeDivisionId = municipalityId, Count = count });
    }

    [Test]
    public async Task Added_intakes_are_listed_newest_first_and_count_in_the_totals() {
        using var client = await HolderAsync(_state);
        await AddAsync(client, _ahnsbeck.Id, 4);
        await AddAsync(client, _eldingen.Id, 2);

        var intakes = await client.GetFromJsonAsync<List<SignatureIntakeDTO>>($"/api/support-signatures/campaigns/{_campaign.Id}/intakes");
        var campaign = await client.GetFromJsonAsync<SignatureCampaignDTO>($"/api/support-signatures/campaigns/{_campaign.Id}");

        await Assert.That(intakes!.Select(i => i.MunicipalityName).ToList()).IsEquivalentTo(["Eldingen", "Ahnsbeck"]);
        await Assert.That(campaign!.Totals.Received).IsEqualTo(6);
        await Assert.That(campaign.Totals.Waiting).IsEqualTo(6);
        await Assert.That(campaign.Totals.Goal).IsEqualTo(2400);
    }

    [Test]
    public async Task Intake_needs_a_municipality() {
        using var client = await HolderAsync(_state);

        var response = await client.PostAsJsonAsync($"/api/support-signatures/campaigns/{_campaign.Id}/intakes", Intake(_county.Id, 3));

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
        await Assert.That(await response.Content.ReadAsStringAsync()).Contains(I18nKey.Error.SupportSignatures.Intake.NotAMunicipality);
    }

    [Test]
    public async Task Count_must_be_positive_and_note_short() {
        using var client = await HolderAsync(_state);
        var request = Intake(_ahnsbeck.Id, 0);
        request.Note = new string('x', 501);

        var response = await client.PostAsJsonAsync($"/api/support-signatures/campaigns/{_campaign.Id}/intakes", request);

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadAsStringAsync();
        await Assert.That(body).Contains(I18nKey.Error.SupportSignatures.Intake.CountInvalid);
        await Assert.That(body).Contains(I18nKey.Error.SupportSignatures.Intake.NoteTooLong);
    }

    [Test]
    public async Task Update_or_delete_refused_when_forms_were_sent() {
        using var client = await HolderAsync(_state);
        var intake = await AddAsync(client, _ahnsbeck.Id, 5);
        SendAll(_ahnsbeck.Id, 5);

        var lower = await client.PutAsJsonAsync($"/api/support-signatures/intakes/{intake.Id}", Intake(_ahnsbeck.Id, 3));
        var moved = await client.PutAsJsonAsync($"/api/support-signatures/intakes/{intake.Id}", Intake(_eldingen.Id, 5));
        var delete = await client.DeleteAsync($"/api/support-signatures/intakes/{intake.Id}");
        var higher = await client.PutAsJsonAsync($"/api/support-signatures/intakes/{intake.Id}", Intake(_ahnsbeck.Id, 7));

        await Assert.That(lower.StatusCode).IsEqualTo(HttpStatusCode.Conflict);
        await Assert.That(await lower.Content.ReadAsStringAsync()).Contains(I18nKey.Error.SupportSignatures.Intake.AlreadySent);
        await Assert.That(moved.StatusCode).IsEqualTo(HttpStatusCode.Conflict);
        await Assert.That(delete.StatusCode).IsEqualTo(HttpStatusCode.Conflict);
        await Assert.That(higher.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }

    [Test]
    public async Task Waiting_intake_can_be_corrected_and_deleted() {
        using var client = await HolderAsync(_state);
        var intake = await AddAsync(client, _ahnsbeck.Id, 5);

        var update = await client.PutAsJsonAsync($"/api/support-signatures/intakes/{intake.Id}", Intake(_eldingen.Id, 2));
        var delete = await client.DeleteAsync($"/api/support-signatures/intakes/{intake.Id}");

        await Assert.That(update.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(delete.StatusCode).IsEqualTo(HttpStatusCode.NoContent);
        await Assert.That(Db.SignatureIntakes.Count()).IsEqualTo(0);
    }

    [Test]
    public async Task Overview_groups_by_landkreis() {
        using var client = await HolderAsync(_state);
        await AddAsync(client, _ahnsbeck.Id, 4);
        await AddAsync(client, _eldingen.Id, 2);

        var overview = await client.GetFromJsonAsync<SignatureOverviewDTO>($"/api/support-signatures/campaigns/{_campaign.Id}/overview");

        var county = overview!.Regions.Single();
        await Assert.That(county.Name).IsEqualTo("Landkreis Celle");
        await Assert.That(county.Received).IsEqualTo(6);
        await Assert.That(county.Children.Select(c => c.Name).ToList()).IsEquivalentTo(["Ahnsbeck", "Eldingen"]);
        await Assert.That(overview.Totals.Received).IsEqualTo(6);
        await Assert.That(overview.DaysLeft).IsGreaterThanOrEqualTo(29);
    }

    [Test]
    public async Task Intakes_of_a_foreign_chapter_campaign_are_forbidden() {
        var other = Builder.SeedChapter("Bayern");
        using var client = await HolderAsync(other);

        var list = await client.GetAsync($"/api/support-signatures/campaigns/{_campaign.Id}/intakes");
        var add = await client.PostAsJsonAsync($"/api/support-signatures/campaigns/{_campaign.Id}/intakes", Intake(_ahnsbeck.Id, 1));
        var overview = await client.GetAsync($"/api/support-signatures/campaigns/{_campaign.Id}/overview");

        await Assert.That(list.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
        await Assert.That(add.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
        await Assert.That(overview.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
    }
}
