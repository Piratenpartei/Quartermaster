using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api;
using Quartermaster.Api.NewsPosts;
using Quartermaster.Data.NewsPosts;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.NewsPosts.Endpoints;

public class ProgrammeSourceCreateEndpoint : Endpoint<ProgrammeSourceCreateRequest, ProgrammeSourceDTO> {
    private readonly ProgrammeRepository _programmeRepo;
    private readonly PermissionContext _perms;

    public ProgrammeSourceCreateEndpoint(ProgrammeRepository programmeRepo, PermissionContext perms) {
        _programmeRepo = programmeRepo;
        _perms = perms;
    }

    public override void Configure() {
        Post("/api/newsposts/sources");
    }

    public override async Task HandleAsync(ProgrammeSourceCreateRequest req, CancellationToken ct) {
        if (_perms.UserId == null) {
            await SendUnauthorizedAsync(ct);
            return;
        }
        if (!_perms.HasGlobal(PermissionIdentifier.ManageNewsPosts)) {
            await SendForbiddenAsync(ct);
            return;
        }

        var title = req.WikiPageTitle.Trim();
        if (string.IsNullOrWhiteSpace(title)) {
            AddError("Bitte einen Seitentitel angeben.");
            await SendErrorsAsync(400, ct);
            return;
        }
        if (_programmeRepo.SourceExists(title)) {
            AddError("Diese Quelle existiert bereits.");
            await SendErrorsAsync(409, ct);
            return;
        }

        var source = new ProgrammeSource { WikiPageTitle = title };
        _programmeRepo.CreateSource(source);

        await SendAsync(new ProgrammeSourceDTO {
            Id = source.Id,
            WikiPageTitle = source.WikiPageTitle,
            LastSyncedAt = null,
            LastError = null,
            SectionCount = 0
        }, cancellation: ct);
    }
}
