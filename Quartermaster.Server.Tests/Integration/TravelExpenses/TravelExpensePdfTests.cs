using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using LinqToDB;
using Microsoft.Extensions.DependencyInjection;
using QuestPDF.Fluent;
using Quartermaster.Api.Receipts;
using Quartermaster.Api.TravelExpenses;
using Quartermaster.Data.Receipts;
using Quartermaster.Data.TravelExpenses;
using Quartermaster.Server.Receipts;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.Integration.TravelExpenses;

public class TravelExpensePdfTests : IntegrationTestBase {
    private static readonly byte[] OnePixelPng = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg==");

    private static TravelExpenseInputDTO Input(Guid? chapterId = null, params Guid[] receiptIds) {
        return new TravelExpenseInputDTO {
            FirstName = "Anna",
            LastName = "Pirat",
            Purpose = "Landesparteitag",
            TravelChapterId = chapterId,
            Start = new TripPointDTO { Place = "Hannover", At = new DateTime(2026, 10, 1, 7, 0, 0) },
            End = new TripPointDTO { Place = "Hannover", At = new DateTime(2026, 10, 2, 19, 0, 0) },
            Modules = [
                new TripModuleDTO { Kind = TripModuleKind.VehicleLeg, Vehicle = TripVehicle.Car, Km = 120, From = "Hannover", To = "Braunschweig", At = new DateTime(2026, 10, 1, 7, 0, 0), ReceiptIds = receiptIds.ToList() },
                new TripModuleDTO { Kind = TripModuleKind.HotelStay, Amount = 80, At = new DateTime(2026, 10, 1, 18, 0, 0), Until = new DateTime(2026, 10, 2, 9, 0, 0) },
                new TripModuleDTO { Kind = TripModuleKind.ProvidedMeal, Meal = TripMeal.Dinner, At = new DateTime(2026, 10, 1) },
                new TripModuleDTO { Kind = TripModuleKind.OtherCost, Description = "Parken & Maut", Amount = 12 }
            ],
            Iban = "DE89370400440532013000"
        };
    }

    private static int PageCount(byte[] pdf) {
        return Regex.Matches(Encoding.Latin1.GetString(pdf), @"/Type\s*/Page(?![s\w])").Count;
    }

    private async Task<(HttpClient Client, Guid UserId)> ClientAsync() {
        var (user, token) = Builder.SeedAuthenticatedUser();
        return (await AuthenticatedClientWithCsrfAsync(token), user.Id);
    }

