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
using Quartermaster.Api.SupportSignatures;
using Quartermaster.Data.AdministrativeDivisions;
using Quartermaster.Data.Chapters;
using Quartermaster.Data.SupportSignatures;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.Integration.SupportSignatures;

public class ShipmentEndpointTests : IntegrationTestBase {
    private Chapter _state = default!;
    private AdministrativeDivision _ahnsbeck = default!;
    private AdministrativeDivision _unassigned = default!;
    private CertificationOffice _office = default!;
    private SignatureCampaign _campaign = default!;

    [Before(Test)]
    public void Setup() {
        _state = Builder.SeedChapter("Niedersachsen");
        _ahnsbeck = Builder.SeedAdminDivision("Ahnsbeck", depth: 7);
        _unassigned = Builder.SeedAdminDivision("Eldingen", depth: 7);
        _office = new CertificationOffice { Name = "Samtgemeinde Lachendorf", Street = "Oppershäuser Str. 1", PostCode = "29331", City = "Lachendorf" };
        new CertificationOfficeRepository(Db).Create(_office, [_ahnsbeck.Id]);
        _campaign = new SignatureCampaign { ChapterId = _state.Id, Name = "Landesliste", Target = 2000, Deadline = DateTime.UtcNow.AddDays(30), OverdueDays = 21, CreatedAt = DateTime.UtcNow };
        Db.Insert(_campaign);
        Db.Insert(new SignatureIntake { CampaignId = _campaign.Id, AdministrativeDivisionId = _ahnsbeck.Id, Count = 7, ReceivedOn = DateTime.UtcNow.Date, CreatedAt = DateTime.UtcNow });
        Db.Insert(new SignatureIntake { CampaignId = _campaign.Id, AdministrativeDivisionId = _unassigned.Id, Count = 2, ReceivedOn = DateTime.UtcNow.Date, CreatedAt = DateTime.UtcNow });
    }

    private async Task<HttpClient> HolderAsync(Chapter chapter) {
        var (_, token) = Builder.SeedAuthenticatedUser(chapterPermissions: new Dictionary<Guid, string[]> {
            [chapter.Id] = [PermissionIdentifier.ManageSupportSignatures]
        });
        return await AuthenticatedClientWithCsrfAsync(token);
    }

    [Test]
    public async Task Plan_lists_offices_and_unassigned_municipalities() {
        using var client = await HolderAsync(_state);

        var plan = await client.GetFromJsonAsync<ShipmentPlanDTO>($"/api/support-signatures/campaigns/{_campaign.Id}/shipment-plan");

        await Assert.That(plan!.Offices.Single().OfficeName).IsEqualTo("Samtgemeinde Lachendorf");
        await Assert.That(plan.Offices.Single().Lines.Single().Count).IsEqualTo(7);
        await Assert.That(plan.Unassigned.Single().MunicipalityName).IsEqualTo("Eldingen");
    }

