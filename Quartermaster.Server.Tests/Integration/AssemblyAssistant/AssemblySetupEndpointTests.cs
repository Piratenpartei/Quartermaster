using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Quartermaster.Api;
using Quartermaster.Api.AssemblyAssistant;
using Quartermaster.Api.I18n;
using Quartermaster.Data.Options;
using Quartermaster.Server.AssemblyAssistant.Mumble.Proto;
using Quartermaster.Server.Tests.AssemblyAssistant.Fakes;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.Integration.AssemblyAssistant;

[NotInParallel(nameof(AssemblySetupEndpointTests))]
public class AssemblySetupEndpointTests : IntegrationTestBase {
    private const string CertificatePassword = "secret";

    private string _adminToken = "";
    private string _plainToken = "";

    [Before(Test)]
    public void Setup() {
        (_, _adminToken) = Builder.SeedAuthenticatedUser(globalPermissions: [PermissionIdentifier.EditOptions]);
        (_, _plainToken) = Builder.SeedAuthenticatedUser();
    }

    private async Task<HttpClient> ClientAsync(string token) {
        var client = Factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        await AttachAntiforgeryTokenAsync(client);
        return client;
    }

    private void SetOption(string id, string value) {
        using var scope = Factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<OptionRepository>().SetValue(id, null, value);
    }

    private string? GetOption(string id) {
        using var scope = Factory.Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<OptionRepository>().GetGlobalValue(id)?.Value;
    }

    [Test]
    [Arguments("GET", "/api/assembly-assistant/setup")]
    [Arguments("POST", "/api/assembly-assistant/setup/certificate")]
    [Arguments("POST", "/api/assembly-assistant/setup/check")]
    [Arguments("POST", "/api/assembly-assistant/setup/register")]
    [Arguments("POST", "/api/assembly-assistant/setup/channels")]
    public async Task Requires_options_edit(string method, string url) {
        var client = await ClientAsync(_plainToken);
        var request = new HttpRequestMessage(new HttpMethod(method), url);
        if (method == "POST")
            request.Content = JsonContent.Create(new { });

        var response = await client.SendAsync(request);

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task Generate_refuses_overwrite_without_replace() {
        SetOption("mumble.certificate", "");
        var client = await ClientAsync(_adminToken);

        var first = await client.PostAsJsonAsync("/api/assembly-assistant/setup/certificate", new AssemblySetupGenerateCertificateRequest());
        var stored = GetOption("mumble.certificate");
        var second = await client.PostAsJsonAsync("/api/assembly-assistant/setup/certificate", new AssemblySetupGenerateCertificateRequest());
        var third = await client.PostAsJsonAsync("/api/assembly-assistant/setup/certificate", new AssemblySetupGenerateCertificateRequest { Replace = true });

        await Assert.That(first.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(second.StatusCode).IsEqualTo(HttpStatusCode.Conflict);
        await Assert.That(third.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(GetOption("mumble.certificate")).IsNotEqualTo(stored);

        var status = await client.GetFromJsonAsync<AssemblySetupStatusDTO>("/api/assembly-assistant/setup");
        await Assert.That(status!.CertificatePresent).IsTrue();
        await Assert.That(status.CertificateUnreadable).IsFalse();
        await Assert.That(status.CertificateHash!.Length).IsEqualTo(40);
    }

    [Test]
    public async Task Save_requires_all_roles() {
        var client = await ClientAsync(_adminToken);

        var partial = await client.PostAsJsonAsync("/api/assembly-assistant/setup/channels", new AssemblySetupSaveChannelsRequest {
            Channels = new() { [ChannelRole.Chair] = 1 }
        });
        var full = await client.PostAsJsonAsync("/api/assembly-assistant/setup/channels", new AssemblySetupSaveChannelsRequest {
            Channels = AllRoles(1)
        });

        await Assert.That(partial.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
        await Assert.That(full.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(GetOption("mumble.channels.listener")).IsEqualTo("8");
    }

    [Test]
    public async Task Check_reports_unlinked_speaker_channel() {
        await using var server = new FakeMumbleServer();
        var root = new ChannelState { ChannelId = 0, Name = "Root" };
        var chair = new ChannelState { ChannelId = 1, Parent = 0, Name = "Versammlungsleitung" };
        chair.Links.Add(8);
        server.Channels.Add(root);
        server.Channels.Add(chair);
        for (uint id = 2; id <= 8; id++)
            server.Channels.Add(new ChannelState { ChannelId = id, Parent = 0, Name = $"Kanal {id}" });
        server.Start();

        SetOption("mumble.host", "127.0.0.1");
        SetOption("mumble.port", server.Port.ToString());
        SetOption("mumble.username", "Versammlungsassistent");
        SetOption("mumble.certificate", FakeMumbleServer.CreateClientCertificateBase64(CertificatePassword));
        SetOption("mumble.certificate_password", CertificatePassword);
        SetOption("mumble.server_certificate_sha256", server.CertificateSha256);
        var client = await ClientAsync(_adminToken);
        await client.PostAsJsonAsync("/api/assembly-assistant/setup/channels", new AssemblySetupSaveChannelsRequest { Channels = AllRoles(1) });

        var response = await client.PostAsync("/api/assembly-assistant/setup/check", null);
        var result = await response.Content.ReadFromJsonAsync<AssemblySetupCheckDTO>();

        await Assert.That(result!.Connected).IsTrue();
        await Assert.That(result.ServerVersion).IsEqualTo("1.3.4");
        await Assert.That(result.Channels.Count).IsEqualTo(9);
        var notLinked = result.Problems
            .Where(p => p.MessageKey == I18nKey.Ui.AssemblySetup.Problem.NotLinkedToChair)
            .Select(p => p.Role)
            .ToList();
        await Assert.That(notLinked).IsEquivalentTo(new List<ChannelRole?> { ChannelRole.Speaker, ChannelRole.MotionAuthor });
    }

    [Test]
    public async Task Check_without_configuration_reports_not_configured() {
        SetOption("mumble.certificate", "");
        var client = await ClientAsync(_adminToken);

        var response = await client.PostAsync("/api/assembly-assistant/setup/check", null);
        var result = await response.Content.ReadFromJsonAsync<AssemblySetupCheckDTO>();

        await Assert.That(result!.Connected).IsFalse();
        await Assert.That(result.ErrorKey).IsEqualTo(I18nKey.Ui.AssemblySetup.Error.NotConfigured);
    }

    // Chair 1, Speaker 2, MotionAuthor 3, Pro 4, Contra 5, Question 6, Point 7, Listener 8 when starting at 1.
    private static Dictionary<ChannelRole, uint> AllRoles(uint first) {
        return new Dictionary<ChannelRole, uint> {
            [ChannelRole.Chair] = first,
            [ChannelRole.Speaker] = first + 1,
            [ChannelRole.MotionAuthor] = first + 2,
            [ChannelRole.Pro] = first + 3,
            [ChannelRole.Contra] = first + 4,
            [ChannelRole.Question] = first + 5,
            [ChannelRole.Point] = first + 6,
            [ChannelRole.Listener] = first + 7
        };
    }
}
