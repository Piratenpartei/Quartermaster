using System;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api.Chapters;
using Quartermaster.Data.AdministrativeDivisions;
using Quartermaster.Data.Chapters;

namespace Quartermaster.Server.Chapters;

public class ChapterForDivisionRequest {
    public Guid DivisionId { get; set; }
}

public class ChapterForDivisionEndpoint : Endpoint<ChapterForDivisionRequest, ChapterDTO> {
    private readonly ChapterRepository _chapterRepository;
    private readonly AdministrativeDivisionRepository _adminDivRepository;

    public ChapterForDivisionEndpoint(ChapterRepository chapterRepository,
        AdministrativeDivisionRepository adminDivRepository) {
        _chapterRepository = chapterRepository;
        _adminDivRepository = adminDivRepository;
    }

    public override void Configure() {
        Get("/api/chapters/for-division/{DivisionId}");
        AllowAnonymous();
    }

    public override async Task HandleAsync(ChapterForDivisionRequest req, CancellationToken ct) {
        var chapter = _chapterRepository.FindForDivision(req.DivisionId, _adminDivRepository);
        if (chapter == null) {
            await SendNotFoundAsync(ct);
            return;
        }
        await SendAsync(new ChapterDTO {
            Id = chapter.Id,
            Name = chapter.Name,
            ShortCode = chapter.ShortCode,
            AdministrativeDivisionId = chapter.AdministrativeDivisionId,
            ExternalCode = chapter.ExternalCode,
            ParentChapterId = chapter.ParentChapterId
        }, cancellation: ct);
    }
}
