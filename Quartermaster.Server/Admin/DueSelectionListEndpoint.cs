using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api;
using Quartermaster.Api.DueSelector;
using Quartermaster.Data.Chapters;
using Quartermaster.Data.DueSelector;
using Quartermaster.Data.UserChapterPermissions;
using Quartermaster.Data.UserGlobalPermissions;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.Admin;

public class DueSelectionListEndpoint
    : Endpoint<DueSelectionListRequest, DueSelectionListResponse> {

    private readonly DueSelectionRepository _dueSelectionRepo;
    private readonly ChapterRepository _chapterRepo;
    private readonly UserGlobalPermissionRepository _globalPermRepo;
    private readonly UserChapterPermissionRepository _chapterPermRepo;

    public DueSelectionListEndpoint(
        DueSelectionRepository dueSelectionRepo,
        ChapterRepository chapterRepo,
        UserGlobalPermissionRepository globalPermRepo,
        UserChapterPermissionRepository chapterPermRepo) {
        _dueSelectionRepo = dueSelectionRepo;
        _chapterRepo = chapterRepo;
        _globalPermRepo = globalPermRepo;
        _chapterPermRepo = chapterPermRepo;
    }

    public override void Configure() {
        Get("/api/admin/dueselections");
    }

    public override async Task HandleAsync(DueSelectionListRequest req, CancellationToken ct) {
        var userId = EndpointAuthorizationHelper.GetUserId(User);
        if (userId == null) {
            await SendUnauthorizedAsync(ct);
            return;
        }

        var allowedChapterIds = EndpointAuthorizationHelper.GetPermittedChapterIds(
            userId.Value, PermissionIdentifier.ViewDueSelections, _globalPermRepo, _chapterPermRepo, _chapterRepo);
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
