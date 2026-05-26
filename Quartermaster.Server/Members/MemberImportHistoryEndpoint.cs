using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api;
using Quartermaster.Api.Members;
using Quartermaster.Data.Members;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.Members;

public class MemberImportHistoryRequest {
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 25;
}

public class MemberImportHistoryEndpoint
    : Endpoint<MemberImportHistoryRequest, MemberImportLogListResponse> {

    private readonly MemberRepository _memberRepo;
    private readonly PermissionContext _perms;

    public MemberImportHistoryEndpoint(MemberRepository memberRepo, PermissionContext perms) {
        _memberRepo = memberRepo;
        _perms = perms;
    }

    public override void Configure() {
        Get("/api/members/import/history");
    }

    public override async Task HandleAsync(MemberImportHistoryRequest req, CancellationToken ct) {
        if (_perms.UserId == null) {
            await SendUnauthorizedAsync(ct);
            return;
        }
        if (!_perms.HasGlobal(PermissionIdentifier.ViewAllMembers)) {
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
            TotalCount = totalCount,
            Page = req.Page,
            PageSize = req.PageSize
        }, cancellation: ct);
    }
}
