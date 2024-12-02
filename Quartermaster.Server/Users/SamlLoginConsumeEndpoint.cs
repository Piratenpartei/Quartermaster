using FastEndpoints;
using Microsoft.AspNetCore.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Quartermaster.Server.Users;

public class SamlLoginConsumeEndpoint : Endpoint<SamlLoginRequest, EmptyResponse> {
    public override void Configure() {
        Post("/api/users/SamlConsume");
        AllowAnonymous();
        AllowFormData(true);
        Description(x => x.Accepts<SamlLoginRequest>("application/x-www-form-urlencoded"));
    }

    public override Task<EmptyResponse> ExecuteAsync(SamlLoginRequest req, CancellationToken ct) {
        var cert = "-----BEGIN CERTIFICATE-----"
            + Config["SamlSettings:SamlCertificate"]
            + "-----END CERTIFICATE-----";

        var samlResponse = new Saml.Response(cert, req.SamlData);
        return Task.FromResult(new EmptyResponse());
    }
}

public class SamlLoginRequest {
    [BindFrom("SAMLResponse")]
    public string? SamlData { get; set; }
}