    [Test]
    public async Task Prepare_returns_the_new_ids_and_list_shows_them_unconfirmed() {
        using var client = await HolderAsync(_state);

        var prepare = await client.PostAsJsonAsync($"/api/support-signatures/campaigns/{_campaign.Id}/shipments/prepare",
            new ShipmentPrepareRequest { OfficeIds = [_office.Id] });
        var prepared = await prepare.Content.ReadFromJsonAsync<ShipmentPrepareResponse>();
        var listed = await client.GetFromJsonAsync<List<SignatureShipmentDTO>>($"/api/support-signatures/campaigns/{_campaign.Id}/shipments");
        var confirm = await client.PostAsJsonAsync($"/api/support-signatures/shipments/{prepared!.ShipmentIds.Single()}/confirm",
            new ShipmentConfirmRequest { SentOn = DateOnly.FromDateTime(DateTime.UtcNow) });
        var returned = await client.PostAsJsonAsync($"/api/support-signatures/shipments/{prepared.ShipmentIds.Single()}/returns", new ReturnSaveRequest {
            ReturnedOn = DateOnly.FromDateTime(DateTime.UtcNow),
            Lines = [new ReturnLineInput { MunicipalityId = _ahnsbeck.Id, Certified = 6, Rejected = 1, RejectionReason = SignatureRejectionReason.NotResident }]
        });
        var campaign = await client.GetFromJsonAsync<SignatureCampaignDTO>($"/api/support-signatures/campaigns/{_campaign.Id}");

        await Assert.That(prepare.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(listed!.Single().SentOn).IsNull();
        await Assert.That(listed.Single().Lines.Single().Sent).IsEqualTo(7);
        await Assert.That(confirm.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(returned.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(campaign!.Totals.Certified).IsEqualTo(6);
        await Assert.That(campaign.Totals.Rejected).IsEqualTo(1);
        await Assert.That(campaign.Totals.Waiting).IsEqualTo(2);
    }

    [Test]
    public async Task Letters_endpoint_returns_one_pdf_for_the_prepared_shipments() {
        using var client = await HolderAsync(_state);
        var prepare = await client.PostAsJsonAsync($"/api/support-signatures/campaigns/{_campaign.Id}/shipments/prepare",
            new ShipmentPrepareRequest { OfficeIds = [_office.Id] });
        var id = (await prepare.Content.ReadFromJsonAsync<ShipmentPrepareResponse>())!.ShipmentIds.Single();

        var letters = await client.GetAsync($"/api/support-signatures/campaigns/{_campaign.Id}/shipments/letters?ids={id}");
        var none = await client.GetAsync($"/api/support-signatures/campaigns/{_campaign.Id}/shipments/letters?ids={Guid.NewGuid()}");

        await Assert.That(letters.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(letters.Content.Headers.ContentType!.MediaType).IsEqualTo("application/pdf");
        var bytes = await letters.Content.ReadAsByteArrayAsync();
        await Assert.That(Encoding.ASCII.GetString(bytes, 0, 5)).IsEqualTo("%PDF-");
        await Assert.That(none.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task Negative_return_numbers_are_refused() {
        using var client = await HolderAsync(_state);
        var prepare = await client.PostAsJsonAsync($"/api/support-signatures/campaigns/{_campaign.Id}/shipments/prepare",
            new ShipmentPrepareRequest { OfficeIds = [_office.Id] });
        var id = (await prepare.Content.ReadFromJsonAsync<ShipmentPrepareResponse>())!.ShipmentIds.Single();
        await client.PostAsJsonAsync($"/api/support-signatures/shipments/{id}/confirm", new ShipmentConfirmRequest { SentOn = DateOnly.FromDateTime(DateTime.UtcNow) });

        var response = await client.PostAsJsonAsync($"/api/support-signatures/shipments/{id}/returns", new ReturnSaveRequest {
            ReturnedOn = DateOnly.FromDateTime(DateTime.UtcNow),
            Lines = [new ReturnLineInput { MunicipalityId = _ahnsbeck.Id, Certified = -1 }]
        });

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task Shipments_of_a_foreign_chapter_campaign_are_forbidden() {
        var other = Builder.SeedChapter("Bayern");
        using var client = await HolderAsync(other);

        var plan = await client.GetAsync($"/api/support-signatures/campaigns/{_campaign.Id}/shipment-plan");
        var prepare = await client.PostAsJsonAsync($"/api/support-signatures/campaigns/{_campaign.Id}/shipments/prepare",
            new ShipmentPrepareRequest { OfficeIds = [_office.Id] });
        var list = await client.GetAsync($"/api/support-signatures/campaigns/{_campaign.Id}/shipments");

        await Assert.That(plan.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
        await Assert.That(prepare.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
        await Assert.That(list.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
        await Assert.That(Db.SignatureShipments.Count()).IsEqualTo(0);
    }

    [Test]
    public async Task Holder_updates_a_return_and_a_foreign_chapter_cannot() {
        using var client = await HolderAsync(_state);
        using var foreignClient = await HolderAsync(Builder.SeedChapter("Bayern"));
        var prepare = await client.PostAsJsonAsync($"/api/support-signatures/campaigns/{_campaign.Id}/shipments/prepare",
            new ShipmentPrepareRequest { OfficeIds = [_office.Id] });
        var id = (await prepare.Content.ReadFromJsonAsync<ShipmentPrepareResponse>())!.ShipmentIds.Single();
        await client.PostAsJsonAsync($"/api/support-signatures/shipments/{id}/confirm", new ShipmentConfirmRequest { SentOn = DateOnly.FromDateTime(DateTime.UtcNow) });
        await client.PostAsJsonAsync($"/api/support-signatures/shipments/{id}/returns", new ReturnSaveRequest {
            ReturnedOn = DateOnly.FromDateTime(DateTime.UtcNow),
            Lines = [new ReturnLineInput { MunicipalityId = _ahnsbeck.Id, Certified = 3 }]
        });
        var returnId = Db.SignatureReturns.Single().Id;
        var update = new ReturnSaveRequest {
            ReturnedOn = DateOnly.FromDateTime(DateTime.UtcNow),
            Lines = [
                new ReturnLineInput { MunicipalityId = _ahnsbeck.Id, Certified = 5 },
                new ReturnLineInput { MunicipalityId = _ahnsbeck.Id, Rejected = 1, RejectionReason = SignatureRejectionReason.SignedTwice },
                new ReturnLineInput { MunicipalityId = _ahnsbeck.Id, Rejected = 1, RejectionReason = SignatureRejectionReason.NotResident }
            ]
        };

        var foreign = await foreignClient.PutAsJsonAsync($"/api/support-signatures/returns/{returnId}", update);
        var own = await client.PutAsJsonAsync($"/api/support-signatures/returns/{returnId}", update);
        var campaign = await client.GetFromJsonAsync<SignatureCampaignDTO>($"/api/support-signatures/campaigns/{_campaign.Id}");

        await Assert.That(foreign.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
        await Assert.That(own.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(campaign!.Totals.Certified).IsEqualTo(5);
        await Assert.That(campaign.Totals.Rejected).IsEqualTo(2);
    }
}
