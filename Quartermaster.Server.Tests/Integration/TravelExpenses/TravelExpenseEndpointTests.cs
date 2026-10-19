using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using LinqToDB;
using Quartermaster.Api.TravelExpenses;
using Quartermaster.Data.TravelExpenses;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.Integration.TravelExpenses;

public class TravelExpenseEndpointTests : IntegrationTestBase {
    private static TravelExpenseInputDTO Input(decimal km = 100) {
        return new TravelExpenseInputDTO {
            FirstName = "Anna",
            LastName = "Pirat",
            Purpose = "Landesparteitag",
            Start = new TripPointDTO { Place = "Hannover", At = new DateTime(2026, 10, 1, 7, 0, 0) },
            End = new TripPointDTO { Place = "Hannover", At = new DateTime(2026, 10, 1, 19, 0, 0) },
            Modules = [new TripModuleDTO { Kind = TripModuleKind.VehicleLeg, Vehicle = TripVehicle.Car, Km = km, At = new DateTime(2026, 10, 1, 7, 0, 0) }]
        };
    }

    private async Task<HttpClient> ClientAsync() {
        var (_, token) = Builder.SeedAuthenticatedUser();
        return await AuthenticatedClientWithCsrfAsync(token);
    }

    private static async Task<TravelExpenseResponseDTO> CreateAsync(HttpClient client, TravelExpenseInputDTO input) {
        var response = await client.PostAsJsonAsync("/api/forms/travel-expense", input);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<TravelExpenseResponseDTO>())!;
    }

    [Test]
    public async Task Returns_401_when_anonymous() {
        using var client = AnonymousClient();
        await AttachAntiforgeryTokenAsync(client);
        var response = await client.PostAsJsonAsync("/api/forms/travel-expense", Input());
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Create_stores_the_input_and_the_calculated_total() {
        using var client = await ClientAsync();

        var created = await CreateAsync(client, Input());

        await Assert.That(created.Status).IsEqualTo(TravelExpenseStatusDTO.Draft);
        await Assert.That(created.Input.Modules.Single().Km).IsEqualTo(100m);
        var row = Db.TravelExpenseReimbursements.Single(f => f.Id == created.Id);
        await Assert.That(row.Total).IsEqualTo(44m);
        await Assert.That(row.Purpose).IsEqualTo("Landesparteitag");
    }

    [Test]
    public async Task Create_prefills_an_empty_form_from_the_member_record() {
        var chapter = Builder.SeedChapter("Niedersachsen");
        var (user, token) = Builder.SeedAuthenticatedUser(firstName: "Konto", lastName: "Name");
        Builder.SeedMember(chapterId: chapter.Id, memberNumber: 4711, firstName: "Anna", lastName: "Pirat", email: "anna@example.org", userId: user.Id);
        using var client = await AuthenticatedClientWithCsrfAsync(token);

        var created = await CreateAsync(client, new TravelExpenseInputDTO());

        await Assert.That(created.Input.FirstName).IsEqualTo("Anna");
        await Assert.That(created.Input.MemberNumber).IsEqualTo(4711);
        await Assert.That(created.Input.Email).IsEqualTo("anna@example.org");
        await Assert.That(created.Input.MembershipChapterName).IsEqualTo("Niedersachsen");
    }

    [Test]
    public async Task Create_keeps_given_person_data() {
        var (user, token) = Builder.SeedAuthenticatedUser();
        Builder.SeedMember(firstName: "Other", userId: user.Id);
        using var client = await AuthenticatedClientWithCsrfAsync(token);

        var created = await CreateAsync(client, Input());

        await Assert.That(created.Input.FirstName).IsEqualTo("Anna");
    }

    [Test]
    public async Task Update_replaces_the_input_of_an_own_draft() {
        using var client = await ClientAsync();
        var created = await CreateAsync(client, Input());
        var changed = Input(km: 200);

        var response = await client.PutAsJsonAsync($"/api/forms/travel-expense/{created.Id}", changed);

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(Db.TravelExpenseReimbursements.Single(f => f.Id == created.Id).Total).IsEqualTo(74m);
    }

    [Test]
    public async Task Submitted_forms_cannot_be_changed() {
        using var client = await ClientAsync();
        var created = await CreateAsync(client, Input());
        Db.TravelExpenseReimbursements
            .Where(f => f.Id == created.Id)
            .Set(f => f.Status, TravelExpenseStatus.Submitted)
            .Update();

        var response = await client.PutAsJsonAsync($"/api/forms/travel-expense/{created.Id}", Input(km: 1));

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Conflict);
    }

    [Test]
    public async Task Other_users_cannot_read_download_change_copy_submit_or_delete() {
        using var owner = await ClientAsync();
        using var other = await ClientAsync();
        var created = await CreateAsync(owner, Input());

        var get = await other.GetAsync($"/api/forms/travel-expense/{created.Id}");
        var put = await other.PutAsJsonAsync($"/api/forms/travel-expense/{created.Id}", Input());
        var copy = await other.PostAsJsonAsync($"/api/forms/travel-expense/{created.Id}/copy", new { });
        var pdf = await other.GetAsync($"/api/forms/travel-expense/{created.Id}/pdf");
        var submit = await other.PostAsJsonAsync($"/api/forms/travel-expense/{created.Id}/submit", new { });
        var delete = await other.DeleteAsync($"/api/forms/travel-expense/{created.Id}");
        var list = await other.GetFromJsonAsync<List<TravelExpenseListItemDTO>>("/api/forms/travel-expense");

        await Assert.That(get.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
        await Assert.That(put.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
        await Assert.That(copy.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
        await Assert.That(pdf.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
        await Assert.That(submit.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
        await Assert.That(delete.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
        await Assert.That(list).IsEmpty();
    }

    [Test]
    public async Task Copy_creates_a_new_draft_with_new_module_ids() {
        using var client = await ClientAsync();
        var created = await CreateAsync(client, Input());

        var response = await client.PostAsJsonAsync($"/api/forms/travel-expense/{created.Id}/copy", new { });
        var copy = (await response.Content.ReadFromJsonAsync<TravelExpenseResponseDTO>())!;

        await Assert.That(copy.Id).IsNotEqualTo(created.Id);
        await Assert.That(copy.Status).IsEqualTo(TravelExpenseStatusDTO.Draft);
        await Assert.That(copy.Input.Modules.Single().Id).IsNotEqualTo(created.Input.Modules.Single().Id);
        var list = await client.GetFromJsonAsync<List<TravelExpenseListItemDTO>>("/api/forms/travel-expense");
        await Assert.That(list!.Count).IsEqualTo(2);
    }

    [Test]
    public async Task Delete_removes_the_form() {
        using var client = await ClientAsync();
        var created = await CreateAsync(client, Input());

        var response = await client.DeleteAsync($"/api/forms/travel-expense/{created.Id}");

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(Db.TravelExpenseReimbursements.Any(f => f.Id == created.Id)).IsFalse();
    }

    [Test]
    public async Task Too_many_modules_are_refused() {
        using var client = await ClientAsync();
        var input = Input();
        input.Modules = Enumerable.Range(0, 201).Select(_ => new TripModuleDTO { Kind = TripModuleKind.OtherCost, Amount = 1 }).ToList();

        var response = await client.PostAsJsonAsync("/api/forms/travel-expense", input);

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task Rules_are_public_and_follow_the_chapter() {
        var chapter = Builder.SeedChapter("Bayern");
        var ruleSet = new TravelExpenseRuleSet { ChapterId = chapter.Id, UpdatedAt = DateTime.UtcNow };
        ruleSet.Apply(TravelExpenseRulePresets.Bavaria());
        Db.Insert(ruleSet);
        using var client = AnonymousClient();

        var federal = await client.GetFromJsonAsync<TravelExpenseRulesDTO>("/api/forms/travel-expense/rules");
        var bavaria = await client.GetFromJsonAsync<TravelExpenseRulesDTO>($"/api/forms/travel-expense/rules?chapterId={chapter.Id}&date=2026-10-01");

        await Assert.That(federal!.VersionLabel).IsEqualTo("J2.0");
        await Assert.That(bavaria!.VersionLabel).IsEqualTo("2.0-LV-BY");
        await Assert.That(bavaria.MotorcycleRates.Single().RatePerKm).IsEqualTo(0.13m);
    }
}
