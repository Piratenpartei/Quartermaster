using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api.Chapters;
using Quartermaster.Data.Chapters;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.Chapters;

public class ChapterChildrenRequest {
    public Guid ParentId { get; set; }
}

public class ChapterChildrenEndpoint : Endpoint<ChapterChildrenRequest, List<ChapterDTO>> {
    private readonly ChapterRepository _chapterRepo;
    private readonly PermissionContext _perms;

    public ChapterChildrenEndpoint(ChapterRepository chapterRepo, PermissionContext perms) {
        _chapterRepo = chapterRepo;
        _perms = perms;
    }

    public override void Configure() {
        Get("/api/chapters/{ParentId}/children");
        AllowAnonymous();
    }

    public override async Task HandleAsync(ChapterChildrenRequest req, CancellationToken ct) {
        var dtos = _chapterRepo.ToDtos(_chapterRepo.GetChildren(req.ParentId), includeMemberFields: _perms.UserId != null);
        await SendAsync(dtos, cancellation: ct);
    }
}
