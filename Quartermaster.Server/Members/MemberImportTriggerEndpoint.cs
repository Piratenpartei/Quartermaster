using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api.Members;
using Quartermaster.Data.Chapters;
using Quartermaster.Data.Options;

namespace Quartermaster.Server.Members;

public class MemberImportTriggerEndpoint : EndpointWithoutRequest<MemberImportLogDTO> {
    private readonly MemberImportService _importService;
    private readonly OptionRepository _optionRepo;
    private readonly ChapterRepository _chapterRepo;

    public MemberImportTriggerEndpoint(
        MemberImportService importService,
        OptionRepository optionRepo,
        ChapterRepository chapterRepo) {
        _importService = importService;
        _optionRepo = optionRepo;
        _chapterRepo = chapterRepo;
    }

    public override void Configure() {
        Post("/api/members/import");
        AllowAnonymous();
    }

    public override async Task HandleAsync(CancellationToken ct) {
        var filePath = _optionRepo.ResolveValue("member_import.file_path", null, _chapterRepo);

        if (string.IsNullOrEmpty(filePath) || !System.IO.File.Exists(filePath)) {
            ThrowError("Import file path is not configured or file does not exist.");
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
