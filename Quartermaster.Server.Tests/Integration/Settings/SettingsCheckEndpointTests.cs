using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Quartermaster.Api;
using Quartermaster.Api.I18n;
using Quartermaster.Api.Settings;
using Quartermaster.Data.Options;
using Quartermaster.Server.Accreditation;
using Quartermaster.Server.AssemblyMotions.OpenSlides;
using Quartermaster.Server.AssemblyMotions.Wiki;
using Quartermaster.Server.NewsPosts.Mistral;
using Quartermaster.Server.NewsPosts.Postiz;
using Quartermaster.Server.Tests.Accreditation.Fakes;
using Quartermaster.Server.Tests.AssemblyMotions.Fakes;
using Quartermaster.Server.Tests.Infrastructure;
using Quartermaster.Server.Tests.NewsPosts.Fakes;

namespace Quartermaster.Server.Tests.Integration.Settings;

public class SettingsCheckEndpointTests : IntegrationTestBase {
    [Test]
    public async Task Returns_401_when_anonymous() {
        using var client = AnonymousClient();
        await AttachAntiforgeryTokenAsync(client);
        var response = await client.PostAsJsonAsync("/api/settings/check", new SettingsCheckRequest { Kind = SettingsCheckKind.Telegram });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Returns_403_without_edit_options_permission() {
        var (_, token) = Builder.SeedAuthenticatedUser(globalPermissions: [PermissionIdentifier.ViewOptions]);
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var response = await client.PostAsJsonAsync("/api/settings/check", new SettingsCheckRequest { Kind = SettingsCheckKind.Telegram });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
    }

    [Test]
    [Arguments(SettingsCheckKind.Telegram)]
    [Arguments(SettingsCheckKind.MemberImport)]
    [Arguments(SettingsCheckKind.Keycloak)]
    [Arguments(SettingsCheckKind.Wiki)]
    [Arguments(SettingsCheckKind.OpenSlides)]
    [Arguments(SettingsCheckKind.Mistral)]
    [Arguments(SettingsCheckKind.Postiz)]
    public async Task Reports_not_configured_without_credentials(SettingsCheckKind kind) {
        var result = await CheckAsync(kind);
        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.MessageKey).IsEqualTo(I18nKey.Ui.SettingsCheck.NotConfigured);
    }

    [Test]
    public async Task Member_import_check_counts_rows_of_a_readable_file() {
        var path = WriteTempFile("USER_Mitgliedsnummer;Name1\n1;Anna\n2;Bob\n");
        try {
            SetOption("member_import.file_path", path);
            var result = await CheckAsync(SettingsCheckKind.MemberImport);
            await Assert.That(result.Success).IsTrue();
            await Assert.That(result.MessageKey).StartsWith(I18nKey.Ui.SettingsCheck.MemberImportOk + "?");
            await Assert.That(result.MessageKey).Contains("rows=2");
        } finally {
            File.Delete(path);
        }
    }

    [Test]
    public async Task Member_import_check_rejects_a_file_without_member_numbers() {
        var path = WriteTempFile("Name1;Name2\nAnna;Test\n");
        try {
            SetOption("member_import.file_path", path);
            var result = await CheckAsync(SettingsCheckKind.MemberImport);
            await Assert.That(result.Success).IsFalse();
            await Assert.That(result.MessageKey).StartsWith(I18nKey.Ui.SettingsCheck.MemberImportColumnMissing);
        } finally {
            File.Delete(path);
        }
    }

    [Test]
    public async Task Member_import_check_reports_a_missing_file() {
        SetOption("member_import.file_path", Path.Combine(Path.GetTempPath(), $"qm-missing-{Guid.NewGuid():N}.csv"));
        var result = await CheckAsync(SettingsCheckKind.MemberImport);
        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.MessageKey).IsEqualTo(I18nKey.Ui.SettingsCheck.MemberImportFileMissing);
    }

    private static string WriteTempFile(string content) {
        var path = Path.Combine(Path.GetTempPath(), $"qm-import-check-{Guid.NewGuid():N}.csv");
        File.WriteAllText(path, content);
        return path;
    }

    private void SetOption(string key, string value) {
        using var scope = Factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<OptionRepository>().SetValue(key, null, value);
    }

    private async Task<SettingsCheckResultDTO> CheckAsync(SettingsCheckKind kind) {
        var (_, token) = Builder.SeedAuthenticatedUser(globalPermissions: [PermissionIdentifier.EditOptions]);
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var response = await client.PostAsJsonAsync("/api/settings/check", new SettingsCheckRequest { Kind = kind });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<SettingsCheckResultDTO>())!;
    }
}

