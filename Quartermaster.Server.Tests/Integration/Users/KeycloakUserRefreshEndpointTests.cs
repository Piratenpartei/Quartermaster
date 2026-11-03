using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Quartermaster.Api;
using Quartermaster.Api.Users;
using Quartermaster.Data.Options;
using Quartermaster.Server.Accreditation;
using Quartermaster.Server.Tests.Accreditation.Fakes;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.Integration.Users;

[NotInParallel("FakeKeycloakAdminClient")]
public class KeycloakUserRefreshEndpointTests : IntegrationTestBase {
    private WebApplicationFactory<Program> _host = default!;
    private string _token = "";

    [Before(Test)]
    public void Setup() {
        FakeKeycloakAdminClient.Reset();

        _host = Factory.WithWebHostBuilder(b => b.ConfigureServices(services => {
            services.RemoveAll<IKeycloakAdminClient>();
            services.AddScoped<IKeycloakAdminClient, FakeKeycloakAdminClient>();
        }));

        ConfigureKeycloak("https://sso.test");
        (_, _token) = Builder.SeedAuthenticatedUser(globalPermissions: [PermissionIdentifier.CreateUser]);
    }

    [After(Test)]
    public void TearDown() {
        _host.Dispose();
    }

    private void ConfigureKeycloak(string baseUrl) {
        using var scope = _host.Services.CreateScope();
        var optionRepo = scope.ServiceProvider.GetRequiredService<OptionRepository>();
        optionRepo.SetValue("keycloak.base_url", null, baseUrl);
        optionRepo.SetValue("keycloak.auth_realm", null, "master");
        optionRepo.SetValue("keycloak.realm", null, "users");
        optionRepo.SetValue("keycloak.client_id", null, "admin-api");
        optionRepo.SetValue("keycloak.client_secret", null, "s3cret");
    }

    private async Task<HttpClient> ClientAsync(string? token = null) {
        var client = _host.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        if (token != "")
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token ?? _token);
        await AttachAntiforgeryTokenAsync(client);
        return client;
    }

    private static Task<HttpResponseMessage> RefreshAsync(HttpClient client) {
        return client.PostAsync("/api/users/keycloak-refresh", null);
    }

    [Test]
    public async Task Anonymous_gets_401_and_never_reaches_keycloak() {
        FakeKeycloakAdminClient.FailNext = new KeycloakException(500, "Keycloak must not be called");
        using var client = await ClientAsync("");

        var response = await RefreshAsync(client);

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
        await Assert.That(FakeKeycloakAdminClient.FailNext).IsNotNull();
    }

    [Test]
    public async Task Without_the_user_permission_gets_403() {
        var (_, token) = Builder.SeedAuthenticatedUser();
        using var client = await ClientAsync(token);

        var response = await RefreshAsync(client);

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task Unconfigured_keycloak_gets_503() {
        ConfigureKeycloak("");
        using var client = await ClientAsync();

        var response = await RefreshAsync(client);

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.ServiceUnavailable);
    }

    [Test]
    public async Task Keycloak_failure_gets_502() {
        FakeKeycloakAdminClient.FailNext = new KeycloakException(500, "down");
        using var client = await ClientAsync();

        var response = await RefreshAsync(client);

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadGateway);
    }

    [Test]
    public async Task Refresh_stores_the_keycloak_id_and_reports_the_summary() {
        var user = Builder.SeedUser();
        var member = Builder.SeedMember(userId: user.Id);
        var id = Guid.NewGuid().ToString();
        FakeKeycloakAdminClient.AddUser(id, "anna.pirat", member.MemberNumber.ToString());
        FakeKeycloakAdminClient.AddUser(Guid.NewGuid().ToString(), "bernd.pirat", "987654321");
        using var client = await ClientAsync();

        var response = await RefreshAsync(client);

        var result = await response.Content.ReadFromJsonAsync<KeycloakUserRefreshDTO>();
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(result!.AccountsRead).IsEqualTo(2);
        await Assert.That(result.KeycloakIdsStored).IsEqualTo(1);
        await Assert.That(result.AccountsWithoutUser).IsEqualTo(1);
        await Assert.That(Db.Users.Single(u => u.Id == user.Id).KeycloakUserId).IsEqualTo(id);
    }
}
