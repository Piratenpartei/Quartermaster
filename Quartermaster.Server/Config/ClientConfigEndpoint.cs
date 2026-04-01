using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api.Config;
using Quartermaster.Data.Options;

namespace Quartermaster.Server.Config;

public class ClientConfigEndpoint : EndpointWithoutRequest<ClientConfigDTO> {
    private readonly OptionRepository _optionRepo;

    public ClientConfigEndpoint(OptionRepository optionRepo) {
        _optionRepo = optionRepo;
    }

    public override void Configure() {
        Get("/api/config/client");
        AllowAnonymous();
    }

    public override async Task HandleAsync(CancellationToken ct) {
        var errorContact = _optionRepo.GetGlobalValue("general.error.contact")?.Value ?? "";
        var showDetails = _optionRepo.GetGlobalValue("general.error.show_details")?.Value ?? "false";

        await SendAsync(new ClientConfigDTO {
            ErrorContact = errorContact,
            ShowDetailedErrors = showDetails.Equals("true", System.StringComparison.OrdinalIgnoreCase)
        }, cancellation: ct);
    }
}