/// <summary>Checks that call a remote service, each against its fake.</summary>
public abstract class SettingsCheckWithFakeTestBase : IntegrationTestBase {
    private WebApplicationFactory<Program> _host = default!;

    protected abstract void ReplaceClient(IServiceCollection services);

    protected virtual void ResetFake() { }

    [Before(Test)]
    public void CreateHost() {
        ResetFake();
        _host = Factory.WithWebHostBuilder(b => b.ConfigureServices(ReplaceClient));
    }

    [After(Test)]
    public void DisposeHost() {
        _host.Dispose();
    }

    protected void SetOption(string key, string value) {
        using var scope = _host.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<OptionRepository>().SetValue(key, null, value);
    }

    protected async Task<SettingsCheckResultDTO> CheckAsync(SettingsCheckKind kind) {
        var (_, token) = Builder.SeedAuthenticatedUser(globalPermissions: [PermissionIdentifier.EditOptions]);
        using var client = _host.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        await AttachAntiforgeryTokenAsync(client);
        var response = await client.PostAsJsonAsync("/api/settings/check", new SettingsCheckRequest { Kind = kind });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<SettingsCheckResultDTO>())!;
    }
}

[NotInParallel("FakeKeycloakAdminClient")]
public class KeycloakSettingsCheckTests : SettingsCheckWithFakeTestBase {
    protected override void ResetFake() {
        FakeKeycloakAdminClient.Reset();
    }

    protected override void ReplaceClient(IServiceCollection services) {
        services.RemoveAll<IKeycloakAdminClient>();
        services.AddScoped<IKeycloakAdminClient, FakeKeycloakAdminClient>();
    }

    private void SeedKeycloak() {
        SetOption("keycloak.base_url", "https://sso.test");
        SetOption("keycloak.realm", "Piraten");
        SetOption("keycloak.client_id", "quartermaster");
        SetOption("keycloak.client_secret", "secret");
    }

    [Test]
    public async Task Reports_the_realm_when_access_works() {
        SeedKeycloak();
        var result = await CheckAsync(SettingsCheckKind.Keycloak);
        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.MessageKey).IsEqualTo(I18nParams.With(I18nKey.Ui.SettingsCheck.KeycloakOk, ("realm", "Piraten")));
    }

    [Test]
    public async Task Reports_the_keycloak_error() {
        SeedKeycloak();
        FakeKeycloakAdminClient.FailNext = new KeycloakException(401, "Keycloak token request returned 401");
        var result = await CheckAsync(SettingsCheckKind.Keycloak);
        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.MessageKey).IsEqualTo(I18nKey.Ui.SettingsCheck.Failed);
        await Assert.That(result.Detail).IsEqualTo("Keycloak token request returned 401");
    }
}

[NotInParallel("FakeWikiBotClient")]
public class WikiSettingsCheckTests : SettingsCheckWithFakeTestBase {
    protected override void ResetFake() {
        FakeWikiBotClient.Reset();
    }

    protected override void ReplaceClient(IServiceCollection services) {
        services.RemoveAll<IWikiBotClient>();
        services.AddSingleton<IWikiBotClient, FakeWikiBotClient>();
    }

    private void SeedWiki() {
        SetOption("wiki.api_url", "https://wiki.test/api.php");
        SetOption("wiki.bot_username", "Portalbot");
        SetOption("wiki.bot_password", "bot-secret");
    }

