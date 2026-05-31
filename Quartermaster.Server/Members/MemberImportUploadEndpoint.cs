using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Microsoft.AspNetCore.Http;
using Quartermaster.Api;
using Quartermaster.Api.I18n;
using Quartermaster.Api.Members;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.Members;

public class MemberImportUploadRequest {
    public IFormFile File { get; set; } = default!;
}

public class MemberImportUploadEndpoint : Endpoint<MemberImportUploadRequest, MemberImportLogDTO> {
    private readonly MemberImportService _importService;
    private readonly PermissionContext _perms;

    public MemberImportUploadEndpoint(
        MemberImportService importService,
        PermissionContext perms) {
        _importService = importService;
        _perms = perms;
    }

    public override void Configure() {
        Post("/api/members/import/upload");
        AllowFileUploads();
    }

    public override async Task HandleAsync(MemberImportUploadRequest req, CancellationToken ct) {
        if (_perms.UserId == null) {
            await SendUnauthorizedAsync(ct);
            return;
        }
        if (!_perms.HasGlobal(PermissionIdentifier.TriggerMemberImport)) {
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
        await using (var stream = new FileStream(tempPath, FileMode.Create)) {
            await req.File.CopyToAsync(stream, ct);
        }

        // Ownership of tempPath transfers to the background worker; it deletes the file when the import finishes.
        var pending = _importService.StartBackgroundImport(tempPath, req.File.FileName, deleteAfter: true);
        await SendAsync(pending.ToDto(), 202, cancellation: ct);
    }
}
