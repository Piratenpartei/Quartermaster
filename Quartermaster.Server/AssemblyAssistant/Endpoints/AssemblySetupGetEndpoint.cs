using System;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Microsoft.Extensions.Logging;
using Quartermaster.Api.AssemblyAssistant;
using Quartermaster.Data.Options;
using Quartermaster.Server.AssemblyAssistant.Mumble;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.AssemblyAssistant.Endpoints;

public class AssemblySetupGetEndpoint : EndpointWithoutRequest<AssemblySetupStatusDTO> {
    private readonly PermissionContext _perms;
    private readonly OptionRepository _options;
    private readonly ILogger<AssemblySetupGetEndpoint> _logger;

    public AssemblySetupGetEndpoint(PermissionContext perms, OptionRepository options, ILogger<AssemblySetupGetEndpoint> logger) {
        _perms = perms;
        _options = options;
        _logger = logger;
    }

    public override void Configure() {
        Get("/api/assembly-assistant/setup");
    }

    public override async Task HandleAsync(CancellationToken ct) {
        var status = AssemblySetupGuard.Check(_perms);
        if (status != null) {
            await SendErrorsAsync(status.Value, ct);
            return;
        }

        var settings = MumbleSettings.ReadFrom(_options);
        var dto = new AssemblySetupStatusDTO {
            Host = settings.Host,
            Port = settings.Port,
            Username = settings.Username,
            CertificatePresent = settings.CertificateBase64.Length > 0,
            Channels = new(settings.Channels)
        };

        if (dto.CertificatePresent)
            ReadCertificate(settings, dto);

        await SendAsync(dto, cancellation: ct);
    }

    private void ReadCertificate(MumbleSettings settings, AssemblySetupStatusDTO dto) {
        try {
            using var certificate = X509CertificateLoader.LoadPkcs12(Convert.FromBase64String(settings.CertificateBase64), settings.CertificatePassword);
            dto.CertificateHash = MumbleCertificateGenerator.MumbleHash(certificate);
            dto.CertificateNotAfter = certificate.NotAfter.ToUniversalTime();
        } catch (Exception ex) when (ex is CryptographicException || ex is FormatException) {
            _logger.LogWarning(ex, "Stored Mumble certificate cannot be read");
            dto.CertificateUnreadable = true;
        }
    }
}
