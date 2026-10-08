using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Quartermaster.Api;
using Quartermaster.Api.Accreditation;
using Quartermaster.Data.Options;
using Quartermaster.Server.Accreditation;
using Quartermaster.Server.Tests.Accreditation.Fakes;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.Accreditation;

[NotInParallel("FakeKeycloakAdminClient")]
public class AccreditationEndpointTests : IntegrationTestBase {
    private WebApplicationFactory<Program> _host = default!;
    private string _token = "";

    [Before(Test)]
    public void Setup() {
        FakeKeycloakAdminClient.Reset();

        _host = Factory.WithWebHostBuilder(b => b.ConfigureServices(services => {
            services.RemoveAll<IKeycloakAdminClient>();
            services.AddScoped<IKeycloakAdminClient, FakeKeycloakAdminClient>();
        }));

        SeedKeycloakOptions();
        (_, _token) = Builder.SeedAuthenticatedUser(globalPermissions: [PermissionIdentifier.ManageAccreditation]);
    }

    [After(Test)]
    public void TearDown() {
        _host.Dispose();
    }

    private HttpClient HostClient() {
        return _host.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
    }

    private async Task<HttpClient> ClientAsync() {
        var client = HostClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _token);
        await AttachAntiforgeryTokenAsync(client);
        return client;
    }

    private void SeedKeycloakOptions() {
        using var scope = _host.Services.CreateScope();
        var optionRepo = scope.ServiceProvider.GetRequiredService<OptionRepository>();
        optionRepo.SetValue("keycloak.base_url", null, "https://sso.test");
        optionRepo.SetValue("keycloak.auth_realm", null, "master");
        optionRepo.SetValue("keycloak.realm", null, "users");
        optionRepo.SetValue("keycloak.client_id", null, "admin-api");
        optionRepo.SetValue("keycloak.client_secret", null, "s3cret");
        optionRepo.SetValue("keycloak.username_separator", null, ".");
    }

    private void UnsetKeycloakOptions() {
        using var scope = _host.Services.CreateScope();
        var optionRepo = scope.ServiceProvider.GetRequiredService<OptionRepository>();
        optionRepo.SetValue("keycloak.base_url", null, "");
        optionRepo.SetValue("keycloak.realm", null, "");
        optionRepo.SetValue("keycloak.client_id", null, "");
        optionRepo.SetValue("keycloak.client_secret", null, "");
    }

    private static Task<HttpResponseMessage> SendAsync(HttpClient client, string method, string url) {
        var request = new HttpRequestMessage(new HttpMethod(method), url);
        if (method == "POST")
            request.Content = new StringContent("{}", Encoding.UTF8, "application/json");
        return client.SendAsync(request);
    }

    [Test]
    [Arguments("GET", "/api/accreditation/members/4711")]
    [Arguments("POST", "/api/accreditation/members/4711/eligible")]
    [Arguments("POST", "/api/accreditation/temporary-accounts/check")]
    [Arguments("POST", "/api/accreditation/temporary-accounts")]
    [Arguments("GET", "/api/accreditation/config")]
    public async Task Anonymous_gets_401(string method, string url) {
        using var client = HostClient();
        await AttachAntiforgeryTokenAsync(client);

        var response = await SendAsync(client, method, url);

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    [Arguments("GET", "/api/accreditation/members/4711")]
    [Arguments("POST", "/api/accreditation/members/4711/eligible")]
    [Arguments("POST", "/api/accreditation/temporary-accounts/check")]
    [Arguments("POST", "/api/accreditation/temporary-accounts")]
    [Arguments("GET", "/api/accreditation/config")]
    public async Task Without_permission_gets_403(string method, string url) {
        var (_, token) = Builder.SeedAuthenticatedUser();
        using var client = HostClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        await AttachAntiforgeryTokenAsync(client);

        var response = await SendAsync(client, method, url);

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task Search_with_no_matches_returns_empty_list() {
        using var client = await ClientAsync();

        var response = await client.GetAsync("/api/accreditation/members/4711");

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var accounts = await response.Content.ReadFromJsonAsync<List<AccreditationAccountDTO>>();
        await Assert.That(accounts!.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Search_maps_account_fields() {
        FakeKeycloakAdminClient.AddUser(Guid.NewGuid().ToString(), "erika.mustermann", "4711");
        using var client = await ClientAsync();

        var response = await client.GetAsync("/api/accreditation/members/4711");

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var account = (await response.Content.ReadFromJsonAsync<List<AccreditationAccountDTO>>())!.Single();
        await Assert.That(account.Username).IsEqualTo("erika.mustermann");
        await Assert.That(account.Eligible).IsFalse();
        await Assert.That(account.Closed).IsFalse();
        await Assert.That(account.Enabled).IsTrue();
    }

    [Test]
    public async Task Search_shows_closed_disabled_account() {
        FakeKeycloakAdminClient.AddUser(Guid.NewGuid().ToString(), "old.member", "4712", enabled: false, eligible: "false", closed: true);
        using var client = await ClientAsync();

        var response = await client.GetAsync("/api/accreditation/members/4712");

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var account = (await response.Content.ReadFromJsonAsync<List<AccreditationAccountDTO>>())!.Single();
        await Assert.That(account.Enabled).IsFalse();
        await Assert.That(account.Closed).IsTrue();
    }

    [Test]
    public async Task Set_eligible_updates_attribute_and_logs_audit_row() {
        var user = FakeKeycloakAdminClient.AddUser(Guid.NewGuid().ToString(), "erika.mustermann", "4711", eligible: "false");
        using var client = await ClientAsync();

        var response = await client.PostAsync("/api/accreditation/members/4711/eligible", null);

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var account = await response.Content.ReadFromJsonAsync<AccreditationAccountDTO>();
        await Assert.That(account!.Eligible).IsTrue();
        await Assert.That(FakeKeycloakAdminClient.Users.Single(u => u.Id == user.Id).Attribute("ekklesia_eligible")).IsEqualTo("true");

        var audit = Db.AuditLogs.Single(a => a.EntityType == "KeycloakAccount" && a.EntityId == Guid.Parse(user.Id));
        await Assert.That(audit.FieldName).IsEqualTo("ekklesia_eligible");
        await Assert.That(audit.OldValue).IsEqualTo("false (member 4711)");
        await Assert.That(audit.NewValue).IsEqualTo("true (member 4711)");
    }

    [Test]
    public async Task Set_eligible_with_no_account_returns_404() {
        using var client = await ClientAsync();

        var response = await client.PostAsync("/api/accreditation/members/4711/eligible", null);

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }

    [Test]
    [Arguments("abc")]
    [Arguments("0")]
    [Arguments("--1")]
    [Arguments("1-")]
    [Arguments("1.5")]
    public async Task Set_eligible_with_invalid_member_number_returns_400(string memberNumber) {
        using var client = await ClientAsync();

        var response = await client.PostAsync($"/api/accreditation/members/{memberNumber}/eligible", null);

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task Set_eligible_with_multiple_accounts_returns_409_and_leaves_unchanged() {
        FakeKeycloakAdminClient.AddUser(Guid.NewGuid().ToString(), "one.user", "4711", eligible: "false");
        FakeKeycloakAdminClient.AddUser(Guid.NewGuid().ToString(), "two.user", "4711", eligible: "false");
        using var client = await ClientAsync();

        var response = await client.PostAsync("/api/accreditation/members/4711/eligible", null);

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Conflict);
        await Assert.That(FakeKeycloakAdminClient.Users.All(u => u.Attribute("ekklesia_eligible") == "false")).IsTrue();
    }

    [Test]
    public async Task Set_eligible_with_non_guid_keycloak_id_falls_back_to_empty_guid() {
        FakeKeycloakAdminClient.AddUser("legacy-id-1", "alt.user", "9999", eligible: "false");
        using var client = await ClientAsync();

        var response = await client.PostAsync("/api/accreditation/members/9999/eligible", null);

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var audit = Db.AuditLogs.Single(a => a.EntityType == "KeycloakAccount" && a.EntityId == Guid.Empty && a.Action == "Updated");
        await Assert.That(audit.FieldName).IsEqualTo("ekklesia_eligible (keycloak id legacy-id-1)");
        await Assert.That(audit.OldValue).IsEqualTo("false (member 9999)");
        await Assert.That(audit.NewValue).IsEqualTo("true (member 9999)");
    }

    [Test]
    public async Task Set_eligible_when_reread_fails_returns_200_with_eligible_true() {
        FakeKeycloakAdminClient.AddUser(Guid.NewGuid().ToString(), "erika.mustermann", "4713", eligible: "false");
        FakeKeycloakAdminClient.FailOnFindAfterSetAttribute = true;
        using var client = await ClientAsync();

        var response = await client.PostAsync("/api/accreditation/members/4713/eligible", null);

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var account = await response.Content.ReadFromJsonAsync<AccreditationAccountDTO>();
        await Assert.That(account!.Eligible).IsTrue();
        await Assert.That(account.Username).IsEqualTo("erika.mustermann");
        await Assert.That(FakeKeycloakAdminClient.Users.Single(u => u.Attribute("member_number") == "4713").Attribute("ekklesia_eligible")).IsEqualTo("true");
    }

    [Test]
    public async Task Set_eligible_when_reread_finds_no_accounts_returns_200_with_eligible_true() {
        FakeKeycloakAdminClient.AddUser(Guid.NewGuid().ToString(), "erika.mustermann", "4714", eligible: "false");
        FakeKeycloakAdminClient.VanishAfterSetAttribute = true;
        using var client = await ClientAsync();

        var response = await client.PostAsync("/api/accreditation/members/4714/eligible", null);

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var account = await response.Content.ReadFromJsonAsync<AccreditationAccountDTO>();
        await Assert.That(account!.Eligible).IsTrue();
        await Assert.That(account.Username).IsEqualTo("erika.mustermann");
        await Assert.That(FakeKeycloakAdminClient.Users).IsEmpty();
    }

    private static TemporaryAccountRequest CheckRequest(string username = "Erika.Mustermann", string memberNumber = "4711", string? email = null) => new() {
        FirstName = "Erika",
        LastName = "Mustermann",
        MemberNumber = memberNumber,
        Username = username,
        Email = email,
        Eligible = true
    };

    private async Task<TemporaryAccountCheckDTO> CheckAsync(TemporaryAccountRequest request) {
        using var client = await ClientAsync();
        var response = await client.PostAsJsonAsync("/api/accreditation/temporary-accounts/check", request);
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<TemporaryAccountCheckDTO>())!;
    }

    [Test]
    public async Task Check_passes_for_free_username_and_new_member_number() {
        var result = await CheckAsync(CheckRequest());

        await Assert.That(result.Ok).IsTrue();
        await Assert.That(result.Problems).IsEmpty();
        await Assert.That(FakeKeycloakAdminClient.Created).IsEmpty();
    }

    [Test]
    public async Task Check_reports_every_field_problem_at_once() {
        var result = await CheckAsync(new TemporaryAccountRequest { FirstName = " ", LastName = "", MemberNumber = "abc", Username = "a b", Email = "kein mail" });

        await Assert.That(result.Ok).IsFalse();
        await Assert.That(result.Problems.Select(p => p.Field).ToList())
            .IsEquivalentTo(new List<string> { "firstName", "lastName", "memberNumber", "email", "username" });
    }

    [Test]
    public async Task Check_reports_existing_account_for_member_number_including_closed_ones() {
        FakeKeycloakAdminClient.AddUser(Guid.NewGuid().ToString(), "alt.konto", "4711", enabled: false, closed: true);

        var result = await CheckAsync(CheckRequest());

        await Assert.That(result.Ok).IsFalse();
        await Assert.That(result.Problems.Single().Field).IsEqualTo("memberNumber");
        await Assert.That(result.ExistingAccounts.Single().Username).IsEqualTo("alt.konto");
    }

    [Test]
    public async Task Check_suggests_suffixed_name_when_base_name_is_taken() {
        FakeKeycloakAdminClient.AddUser(Guid.NewGuid().ToString(), "erika.mustermann", "1234");

        var result = await CheckAsync(CheckRequest());

        await Assert.That(result.Ok).IsFalse();
        await Assert.That(result.Problems.Single().Field).IsEqualTo("username");
        await Assert.That(result.SuggestedUsername).IsEqualTo($"Erika.Mustermann.{AccreditationUsernameGenerator.HashSuffix("4711")}");
    }

    [Test]
    public async Task Check_suggests_base_name_when_a_manual_name_is_taken_but_base_is_free() {
        FakeKeycloakAdminClient.AddUser(Guid.NewGuid().ToString(), "erika", "1234");

        var result = await CheckAsync(CheckRequest(username: "erika"));

        await Assert.That(result.SuggestedUsername).IsEqualTo("Erika.Mustermann");
    }

    [Test]
    public async Task Check_offers_no_suggestion_when_base_and_suffixed_names_are_taken() {
        FakeKeycloakAdminClient.AddUser(Guid.NewGuid().ToString(), "erika.mustermann", "1234");
        FakeKeycloakAdminClient.AddUser(Guid.NewGuid().ToString(), $"erika.mustermann.{AccreditationUsernameGenerator.HashSuffix("4711")}", "5678");

        var result = await CheckAsync(CheckRequest());

        await Assert.That(result.Problems.Single().Field).IsEqualTo("username");
        await Assert.That(result.SuggestedUsername).IsNull();
    }

    [Test]
    public async Task Check_maps_keycloak_failure_to_502() {
        FakeKeycloakAdminClient.FailNext = new KeycloakException(null, "down");
        using var client = await ClientAsync();

        var response = await client.PostAsJsonAsync("/api/accreditation/temporary-accounts/check", CheckRequest());

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadGateway);
        await Assert.That(await response.Content.ReadAsStringAsync()).Contains("Keycloak ist nicht erreichbar.");
    }

    [Test]
    public async Task Create_returns_username_password_and_sets_attributes() {
        using var client = await ClientAsync();

        var response = await client.PostAsJsonAsync("/api/accreditation/temporary-accounts", new TemporaryAccountRequest {
            FirstName = "Erika", LastName = "Mustermann", MemberNumber = "4711"
        });

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var dto = await response.Content.ReadFromJsonAsync<TemporaryAccountCreatedDTO>();
        await Assert.That(dto!.Username).IsEqualTo("erika.mustermann");
        await Assert.That(Regex.IsMatch(dto.Password, "^[A-Z][a-z]+-[A-Z][a-z]+-[A-Z][a-z]+-[0-9]{2}$")).IsTrue();

        var created = FakeKeycloakAdminClient.Created.Single();
        await Assert.That(created.Attributes["member_number"].Single()).IsEqualTo("4711");
        await Assert.That(created.Attributes["ekklesia_verified"].Single()).IsEqualTo("true");
        await Assert.That(created.Attributes["ekklesia_eligible"].Single()).IsEqualTo("true");
        await Assert.That(created.Attributes["ekklesia_external_voting"].Single()).IsEqualTo("false");
        await Assert.That(Regex.IsMatch(created.Attributes["created_time"].Single(), @"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}\.\d{6}\+00:00$")).IsTrue();

        var audit = Db.AuditLogs.Single(a => a.EntityType == "KeycloakAccount" && a.Action == "Created");
        await Assert.That(audit.FieldName).IsEqualTo("temporary_account");
        await Assert.That(audit.OldValue).IsNull();
        await Assert.That(audit.NewValue).IsEqualTo("member 4711, username Erika.Mustermann, eligible True");
    }

    [Test]
    public async Task Create_with_eligible_false_sets_attribute_false() {
        using var client = await ClientAsync();

        var response = await client.PostAsJsonAsync("/api/accreditation/temporary-accounts", new TemporaryAccountRequest {
            FirstName = "Erika", LastName = "Mustermann", MemberNumber = "4712", Eligible = false
        });

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(FakeKeycloakAdminClient.Created.Single().Attributes["ekklesia_eligible"].Single()).IsEqualTo("false");
    }

    [Test]
    public async Task Create_is_refused_when_member_number_has_existing_account() {
        FakeKeycloakAdminClient.AddUser(Guid.NewGuid().ToString(), "active.one", "5001");
        FakeKeycloakAdminClient.AddUser(Guid.NewGuid().ToString(), "closed.one", "5001", enabled: false, closed: true);
        using var client = await ClientAsync();

        var response = await client.PostAsJsonAsync("/api/accreditation/temporary-accounts", new TemporaryAccountRequest {
            FirstName = "Erika", LastName = "Mustermann", MemberNumber = "5001"
        });

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Conflict);
        var conflict = await response.Content.ReadFromJsonAsync<TemporaryAccountConflictDTO>();
        await Assert.That(conflict!.ExistingAccounts.Count).IsEqualTo(2);
        await Assert.That(FakeKeycloakAdminClient.Created).IsEmpty();
    }

    [Test]
    public async Task Create_with_existing_manual_username_returns_400() {
        FakeKeycloakAdminClient.AddUser(Guid.NewGuid().ToString(), "taken.user", "6000");
        using var client = await ClientAsync();

        var response = await client.PostAsJsonAsync("/api/accreditation/temporary-accounts", new TemporaryAccountRequest {
            FirstName = "Erika", LastName = "Mustermann", MemberNumber = "6001", Username = "taken.user"
        });

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
        await Assert.That(FakeKeycloakAdminClient.Created).IsEmpty();
    }

    [Test]
    public async Task Create_with_invalid_manual_username_returns_400() {
        using var client = await ClientAsync();

        var response = await client.PostAsJsonAsync("/api/accreditation/temporary-accounts", new TemporaryAccountRequest {
            FirstName = "Erika", LastName = "Mustermann", MemberNumber = "6002", Username = "a b"
        });

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
        await Assert.That(FakeKeycloakAdminClient.Created).IsEmpty();
    }

    [Test]
    public async Task Create_with_both_candidate_usernames_taken_asks_for_manual_username() {
        var suffix = AccreditationUsernameGenerator.HashSuffix("6003");
        FakeKeycloakAdminClient.AddUser(Guid.NewGuid().ToString(), "Erika.Mustermann", "1");
        FakeKeycloakAdminClient.AddUser(Guid.NewGuid().ToString(), $"Erika.Mustermann.{suffix}", "2");
        using var client = await ClientAsync();

        var response = await client.PostAsJsonAsync("/api/accreditation/temporary-accounts", new TemporaryAccountRequest {
            FirstName = "Erika", LastName = "Mustermann", MemberNumber = "6003"
        });

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadAsStringAsync();
        await Assert.That(body).Contains("Benutzername");
        await Assert.That(FakeKeycloakAdminClient.Created).IsEmpty();
    }

    [Test]
    public async Task Create_with_race_returns_200_with_password_and_warning() {
        FakeKeycloakAdminClient.RaceOnCreate = true;
        using var client = await ClientAsync();

        var response = await client.PostAsJsonAsync("/api/accreditation/temporary-accounts", new TemporaryAccountRequest {
            FirstName = "Erika", LastName = "Mustermann", MemberNumber = "7001"
        });

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var dto = await response.Content.ReadFromJsonAsync<TemporaryAccountCreatedDTO>();
        await Assert.That(Regex.IsMatch(dto!.Password, "^[A-Z][a-z]+-[A-Z][a-z]+-[A-Z][a-z]+-[0-9]{2}$")).IsTrue();
        await Assert.That(dto.Warning).IsEqualTo("Für diese Mitgliedsnummer gibt es jetzt mehrere Konten. Diese werden nachts nicht " +
            "automatisch geschlossen; bitte die IT informieren, damit ein Konto gelöscht wird.");
        await Assert.That(FakeKeycloakAdminClient.Created.Count).IsEqualTo(1);
        await Assert.That(FakeKeycloakAdminClient.Users.Count(u => u.Attribute("member_number") == "7001")).IsEqualTo(2);

        var auditRows = Db.AuditLogs.Count(a => a.EntityType == "KeycloakAccount" && a.Action == "Created" && a.FieldName == "temporary_account");
        await Assert.That(auditRows).IsEqualTo(1);
    }

    [Test]
    public async Task Create_when_duplicate_recheck_fails_returns_200_with_password_and_warning() {
        FakeKeycloakAdminClient.FailOnFindAfterCreate = true;
        using var client = await ClientAsync();

        var response = await client.PostAsJsonAsync("/api/accreditation/temporary-accounts", new TemporaryAccountRequest {
            FirstName = "Erika", LastName = "Mustermann", MemberNumber = "7002"
        });

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var dto = await response.Content.ReadFromJsonAsync<TemporaryAccountCreatedDTO>();
        await Assert.That(Regex.IsMatch(dto!.Password, "^[A-Z][a-z]+-[A-Z][a-z]+-[A-Z][a-z]+-[0-9]{2}$")).IsTrue();
        await Assert.That(dto.Warning).IsEqualTo("Zugang angelegt, aber die Prüfung auf doppelte Konten ist fehlgeschlagen. Bitte die IT informieren.");
        await Assert.That(FakeKeycloakAdminClient.Created.Count).IsEqualTo(1);

        var auditRows = Db.AuditLogs.Count(a => a.EntityType == "KeycloakAccount" && a.Action == "Created" && a.FieldName == "temporary_account");
        await Assert.That(auditRows).IsEqualTo(1);
    }

    [Test]
    public async Task Create_when_duplicate_recheck_throws_connection_error_returns_200_with_password_and_warning() {
        FakeKeycloakAdminClient.FailOnFindAfterCreate = true;
        FakeKeycloakAdminClient.NextFindException = new HttpRequestException("connection refused");
        using var client = await ClientAsync();

        var response = await client.PostAsJsonAsync("/api/accreditation/temporary-accounts", new TemporaryAccountRequest {
            FirstName = "Erika", LastName = "Mustermann", MemberNumber = "7003"
        });

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var dto = await response.Content.ReadFromJsonAsync<TemporaryAccountCreatedDTO>();
        await Assert.That(Regex.IsMatch(dto!.Password, "^[A-Z][a-z]+-[A-Z][a-z]+-[A-Z][a-z]+-[0-9]{2}$")).IsTrue();
        await Assert.That(dto.Warning).IsEqualTo("Zugang angelegt, aber die Prüfung auf doppelte Konten ist fehlgeschlagen. Bitte die IT informieren.");
        await Assert.That(FakeKeycloakAdminClient.Created.Count).IsEqualTo(1);
    }

    [Test]
    public async Task Create_when_the_response_times_out_but_the_account_was_actually_created_returns_502_with_maybe_created_message() {
        FakeKeycloakAdminClient.CreateTimesOutButSucceeds = true;
        using var client = await ClientAsync();

        var response = await client.PostAsJsonAsync("/api/accreditation/temporary-accounts", new TemporaryAccountRequest {
            FirstName = "Erika", LastName = "Mustermann", MemberNumber = "7004"
        });

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadGateway);
        var body = await response.Content.ReadAsStringAsync();
        await Assert.That(body).Contains("Zugang wurde möglicherweise angelegt, das Passwort ist aber unbekannt. Bitte die IT informieren.");
        await Assert.That(FakeKeycloakAdminClient.Users.Count(u => u.Attribute("member_number") == "7004")).IsEqualTo(1);
    }

    [Test]
    public async Task Create_when_keycloak_is_unreachable_and_no_account_was_created_returns_the_usual_unreachable_message() {
        FakeKeycloakAdminClient.FailNextCreate = new KeycloakException(null, "connection refused");
        using var client = await ClientAsync();

        var response = await client.PostAsJsonAsync("/api/accreditation/temporary-accounts", new TemporaryAccountRequest {
            FirstName = "Erika", LastName = "Mustermann", MemberNumber = "7005"
        });

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadGateway);
        var body = await response.Content.ReadAsStringAsync();
        await Assert.That(body).Contains("Keycloak ist nicht erreichbar.");
        await Assert.That(FakeKeycloakAdminClient.Users.Count(u => u.Attribute("member_number") == "7005")).IsEqualTo(0);
    }

    [Test]
    public async Task Password_never_appears_in_audit_log() {
        using var client = await ClientAsync();

        var response = await client.PostAsJsonAsync("/api/accreditation/temporary-accounts", new TemporaryAccountRequest {
            FirstName = "Erika", LastName = "Mustermann", MemberNumber = "8001"
        });

        var dto = await response.Content.ReadFromJsonAsync<TemporaryAccountCreatedDTO>();
        var password = dto!.Password;
        var rows = Db.AuditLogs.ToList();

        await Assert.That(rows.Any(r => r.OldValue != null && r.OldValue.Contains(password))).IsFalse();
        await Assert.That(rows.Any(r => r.NewValue != null && r.NewValue.Contains(password))).IsFalse();
        await Assert.That(rows.Any(r => r.FieldName != null && r.FieldName.Contains(password))).IsFalse();
    }

    [Test]
    public async Task Search_with_keycloak_error_returns_502_without_exception_detail() {
        using var client = await ClientAsync();
        FakeKeycloakAdminClient.FailNext = new KeycloakException(500, "boom");

        var response = await client.GetAsync("/api/accreditation/members/4711");

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadGateway);
        var body = await response.Content.ReadAsStringAsync();
        await Assert.That(body).Contains("Keycloak meldet einen Fehler.");
        await Assert.That(body).DoesNotContain("boom");
    }

    [Test]
    public async Task Search_with_keycloak_forbidden_returns_502_with_permission_message() {
        using var client = await ClientAsync();
        FakeKeycloakAdminClient.FailNext = new KeycloakException(403, "forbidden");

        var response = await client.GetAsync("/api/accreditation/members/4711");

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadGateway);
        var body = await response.Content.ReadAsStringAsync();
        await Assert.That(body).Contains("Quartermaster hat keine Berechtigung in Keycloak.");
    }

    [Test]
    public async Task Config_returns_separator_and_configured_flag() {
        using var client = await ClientAsync();

        var config = await client.GetFromJsonAsync<AccreditationConfigDTO>("/api/accreditation/config");

        await Assert.That(config!.UsernameSeparator).IsEqualTo(".");
        await Assert.That(config.KeycloakConfigured).IsTrue();
    }

    [Test]
    public async Task Config_answers_even_when_keycloak_is_not_configured() {
        UnsetKeycloakOptions();
        using var client = await ClientAsync();

        var response = await client.GetAsync("/api/accreditation/config");
        var config = await response.Content.ReadFromJsonAsync<AccreditationConfigDTO>();

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(config!.KeycloakConfigured).IsFalse();
    }

    [Test]
    public async Task Search_when_keycloak_not_configured_returns_503() {
        UnsetKeycloakOptions();
        using var client = await ClientAsync();

        var response = await client.GetAsync("/api/accreditation/members/4711");

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.ServiceUnavailable);
    }

    [Test]
    [Arguments("abc")]
    [Arguments("0")]
    [Arguments("--1")]
    [Arguments("1-")]
    [Arguments("1.5")]
    public async Task Search_with_invalid_member_number_returns_400(string memberNumber) {
        using var client = await ClientAsync();

        var response = await client.GetAsync($"/api/accreditation/members/{memberNumber}");

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task Negative_member_number_works_for_search_and_set_eligible() {
        var id = Guid.NewGuid().ToString();
        FakeKeycloakAdminClient.AddUser(id, "neg.member", "-4711", eligible: "false");
        using var client = await ClientAsync();

        var search = await client.GetFromJsonAsync<List<AccreditationAccountDTO>>("/api/accreditation/members/-4711");
        var setResponse = await client.PostAsync("/api/accreditation/members/-4711/eligible", null);

        await Assert.That(search!.Single().MemberNumber).IsEqualTo("-4711");
        await Assert.That(setResponse.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(FakeKeycloakAdminClient.Users.Single(u => u.Id == id).Attribute("ekklesia_eligible")).IsEqualTo("true");
    }

    [Test]
    public async Task Negative_member_number_works_for_check_and_create() {
        using var client = await ClientAsync();
        var request = new TemporaryAccountRequest {
            FirstName = "Erika", LastName = "Mustermann", MemberNumber = "-4711", Username = "Erika.Mustermann", Eligible = true
        };

        var check = await (await client.PostAsJsonAsync("/api/accreditation/temporary-accounts/check", request))
            .Content.ReadFromJsonAsync<TemporaryAccountCheckDTO>();
        var create = await client.PostAsJsonAsync("/api/accreditation/temporary-accounts", request);

        await Assert.That(check!.Ok).IsTrue();
        await Assert.That(create.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(FakeKeycloakAdminClient.Created.Single().Attributes["member_number"].Single()).IsEqualTo("-4711");
    }

    [Test]
    public async Task Search_with_blank_member_number_returns_400() {
        using var client = await ClientAsync();

        var response = await client.GetAsync("/api/accreditation/members/%20");

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    [Arguments("abc")]
    [Arguments("")]
    [Arguments("0")]
    [Arguments("--1")]
    [Arguments("1-")]
    [Arguments("1.5")]
    public async Task Create_with_invalid_member_number_returns_400(string memberNumber) {
        using var client = await ClientAsync();

        var response = await client.PostAsJsonAsync("/api/accreditation/temporary-accounts", new TemporaryAccountRequest {
            FirstName = "Erika", LastName = "Mustermann", MemberNumber = memberNumber
        });

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }
}