    [Test]
    public async Task Logs_in_once() {
        SeedWiki();
        var result = await CheckAsync(SettingsCheckKind.Wiki);
        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.MessageKey).IsEqualTo(I18nParams.With(I18nKey.Ui.SettingsCheck.LoginOk, ("username", "Portalbot")));
        await Assert.That(FakeWikiBotClient.LoginCount).IsEqualTo(1);
    }

    [Test]
    public async Task Reports_a_failed_login() {
        SeedWiki();
        FakeWikiBotClient.Configured = false;
        var result = await CheckAsync(SettingsCheckKind.Wiki);
        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.MessageKey).IsEqualTo(I18nKey.Ui.SettingsCheck.Failed);
        await Assert.That(result.Detail).IsNotNull();
    }
}

[NotInParallel("FakeOpenSlidesClient")]
public class OpenSlidesSettingsCheckTests : SettingsCheckWithFakeTestBase {
    protected override void ResetFake() {
        FakeOpenSlidesClient.Reset();
    }

    protected override void ReplaceClient(IServiceCollection services) {
        services.RemoveAll<IOpenSlidesClient>();
        services.AddSingleton<IOpenSlidesClient, FakeOpenSlidesClient>();
    }

    private void SeedOpenSlides(string url) {
        SetOption("openslides.url", url);
        SetOption("openslides.username", "assistant");
        SetOption("openslides.password", "os-secret");
    }

    [Test]
    public async Task Logs_in_once() {
        SeedOpenSlides("https://openslides.test");
        var result = await CheckAsync(SettingsCheckKind.OpenSlides);
        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.MessageKey).IsEqualTo(I18nParams.With(I18nKey.Ui.SettingsCheck.LoginOk, ("username", "assistant")));
        await Assert.That(FakeOpenSlidesClient.LoginCount).IsEqualTo(1);
    }

    [Test]
    public async Task Rejects_plain_http_without_contacting_openslides() {
        SeedOpenSlides("http://openslides.test");
        var result = await CheckAsync(SettingsCheckKind.OpenSlides);
        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.MessageKey).IsEqualTo(I18nKey.Ui.SettingsCheck.InsecureUrl);
        await Assert.That(FakeOpenSlidesClient.LoginCount).IsEqualTo(0);
    }

    [Test]
    public async Task Reports_a_rejected_login() {
        SeedOpenSlides("https://openslides.test");
        FakeOpenSlidesClient.LoginFailure = new OpenSlidesException("OpenSlides hat die Anmeldung abgelehnt.", "unauthorized");
        var result = await CheckAsync(SettingsCheckKind.OpenSlides);
        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Detail).IsEqualTo("OpenSlides hat die Anmeldung abgelehnt.");
    }
}

public class MistralSettingsCheckTests : SettingsCheckWithFakeTestBase {
    private readonly FakeMistralClient _mistral = new();

    protected override void ReplaceClient(IServiceCollection services) {
        services.RemoveAll<IMistralClient>();
        services.AddSingleton<IMistralClient>(_mistral);
    }

    [Test]
    public async Task Returns_the_available_models() {
        SetOption("newsposts.mistral.api_key", "mistral-key");
        var result = await CheckAsync(SettingsCheckKind.Mistral);
        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Items).IsEquivalentTo(_mistral.Models);
    }
}

[NotInParallel("FakePostizClient")]
public class PostizSettingsCheckTests : SettingsCheckWithFakeTestBase {
    protected override void ResetFake() {
        FakePostizClient.Reset();
    }

    protected override void ReplaceClient(IServiceCollection services) {
        services.RemoveAll<IPostizClient>();
        services.AddScoped<IPostizClient, FakePostizClient>();
    }

    [Test]
    public async Task Lists_the_active_channels() {
        SetOption("newsposts.postiz.base_url", "https://postiz.test/api");
        SetOption("newsposts.postiz.api_key", "postiz-key");
        FakePostizClient.Channels.Add(new PostizChannel("1", "Piratenpartei", "bluesky", null, null, false));
        FakePostizClient.Channels.Add(new PostizChannel("2", "Alt", "x", null, null, true));

        var result = await CheckAsync(SettingsCheckKind.Postiz);
        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Items).IsEquivalentTo(["Piratenpartei (bluesky)"]);
    }
}
