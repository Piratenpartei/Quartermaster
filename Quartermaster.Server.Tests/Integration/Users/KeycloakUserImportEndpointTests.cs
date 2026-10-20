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
public class KeycloakUserImportEndpointTests : IntegrationTestBase {
    private WebApplicationFactory<Program> _host = default!;
    private string _token = "";

    [Before(Test)]
    public void Setup() {
        FakeKeycloakAdminClient.Reset();

        _host = Factory.WithWebHostBuilder(b => b.ConfigureServices(services => {
            services.RemoveAll<IKeycloakAdminClient>();
            services.AddScoped<IKeycloakAdminClient, FakeKeycloakAdminClient>();
        }));

        using (var scope = _host.Services.CreateScope()) {
            var optionRepo = scope.ServiceProvider.GetRequiredService<OptionRepository>();
            optionRepo.SetValue("keycloak.base_url", null, "https://sso.test");
            optionRepo.SetValue("keycloak.auth_realm", null, "master");
            optionRepo.SetValue("keycloak.realm", null, "users");
            optionRepo.SetValue("keycloak.client_id", null, "admin-api");
            optionRepo.SetValue("keycloak.client_secret", null, "s3cret");
        }
        (_, _token) = Builder.SeedAuthenticatedUser(globalPermissions: [PermissionIdentifier.CreateUser]);
    }

    [After(Test)]
    public void TearDown() {
        _host.Dispose();
    }

