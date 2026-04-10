using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api.Chapters;
using Quartermaster.Data.Chapters;

namespace Quartermaster.Server.Chapters;

public class ChapterChildrenRequest {
    public Guid ParentId { get; set; }
}

public class ChapterChildrenEndpoint : Endpoint<ChapterChildrenRequest, List<ChapterDTO>> {
    private readonly ChapterRepository _chapterRepo;

    public ChapterChildrenEndpoint(ChapterRepository chapterRepo) {
        _chapterRepo = chapterRepo;
    }

    public override void Configure() {
        Get("/api/chapters/{ParentId}/children");
        AllowAnonymous();
    }

    public override async Task HandleAsync(ChapterChildrenRequest req, CancellationToken ct) {
        var children = _chapterRepo.GetChildren(req.ParentId);
        var dtos = children.Select(c => new ChapterDTO {
            Id = c.Id,
            Name = c.Name,
            ShortCode = c.ShortCode,
            AdministrativeDivisionId = c.AdministrativeDivisionId,
            ExternalCode = c.ExternalCode,
            ParentChapterId = c.ParentChapterId
        }).ToList();
        await SendAsync(dtos, cancellation: ct);
    }
}
