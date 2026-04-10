using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api;
using Quartermaster.Api.I18n;
using Quartermaster.Api.Members;
using Quartermaster.Data.Chapters;
using Quartermaster.Data.Options;
using Quartermaster.Data.UserGlobalPermissions;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.Members;

public class MemberImportTriggerEndpoint : EndpointWithoutRequest<MemberImportLogDTO> {
    private readonly MemberImportService _importService;
    private readonly OptionRepository _optionRepo;
    private readonly ChapterRepository _chapterRepo;
    private readonly UserGlobalPermissionRepository _globalPermRepo;

    public MemberImportTriggerEndpoint(
        MemberImportService importService,
        OptionRepository optionRepo,
        ChapterRepository chapterRepo,
        UserGlobalPermissionRepository globalPermRepo) {
        _importService = importService;
        _optionRepo = optionRepo;
        _chapterRepo = chapterRepo;
        _globalPermRepo = globalPermRepo;
    }

    public override void Configure() {
        Post("/api/members/import");
    }

    public override async Task HandleAsync(CancellationToken ct) {
        var userId = EndpointAuthorizationHelper.GetUserId(User);
        if (userId == null) {
            await SendUnauthorizedAsync(ct);
            return;
        }
        if (!EndpointAuthorizationHelper.HasGlobalPermission(userId.Value, PermissionIdentifier.TriggerMemberImport, _globalPermRepo)) {
            await SendForbiddenAsync(ct);
            return;
        }

        var filePath = _optionRepo.ResolveValue("member_import.file_path", null, _chapterRepo);

        if (string.IsNullOrEmpty(filePath) || !System.IO.File.Exists(filePath)) {
            ThrowError(I18nKey.Error.Member.Import.FilePathNotConfigured);
            return;
        }

        var log = _importService.ImportFromFile(filePath);

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
    }
}
