using System.Net;
using System.Threading.Tasks;

namespace Quartermaster.Server.Tests.Infrastructure;

public class IntegrationTestFactoryTests : IntegrationTestBase {
    [Test]
    public async Task ClientConfig_endpoint_responds_200_to_anonymous_request() {
        using var client = AnonymousClient();
        var response = await client.GetAsync("/api/config/client");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }

    [Test]
    public async Task Authenticated_request_returns_200_with_valid_bearer_token() {
        var (_, token) = Builder.SeedAuthenticatedUser();
        using var client = AuthenticatedClient(token);
        var response = await client.GetAsync("/api/users/session");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }

    [Test]
    public async Task Anonymous_request_to_protected_endpoint_returns_401() {
        using var client = AnonymousClient();
        var response = await client.GetAsync("/api/users");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Antiforgery_token_endpoint_issues_token() {
        using var client = AnonymousClient();
        var response = await client.GetAsync("/api/antiforgery/token");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }

    [Test]
    public async Task POST_without_csrf_token_returns_403() {
        var (_, token) = Builder.SeedAuthenticatedUser();
        using var client = AuthenticatedClient(token);
        // Deliberately do not call AttachAntiforgeryTokenAsync
        var response = await client.PostAsync("/api/roles", null);
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
    }
}
