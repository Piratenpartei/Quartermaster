using System;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api;
using Quartermaster.Data.Options;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.Options;

public class OptionOverrideDeleteRequest {
    public string Identifier { get; set; } = "";
    public Guid ChapterId { get; set; }
}

public class OptionOverrideDeleteEndpoint : Endpoint<OptionOverrideDeleteRequest> {
    private readonly OptionRepository _optionRepo;
    private readonly PermissionContext _perms;

    public OptionOverrideDeleteEndpoint(OptionRepository optionRepo, PermissionContext perms) {
        _optionRepo = optionRepo;
        _perms = perms;
    }

    public override void Configure() {
        Delete("/api/options/{Identifier}/overrides/{ChapterId}");
    }

    public override async Task HandleAsync(OptionOverrideDeleteRequest req, CancellationToken ct) {
        if (_perms.UserId == null) {
            await SendUnauthorizedAsync(ct);
            return;
        }
        if (!_perms.HasGlobal(PermissionIdentifier.EditOptions)) {
            await SendForbiddenAsync(ct);
            return;
        }

        if (!_optionRepo.DeleteChapterValue(req.Identifier, req.ChapterId)) {
            await SendNotFoundAsync(ct);
            return;
        }

        await SendOkAsync(ct);
    }
}