    private static async Task<UserReceiptDTO> UploadAsync(HttpClient client, byte[] bytes, string name) {
        using var content = new MultipartFormDataContent();
        content.Add(new ByteArrayContent(bytes), "File", name);
        var response = await client.PostAsync("/api/receipts", content);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<UserReceiptDTO>())!;
    }

    private void DeleteReceiptFiles(Guid userId) {
        var storage = Factory.Services.GetRequiredService<ReceiptStorage>();
        foreach (var receipt in Db.UserReceipts.Where(r => r.OwnerUserId == userId).ToList())
            File.Delete(storage.PathOf(userId, receipt.StoredName));
    }

    [Test]
    public async Task Anonymous_render_has_form_rules_and_appendix_pages() {
        using var client = AnonymousClient();
        await AttachAntiforgeryTokenAsync(client);

        var response = await client.PostAsJsonAsync("/api/forms/travel-expense/render", Input());

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(response.Content.Headers.ContentType!.MediaType).IsEqualTo("application/pdf");
        var pdf = await response.Content.ReadAsByteArrayAsync();
        await Assert.That(Encoding.ASCII.GetString(pdf, 0, 5)).IsEqualTo("%PDF-");
        await Assert.That(PageCount(pdf)).IsEqualTo(3);
    }

    [Test]
    public async Task Rules_without_text_leave_out_the_rules_page() {
        var chapter = Builder.SeedChapter("BW");
        var ruleSet = new TravelExpenseRuleSet { ChapterId = chapter.Id, UpdatedAt = DateTime.UtcNow };
        ruleSet.Apply(TravelExpenseRulePresets.BadenWuerttemberg());
        Db.Insert(ruleSet);
        using var client = AnonymousClient();
        await AttachAntiforgeryTokenAsync(client);

        var response = await client.PostAsJsonAsync("/api/forms/travel-expense/render", Input(chapter.Id));

        await Assert.That(PageCount(await response.Content.ReadAsByteArrayAsync())).IsEqualTo(2);
    }

    [Test]
    public async Task Submit_attaches_receipts_and_freezes_the_draft() {
        var (client, userId) = await ClientAsync();
        try {
            var receiptPdf = QuestPDF.Fluent.Document.Create(c => {
                c.Page(p => p.Content().Text("Hotelrechnung Seite 1"));
                c.Page(p => p.Content().Text("Hotelrechnung Seite 2"));
            }).GeneratePdf();
            var image = await UploadAsync(client, OnePixelPng, "route.png");
            var pdf = await UploadAsync(client, receiptPdf, "hotel.pdf");
            var broken = await UploadAsync(client, "%PDF-1.4\nnot really a pdf"u8.ToArray(), "broken.pdf");

            var created = await client.PostAsJsonAsync("/api/forms/travel-expense", Input(null, image.Id, pdf.Id, broken.Id));
            var form = (await created.Content.ReadFromJsonAsync<TravelExpenseResponseDTO>())!;

            var response = await client.PostAsJsonAsync($"/api/forms/travel-expense/{form.Id}/submit", new { });
            await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
            var bytes = await response.Content.ReadAsByteArrayAsync();
            await Assert.That(PageCount(bytes)).IsEqualTo(3 + 1 + 2 + 1);

            await Assert.That(Db.TravelExpenseReimbursements.Single(f => f.Id == form.Id).Status).IsEqualTo(TravelExpenseStatus.Submitted);
            var again = await client.PostAsJsonAsync($"/api/forms/travel-expense/{form.Id}/submit", new { });
            await Assert.That(again.StatusCode).IsEqualTo(HttpStatusCode.Conflict);

            var download = await client.GetAsync($"/api/forms/travel-expense/{form.Id}/pdf");
            await Assert.That(download.StatusCode).IsEqualTo(HttpStatusCode.OK);
        } finally {
            DeleteReceiptFiles(userId);
            client.Dispose();
        }
    }

    [Test]
    public async Task Receipts_of_other_users_are_never_attached() {
        var (owner, ownerId) = await ClientAsync();
        var (other, _) = await ClientAsync();
        try {
            var foreign = await UploadAsync(owner, OnePixelPng, "route.png");
            var created = await other.PostAsJsonAsync("/api/forms/travel-expense", Input(null, foreign.Id));
            var form = (await created.Content.ReadFromJsonAsync<TravelExpenseResponseDTO>())!;

            var bytes = await other.GetByteArrayAsync($"/api/forms/travel-expense/{form.Id}/pdf");

            await Assert.That(PageCount(bytes)).IsEqualTo(3);
            var forbidden = await owner.GetAsync($"/api/forms/travel-expense/{form.Id}/pdf");
            await Assert.That(forbidden.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
        } finally {
            DeleteReceiptFiles(ownerId);
            owner.Dispose();
            other.Dispose();
        }
    }

    [Test]
    public async Task Anonymous_render_never_attaches_stored_receipts() {
        var (owner, ownerId) = await ClientAsync();
        try {
            var stored = await UploadAsync(owner, OnePixelPng, "route.png");
            using var client = AnonymousClient();
            await AttachAntiforgeryTokenAsync(client);

            var response = await client.PostAsJsonAsync("/api/forms/travel-expense/render", Input(null, stored.Id));

            await Assert.That(PageCount(await response.Content.ReadAsByteArrayAsync())).IsEqualTo(3);
        } finally {
            DeleteReceiptFiles(ownerId);
            owner.Dispose();
        }
    }
}
