using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api.DueSelector;
using Quartermaster.Data.DueSelector;

namespace Quartermaster.Server.Admin;

public class DueSelectionListEndpoint
    : Endpoint<DueSelectionListRequest, DueSelectionListResponse> {

    private readonly DueSelectionRepository _dueSelectionRepo;

    public DueSelectionListEndpoint(DueSelectionRepository dueSelectionRepo) {
        _dueSelectionRepo = dueSelectionRepo;
    }

    public override void Configure() {
        Get("/api/admin/dueselections");
        AllowAnonymous(); // TODO: Replace with auth when login UI exists
    }

    public override async Task HandleAsync(DueSelectionListRequest req, CancellationToken ct) {
        DueSelectionStatus? status = req.Status.HasValue
            ? (DueSelectionStatus)req.Status.Value
            : null;

        var (items, totalCount) = _dueSelectionRepo.List(status, req.Page, req.PageSize);

        var dtos = items.Select(d => new DueSelectionAdminDTO {
            Id = d.Id,
            FirstName = d.FirstName,
            LastName = d.LastName,
            EMail = d.EMail,
            SelectedDue = d.SelectedDue,
            ReducedAmount = d.ReducedAmount,
            ReducedJustification = d.ReducedJustification,
            SelectedValuation = (int)d.SelectedValuation,
            Status = (int)d.Status,
            ProcessedAt = d.ProcessedAt
        }).ToList();

        await SendAsync(new DueSelectionListResponse {
            Items = dtos,
            TotalCount = totalCount,
            Page = req.Page,
            PageSize = req.PageSize
        }, cancellation: ct);
    }
}
