using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api.Options;
using Quartermaster.Data.Options;

namespace Quartermaster.Server.Options;

public class OptionUpdateEndpoint : Endpoint<OptionUpdateRequest> {
    private readonly OptionRepository _optionRepo;

    public OptionUpdateEndpoint(OptionRepository optionRepo) {
        _optionRepo = optionRepo;
    }

    public override void Configure() {
        Post("/api/options");
        AllowAnonymous();
    }

    public override async Task HandleAsync(OptionUpdateRequest req, CancellationToken ct) {
        var def = _optionRepo.GetDefinition(req.Identifier);
        if (def == null) {
            await SendNotFoundAsync(ct);
            return;
        }

        if (req.ChapterId.HasValue && !def.IsOverridable) {
            await SendErrorsAsync(400, ct);
            return;
        }

        _optionRepo.SetValue(req.Identifier, req.ChapterId, req.Value);
        await SendOkAsync(ct);
    }
}
