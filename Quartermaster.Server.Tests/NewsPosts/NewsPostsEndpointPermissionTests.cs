using System;
using System.Net;
using System.Net.Http;
using System.Text;
using Quartermaster.Api;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.NewsPosts;

public class NewsPostsEndpointPermissionTests : IntegrationTestBase {
    [Test]
    [Arguments("GET", "/api/newsposts/items?view=suggestions")]
    [Arguments("GET", "/api/newsposts/publications")]
    [Arguments("GET", "/api/newsposts/channels")]
    [Arguments("POST", "/api/newsposts/items/{id}/publish")]
    [Arguments("POST", "/api/newsposts/items/{id}/generate")]
    [Arguments("POST", "/api/newsposts/items/{id}/dismiss")]
    [Arguments("POST", "/api/newsposts/items/{id}/retry")]
    public async Task Review_endpoints_forbid_users_without_review_permission(string method, string url) {
        var (_, token) = Builder.SeedAuthenticatedUser(globalPermissions: [PermissionIdentifier.ManageNewsPosts]);
        using var client = await AuthenticatedClientWithCsrfAsync(token);

        var response = await SendAsync(client, method, url);

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
    }

    [Test]
    [Arguments("GET", "/api/newsposts/feeds")]
    [Arguments("PUT", "/api/newsposts/feeds")]
    [Arguments("DELETE", "/api/newsposts/feeds/{id}")]
    [Arguments("GET", "/api/newsposts/sources")]
    [Arguments("POST", "/api/newsposts/sources")]
    [Arguments("DELETE", "/api/newsposts/sources/{id}")]
    [Arguments("POST", "/api/newsposts/sources/sync")]
    [Arguments("GET", "/api/newsposts/status")]
    public async Task Settings_endpoints_forbid_reviewers_without_manage_permission(string method, string url) {
        var (_, token) = Builder.SeedAuthenticatedUser(globalPermissions: [PermissionIdentifier.ReviewNewsPosts]);
        using var client = await AuthenticatedClientWithCsrfAsync(token);

        var response = await SendAsync(client, method, url);

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
    }

    [Test]
    [Arguments("GET", "/api/newsposts/items?view=all")]
    [Arguments("GET", "/api/newsposts/publications")]
    [Arguments("GET", "/api/newsposts/channels")]
    [Arguments("POST", "/api/newsposts/items/{id}/publish")]
    [Arguments("POST", "/api/newsposts/items/{id}/generate")]
    [Arguments("POST", "/api/newsposts/items/{id}/dismiss")]
    [Arguments("POST", "/api/newsposts/items/{id}/retry")]
    [Arguments("GET", "/api/newsposts/feeds")]
    [Arguments("PUT", "/api/newsposts/feeds")]
    [Arguments("DELETE", "/api/newsposts/feeds/{id}")]
    [Arguments("GET", "/api/newsposts/sources")]
    [Arguments("POST", "/api/newsposts/sources")]
    [Arguments("DELETE", "/api/newsposts/sources/{id}")]
    [Arguments("POST", "/api/newsposts/sources/sync")]
    [Arguments("GET", "/api/newsposts/status")]
    public async Task Anonymous_gets_401(string method, string url) {
        using var client = AnonymousClient();
        await AttachAntiforgeryTokenAsync(client);

        var response = await SendAsync(client, method, url);

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Reviewer_sees_suggestions_list() {
        var (_, token) = Builder.SeedAuthenticatedUser(globalPermissions: [PermissionIdentifier.ReviewNewsPosts]);
        using var client = AuthenticatedClient(token);

        var response = await client.GetAsync("/api/newsposts/items?view=suggestions");

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }

    private static Task<HttpResponseMessage> SendAsync(HttpClient client, string method, string url) {
        var request = new HttpRequestMessage(new HttpMethod(method), url.Replace("{id}", Guid.NewGuid().ToString()));
        if (method == "POST" || method == "PUT")
            request.Content = new StringContent("{}", Encoding.UTF8, "application/json");
        return client.SendAsync(request);
    }
}
