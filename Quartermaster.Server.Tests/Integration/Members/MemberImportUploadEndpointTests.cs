using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Quartermaster.Api;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.Integration.Members;

public class MemberImportUploadEndpointTests : IntegrationTestBase {
    private static MultipartFormDataContent BuildMultipart(byte[] content, string fileName, string contentType = "text/csv") {
        var multipart = new MultipartFormDataContent();
        var streamContent = new StreamContent(new MemoryStream(content));
        streamContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);
        multipart.Add(streamContent, "File", fileName);
        return multipart;
    }

    [Test]
    public async Task Returns_401_when_anonymous() {
        using var client = AnonymousClient();
        await AttachAntiforgeryTokenAsync(client);
        var content = BuildMultipart(Encoding.UTF8.GetBytes("a;b;c\n1;2;3"), "test.csv");
        var response = await client.PostAsync("/api/members/import/upload", content);
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Returns_403_when_user_lacks_trigger_import_permission() {
        var (_, token) = Builder.SeedAuthenticatedUser();
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var content = BuildMultipart(Encoding.UTF8.GetBytes("a;b;c\n1;2;3"), "test.csv");
        var response = await client.PostAsync("/api/members/import/upload", content);
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task Returns_400_when_file_has_non_csv_extension() {
        var (_, token) = Builder.SeedAuthenticatedUser(
            globalPermissions: new[] { PermissionIdentifier.TriggerMemberImport });
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var content = BuildMultipart(Encoding.UTF8.GetBytes("not a csv"), "test.txt", "text/plain");
        var response = await client.PostAsync("/api/members/import/upload", content);
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task Returns_400_when_file_is_empty() {
        var (_, token) = Builder.SeedAuthenticatedUser(
            globalPermissions: new[] { PermissionIdentifier.TriggerMemberImport });
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var content = BuildMultipart(System.Array.Empty<byte>(), "empty.csv");
        var response = await client.PostAsync("/api/members/import/upload", content);
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task Returns_400_when_file_has_wrong_extension_even_if_content_type_is_csv() {
        var (_, token) = Builder.SeedAuthenticatedUser(
            globalPermissions: new[] { PermissionIdentifier.TriggerMemberImport });
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var content = BuildMultipart(Encoding.UTF8.GetBytes("a;b\n1;2"), "upload.xlsx");
        var response = await client.PostAsync("/api/members/import/upload", content);
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }
}
