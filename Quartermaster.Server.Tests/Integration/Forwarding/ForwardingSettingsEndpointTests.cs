using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Quartermaster.Api;
using Quartermaster.Api.Forwarding;
using Quartermaster.Api.Options;
using Quartermaster.Data.Chapters;
using Quartermaster.Data.Forwarding;
using Quartermaster.Server.Forwarding.Redmine;
using Quartermaster.Server.Tests.Forwarding.Fakes;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.Integration.Forwarding;

[NotInParallel("FakeRedmineClient")]
public class ForwardingSettingsEndpointTests : IntegrationTestBase {
    private WebApplicationFactory<Program> _host = default!;
    private Chapter _federal = default!;
    private Chapter _state = default!;
    private Chapter _district = default!;

    [Before(Test)]
    public void Setup() {
        FakeRedmineClient.Reset();
        _host = Factory.WithWebHostBuilder(b => b.ConfigureServices(services => {
            services.RemoveAll<IRedmineClient>();
            services.AddSingleton<IRedmineClient, FakeRedmineClient>();
        }));
        _federal = Builder.SeedChapter("Bund");
        _state = Builder.SeedChapter("Land", parentChapterId: _federal.Id);
        _district = Builder.SeedChapter("Kreis", parentChapterId: _state.Id);
    }

    [After(Test)]
    public void TearDown() {
        _host.Dispose();
    }

    private async Task<HttpClient> AdminAsync() {
        var (_, token) = Builder.SeedAuthenticatedUser(globalPermissions: [PermissionIdentifier.EditOptions]);
        return await ClientAsync(token);
    }

    private async Task<HttpClient> ClientAsync(string? token) {
        var client = _host.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        if (token != null)
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        await AttachAntiforgeryTokenAsync(client);
        return client;
    }

    private RedmineConnection? Connection(Guid chapterId) {
        using var scope = _host.Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<RedmineConnectionRepository>().Get(chapterId);
    }

