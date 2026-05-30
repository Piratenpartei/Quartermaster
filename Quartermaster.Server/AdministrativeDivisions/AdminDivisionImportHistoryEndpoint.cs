using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api;
using Quartermaster.Api.AdministrativeDivisions;
using Quartermaster.Data.AdministrativeDivisions;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.AdministrativeDivisions;

public class AdminDivisionImportHistoryRequest {
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 25;
}

public class AdminDivisionImportHistoryEndpoint
    : Endpoint<AdminDivisionImportHistoryRequest, AdminDivisionImportLogListResponse> {

    private readonly AdministrativeDivisionRepository _adminDivRepo;
    private readonly PermissionContext _perms;

    public AdminDivisionImportHistoryEndpoint(
        AdministrativeDivisionRepository adminDivRepo,
        PermissionContext perms) {
        _adminDivRepo = adminDivRepo;
        _perms = perms;
    }

    public override void Configure() {
        Get("/api/admindivisions/import/history");
    }

    public override async Task HandleAsync(AdminDivisionImportHistoryRequest req, CancellationToken ct) {
        if (_perms.UserId == null) {
            await SendUnauthorizedAsync(ct);
            return;
        }
        if (!_perms.HasGlobal(PermissionIdentifier.ViewAllMembers)) {
            await SendForbiddenAsync(ct);
            return;
        }

        var (items, totalCount) = _adminDivRepo.GetImportHistory(req.Page, req.PageSize);

        var dtos = items.Select(l => new AdminDivisionImportLogDTO {
            Id = l.Id,
            ImportedAt = l.ImportedAt.ToDtoUtc(),
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
            TotalCount = totalCount,
            Page = req.Page,
            PageSize = req.PageSize
        }, cancellation: ct);
    }
}
