using System;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api.AssemblyAssistant;
using Quartermaster.Api.I18n;
using Quartermaster.Data.Options;
using Quartermaster.Server.AssemblyAssistant.Mumble;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.AssemblyAssistant.Endpoints;

public class AssemblySetupGenerateCertificateEndpoint : Endpoint<AssemblySetupGenerateCertificateRequest> {
    private readonly PermissionContext _perms;
    private readonly OptionRepository _options;

    public AssemblySetupGenerateCertificateEndpoint(PermissionContext perms, OptionRepository options) {
        _perms = perms;
        _options = options;
    }

    public override void Configure() {
        Post("/api/assembly-assistant/setup/certificate");
    }

    public override async Task HandleAsync(AssemblySetupGenerateCertificateRequest req, CancellationToken ct) {
        var status = AssemblySetupGuard.Check(_perms);
        if (status != null) {
            await SendErrorsAsync(status.Value, ct);
            return;
        }

        var settings = MumbleSettings.ReadFrom(_options);
        if (settings.CertificateBase64.Length > 0 && !req.Replace)
            ThrowError(I18nKey.Ui.AssemblySetup.Error.CertificateExists, 409);

        var username = settings.Username.Length > 0 ? settings.Username : "Versammlungsassistent";
        var generated = MumbleCertificateGenerator.Create(username, DateTime.UtcNow);
        _options.SetValue("mumble.certificate", null, generated.Pkcs12Base64);
        _options.SetValue("mumble.certificate_password", null, generated.Password);

        await SendOkAsync(ct);
    }
}
