using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Microsoft.AspNetCore.Http;
using Quartermaster.Data.Options;

namespace Quartermaster.Server.Users;

public class SamlLoginConsumeEndpoint : Endpoint<SamlLoginRequest, EmptyResponse> {
    private readonly OptionRepository _optionRepo;

    public SamlLoginConsumeEndpoint(OptionRepository optionRepo) {
        _optionRepo = optionRepo;
    }

    public override void Configure() {
        Post("/api/users/SamlConsume");
        AllowAnonymous();
        AllowFormData(true);
        Description(x => x.Accepts<SamlLoginRequest>("application/x-www-form-urlencoded"));
    }

    public override async Task HandleAsync(SamlLoginRequest req, CancellationToken ct) {
        var certBase64 = _optionRepo.GetGlobalValue("auth.saml.certificate")?.Value;
        if (string.IsNullOrEmpty(certBase64)) {
            await SendAsync(new EmptyResponse(), 503, ct);
            return;
        }

        var cert = "-----BEGIN CERTIFICATE-----"
            + certBase64
            + "-----END CERTIFICATE-----";

        var samlResponse = new Saml.Response(cert, req.SamlData);
        await SendOkAsync(ct);
    }
}

public class SamlLoginRequest {
    [BindFrom("SAMLResponse")]
    public string? SamlData { get; set; }
}
