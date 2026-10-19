using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Quartermaster.Api;
using Quartermaster.Api.I18n;
using Quartermaster.Api.Receipts;
using Quartermaster.Data.Options;
using Quartermaster.Data.Receipts;
using Quartermaster.Server.Receipts;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.Integration.Receipts;

public class ReceiptEndpointTests : IntegrationTestBase {
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0];
    private static readonly byte[] Pdf = "%PDF-1.4\n%fake\n"u8.ToArray();

    private static MultipartFormDataContent Upload(byte[] bytes, string fileName, string? label = null) {
        var content = new MultipartFormDataContent();
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        content.Add(file, "File", fileName);
        if (label != null)
            content.Add(new StringContent(label), "Label");
        return content;
    }

    private async Task<HttpClient> ClientAsync(params string[] globalPermissions) {
        var (_, token) = Builder.SeedAuthenticatedUser(globalPermissions: globalPermissions);
        return await AuthenticatedClientWithCsrfAsync(token);
    }

    private void SetOption(string key, string value) {
        using var scope = Factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<OptionRepository>().SetValue(key, null, value);
    }

    private string StoredPath(UserReceiptDTO dto) {
        var receipt = Db.UserReceipts.Single(r => r.Id == dto.Id);
        return Factory.Services.GetRequiredService<ReceiptStorage>().PathOf(receipt.OwnerUserId, receipt.StoredName);
    }

    [Test]
    public async Task Returns_401_when_anonymous() {
        using var client = AnonymousClient();
        await AttachAntiforgeryTokenAsync(client);
        var response = await client.PostAsync("/api/receipts", Upload(Png, "a.png"));
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Anonymous_users_cannot_list_or_download_receipts() {
        using var owner = await ClientAsync();
        var dto = (await (await owner.PostAsync("/api/receipts", Upload(Png, "a.png"))).Content.ReadFromJsonAsync<UserReceiptDTO>())!;
        try {
            using var client = AnonymousClient();

            var list = await client.GetAsync("/api/receipts");
            var file = await client.GetAsync($"/api/receipts/{dto.Id}/file");

            await Assert.That(list.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
            await Assert.That(file.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
        } finally {
            File.Delete(StoredPath(dto));
        }
    }

    [Test]
    public async Task Upload_stores_the_file_and_lists_it() {
        using var client = await ClientAsync();

        var response = await client.PostAsync("/api/receipts", Upload(Pdf, "hotel.pdf", "Hotel Hannover"));
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var dto = (await response.Content.ReadFromJsonAsync<UserReceiptDTO>())!;
        try {
            await Assert.That(dto.ContentType).IsEqualTo("application/pdf");
            await Assert.That(dto.Label).IsEqualTo("Hotel Hannover");
            await Assert.That(File.Exists(StoredPath(dto))).IsTrue();

            var list = await client.GetFromJsonAsync<UserReceiptListDTO>("/api/receipts");
            await Assert.That(list!.Items.Single().Id).IsEqualTo(dto.Id);
            await Assert.That(list.MaxCount).IsEqualTo(100);

            var file = await client.GetByteArrayAsync($"/api/receipts/{dto.Id}/file");
            await Assert.That(file).IsEquivalentTo(Pdf);
        } finally {
            File.Delete(StoredPath(dto));
        }
    }

    [Test]
    public async Task Files_are_recognised_by_content_not_name() {
        using var client = await ClientAsync();

        var response = await client.PostAsync("/api/receipts", Upload("hello"u8.ToArray(), "fake.png"));

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
        await Assert.That(await response.Content.ReadAsStringAsync()).Contains(I18nKey.Error.Receipt.UnsupportedType);
    }

    [Test]
    public async Task The_count_limit_applies_unless_the_user_may_upload_without_limit() {
        SetOption(ReceiptOptions.MaxPerUser, "1");
        using var limited = await ClientAsync();
        using var unlimited = await ClientAsync(PermissionIdentifier.UnlimitedReceipts);

        var first = await limited.PostAsync("/api/receipts", Upload(Png, "a.png"));
        var second = await limited.PostAsync("/api/receipts", Upload(Png, "b.png"));
        var free1 = await unlimited.PostAsync("/api/receipts", Upload(Png, "c.png"));
        var free2 = await unlimited.PostAsync("/api/receipts", Upload(Png, "d.png"));

        try {
            await Assert.That(first.StatusCode).IsEqualTo(HttpStatusCode.OK);
            await Assert.That(second.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
            await Assert.That(await second.Content.ReadAsStringAsync()).Contains(I18nKey.Error.Receipt.LimitReached);
            await Assert.That(free2.StatusCode).IsEqualTo(HttpStatusCode.OK);
            var list = await unlimited.GetFromJsonAsync<UserReceiptListDTO>("/api/receipts");
            await Assert.That(list!.MaxCount).IsNull();
        } finally {
            foreach (var response in new[] { first, free1, free2 }.Where(r => r.IsSuccessStatusCode))
                File.Delete(StoredPath((await response.Content.ReadFromJsonAsync<UserReceiptDTO>())!));
        }
    }

    [Test]
    public async Task Files_above_the_size_limit_are_refused() {
        SetOption(ReceiptOptions.MaxFileMegabytes, "1");
        using var client = await ClientAsync();
        var big = new byte[1024 * 1024 + 1];
        Pdf.CopyTo(big, 0);

        var response = await client.PostAsync("/api/receipts", Upload(big, "big.pdf"));

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
        await Assert.That(await response.Content.ReadAsStringAsync()).Contains(I18nKey.Error.Receipt.TooLarge);
    }

    [Test]
    public async Task Other_users_cannot_see_rename_or_delete_a_receipt() {
        using var owner = await ClientAsync();
        using var other = await ClientAsync();
        var dto = (await (await owner.PostAsync("/api/receipts", Upload(Png, "a.png"))).Content.ReadFromJsonAsync<UserReceiptDTO>())!;
        try {
            var file = await other.GetAsync($"/api/receipts/{dto.Id}/file");
            var rename = await other.PutAsJsonAsync($"/api/receipts/{dto.Id}", new UserReceiptRenameRequest { Id = dto.Id, Label = "x" });
            var delete = await other.DeleteAsync($"/api/receipts/{dto.Id}");

            await Assert.That(file.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
            await Assert.That(rename.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
            await Assert.That(delete.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
            await Assert.That((await other.GetFromJsonAsync<UserReceiptListDTO>("/api/receipts"))!.Items).IsEmpty();
        } finally {
            File.Delete(StoredPath(dto));
        }
    }

    [Test]
    public async Task Delete_removes_row_and_file() {
        using var client = await ClientAsync();
        var dto = (await (await client.PostAsync("/api/receipts", Upload(Png, "route.png"))).Content.ReadFromJsonAsync<UserReceiptDTO>())!;
        var path = StoredPath(dto);

        var response = await client.DeleteAsync($"/api/receipts/{dto.Id}");

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(File.Exists(path)).IsFalse();
        await Assert.That(Db.UserReceipts.Any(r => r.Id == dto.Id)).IsFalse();
    }

    [Test]
    public async Task Rename_changes_the_label() {
        using var client = await ClientAsync();
        var dto = (await (await client.PostAsync("/api/receipts", Upload(Png, "route.png"))).Content.ReadFromJsonAsync<UserReceiptDTO>())!;
        try {
            var response = await client.PutAsJsonAsync($"/api/receipts/{dto.Id}", new UserReceiptRenameRequest { Id = dto.Id, Label = "  Strecke Hannover - Berlin  " });

            await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
            await Assert.That(Db.UserReceipts.Single(r => r.Id == dto.Id).Label).IsEqualTo("Strecke Hannover - Berlin");
        } finally {
            File.Delete(StoredPath(dto));
        }
    }
}
