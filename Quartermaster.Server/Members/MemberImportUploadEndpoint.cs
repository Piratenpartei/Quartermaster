using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Microsoft.AspNetCore.Http;
using Quartermaster.Api;
using Quartermaster.Api.I18n;
using Quartermaster.Api.Members;
using Quartermaster.Data.UserGlobalPermissions;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.Members;

public class MemberImportUploadRequest {
    public IFormFile File { get; set; } = default!;
}

public class MemberImportUploadEndpoint : Endpoint<MemberImportUploadRequest, MemberImportLogDTO> {
    private readonly MemberImportService _importService;
    private readonly UserGlobalPermissionRepository _globalPermRepo;

    public MemberImportUploadEndpoint(
        MemberImportService importService,
        UserGlobalPermissionRepository globalPermRepo) {
        _importService = importService;
        _globalPermRepo = globalPermRepo;
    }

    public override void Configure() {
        Post("/api/members/import/upload");
        AllowFileUploads();
    }

    public override async Task HandleAsync(MemberImportUploadRequest req, CancellationToken ct) {
        var userId = EndpointAuthorizationHelper.GetUserId(User);
        if (userId == null) {
            await SendUnauthorizedAsync(ct);
            return;
        }
        if (!EndpointAuthorizationHelper.HasGlobalPermission(userId.Value, PermissionIdentifier.TriggerMemberImport, _globalPermRepo)) {
            await SendForbiddenAsync(ct);
            return;
        }

        if (req.File == null || req.File.Length == 0) {
            ThrowError(I18nKey.Error.Member.Import.NoFileUploaded);
            return;
        }

        var ext = Path.GetExtension(req.File.FileName);
        if (!string.Equals(ext, ".csv", System.StringComparison.OrdinalIgnoreCase)) {
            ThrowError(I18nKey.Error.Member.Import.OnlyCsvAllowed);
            return;
        }

        var tempPath = Path.Combine(Path.GetTempPath(), $"qm_import_{System.Guid.NewGuid():N}.csv");
        try {
            await using (var stream = new FileStream(tempPath, FileMode.Create)) {
                await req.File.CopyToAsync(stream, ct);
            }

            var log = _importService.ImportFromFile(tempPath, req.File.FileName);

            await SendAsync(new MemberImportLogDTO {
                Id = log.Id,
                ImportedAt = log.ImportedAt,
                FileName = log.FileName,
                FileHash = log.FileHash,
                TotalRecords = log.TotalRecords,
                NewRecords = log.NewRecords,
                UpdatedRecords = log.UpdatedRecords,
                ErrorCount = log.ErrorCount,
                Errors = log.Errors,
                DurationMs = log.DurationMs
            }, cancellation: ct);
        } finally {
            if (File.Exists(tempPath))
                File.Delete(tempPath);
        }
    }
}
