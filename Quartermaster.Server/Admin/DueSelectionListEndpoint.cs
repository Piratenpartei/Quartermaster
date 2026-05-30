using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api;
using Quartermaster.Api.DueSelector;
using Quartermaster.Data.DueSelector;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.Admin;

public class DueSelectionListEndpoint
    : Endpoint<DueSelectionListRequest, DueSelectionListResponse> {

    private readonly DueSelectionRepository _dueSelectionRepo;
    private readonly PermissionContext _perms;

    public DueSelectionListEndpoint(
        DueSelectionRepository dueSelectionRepo,
        PermissionContext perms) {
        _dueSelectionRepo = dueSelectionRepo;
        _perms = perms;
    }

    public override void Configure() {
        Get("/api/admin/dueselections");
    }

    public override async Task HandleAsync(DueSelectionListRequest req, CancellationToken ct) {
        if (_perms.UserId == null) {
            await SendUnauthorizedAsync(ct);
            return;
        }

        var allowedChapterIds = _perms.GetPermittedChapterIds(PermissionIdentifier.ViewDueSelections);
        if (allowedChapterIds is { Count: 0 }) {
            await SendForbiddenAsync(ct);
            return;
        }

        DueSelectionStatus? status = req.Status.HasValue
            ? (DueSelectionStatus)req.Status.Value
            : null;

        var (items, totalCount) = _dueSelectionRepo.List(status, req.Page, req.PageSize, allowedChapterIds);

        var dtos = items.Select(d => new DueSelectionAdminDTO {
            Id = d.Id,
            FirstName = d.FirstName,
            LastName = d.LastName,
            Email = d.Email,
            SelectedDue = d.SelectedDue,
            ReducedAmount = d.ReducedAmount,
            ReducedJustification = d.ReducedJustification,
            SelectedValuation = d.SelectedValuation,
            Status = d.Status,
            ProcessedAt = d.ProcessedAt.ToDtoUtc()
        }).ToList();

        await SendAsync(new DueSelectionListResponse {
            Items = dtos,
            TotalCount = totalCount,
            Page = req.Page,
            PageSize = req.PageSize
        }, cancellation: ct);
    }
}
