using System;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Data.ChapterAssociates;

namespace Quartermaster.Server.ChapterAssociates;

public class ChapterOfficerDeleteRequest {
    public Guid MemberId { get; set; }
    public Guid ChapterId { get; set; }
}

public class ChapterOfficerDeleteEndpoint : Endpoint<ChapterOfficerDeleteRequest> {
    private readonly ChapterOfficerRepository _officerRepo;

    public ChapterOfficerDeleteEndpoint(ChapterOfficerRepository officerRepo) {
        _officerRepo = officerRepo;
    }

    public override void Configure() {
        Delete("/api/chapterofficers");
        AllowAnonymous();
    }

    public override async Task HandleAsync(ChapterOfficerDeleteRequest req, CancellationToken ct) {
        _officerRepo.Delete(req.MemberId, req.ChapterId);
        await SendOkAsync(ct);
    }
}