    private async Task<HttpClient> ClientAsync(string? token = null) {
        var client = _host.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token ?? _token);
        await AttachAntiforgeryTokenAsync(client);
        return client;
    }

    private static string AddAccount(int memberNumber, string? email = "anna@example.org", bool closed = false, string? department = null,
        string eligible = "false") {
        var id = Guid.NewGuid().ToString();
        FakeKeycloakAdminClient.AddUser(id, $"anna.pirat{memberNumber}", memberNumber.ToString(), eligible: eligible, closed: closed,
            firstName: "Anna", lastName: "Pirat", email: email, department: department);
        return id;
    }

    private static Task<HttpResponseMessage> ImportAsync(HttpClient client, string keycloakUserId) {
        return client.PostAsJsonAsync("/api/users/keycloak-accounts", new KeycloakUserImportRequest { KeycloakUserId = keycloakUserId });
    }

    [Test]
    public async Task Anonymous_search_and_import_get_401_and_never_reach_keycloak() {
        FakeKeycloakAdminClient.AddUser(Guid.NewGuid().ToString(), "anna.pirat", "4711", email: "anna@example.org");
        FakeKeycloakAdminClient.FailNext = new KeycloakException(500, "Keycloak must not be called");
        using var client = _host.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        await AttachAntiforgeryTokenAsync(client);

        var search = await client.GetAsync("/api/users/keycloak-accounts?query=anna");
        var import = await ImportAsync(client, Guid.NewGuid().ToString());

        await Assert.That(search.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
        await Assert.That(import.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
        await Assert.That(await search.Content.ReadAsStringAsync()).DoesNotContain("anna");
    }

    [Test]
    public async Task Without_permission_search_and_import_get_403() {
        var (_, token) = Builder.SeedAuthenticatedUser();
        using var client = await ClientAsync(token);

        var search = await client.GetAsync("/api/users/keycloak-accounts?memberNumber=4711");
        var import = await ImportAsync(client, Guid.NewGuid().ToString());

        await Assert.That(search.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
        await Assert.That(import.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task Import_creates_a_linked_user_and_places_the_member_in_the_department_chapter() {
        var department = $"Piratenpartei {Guid.NewGuid():N}";
        var state = Builder.SeedChapter("LV", keycloakDepartment: department);
        var number = TestDataBuilder.NextMemberNumber();
        var id = AddAccount(number, department: department);
        using var client = await ClientAsync();

        var response = await ImportAsync(client, id);

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var userId = (await response.Content.ReadFromJsonAsync<KeycloakUserImportedDTO>())!.UserId;
        var member = Db.Members.Single(m => m.MemberNumber == number);
        await Assert.That(member.UserId).IsEqualTo(userId);
        await Assert.That(member.ChapterId).IsEqualTo(state.Id);
        await Assert.That(Db.Users.Single(u => u.Id == userId).Email).IsEqualTo("anna@example.org");
    }

    [Test]
    public async Task Import_of_a_new_member_takes_the_voting_rights_from_the_account() {
        var number = TestDataBuilder.NextMemberNumber();
        var id = AddAccount(number, eligible: "true");
        using var client = await ClientAsync();

        var response = await ImportAsync(client, id);

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(Db.Members.Single(m => m.MemberNumber == number).HasVotingRights).IsTrue();
    }

    [Test]
    public async Task A_second_import_is_refused_and_search_links_the_existing_user() {
        var number = TestDataBuilder.NextMemberNumber();
        var id = AddAccount(number);
        using var client = await ClientAsync();
        var first = await (await ImportAsync(client, id)).Content.ReadFromJsonAsync<KeycloakUserImportedDTO>();

        var again = await ImportAsync(client, id);
        var search = await client.GetFromJsonAsync<List<KeycloakUserCandidateDTO>>($"/api/users/keycloak-accounts?memberNumber={number}");

        await Assert.That(again.StatusCode).IsEqualTo(HttpStatusCode.Conflict);
        await Assert.That(search!.Single().ExistingUserId).IsEqualTo(first!.UserId);
        await Assert.That(search.Single().Problem).IsNull();
    }

    [Test]
    public async Task Accounts_without_email_or_closed_ones_are_refused() {
        var withoutEmailNumber = TestDataBuilder.NextMemberNumber();
        var closedNumber = TestDataBuilder.NextMemberNumber();
        var withoutEmail = AddAccount(withoutEmailNumber, email: null);
        var closed = AddAccount(closedNumber, closed: true);
        using var client = await ClientAsync();

        var noEmailResponse = await ImportAsync(client, withoutEmail);
        var closedResponse = await ImportAsync(client, closed);

        await Assert.That(noEmailResponse.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
        await Assert.That(await noEmailResponse.Content.ReadAsStringAsync()).Contains("error.keycloak_account.no_email");
        await Assert.That(closedResponse.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
        await Assert.That(Db.Members.Any(m => m.MemberNumber == withoutEmailNumber || m.MemberNumber == closedNumber)).IsFalse();
    }

    [Test]
    public async Task Unknown_account_returns_404() {
        using var client = await ClientAsync();

        var response = await ImportAsync(client, Guid.NewGuid().ToString());

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }

    [Test]
    [Arguments("..")]
    [Arguments("../../roles")]
    [Arguments("not-a-uuid")]
    public async Task Ids_that_are_no_uuid_never_reach_keycloak(string keycloakUserId) {
        FakeKeycloakAdminClient.FailNext = new KeycloakException(500, "Keycloak must not be called");
        using var client = await ClientAsync();

        var response = await ImportAsync(client, keycloakUserId);

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task Search_shows_the_chapter_the_department_maps_to() {
        var department = $"Piratenpartei {Guid.NewGuid():N}";
        Builder.SeedChapter("Landesverband Test", keycloakDepartment: department);
        var number = TestDataBuilder.NextMemberNumber();
        AddAccount(number, department: department.ToUpperInvariant());
        using var client = await ClientAsync();

        var search = await client.GetFromJsonAsync<List<KeycloakUserCandidateDTO>>($"/api/users/keycloak-accounts?memberNumber={number}");

        await Assert.That(search!.Single().ChapterName).IsEqualTo("Landesverband Test");
    }

    [Test]
    public async Task A_chapter_below_the_department_chapter_is_kept_but_one_in_another_state_is_replaced() {
        var department = $"Piratenpartei {Guid.NewGuid():N}";
        var state = Builder.SeedChapter("LV", keycloakDepartment: department);
        var district = Builder.SeedChapter("KV", parentChapterId: state.Id);
        var otherState = Builder.SeedChapter("Other LV");
        var inDistrict = Builder.SeedMember(chapterId: district.Id, email: "a@example.org");
        var elsewhere = Builder.SeedMember(chapterId: otherState.Id, email: "b@example.org");
        var first = AddAccount(inDistrict.MemberNumber!.Value, department: department);
        var second = AddAccount(elsewhere.MemberNumber!.Value, department: department);
        using var client = await ClientAsync();

        await ImportAsync(client, first);
        await ImportAsync(client, second);

        await Assert.That(Db.Members.Single(m => m.Id == inDistrict.Id).ChapterId).IsEqualTo(district.Id);
        await Assert.That(Db.Members.Single(m => m.Id == elsewhere.Id).ChapterId).IsEqualTo(state.Id);
    }
}