    [Test]
    public async Task All_routes_require_edit_options() {
        var (_, token) = Builder.SeedAuthenticatedUser();
        using var plain = await ClientAsync(token);
        using var anonymous = await ClientAsync(null);
        var routes = new List<Func<HttpClient, Task<HttpResponseMessage>>> {
            c => c.GetAsync("/api/forwarding/redmine-connections"),
            c => c.PutAsJsonAsync($"/api/forwarding/redmine-connections/{_federal.Id}", new RedmineConnectionSaveRequest { BaseUrl = "https://x.invalid", ApiKey = "k" }),
            c => c.DeleteAsync($"/api/forwarding/redmine-connections/{_federal.Id}"),
            c => c.PostAsJsonAsync($"/api/forwarding/redmine-connections/{_federal.Id}/check", new { }),
            c => c.GetAsync($"/api/forwarding/redmine-lookups/{_federal.Id}"),
            c => c.GetAsync("/api/forwarding/chapters"),
            c => c.PutAsJsonAsync($"/api/forwarding/chapters/{_federal.Id}", new ForwardingChapterSaveRequest()),
            c => c.DeleteAsync($"/api/forwarding/chapters/{_federal.Id}")
        };

        foreach (var route in routes) {
            await Assert.That((await route(plain)).StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
            await Assert.That((await route(anonymous)).StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
        }
    }

    [Test]
    public async Task Api_key_is_never_returned() {
        using var client = await AdminAsync();
        await client.PutAsJsonAsync($"/api/forwarding/redmine-connections/{_federal.Id}", new RedmineConnectionSaveRequest { BaseUrl = "https://redmine.invalid", ApiKey = "geheimer-schluessel" });

        var response = await client.GetAsync("/api/forwarding/redmine-connections");
        var body = await response.Content.ReadAsStringAsync();
        var list = await response.Content.ReadFromJsonAsync<List<RedmineConnectionDTO>>();

        await Assert.That(body).DoesNotContain("geheimer-schluessel");
        await Assert.That(list!.Single(c => c.ChapterId == _federal.Id).HasApiKey).IsTrue();
    }

    [Test]
    public async Task Empty_key_keeps_stored_key() {
        using var client = await AdminAsync();
        await client.PutAsJsonAsync($"/api/forwarding/redmine-connections/{_federal.Id}", new RedmineConnectionSaveRequest { BaseUrl = "https://redmine.invalid", ApiKey = "k1" });

        var response = await client.PutAsJsonAsync($"/api/forwarding/redmine-connections/{_federal.Id}", new RedmineConnectionSaveRequest { BaseUrl = "https://redmine.invalid/redmine", ApiKey = "" });

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(Connection(_federal.Id)!.ApiKey).IsEqualTo("k1");
        await Assert.That(Connection(_federal.Id)!.BaseUrl).IsEqualTo("https://redmine.invalid/redmine");
    }

    [Test]
    public async Task Changing_the_host_requires_a_new_key() {
        using var client = await AdminAsync();
        await client.PutAsJsonAsync($"/api/forwarding/redmine-connections/{_federal.Id}", new RedmineConnectionSaveRequest { BaseUrl = "https://redmine.invalid", ApiKey = "k1" });

        var otherHost = await client.PutAsJsonAsync($"/api/forwarding/redmine-connections/{_federal.Id}", new RedmineConnectionSaveRequest { BaseUrl = "https://attacker.invalid" });
        var otherPort = await client.PutAsJsonAsync($"/api/forwarding/redmine-connections/{_federal.Id}", new RedmineConnectionSaveRequest { BaseUrl = "https://redmine.invalid:8443" });

        await Assert.That(otherHost.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
        await Assert.That(otherPort.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
        await Assert.That(Connection(_federal.Id)!.BaseUrl).IsEqualTo("https://redmine.invalid");
    }

    [Test]
    public async Task New_connection_needs_a_key_and_a_secure_url() {
        using var client = await AdminAsync();

        var noKey = await client.PutAsJsonAsync($"/api/forwarding/redmine-connections/{_state.Id}", new RedmineConnectionSaveRequest { BaseUrl = "https://redmine.invalid" });
        var insecure = await client.PutAsJsonAsync($"/api/forwarding/redmine-connections/{_state.Id}", new RedmineConnectionSaveRequest { BaseUrl = "http://redmine.invalid", ApiKey = "k" });

        await Assert.That(noKey.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
        await Assert.That(insecure.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
        await Assert.That(Connection(_state.Id)).IsNull();
    }

    [Test]
    public async Task Redmine_mode_without_connection_is_refused() {
        using var client = await AdminAsync();

        var response = await client.PutAsJsonAsync($"/api/forwarding/chapters/{_district.Id}", new ForwardingChapterSaveRequest {
            ApplicationMode = ForwardingMode.Redmine,
            Targets = [new ForwardingTargetDTO { Purpose = ForwardingPurpose.Application, ProjectId = 1, TrackerId = 2 }]
        });

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
        await Assert.That(await response.Content.ReadAsStringAsync()).Contains("error.forwarding.redmine_connection_missing");
    }

    [Test]
    public async Task Redmine_mode_without_target_is_refused() {
        using var client = await AdminAsync();
        await client.PutAsJsonAsync($"/api/forwarding/redmine-connections/{_federal.Id}", new RedmineConnectionSaveRequest { BaseUrl = "https://redmine.invalid", ApiKey = "k" });

        var response = await client.PutAsJsonAsync($"/api/forwarding/chapters/{_district.Id}", new ForwardingChapterSaveRequest { ApplicationMode = ForwardingMode.Redmine });

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
        await Assert.That(await response.Content.ReadAsStringAsync()).Contains("error.forwarding.target_missing");
    }

    [Test]
    public async Task Reduction_mode_refused_below_state_level() {
        using var client = await AdminAsync();

        var response = await client.PutAsJsonAsync($"/api/forwarding/chapters/{_district.Id}", new ForwardingChapterSaveRequest { ReductionMode = ForwardingMode.Quartermaster });

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
        await Assert.That(await response.Content.ReadAsStringAsync()).Contains("error.forwarding.reduction_mode_not_allowed");
    }

    [Test]
    public async Task Saved_chapter_settings_come_back_in_the_list() {
        using var client = await AdminAsync();
        await client.PutAsJsonAsync($"/api/forwarding/redmine-connections/{_federal.Id}", new RedmineConnectionSaveRequest { BaseUrl = "https://redmine.invalid", ApiKey = "k" });

        var save = await client.PutAsJsonAsync($"/api/forwarding/chapters/{_state.Id}", new ForwardingChapterSaveRequest {
            ReductionMode = ForwardingMode.Redmine,
            FailureEmail = "mv@land.invalid",
            Targets = [new ForwardingTargetDTO { Purpose = ForwardingPurpose.Reduction, ProjectId = 4, TrackerId = 5, CustomFields = [new ForwardingCustomFieldDTO { FieldId = 7, Template = "{{ member.Number }}" }] }]
        });
        var list = await client.GetFromJsonAsync<List<ForwardingChapterSettingDTO>>("/api/forwarding/chapters");

        var state = list!.Single(c => c.ChapterId == _state.Id);
        await Assert.That(save.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(state.IsStateChapter).IsTrue();
        await Assert.That(state.HasConnection).IsTrue();
        await Assert.That(state.Targets.Single().CustomFields.Single().FieldId).IsEqualTo(7);
        await Assert.That(state.FailureEmail).IsEqualTo("mv@land.invalid");
    }

    [Test]
    public async Task Lookups_use_resolved_connection() {
        using var client = await AdminAsync();
        await client.PutAsJsonAsync($"/api/forwarding/redmine-connections/{_federal.Id}", new RedmineConnectionSaveRequest { BaseUrl = "https://redmine.invalid", ApiKey = "k" });

        var lookups = await client.GetFromJsonAsync<RedmineLookupDTO>($"/api/forwarding/redmine-lookups/{_district.Id}");

        await Assert.That(lookups!.Projects.Count).IsEqualTo(2);
        await Assert.That(lookups.Statuses.Any(s => s.Name == "Zur Eintragung")).IsTrue();
    }

    [Test]
    public async Task Check_returns_account_name_without_key() {
        using var client = await AdminAsync();
        await client.PutAsJsonAsync($"/api/forwarding/redmine-connections/{_federal.Id}", new RedmineConnectionSaveRequest { BaseUrl = "https://redmine.invalid", ApiKey = "geheim" });

        var response = await client.PostAsJsonAsync($"/api/forwarding/redmine-connections/{_federal.Id}/check", new { });
        var result = await response.Content.ReadFromJsonAsync<ConnectionCheckResultDTO>();

        await Assert.That(result!.Ok).IsTrue();
        await Assert.That(result.Message).Contains("qm-bot");
        await Assert.That(result.Message).DoesNotContain("geheim");
    }
    [Test]
    public async Task Switch_needs_federal_connection_and_targets() {
        using var client = await AdminAsync();

        var withoutSetup = await client.PostAsJsonAsync("/api/forwarding/switches", new ForwardingSwitchRequest { Key = ForwardingOptionKeys.DueSelectionEnabled, Enabled = true });
        await client.PutAsJsonAsync($"/api/forwarding/redmine-connections/{_federal.Id}", new RedmineConnectionSaveRequest { BaseUrl = "https://redmine.invalid", ApiKey = "k" });
        await client.PutAsJsonAsync($"/api/forwarding/chapters/{_federal.Id}", new ForwardingChapterSaveRequest {
            Targets = [new ForwardingTargetDTO { Purpose = ForwardingPurpose.Regular, ProjectId = 1, TrackerId = 2 }]
        });
        var regularOnly = await client.PostAsJsonAsync("/api/forwarding/switches", new ForwardingSwitchRequest { Key = ForwardingOptionKeys.DueSelectionEnabled, Enabled = true });
        await client.PutAsJsonAsync($"/api/forwarding/chapters/{_federal.Id}", new ForwardingChapterSaveRequest {
            Targets = [
                new ForwardingTargetDTO { Purpose = ForwardingPurpose.Regular, ProjectId = 1, TrackerId = 2 },
                new ForwardingTargetDTO { Purpose = ForwardingPurpose.Reduction, ProjectId = 3, TrackerId = 2 }
            ]
        });
        var complete = await client.PostAsJsonAsync("/api/forwarding/switches", new ForwardingSwitchRequest { Key = ForwardingOptionKeys.DueSelectionEnabled, Enabled = true });
        var disable = await client.PostAsJsonAsync("/api/forwarding/switches", new ForwardingSwitchRequest { Key = ForwardingOptionKeys.MembershipApplicationEnabled, Enabled = false });

        await Assert.That(withoutSetup.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
        await Assert.That(await withoutSetup.Content.ReadAsStringAsync()).Contains("error.forwarding.federal_setup_incomplete");
        await Assert.That(regularOnly.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
        await Assert.That(complete.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(disable.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }

    [Test]
    public async Task Switch_requires_edit_options() {
        var (_, token) = Builder.SeedAuthenticatedUser();
        using var client = await ClientAsync(token);

        var response = await client.PostAsJsonAsync("/api/forwarding/switches", new ForwardingSwitchRequest { Key = ForwardingOptionKeys.DueSelectionEnabled, Enabled = false });

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
    }
    [Test]
    [Arguments("""{"targets":null}""")]
    [Arguments("""{"targets":[{"purpose":0,"projectId":1,"trackerId":2,"customFields":null}]}""")]
    [Arguments("""{"targets":[{"purpose":0,"projectId":1,"trackerId":2,"customFields":[{"fieldId":5,"template":null}]}]}""")]
    public async Task Null_lists_are_refused(string json) {
        using var client = await AdminAsync();

        var response = await client.PutAsync($"/api/forwarding/chapters/{_federal.Id}", new StringContent(json, Encoding.UTF8, "application/json"));

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task Broken_custom_field_template_is_refused() {
        using var client = await AdminAsync();

        var response = await client.PutAsJsonAsync($"/api/forwarding/chapters/{_federal.Id}", new ForwardingChapterSaveRequest {
            Targets = [new ForwardingTargetDTO {
                Purpose = ForwardingPurpose.Regular, ProjectId = 1, TrackerId = 2,
                CustomFields = [new ForwardingCustomFieldDTO { FieldId = 5, Template = "{{ member.Number" }]
            }]
        });

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
        await Assert.That(await response.Content.ReadAsStringAsync()).Contains("error.forwarding.custom_field_template_invalid");
    }
    [Test]
    public async Task Generic_option_endpoint_does_not_bypass_the_switch_check() {
        using var client = await AdminAsync();

        var response = await client.PostAsJsonAsync("/api/options", new OptionUpdateRequest { Identifier = ForwardingOptionKeys.DueSelectionEnabled, Value = "true" });

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
        await Assert.That(Db.SystemOptions.Any(o => o.Identifier == ForwardingOptionKeys.DueSelectionEnabled && o.Value == "true")).IsFalse();
    }
}
