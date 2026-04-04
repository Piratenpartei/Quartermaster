using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api;
using Quartermaster.Api.AdministrativeDivisions;
using Quartermaster.Data.AdministrativeDivisions;
using Quartermaster.Data.UserGlobalPermissions;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.AdministrativeDivisions;

public class AdminDivisionImportHistoryRequest {
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 25;
}

public class AdminDivisionImportHistoryEndpoint
    : Endpoint<AdminDivisionImportHistoryRequest, AdminDivisionImportLogListResponse> {

    private readonly AdministrativeDivisionRepository _adminDivRepo;
    private readonly UserGlobalPermissionRepository _globalPermRepo;

    public AdminDivisionImportHistoryEndpoint(
        AdministrativeDivisionRepository adminDivRepo,
        UserGlobalPermissionRepository globalPermRepo) {
        _adminDivRepo = adminDivRepo;
        _globalPermRepo = globalPermRepo;
    }

    public override void Configure() {
        Get("/api/admindivisions/import/history");
    }

    public override async Task HandleAsync(AdminDivisionImportHistoryRequest req, CancellationToken ct) {
        var userId = EndpointAuthorizationHelper.GetUserId(User);
        if (userId == null) {
            await SendUnauthorizedAsync(ct);
            return;
        }
        if (!EndpointAuthorizationHelper.HasGlobalPermission(userId.Value, PermissionIdentifier.ViewAllMembers, _globalPermRepo)) {
            await SendForbiddenAsync(ct);
            return;
        }

        var (items, totalCount) = _adminDivRepo.GetImportHistory(req.Page, req.PageSize);

        var dtos = items.Select(l => new AdminDivisionImportLogDTO {
            Id = l.Id,
            ImportedAt = l.ImportedAt,
            FileHash = l.FileHash,
            TotalRecords = l.TotalRecords,
            AddedRecords = l.AddedRecords,
            UpdatedRecords = l.UpdatedRecords,
            RemovedRecords = l.RemovedRecords,
            RemappedRecords = l.RemappedRecords,
            OrphanedRecords = l.OrphanedRecords,
            ErrorCount = l.ErrorCount,
            Errors = l.Errors,
            DurationMs = l.DurationMs
        }).ToList();

        await SendAsync(new AdminDivisionImportLogListResponse {
            Items = dtos,
            TotalCount = totalCount
        }, cancellation: ct);
    }
}
