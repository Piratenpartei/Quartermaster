using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api;
using Quartermaster.Api.Members;
using Quartermaster.Data.Members;
using Quartermaster.Data.UserGlobalPermissions;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.Members;

public class MemberImportHistoryRequest {
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 25;
}

public class MemberImportHistoryEndpoint
    : Endpoint<MemberImportHistoryRequest, MemberImportLogListResponse> {

    private readonly MemberRepository _memberRepo;
    private readonly UserGlobalPermissionRepository _globalPermRepo;

    public MemberImportHistoryEndpoint(MemberRepository memberRepo,
        UserGlobalPermissionRepository globalPermRepo) {
        _memberRepo = memberRepo;
        _globalPermRepo = globalPermRepo;
    }

    public override void Configure() {
        Get("/api/members/import/history");
    }

    public override async Task HandleAsync(MemberImportHistoryRequest req, CancellationToken ct) {
        var userId = EndpointAuthorizationHelper.GetUserId(User);
        if (userId == null) {
            await SendUnauthorizedAsync(ct);
            return;
        }
        if (!EndpointAuthorizationHelper.HasGlobalPermission(userId.Value, PermissionIdentifier.ViewAllMembers, _globalPermRepo)) {
            await SendForbiddenAsync(ct);
            return;
        }

        var (items, totalCount) = _memberRepo.GetImportHistory(req.Page, req.PageSize);

        var dtos = items.Select(l => new MemberImportLogDTO {
            Id = l.Id,
            ImportedAt = l.ImportedAt,
            FileName = l.FileName,
            FileHash = l.FileHash,
            TotalRecords = l.TotalRecords,
            NewRecords = l.NewRecords,
            UpdatedRecords = l.UpdatedRecords,
            ErrorCount = l.ErrorCount,
            Errors = l.Errors,
            DurationMs = l.DurationMs
        }).ToList();

        await SendAsync(new MemberImportLogListResponse {
            Items = dtos,
            TotalCount = totalCount
        }, cancellation: ct);
    }
}
